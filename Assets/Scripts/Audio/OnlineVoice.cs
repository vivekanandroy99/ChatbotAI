using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatbotAI.Audio
{
    /// Menu > Advanced > AI models > Online AI > Voice: sentences can be spoken by an online service instead of the voice on
    /// this PC - Sarvam (Indian languages), OpenAI, ElevenLabs, Google Gemini or any OpenAI-style server. Only the sentence's
    /// text goes out. The language comes from the text's script (Languages.OfText), the gender from the bot's own voice.
    /// If the service fails, that sentence is spoken by the voice on this PC. Keys live in SecretStore (OpenAI and Gemini
    /// share the key the online brain uses).
    public static class OnlineVoice
    {
        public enum Provider { Sarvam, OpenAI, ElevenLabs, Gemini, Other }

        public static readonly string[] ProviderNames = { "Sarvam", "OpenAI", "ElevenLabs", "Gemini", "Other" };

        const string EnabledKey = "online/voice", ProviderKey = "online/voice/provider";

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
            Provider.Sarvam => "bulbul:v3",
            Provider.OpenAI => "gpt-4o-mini-tts",
            Provider.ElevenLabs => "eleven_flash_v2_5",
            Provider.Gemini => "gemini-3.8-flash-tts",
            _ => "tts-1",
        };

        public static string Model(Provider p) => PlayerPrefs.GetString("online/voice/model/" + p, DefaultModel(p));

        public static void SetModel(Provider p, string model)
        {
            PlayerPrefs.SetString("online/voice/model/" + p, (model ?? "").Trim());
            PlayerPrefs.Save();
            TryAgain();
        }

        public static string DefaultServer(Provider p) => p switch
        {
            Provider.Sarvam => "https://api.sarvam.ai",
            Provider.OpenAI => "https://api.openai.com",
            Provider.ElevenLabs => "https://api.elevenlabs.io",
            Provider.Gemini => "https://generativelanguage.googleapis.com",
            _ => "",
        };

        public static string Server(Provider p)
        {
            string s = PlayerPrefs.GetString("online/voice/server/" + p, DefaultServer(p)).Trim().TrimEnd('/');
            if (s.Length == 0) s = DefaultServer(p);   // a cleared box means the official address
            return s.EndsWith("/v1") ? s.Substring(0, s.Length - 3) : s;
        }

        public static void SetServer(Provider p, string address)
        {
            PlayerPrefs.SetString("online/voice/server/" + p, (address ?? "").Trim());
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- voices

        // Voices each service offers. Sarvam's v3 voices speak every Sarvam language; OpenAI's and Gemini's speak all of theirs.
        public static readonly string[] SarvamV3Voices =
        {
            "shubh", "aditya", "ritu", "priya", "neha", "rahul", "pooja", "rohan", "simran", "kavya", "amit", "dev", "ishita", "shreya",
            "ratan", "varun", "manan", "sumit", "roopa", "kabir", "aayan", "ashutosh", "advait", "anand", "tanya", "tarun", "sunny",
            "mani", "gokul", "vijay", "shruti", "suhani", "mohit", "kavitha", "rehan", "soham", "rupali",
        };
        public static readonly string[] SarvamV2Voices = { "anushka", "manisha", "vidya", "arya", "abhilash", "karun", "hitesh" };
        public static readonly string[] OpenAIVoices = { "alloy", "ash", "ballad", "coral", "echo", "fable", "nova", "onyx", "sage", "shimmer", "verse" };
        public static readonly string[] GeminiVoices =
        {
            "Kore", "Leda", "Aoede", "Zephyr", "Callirrhoe", "Autonoe", "Despina", "Erinome", "Achernar", "Gacrux", "Vindemiatrix", "Sulafat",
            "Puck", "Charon", "Fenrir", "Orus", "Enceladus", "Iapetus", "Umbriel", "Algieba", "Algenib", "Rasalgethi", "Alnilam", "Schedar",
            "Achird", "Zubenelgenubi", "Sadachbia", "Sadaltager", "Laomedeia", "Pulcherrima",
        };

        /// The voices to choose from; empty for ElevenLabs and Other (a voice id is typed, or listed from the account).
        public static string[] VoiceChoices(Provider p) => p switch
        {
            Provider.Sarvam => Model(p).Contains("v2") ? SarvamV2Voices : SarvamV3Voices,
            Provider.OpenAI => OpenAIVoices,
            Provider.Gemini => GeminiVoices,
            _ => new string[0],
        };

        public static string DefaultVoice(Provider p, bool female) => p switch
        {
            Provider.Sarvam => Model(p).Contains("v2") ? (female ? "anushka" : "abhilash") : (female ? "priya" : "shubh"),
            Provider.OpenAI => female ? "nova" : "onyx",
            Provider.ElevenLabs => female ? "21m00Tcm4TlvDq8ikWAM" : "pNInz6obpgDQGcFmaJgB",   // ElevenLabs' premade Rachel and Adam
            Provider.Gemini => female ? "Kore" : "Charon",
            _ => female ? "nova" : "onyx",
        };

        public static string Voice(Provider p, bool female) =>
            PlayerPrefs.GetString($"online/voice/voice/{p}/{(female ? "f" : "m")}", DefaultVoice(p, female));

        public static void SetVoice(Provider p, bool female, string voice)
        {
            PlayerPrefs.SetString($"online/voice/voice/{p}/{(female ? "f" : "m")}", (voice ?? "").Trim());
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- keys

        // OpenAI and Gemini: the same key as the online brain's.
        static string KeyName(Provider p) => p == Provider.OpenAI || p == Provider.Gemini ? "api-key-" + p : "api-key-voice-" + p;
        public static bool HasKey(Provider p) => SecretStore.Has(KeyName(p));
        public static void SetKey(Provider p, string key) { SecretStore.Set(KeyName(p), (key ?? "").Trim()); TryAgain(); }
        public static void RemoveKey(Provider p) => SecretStore.Delete(KeyName(p));

        // ---------------------------------------------------------------- state

        public static bool Ready => Enabled && Configured(Current) && DateTime.UtcNow >= pausedUntil;

        static bool Configured(Provider p) => HasKey(p) && Server(p).Length > 0 && Voice(p, true).Length > 0 &&
                                              (Model(p).Length > 0 || p == Provider.ElevenLabs);

        public static string Status { get; private set; } = "";
        static DateTime pausedUntil = DateTime.MinValue;
        static int failures;

        static void TryAgain() { failures = 0; pausedUntil = DateTime.MinValue; }

        /// Can this service speak that language? (Sarvam: its eleven Indian languages and Indian English. The others: all.)
        public static bool CanSpeak(Provider p, string language)
        {
            if (p != Provider.Sarvam) return true;
            return Languages.TryGet(language, out var l) && l.sarvam != null;
        }

        /// The online voice is on and can say this language (a reply in it is worth writing).
        public static bool Speaks(string language) => Ready && CanSpeak(Current, language);

        /// The sentence's request to the service, or null when the voice on this PC should speak it (not set up, paused,
        /// or the service can't speak that language). The stream is the request's download handler.
        public static UnityWebRequest Request(string text, string localVoice, float speed)
        {
            if (!Ready) return null;
            var p = Current;
            string language = Languages.OfText(text);
            if (!CanSpeak(p, language)) return null;
            bool female = !VoiceCatalog.TryGet(VoiceCatalog.Current(localVoice), out var v) || v.female;
            return Build(p, text, language, female, speed);
        }

        static UnityWebRequest Build(Provider p, string text, string language, bool female, float speed)
        {
            string key = SecretStore.Get(KeyName(p)), server = Server(p), model = Model(p), voice = Voice(p, female);
            string url;
            var body = new JObject();
            var headers = new List<(string, string)>();
            PcmStream stream;
            speed = speed <= 0f ? 1f : speed;

            switch (p)
            {
                case Provider.Sarvam:
                {
                    url = server + "/text-to-speech";
                    headers.Add(("api-subscription-key", key));
                    string code = Languages.TryGet(language, out var l) && l.sarvam != null ? l.sarvam : "en-IN";
                    body["text"] = text;
                    body["language_code"] = code;
                    body["target_language_code"] = code;     // the older name of the same field
                    body["speaker"] = voice;
                    body["model"] = model;
                    body["pace"] = Mathf.Clamp(speed, 0.5f, 2f);
                    body["speech_sample_rate"] = 24000;
                    body["output_audio_codec"] = "wav";
                    stream = new PcmStream(0, DecodeSarvam);
                    break;
                }
                case Provider.ElevenLabs:
                {
                    url = $"{server}/v1/text-to-speech/{UnityWebRequest.EscapeURL(voice)}?output_format=pcm_24000";
                    headers.Add(("xi-api-key", key));
                    body["text"] = text;
                    if (model.Length > 0) body["model_id"] = model;
                    body["voice_settings"] = new JObject { ["speed"] = Mathf.Clamp(speed, 0.7f, 1.2f) };
                    stream = new PcmStream(24000, null);
                    break;
                }
                case Provider.Gemini:
                {
                    url = $"{server}/v1beta/models/{model}:generateContent";
                    headers.Add(("x-goog-api-key", key));
                    body["contents"] = new JArray(new JObject { ["parts"] = new JArray(new JObject { ["text"] = text }) });
                    body["generationConfig"] = new JObject
                    {
                        ["responseModalities"] = new JArray("AUDIO"),
                        ["speechConfig"] = new JObject
                        {
                            ["voiceConfig"] = new JObject { ["prebuiltVoiceConfig"] = new JObject { ["voiceName"] = voice } },
                        },
                    };
                    stream = new PcmStream(0, DecodeGemini);
                    break;
                }
                default:    // OpenAI and anything that speaks the same way
                {
                    url = server + "/v1/audio/speech";
                    headers.Add(("Authorization", "Bearer " + key));
                    body["model"] = model;
                    body["input"] = text;
                    body["voice"] = voice;
                    body["response_format"] = "pcm";           // raw 24 kHz 16-bit mono
                    body["speed"] = Mathf.Clamp(speed, 0.25f, 4f);
                    stream = new PcmStream(24000, null);
                    break;
                }
            }

            stream.Online = true;
            var req = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None))),
                downloadHandler = stream,
                timeout = 30,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            foreach (var (k, v) in headers) req.SetRequestHeader(k, v);
            req.SendWebRequest();
            return req;
        }

        // ---------------------------------------------------------------- decoding replies that carry base64 audio

        static (int rate, byte[] pcm) DecodeSarvam(byte[] reply)
        {
            var root = JObject.Parse(Encoding.UTF8.GetString(reply));
            string b64 = (string)root["audios"]?[0];
            return string.IsNullOrEmpty(b64) ? (0, null) : WavToPcm(Convert.FromBase64String(b64));
        }

        static (int rate, byte[] pcm) DecodeGemini(byte[] reply)
        {
            var root = JObject.Parse(Encoding.UTF8.GetString(reply));
            var data = root["candidates"]?[0]?["content"]?["parts"]?[0]?["inlineData"];
            string b64 = (string)data?["data"];
            if (string.IsNullOrEmpty(b64)) return (0, null);
            byte[] bytes = Convert.FromBase64String(b64);
            string mime = ((string)data["mimeType"] ?? "").ToLowerInvariant();
            if (mime.Contains("wav") || (bytes.Length > 12 && bytes[0] == 'R' && bytes[1] == 'I')) return WavToPcm(bytes);
            var m = Regex.Match(mime, @"rate=(\d+)");
            return (m.Success ? int.Parse(m.Groups[1].Value) : 24000, bytes);   // raw 16-bit mono
        }

        /// 16-bit PCM out of a WAV file (stereo is mixed down to mono).
        public static (int rate, byte[] pcm) WavToPcm(byte[] wav)
        {
            if (wav.Length < 44 || wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F') return (0, null);
            int rate = 0, channels = 1, bits = 16;
            for (int at = 12; at + 8 <= wav.Length;)
            {
                string id = Encoding.ASCII.GetString(wav, at, 4);
                long size = BitConverter.ToUInt32(wav, at + 4);
                int body = at + 8;
                if (id == "fmt " && body + 16 <= wav.Length)
                {
                    channels = BitConverter.ToInt16(wav, body + 2);
                    rate = BitConverter.ToInt32(wav, body + 4);
                    bits = BitConverter.ToInt16(wav, body + 14);
                }
                else if (id == "data")
                {
                    int length = (int)Math.Min(size, wav.Length - body);    // a streamed WAV may say 0xFFFFFFFF
                    if (bits != 16 || rate <= 0) return (0, null);
                    var pcm = new byte[length];
                    Buffer.BlockCopy(wav, body, pcm, 0, length);
                    if (channels == 2)
                    {
                        var mono = new byte[length / 2 / 2 * 2];
                        for (int i = 0; i + 1 < mono.Length; i += 2)
                        {
                            int a = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8)), b = (short)(pcm[i * 2 + 2] | (pcm[i * 2 + 3] << 8));
                            short mix = (short)((a + b) / 2);
                            mono[i] = (byte)(mix & 0xFF);
                            mono[i + 1] = (byte)((mix >> 8) & 0xFF);
                        }
                        pcm = mono;
                    }
                    return (rate, pcm);
                }
                at = body + (int)Math.Min(size + (size & 1), int.MaxValue - body);
            }
            return (0, null);
        }

        // ---------------------------------------------------------------- results

        public static void NoteSuccess(float seconds)
        {
            failures = 0;
            Status = $"Spoken by {ProviderNames[(int)Current]} ({seconds:0.0} s to the first sound).";
        }

        /// A sentence the service could not speak: say why, and after two in a row use this PC's voice for a minute.
        public static void NoteFailure(long code, string reply, string networkError)
        {
            Status = Explain(Current, code, reply, networkError);
            if (++failures >= 2) pausedUntil = DateTime.UtcNow.AddSeconds(60);
            Debug.LogWarning($"OnlineVoice: this sentence is spoken by the voice on this PC ({Status}).");
        }

        static string Explain(Provider p, long code, string reply, string networkError)
        {
            string who = ProviderNames[(int)p];
            if (code == 0) return $"Couldn't reach {who} ({networkError}).";
            string detail = Detail(reply);
            if (detail.Length > 160) detail = detail.Substring(0, 160) + "…";
            return code switch
            {
                401 or 403 => $"{who} didn't accept this API key ({code}). Check the key and that the account may use voices.",
                404 => $"{who} doesn't know this model, voice or address ({code}). {detail}",
                422 or 400 => $"{who} refused the request ({code}) - often a voice or model name that doesn't exist. {detail}",
                429 => $"{who} says too many requests, or the account is out of credit ({code}). {detail}",
                >= 500 => $"{who} has a problem at its end ({code}). {detail}",
                _ => $"{who} refused the request ({code}). {detail}",
            };
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

        /// "Test": speaks a short line and reports how long the first sound took. The audio is not played.
        public static async Task<(bool ok, string message)> Test()
        {
            var p = Current;
            if (!HasKey(p)) return (false, "Save an API key first.");
            string line = "Hello! This is a test of the online voice.";
            var started = System.Diagnostics.Stopwatch.StartNew();
            bool paused = DateTime.UtcNow < pausedUntil;
            using var req = Build(p, line, "en", true, 1f);
            while (!req.isDone) await Task.Yield();
            var stream = (PcmStream)req.downloadHandler;
            bool net = req.result == UnityWebRequest.Result.ConnectionError;
            if (net || req.responseCode < 200 || req.responseCode >= 300 || stream.SampleRate == 0)
            {
                bool unreadable = !net && req.responseCode >= 200 && req.responseCode < 300;
                string message = unreadable
                    ? $"{ProviderNames[(int)p]} answered, but its audio couldn't be read. {Detail(stream.Head)}"
                    : Explain(p, net ? 0 : req.responseCode, stream.Head, req.error);
                Status = message;
                return (false, message);
            }
            if (paused) TryAgain();
            failures = 0;
            string ok = $"Connected: {ProviderNames[(int)p]} · {Model(p)} spoke {stream.ReceivedSeconds:0.0} s of audio in {started.Elapsed.TotalSeconds:0.0} s.";
            Status = ok;
            pausedUntil = DateTime.MinValue;
            return (true, ok);
        }

        /// ElevenLabs: the voices on the account (name, id) for the page's list.
        public static async Task<(List<(string name, string id)> voices, string error)> ListElevenLabsVoices()
        {
            var empty = new List<(string, string)>();
            if (!HasKey(Provider.ElevenLabs)) return (empty, "Save an API key first.");
            using var req = UnityWebRequest.Get(Server(Provider.ElevenLabs) + "/v1/voices");
            req.SetRequestHeader("xi-api-key", SecretStore.Get(KeyName(Provider.ElevenLabs)));
            req.timeout = 20;
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            if (req.result == UnityWebRequest.Result.ConnectionError) return (empty, $"Couldn't reach ElevenLabs ({req.error}).");
            if (req.responseCode < 200 || req.responseCode >= 300) return (empty, Explain(Provider.ElevenLabs, req.responseCode, req.downloadHandler.text, req.error));
            try
            {
                var root = JObject.Parse(req.downloadHandler.text);
                var list = (root["voices"] as JArray ?? new JArray())
                    .Select(v => ((string)v["name"] ?? "", (string)v["voice_id"] ?? "")).Where(v => v.Item2.Length > 0).Take(80).ToList();
                return (list, list.Count == 0 ? "The account lists no voices." : null);
            }
            catch (Exception) { return (empty, "The voice list couldn't be read."); }
        }
    }
}
