using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatbotAI.Audio
{
    /// Menu > Advanced > AI models > Online AI > Ears: what a visitor says can be turned into text by an online service instead
    /// of Whisper on this PC - Groq (free tier), Sarvam (Indian languages), OpenAI, ElevenLabs, Google Gemini or any OpenAI-style
    /// server. The recording of the visitor's voice goes out (after the local noise filter has decided there is a voice), so
    /// the page says so. The service also reports the language, which the bot answers in. If the service fails, Whisper on this PC
    /// listens to that question. Keys are in SecretStore, shared with the voice and brain pages where it is the same service.
    public static class OnlineEars
    {
        public enum Provider { Groq, Sarvam, OpenAI, ElevenLabs, Gemini, Other }

        public static readonly string[] ProviderNames = { "Groq", "Sarvam", "OpenAI", "ElevenLabs", "Gemini", "Other" };

        const string EnabledKey = "online/ears", ProviderKey = "online/ears/provider";

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

        public static string DefaultModel(Provider p) => p switch
        {
            Provider.Groq => "whisper-large-v3-turbo",
            Provider.Sarvam => "saaras:v4",
            Provider.OpenAI => "gpt-4o-mini-transcribe",
            Provider.ElevenLabs => "scribe_v2",
            Provider.Gemini => "gemini-3.5-flash",
            _ => "whisper-1",
        };

        public static string Model(Provider p) => PlayerPrefs.GetString("online/ears/model/" + p, DefaultModel(p));

        public static void SetModel(Provider p, string model)
        {
            PlayerPrefs.SetString("online/ears/model/" + p, (model ?? "").Trim());
            PlayerPrefs.Save();
            TryAgain();
        }

        public static string DefaultServer(Provider p) => p switch
        {
            Provider.Groq => "https://api.groq.com/openai",
            Provider.Sarvam => "https://api.sarvam.ai",
            Provider.OpenAI => "https://api.openai.com",
            Provider.ElevenLabs => "https://api.elevenlabs.io",
            Provider.Gemini => "https://generativelanguage.googleapis.com",
            _ => "",
        };

        public static string Server(Provider p)
        {
            string s = PlayerPrefs.GetString("online/ears/server/" + p, DefaultServer(p)).Trim().TrimEnd('/');
            if (s.Length == 0) s = DefaultServer(p);
            return s.EndsWith("/v1") ? s.Substring(0, s.Length - 3) : s;
        }

        public static void SetServer(Provider p, string address)
        {
            PlayerPrefs.SetString("online/ears/server/" + p, (address ?? "").Trim());
            PlayerPrefs.Save();
        }

        // The same service keeps one key across the brain, voice and ears pages.
        static string KeyName(Provider p) => p switch
        {
            Provider.OpenAI => "api-key-OpenAI",
            Provider.Gemini => "api-key-Gemini",
            Provider.Sarvam => "api-key-voice-Sarvam",
            Provider.ElevenLabs => "api-key-voice-ElevenLabs",
            _ => "api-key-ears-" + p,
        };
        public static bool HasKey(Provider p) => SecretStore.Has(KeyName(p));
        public static void SetKey(Provider p, string key) { SecretStore.Set(KeyName(p), (key ?? "").Trim()); TryAgain(); }
        public static void RemoveKey(Provider p) => SecretStore.Delete(KeyName(p));

        public static bool Ready => Enabled && HasKey(Current) && Server(Current).Length > 0 && Model(Current).Length > 0 &&
                                    DateTime.UtcNow >= pausedUntil;

        public static string Status { get; private set; } = "";
        static DateTime pausedUntil = DateTime.MinValue;
        static int failures;

        static void TryAgain() { failures = 0; pausedUntil = DateTime.MinValue; }

        /// The language the visitor speaks, if the menu says so ("" = let the service tell). Two-letter code.
        public const string ListenCodeKey = "companion/listen-language-code";
        public static string ListenCode
        {
            get => PlayerPrefs.GetString(ListenCodeKey, "");
            set { PlayerPrefs.SetString(ListenCodeKey, value ?? ""); PlayerPrefs.Save(); }
        }

        // ---------------------------------------------------------------- the call

        /// What was said. text == null: the service could not be used (Status says why) - the caller listens with Whisper instead.
        /// language: two-letter code the service reported (or guessed from the text's script), null if unknown.
        public static async Task<(string text, string language)> Transcribe(float[] samples, int frequency, string hint, string vocabulary)
        {
            var p = Current;
            var started = System.Diagnostics.Stopwatch.StartNew();
            var (text, language, error) = await Call(p, Wav(samples, frequency), hint, vocabulary, 30);
            if (text == null)
            {
                Status = error;
                if (++failures >= 2) pausedUntil = DateTime.UtcNow.AddSeconds(60);
                Debug.LogWarning($"OnlineEars: listening with Whisper on this PC for this question ({error}).");
                return (null, null);
            }
            failures = 0;
            language = language ?? Languages.ScriptLanguage(text);
            Status = $"Heard by {ProviderNames[(int)p]} in {started.Elapsed.TotalSeconds:0.0} s" + (language != null ? $" ({Languages.NameOf(language)})." : ".");
            return (text.Trim(), language);
        }

        static async Task<(string text, string language, string error)> Call(Provider p, byte[] wav, string hint, string vocabulary, int timeoutSeconds)
        {
            string key = SecretStore.Get(KeyName(p)), server = Server(p), model = Model(p);
            if (key.Length == 0) return (null, null, "No API key saved.");
            if (server.Length == 0) return (null, null, "No server address.");
            if (model.Length == 0) return (null, null, "No model chosen.");
            string who = ProviderNames[(int)p];
            hint = string.IsNullOrEmpty(hint) ? null : hint;

            UnityWebRequest req;
            if (p == Provider.Gemini)
            {
                var body = new JObject
                {
                    ["contents"] = new JArray(new JObject
                    {
                        ["parts"] = new JArray(
                            new JObject { ["text"] = "Transcribe this audio exactly as spoken, in its own language and script" +
                                                     (hint != null ? $" (it is probably {Languages.NameOf(hint)})" : "") +
                                                     ". Reply with only JSON: {\"language\": \"<two-letter ISO 639-1 code>\", \"text\": \"<transcript>\"}. " +
                                                     "If there is no speech, use an empty text." },
                            new JObject { ["inlineData"] = new JObject { ["mimeType"] = "audio/wav", ["data"] = Convert.ToBase64String(wav) } }),
                    }),
                    ["generationConfig"] = new JObject { ["responseMimeType"] = "application/json", ["temperature"] = 0 },
                };
                req = new UnityWebRequest($"{server}/v1beta/models/{model}:generateContent", "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None))),
                    downloadHandler = new DownloadHandlerBuffer(),
                };
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("x-goog-api-key", key);
            }
            else
            {
                var form = new List<IMultipartFormSection> { new MultipartFormFileSection("file", wav, "speech.wav", "audio/wav") };
                string url;
                string headerName = "Authorization", headerValue = "Bearer " + key;
                if (p == Provider.Sarvam)
                {
                    url = server + "/speech-to-text";
                    headerName = "api-subscription-key"; headerValue = key;
                    form.Add(new MultipartFormDataSection("model", model));
                    string code = hint != null && Languages.TryGet(hint, out var l) && l.sarvam != null ? l.sarvam : "unknown";
                    form.Add(new MultipartFormDataSection("language_code", code));
                }
                else if (p == Provider.ElevenLabs)
                {
                    url = server + "/v1/speech-to-text";
                    headerName = "xi-api-key"; headerValue = key;
                    form.Add(new MultipartFormDataSection("model_id", model));
                    form.Add(new MultipartFormDataSection("tag_audio_events", "false"));
                    if (hint != null) form.Add(new MultipartFormDataSection("language_code", hint));
                }
                else    // Groq, OpenAI and anything that speaks the same way
                {
                    url = server + "/v1/audio/transcriptions";
                    form.Add(new MultipartFormDataSection("model", model));
                    // Whisper models can say which language they heard (verbose_json); the newer ones only give text.
                    form.Add(new MultipartFormDataSection("response_format", model.ToLowerInvariant().Contains("whisper") ? "verbose_json" : "json"));
                    if (hint != null) form.Add(new MultipartFormDataSection("language", hint));
                    if (!string.IsNullOrWhiteSpace(vocabulary) && (hint == null || hint == "en")) form.Add(new MultipartFormDataSection("prompt", vocabulary));
                }
                req = UnityWebRequest.Post(url, form);
                req.SetRequestHeader(headerName, headerValue);
            }

            using (req)
            {
                req.timeout = timeoutSeconds;
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                string reply = req.downloadHandler?.text ?? "";
                if (req.result == UnityWebRequest.Result.ConnectionError) return (null, null, $"Couldn't reach {who} ({req.error}).");
                if (req.responseCode < 200 || req.responseCode >= 300) return (null, null, Explain(who, req.responseCode, Detail(reply)));
                try
                {
                    var root = JObject.Parse(reply);
                    if (p == Provider.Gemini)
                    {
                        string raw = string.Concat((root["candidates"]?[0]?["content"]?["parts"] as JArray ?? new JArray()).Select(b => (string)b["text"] ?? ""));
                        var inner = JObject.Parse(raw);
                        return ((string)inner["text"] ?? "", Languages.FromAnyCode((string)inner["language"]), null);
                    }
                    if (p == Provider.Sarvam)
                        return ((string)root["transcript"] ?? "", Languages.FromAnyCode((string)root["language_code"]), null);
                    return ((string)root["text"] ?? "", Languages.FromAnyCode((string)root["language"] ?? (string)root["language_code"]), null);
                }
                catch (Exception) { return (null, null, $"{who} sent an answer that couldn't be read."); }
            }
        }

        static string Detail(string json)
        {
            try
            {
                var root = JToken.Parse(json);
                string m = (string)root["error"]?["message"] ?? (string)root["error"] ?? (string)root["detail"]?["message"] ??
                           (string)root["message"] ?? (root["detail"]?.Type == JTokenType.String ? (string)root["detail"] : null);
                return string.IsNullOrEmpty(m) ? json : m;
            }
            catch (Exception) { return json ?? ""; }
        }

        static string Explain(string who, long code, string why)
        {
            string detail = why.Length > 160 ? why.Substring(0, 160) + "…" : why;
            return code switch
            {
                401 or 403 => $"{who} didn't accept this API key ({code}). Check the key and that the account may transcribe speech.",
                404 => $"{who} doesn't know this model or address ({code}). {detail}",
                413 => $"{who} says the recording is too long ({code}).",
                429 => $"{who} says too many requests, or the account is out of credit ({code}). {detail}",
                >= 500 => $"{who} has a problem at its end ({code}). {detail}",
                _ => $"{who} refused the request ({code}). {detail}",
            };
        }

        /// 16-bit mono WAV of the recording.
        public static byte[] Wav(float[] samples, int frequency)
        {
            int n = samples.Length;
            var bytes = new byte[44 + n * 2];
            void Put(int at, string s) { for (int i = 0; i < s.Length; i++) bytes[at + i] = (byte)s[i]; }
            void Put32(int at, int v) { bytes[at] = (byte)v; bytes[at + 1] = (byte)(v >> 8); bytes[at + 2] = (byte)(v >> 16); bytes[at + 3] = (byte)(v >> 24); }
            void Put16(int at, int v) { bytes[at] = (byte)v; bytes[at + 1] = (byte)(v >> 8); }
            Put(0, "RIFF"); Put32(4, 36 + n * 2); Put(8, "WAVEfmt "); Put32(16, 16); Put16(20, 1); Put16(22, 1);
            Put32(24, frequency); Put32(28, frequency * 2); Put16(32, 2); Put16(34, 16); Put(36, "data"); Put32(40, n * 2);
            for (int i = 0; i < n; i++) Put16(44 + i * 2, (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767));
            return bytes;
        }

        /// "Test": one second of silence to the chosen service - proves the key, the model and the address.
        public static async Task<(bool ok, string message)> Test()
        {
            var p = Current;
            var started = System.Diagnostics.Stopwatch.StartNew();
            var (text, _, error) = await Call(p, Wav(new float[16000], 16000), null, null, 25);
            if (text == null) { Status = error; return (false, error); }
            string msg = $"Connected: {ProviderNames[(int)p]} · {Model(p)} answered in {started.Elapsed.TotalSeconds:0.0} s.";
            Status = msg;
            TryAgain();
            return (true, msg);
        }
    }
}
