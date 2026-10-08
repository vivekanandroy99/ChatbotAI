using UnityEngine;

namespace ChatbotAI
{
    /// AI models picked in the app (menu > Advanced > AI models), kept on this computer. They take effect the next
    /// time the app starts - each model loads once at startup: the brain (LLMModelSelector), speech recognition
    /// (SpeechInputController) and the voices (TTSProcessManager). Empty = the one set in the Inspector.
    public static class ModelChoices
    {
        public const string Brain = "models/llm";
        public const string Ears = "models/whisper";
        public const string Kokoro = "models/kokoro";
        public const string Veena = "models/veena";
        public const string VeenaOff = "models/veena-off";
        /// "1" = the document-search reranker is not loaded (TTSServer knowledge.py), "0"/empty = on.
        public const string RerankerOff = "models/reranker-off";

        /// "1" = that optional voice engine is turned off, "0" = on, empty = as set in the Inspector.
        public static string OffKey(Audio.VoiceModelLibrary.Engine engine) => $"models/{Audio.VoiceModelLibrary.EngineId(engine)}-off";

        public static string Get(string key) => PlayerPrefs.GetString(key, "");

        public static void Set(string key, string value)
        {
            PlayerPrefs.SetString(key, value ?? "");
            PlayerPrefs.Save();
        }

        public static void Clear(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
