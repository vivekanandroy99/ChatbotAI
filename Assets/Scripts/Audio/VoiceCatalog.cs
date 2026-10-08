using System.Collections.Generic;
using System.Linq;

namespace ChatbotAI.Audio
{
    /// The premade voices for the languages the avatar speaks: Kokoro-82M's own
    /// (TTSServer/models/voices-v1.0.bin, grades from Kokoro's voice guide, A best -> F), plus the
    /// optional Hindi engines' voices. Use the Inspector's Preview button to hear them.
    public static class VoiceCatalog
    {
        public enum Language { English, Hindi }

        public readonly struct Voice
        {
            public readonly string id;
            public readonly Language language;
            public readonly bool female;
            public readonly string description;
            /// Null for Kokoro's built-in voices; otherwise the optional engine to download.
            public readonly VoiceEngineDownloads.Engine engine;

            public Voice(string id, Language language, bool female, string description, VoiceEngineDownloads.Engine engine = null)
            {
                this.id = id;
                this.language = language;
                this.female = female;
                this.description = description;
                this.engine = engine;
            }

            public string Label => $"{(female ? "Female" : "Male")} - {description}  ({id})";
        }

        public static readonly Voice[] All =
        {
            // US English, female
            new Voice("af_heart", Language.English, true, "American - best (A)"),
            new Voice("af_bella", Language.English, true, "American - very good (A-)"),
            new Voice("af_nicole", Language.English, true, "American - very good (B-)"),
            new Voice("af_aoede", Language.English, true, "American - good (C+)"),
            new Voice("af_kore", Language.English, true, "American - good (C+)"),
            new Voice("af_sarah", Language.English, true, "American - good (C+)"),
            new Voice("af_nova", Language.English, true, "American - good (C)"),
            new Voice("af_alloy", Language.English, true, "American - good (C)"),
            new Voice("af_sky", Language.English, true, "American - fair (C-)"),
            new Voice("af_jessica", Language.English, true, "American - weak (D)"),
            new Voice("af_river", Language.English, true, "American - weak (D)"),
            // British English, female
            new Voice("bf_emma", Language.English, true, "British - very good (B-)"),
            new Voice("bf_isabella", Language.English, true, "British - good (C)"),
            new Voice("bf_alice", Language.English, true, "British - weak (D)"),
            new Voice("bf_lily", Language.English, true, "British - weak (D)"),
            // US English, male
            new Voice("am_michael", Language.English, false, "American - good (C+)"),
            new Voice("am_fenrir", Language.English, false, "American - good (C+)"),
            new Voice("am_puck", Language.English, false, "American - good (C+)"),
            new Voice("am_echo", Language.English, false, "American - weak (D)"),
            new Voice("am_eric", Language.English, false, "American - weak (D)"),
            new Voice("am_liam", Language.English, false, "American - weak (D)"),
            new Voice("am_onyx", Language.English, false, "American - weak (D)"),
            new Voice("am_adam", Language.English, false, "American - poor (F+)"),
            new Voice("am_santa", Language.English, false, "American, novelty - weak (D-)"),
            // British English, male
            new Voice("bm_george", Language.English, false, "British - good (C)"),
            new Voice("bm_fable", Language.English, false, "British - good (C)"),
            new Voice("bm_lewis", Language.English, false, "British - fair (D+)"),
            new Voice("bm_daniel", Language.English, false, "British - weak (D)"),
            // Kokoro v1.1 (optional download): its only three English voices; the v1.1 model is mostly
            // trained for Chinese, so these had less English training than the ones above. Not graded.
            new Voice("bf_vale", Language.English, true, "British - Kokoro v1.1, Vale", VoiceEngineDownloads.KokoroV11),
            new Voice("af_maple", Language.English, true, "American - Kokoro v1.1, Maple", VoiceEngineDownloads.KokoroV11),
            new Voice("af_sol", Language.English, true, "American - Kokoro v1.1, Sol", VoiceEngineDownloads.KokoroV11),
            // Veena (optional download, see Hindi): the same voices as in Hindi, so an avatar can sound like
            // one person in both languages. Indian accent; brand names may need a pronunciation fix.
            new Voice("veena_kavya", Language.English, true, "Indian - Veena, Kavya (same voice as Hindi)", VoiceEngineDownloads.Veena),
            new Voice("veena_maitri", Language.English, true, "Indian - Veena, Maitri (same voice as Hindi)", VoiceEngineDownloads.Veena),
            // Hindi
            new Voice("hf_alpha", Language.Hindi, true, "Hindi - good (C)"),
            new Voice("hf_beta", Language.Hindi, true, "Hindi - good (C)"),
            new Voice("hm_omega", Language.Hindi, false, "Hindi - good (C)"),
            new Voice("hm_psi", Language.Hindi, false, "Hindi - good (C)"),
            // Hindi, Veena (optional download): studio voices with a natural Indian accent, native Hinglish.
            new Voice("veena_kavya", Language.Hindi, true, "Hindi HD - Veena, Kavya (natural accent)", VoiceEngineDownloads.Veena),
            new Voice("veena_maitri", Language.Hindi, true, "Hindi HD - Veena, Maitri (natural accent, brighter)", VoiceEngineDownloads.Veena),
            new Voice("veena_vinaya", Language.Hindi, false, "Hindi HD - Veena, Vinaya (natural accent)", VoiceEngineDownloads.Veena),
            // Hindi, IndicF5 (optional download): natural Hindi from AI4Bharat's own sample speakers.
            // Clean native-Hindi studio references (IIT Madras IndicTTS) - the best-sounding ones.
            new Voice("indicf5_hin_female", Language.Hindi, true, "Hindi (experimental) - IndicF5, native Hindi (best)", VoiceEngineDownloads.IndicF5),
            new Voice("indicf5_hin_female2", Language.Hindi, true, "Hindi (experimental) - IndicF5, native Hindi, alt take", VoiceEngineDownloads.IndicF5),
            new Voice("indicf5_hin_male", Language.Hindi, false, "Hindi (experimental) - IndicF5, native Hindi (best)", VoiceEngineDownloads.IndicF5),
            new Voice("indicf5_hin_male2", Language.Hindi, false, "Hindi (experimental) - IndicF5, native Hindi, alt take", VoiceEngineDownloads.IndicF5),
            // Noisier references: their background hiss comes through in the voice.
            new Voice("indicf5_hindi", Language.Hindi, true, "Hindi (experimental) - IndicF5, model's own sample", VoiceEngineDownloads.IndicF5),
            new Voice("indicf5_female", Language.Hindi, true, "Hindi (experimental) - IndicF5, Marathi speaker (noisy)", VoiceEngineDownloads.IndicF5),
            new Voice("indicf5_male", Language.Hindi, false, "Hindi (experimental) - IndicF5, Marathi speaker (noisy)", VoiceEngineDownloads.IndicF5),
        };

