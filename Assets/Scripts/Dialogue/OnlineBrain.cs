using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LLMUnity;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatbotAI.Dialogue
{
    /// Menu > Advanced > AI models > Online AI: the brain (answers, router, translator, search helper) can use an online
    /// service instead of the model on this PC - OpenAI, Anthropic Claude, Google Gemini, or any OpenAI-style server.
    /// Everything else (hearing, voice, document search, the topic gate) stays on this PC; only the question, the document
    /// passages it was matched with and the persona text are sent. If the service can't be reached, that question is answered
    /// by the local model (so the local model is still loaded). The API key lives in SecretStore, never in settings or builds.
    public static class OnlineBrain
    {
        public enum Provider { OpenAI, Claude, Gemini, Other }

        public static readonly string[] ProviderNames = { "OpenAI", "Claude", "Gemini", "Other" };

        const string EnabledKey = "online/brain", ProviderKey = "online/provider";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 0) == 1;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); TryAgain(); }
        }

        public static Provider Current
        {
            get => (Provider)Mathf.Clamp(PlayerPrefs.GetInt(ProviderKey, 0), 0, ProviderNames.Length - 1);
            set { PlayerPrefs.SetInt(ProviderKey, (int)value); PlayerPrefs.Save(); TryAgain(); }
        }

        // Models change names often: only Claude has a default; for the others the page lists what the account can use.
        public static string DefaultModel(Provider p) => p == Provider.Claude ? "claude-haiku-5-5" : "";

        public static string Model(Provider p) => PlayerPrefs.GetString("online/model/" + p, DefaultModel(p));

        public static void SetModel(Provider p, string model)
        {
            PlayerPrefs.SetString("online/model/" + p, (model ?? "").Trim());
            PlayerPrefs.Save();
            TryAgain();
        }

        public static string DefaultServer(Provider p) => p switch
        {
            Provider.OpenAI => "https://api.openai.com",
            Provider.Claude => "https://api.anthropic.com",
            Provider.Gemini => "https://generativelanguage.googleapis.com",
            _ => "",
        };

        /// The server's address (no path). Official addresses unless changed - needed for "Other", and used by the tests.
        public static string Server(Provider p)
        {
            string s = PlayerPrefs.GetString("online/server/" + p, DefaultServer(p)).Trim().TrimEnd('/');
            if (s.Length == 0) s = DefaultServer(p);   // a cleared box means the official address
            return s.EndsWith("/v1") ? s.Substring(0, s.Length - 3) : s;
        }

        public static void SetServer(Provider p, string address)
        {
            PlayerPrefs.SetString("online/server/" + p, (address ?? "").Trim());
            PlayerPrefs.Save();
        }

        static string KeyName(Provider p) => "api-key-" + p;
        public static bool HasKey(Provider p) => SecretStore.Has(KeyName(p));
        public static void SetKey(Provider p, string key) { SecretStore.Set(KeyName(p), (key ?? "").Trim()); TryAgain(); }
        public static void RemoveKey(Provider p) => SecretStore.Delete(KeyName(p));

        /// Online answers are on, set up, and the service hasn't just failed.
        public static bool Ready => Enabled && HasKey(Current) && Model(Current).Length > 0 && Server(Current).Length > 0 &&
                                    DateTime.UtcNow >= pausedUntil;

        /// What the page shows under the Test button: the last result.
        public static string Status { get; private set; } = "";
        static DateTime pausedUntil = DateTime.MinValue;        // wall-clock (Time.* restarts when the Editor enters Play mode)
        static int failures;

        // A changed setting is a new chance: forget earlier failures and the pause.
        static void TryAgain() { failures = 0; pausedUntil = DateTime.MinValue; }
        static readonly HashSet<string> noTemperature = new HashSet<string>();
        static readonly HashSet<string> oldTokenName = new HashSet<string>();

        // ------------------------------------------------------------------ the brain's calls

        /// One question to one of the brain's roles (router, answer agent, translator, search helper): online when it's set up,
        /// else - or if the online service fails - the model on this PC. The role's own prompt and examples are sent along.
        public static async Task<string> Chat(LLMAgent role, string prompt)
        {
            if (Ready)
            {
                string reply = await Ask(role.systemPrompt, role.chat, prompt, role.temperature, role.numPredict);
                if (reply != null) return reply;
                Debug.LogWarning($"OnlineBrain: using the model on this PC for this question ({Status}).");
            }
            return await role.Chat(prompt, null, null, addToHistory: false);
        }

        /// A short text from one language into another by the online model (the many-languages path: the rest of the brain
        /// only ever sees English). Null if it couldn't be done - the caller carries on in English.
        public static async Task<string> Translate(string text, string fromLanguage, string toLanguage)
        {
            if (!Ready || string.IsNullOrWhiteSpace(text)) return null;
            string system = $"You translate from {fromLanguage} into {toLanguage} for a voice assistant. The text is a spoken question or a short spoken reply. " +
                            $"Write the way people actually talk in {toLanguage} - natural, simple, never stiff. Keep brand, product and place names (such as Altscape, Altcore, P&I) " +
                            "exactly as written, in Latin letters. Do not answer questions in the text, and add no notes, quotes or explanations. Output only the translation.";
            string reply = await Ask(system, null, text, 0f, 400);
            return string.IsNullOrWhiteSpace(reply) ? null : reply.Trim().Trim('"');
        }

        /// The online answer, or null if it couldn't be had (Status says why).
        public static async Task<string> Ask(string system, IList<ChatMessage> history, string prompt, float temperature, int maxTokens)
        {
            var p = Current;
            var started = System.Diagnostics.Stopwatch.StartNew();
            var (text, error) = await Complete(p, Model(p), system, Messages(history, prompt), temperature, maxTokens, 25);
            if (text != null)
            {
                failures = 0;
                Status = $"Answered by {ProviderNames[(int)p]} in {started.Elapsed.TotalSeconds:0.0} s.";
                return text.Trim();
            }
            Status = error;
            // Offline or out of credit: don't make every question wait - use this PC's model for a minute, then try again.
            if (++failures >= 2) pausedUntil = DateTime.UtcNow.AddSeconds(60);
            return null;
        }

        /// Turns the role's few-shot examples + the question into alternating user/assistant turns (first one a user turn).
        static List<(string role, string text)> Messages(IList<ChatMessage> history, string prompt)
        {
            var turns = new List<(string role, string text)>();
            void Add(string role, string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                role = role == "assistant" || role == "model" ? "assistant" : "user";
                if (turns.Count > 0 && turns[turns.Count - 1].role == role) turns[turns.Count - 1] = (role, turns[turns.Count - 1].text + "\n\n" + text);
                else turns.Add((role, text));
            }
            if (history != null) foreach (var m in history) if (m.role != "system") Add(m.role, m.content);
            Add("user", prompt);
            while (turns.Count > 0 && turns[0].role != "user") turns.RemoveAt(0);
            return turns;
        }

        // ------------------------------------------------------------------ the three kinds of service

        static async Task<(string text, string error)> Complete(Provider p, string model, string system, List<(string role, string text)> turns,
                                                                float temperature, int maxTokens, int timeoutSeconds)
        {
            string key = SecretStore.Get(KeyName(p));
            string server = Server(p);
            if (key.Length == 0) return (null, "No API key saved.");
            if (model.Length == 0) return (null, "No model chosen.");
            if (server.Length == 0) return (null, "No server address.");
            int cap = maxTokens > 0 ? maxTokens : 512;
            string id = p + "/" + model;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                string url;
                var headers = new List<(string, string)>();
                var body = new JObject();
                if (p == Provider.Claude)
                {
                    url = server + "/v1/messages";
                    headers.Add(("x-api-key", key));
                    headers.Add(("anthropic-version", "2023-06-01"));
                    body["model"] = model;
                    body["max_tokens"] = Math.Max(cap, 64);
                    if (!string.IsNullOrEmpty(system)) body["system"] = system;
                    body["messages"] = new JArray(turns.Select(t => new JObject { ["role"] = t.role, ["content"] = t.text }));
                    if (!noTemperature.Contains(id)) body["temperature"] = Mathf.Clamp01(temperature);
                }
                else if (p == Provider.Gemini)
                {
                    url = $"{server}/v1beta/models/{model}:generateContent";
                    headers.Add(("x-goog-api-key", key));
                    if (!string.IsNullOrEmpty(system)) body["systemInstruction"] = new JObject { ["parts"] = new JArray(new JObject { ["text"] = system }) };
                    body["contents"] = new JArray(turns.Select(t => new JObject
                    {
                        ["role"] = t.role == "assistant" ? "model" : "user",
                        ["parts"] = new JArray(new JObject { ["text"] = t.text }),
                    }));
                    var config = new JObject { ["maxOutputTokens"] = Math.Max(cap * 4, 2048) };   // some models "think" first, and count it
                    if (!noTemperature.Contains(id)) config["temperature"] = temperature;
                    body["generationConfig"] = config;
                }
                else
                {
                    url = server + "/v1/chat/completions";
                    headers.Add(("Authorization", "Bearer " + key));
                    body["model"] = model;
                    var messages = new JArray();
                    if (!string.IsNullOrEmpty(system)) messages.Add(new JObject { ["role"] = "system", ["content"] = system });
                    foreach (var t in turns) messages.Add(new JObject { ["role"] = t.role, ["content"] = t.text });
                    body["messages"] = messages;
                    // OpenAI's newer models want max_completion_tokens (and count their thinking in it); other servers max_tokens.
                    bool newName = p == Provider.OpenAI && !oldTokenName.Contains(id);
                    body[newName ? "max_completion_tokens" : "max_tokens"] = p == Provider.OpenAI ? Math.Max(cap * 4, 1500) : cap;
                    if (!noTemperature.Contains(id)) body["temperature"] = temperature;
                }

                var (code, text, netError) = await Post(url, body.ToString(Newtonsoft.Json.Formatting.None), headers, timeoutSeconds);
                if (netError != null && code == 0) return (null, "Couldn't reach " + ProviderNames[(int)p] + " (" + netError + ").");
                if (code >= 200 && code < 300)
                {
                    string answer = Extract(p, text);
                    if (answer == null) return (null, ProviderNames[(int)p] + " sent an answer that couldn't be read.");
                    return (answer, null);
                }

                // A model that refuses a setting: leave it out (or rename it) and ask once more.
                string why = ErrorMessage(text);
                string lower = why.ToLowerInvariant();
                if (code == 400 && attempt < 2)
                {
                    if (lower.Contains("temperature") && noTemperature.Add(id)) continue;
                    if (p == Provider.OpenAI && lower.Contains("max_completion_tokens") && oldTokenName.Add(id)) continue;
                    if (p == Provider.Other && lower.Contains("max_tokens") && lower.Contains("max_completion_tokens") && !oldTokenName.Contains(id)) { oldTokenName.Add(id); continue; }
                }
                return (null, Explain(p, code, why));
            }
            return (null, "The service kept refusing the request.");
        }

        static string Extract(Provider p, string json)
        {
            try
            {
                var root = JObject.Parse(json);
                if (p == Provider.Claude)
                    return string.Concat((root["content"] as JArray ?? new JArray()).Where(b => (string)b["type"] == "text").Select(b => (string)b["text"]));
                if (p == Provider.Gemini)
                    return string.Concat(((root["candidates"]?[0]?["content"]?["parts"]) as JArray ?? new JArray()).Select(b => (string)b["text"] ?? ""));
                return (string)root["choices"]?[0]?["message"]?["content"] ?? "";
            }
            catch (Exception) { return null; }
        }

        static string ErrorMessage(string json)
        {
            try
            {
                var root = JToken.Parse(json);
                string m = (string)root["error"]?["message"] ?? (string)root["error"] ?? (string)root["message"];
                return string.IsNullOrEmpty(m) ? json : m;
            }
            catch (Exception) { return json ?? ""; }
        }

        static string Explain(Provider p, long code, string why)
        {
            string who = ProviderNames[(int)p];
            string detail = why.Length > 160 ? why.Substring(0, 160) + "…" : why;
            return code switch
            {
                401 or 403 => $"{who} didn't accept this API key ({code}). Check the key, and that the account is allowed to use the model.",
                404 => $"{who} doesn't know this model or address ({code}). Pick another model. {detail}",
                429 => $"{who} says too many requests, or the account is out of credit ({code}). {detail}",
                >= 500 => $"{who} has a problem at its end ({code}). {detail}",
                _ => $"{who} refused the request ({code}). {detail}",
            };
        }

        static async Task<(long code, string body, string error)> Post(string url, string json, List<(string, string)> headers, int timeoutSeconds)
        {
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            foreach (var (k, v) in headers) req.SetRequestHeader(k, v);
            req.timeout = timeoutSeconds;
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            bool net = req.result == UnityWebRequest.Result.ConnectionError;
            return (req.responseCode, req.downloadHandler?.text ?? "", net ? req.error : null);
        }

        static async Task<(long code, string body, string error)> Get(string url, List<(string, string)> headers, int timeoutSeconds)
        {
            using var req = UnityWebRequest.Get(url);
            foreach (var (k, v) in headers) req.SetRequestHeader(k, v);
            req.timeout = timeoutSeconds;
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            bool net = req.result == UnityWebRequest.Result.ConnectionError;
            return (req.responseCode, req.downloadHandler?.text ?? "", net ? req.error : null);
        }

        // ------------------------------------------------------------------ the page's buttons

        /// "Test": a tiny question to the chosen service. -> (worked, one plain sentence).
        public static async Task<(bool ok, string message)> Test()
        {
            var p = Current;
            var started = System.Diagnostics.Stopwatch.StartNew();
            var (text, error) = await Complete(p, Model(p), "Reply with the single word OK.",
                                               new List<(string, string)> { ("user", "Test") }, 0f, 16, 20);
            if (text == null) { Status = error; return (false, error); }
            string msg = $"Connected: {ProviderNames[(int)p]} · {Model(p)} answered in {started.Elapsed.TotalSeconds:0.0} s.";
            Status = msg;
            failures = 0;
            pausedUntil = DateTime.MinValue;
            return (true, msg);
        }

        /// The models this account can use (for the page's list). Empty list + a reason if it can't be fetched.
        public static async Task<(List<string> models, string error)> ListModels()
        {
            var p = Current;
            string key = SecretStore.Get(KeyName(p)), server = Server(p);
            if (key.Length == 0) return (new List<string>(), "Save an API key first.");
            if (server.Length == 0) return (new List<string>(), "Type the server's address first.");
            var headers = new List<(string, string)>();
            string url;
            if (p == Provider.Claude) { url = server + "/v1/models?limit=100"; headers.Add(("x-api-key", key)); headers.Add(("anthropic-version", "2023-06-01")); }
            else if (p == Provider.Gemini) { url = server + "/v1beta/models?pageSize=200"; headers.Add(("x-goog-api-key", key)); }
            else { url = server + "/v1/models"; headers.Add(("Authorization", "Bearer " + key)); }
            var (code, body, netError) = await Get(url, headers, 20);
            if (netError != null && code == 0) return (new List<string>(), $"Couldn't reach {ProviderNames[(int)p]} ({netError}).");
            if (code < 200 || code >= 300) return (new List<string>(), Explain(p, code, ErrorMessage(body)));
            try
            {
                var root = JObject.Parse(body);
                IEnumerable<string> ids;
                if (p == Provider.Gemini)
                    ids = (root["models"] as JArray ?? new JArray())
                        .Where(m => ((m["supportedGenerationMethods"] as JArray)?.Any(x => (string)x == "generateContent") ?? true))
                        .Select(m => ((string)m["name"] ?? "").Replace("models/", ""));
                else
                    ids = (root["data"] as JArray ?? new JArray()).Select(m => (string)m["id"] ?? "");
                string[] skip = { "embed", "whisper", "tts", "dall", "image", "audio", "moderation", "realtime", "transcribe", "davinci", "babbage", "search", "vision", "learnlm", "aqa", "veo", "imagen" };
                var list = ids.Where(i => i.Length > 0 && (p == Provider.Other || !skip.Any(s => i.ToLowerInvariant().Contains(s))))
                              .Distinct().OrderBy(i => i, StringComparer.OrdinalIgnoreCase).Take(60).ToList();
                return (list, list.Count == 0 ? "The service listed no models for this key." : null);
            }
            catch (Exception) { return (new List<string>(), "The model list couldn't be read."); }
        }
    }
}