        public static Voice[] For(Language language) => All.Where(v => v.language == language).ToArray();

        /// Voices of engines removed in v5 (Svara, AI4Bharat, Piper; 2026-10-07) -> the Veena voice of the same gender
        /// (Svara's English male -> Kokoro's am_michael), for saved settings and imported bots that still name them.
        static readonly Dictionary<string, string> Retired = new Dictionary<string, string>
        {
            ["svara_hindi_female"] = "veena_kavya", ["svara_english_female"] = "veena_kavya",
            ["svara_hindi_male"] = "veena_vinaya", ["svara_english_male"] = "am_michael",
            ["indictts_female"] = "veena_kavya", ["indictts_male"] = "veena_vinaya",
            ["piper_priyamvada"] = "veena_kavya", ["piper_rohan"] = "veena_vinaya", ["piper_pratham"] = "veena_vinaya",
        };

        public static string Current(string id) => id != null && Retired.TryGetValue(id, out var now) ? now : id;

        /// (Veena's voices are listed under both languages; either entry answers for engine and gender.)
        public static bool TryGet(string id, out Voice voice)
        {
            foreach (var v in All)
                if (v.id == id) { voice = v; return true; }
            voice = default;
            return false;
        }

        public static readonly IReadOnlyDictionary<Language, string> PreviewLines = new Dictionary<Language, string>
        {
            [Language.English] = "Hi there! This is how I sound. Ask me anything you like.",
            [Language.Hindi] = "नमस्ते! मेरी आवाज़ ऐसी है। आप मुझसे कुछ भी पूछ सकते हैं।",
        };
    }
}
