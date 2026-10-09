using System.Collections.Generic;
using System.Linq;

namespace ChatbotAI.Audio
{
    /// Every language the online services can be asked for - Indian and international. Adding a language here makes it
    /// appear in the menu (Language & listening > More languages); the online brain, ears and voice take it from there.
    /// Codes are ISO 639-1 (what Whisper and most services use); Sarvam wants its own "hi-IN" form.
    public static class Languages
    {
        public readonly struct Language
        {
            public readonly string code, name, native, sarvam;
            public readonly bool indian;
            public Language(string code, string name, string native, string sarvam = null, bool indian = false)
            {
                this.code = code; this.name = name; this.native = native; this.sarvam = sarvam; this.indian = indian;
            }
            public string Label => native == name ? name : $"{name} · {native}";
        }

        public static readonly Language[] All =
        {
            new Language("en", "English", "English", "en-IN"),
            // India. Sarvam's voices and ears cover the first eleven; the others come from the general services.
            new Language("hi", "Hindi", "हिन्दी", "hi-IN", true),
            new Language("bn", "Bengali", "বাংলা", "bn-IN", true),
            new Language("ta", "Tamil", "தமிழ்", "ta-IN", true),
            new Language("te", "Telugu", "తెలుగు", "te-IN", true),
            new Language("mr", "Marathi", "मराठी", "mr-IN", true),
            new Language("gu", "Gujarati", "ગુજરાતી", "gu-IN", true),
            new Language("kn", "Kannada", "ಕನ್ನಡ", "kn-IN", true),
            new Language("ml", "Malayalam", "മലയാളം", "ml-IN", true),
            new Language("pa", "Punjabi", "ਪੰਜਾਬੀ", "pa-IN", true),
            new Language("or", "Odia", "ଓଡ଼ିଆ", "od-IN", true),
            new Language("ur", "Urdu", "اردو", null, true),
            new Language("ne", "Nepali", "नेपाली", null, true),
            // The rest of the world.
            new Language("es", "Spanish", "Español"),
            new Language("fr", "French", "Français"),
            new Language("de", "German", "Deutsch"),
            new Language("it", "Italian", "Italiano"),
            new Language("pt", "Portuguese", "Português"),
            new Language("nl", "Dutch", "Nederlands"),
            new Language("sv", "Swedish", "Svenska"),
            new Language("da", "Danish", "Dansk"),
            new Language("no", "Norwegian", "Norsk"),
            new Language("fi", "Finnish", "Suomi"),
            new Language("pl", "Polish", "Polski"),
            new Language("cs", "Czech", "Čeština"),
            new Language("ro", "Romanian", "Română"),
            new Language("hu", "Hungarian", "Magyar"),
            new Language("el", "Greek", "Ελληνικά"),
            new Language("tr", "Turkish", "Türkçe"),
            new Language("ru", "Russian", "Русский"),
            new Language("uk", "Ukrainian", "Українська"),
            new Language("bg", "Bulgarian", "Български"),
            new Language("ar", "Arabic", "العربية"),
            new Language("he", "Hebrew", "עברית"),
            new Language("fa", "Persian", "فارسی"),
            new Language("id", "Indonesian", "Bahasa Indonesia"),
            new Language("ms", "Malay", "Bahasa Melayu"),
            new Language("vi", "Vietnamese", "Tiếng Việt"),
            new Language("th", "Thai", "ไทย"),
            new Language("tl", "Filipino", "Filipino"),
            new Language("zh", "Chinese", "中文"),
            new Language("ja", "Japanese", "日本語"),
            new Language("ko", "Korean", "한국어"),
            new Language("sw", "Swahili", "Kiswahili"),
        };

        public static bool TryGet(string code, out Language language)
        {
            foreach (var l in All)
                if (l.code == code) { language = l; return true; }
            language = default;
            return false;
        }

        public static string NameOf(string code) => TryGet(code, out var l) ? l.name : code;

        /// The language of Latin-script replies (French, Spanish...) - text alone can't say, so whoever decides the reply language
        /// (the dialogue controller) sets it. English unless a reply is being given in another Latin-script language.
        public static string LatinReply { get; set; } = "en";

        /// The language a text is in, from its script; Latin letters mean the current Latin-script reply language.
        /// A script shared by several languages (Devanagari, Arabic, Cyrillic) goes to ScriptPreference if that is one of them.
        public static string OfText(string text) => Refine(ScriptLanguage(text) ?? LatinReply, ScriptPreference);

        /// Set by whoever decides the language of the reply (the dialogue controller): settles Marathi vs Hindi, Urdu vs Arabic...
        public static string ScriptPreference { get; set; }

        /// The language a question was spoken in, as the online ears reported it; taken (and cleared) by the next question.
        public static string HeardLanguage { get; set; }

        public static string TakeHeard()
        {
            string heard = HeardLanguage;
            HeardLanguage = null;
            return heard;
        }

        /// The language of a text from its script alone; null for Latin letters (French, Spanish, English... look the same).
        public static string ScriptLanguage(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var counts = new Dictionary<string, int>();
            foreach (char c in text)
            {
                string code = ScriptOf(c);
                if (code != null) counts[code] = counts.TryGetValue(code, out int n) ? n + 1 : 1;
            }
            if (counts.Count == 0) return null;
            // Kana means Japanese even with Han characters mixed in.
            if (counts.ContainsKey("ja")) return "ja";
            var best = counts.OrderByDescending(kv => kv.Value).First();
            int latin = text.Count(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
            // A few English words inside a Hindi sentence are still Hindi; mostly Latin letters means the Latin language.
            return best.Value * 3 >= latin ? best.Key : null;
        }

        static readonly HashSet<string> NotLatin = new HashSet<string>
        {
            "hi", "bn", "ta", "te", "mr", "gu", "kn", "ml", "pa", "or", "ur", "ne", "ar", "he", "fa", "el", "ru", "uk", "bg", "th", "zh", "ja", "ko",
        };

        /// Written in Latin letters (French, Spanish, Indonesian...) - the voice needs to be told the language, the text can't.
        public static bool IsLatin(string code) => !NotLatin.Contains(code);

        // Names and three-letter codes services answer with ("english", "eng", "hi-IN") -> our two-letter code; null if unknown.
        static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            ["eng"] = "en", ["hin"] = "hi", ["ben"] = "bn", ["tam"] = "ta", ["tel"] = "te", ["mar"] = "mr", ["guj"] = "gu", ["kan"] = "kn",
            ["mal"] = "ml", ["pan"] = "pa", ["ori"] = "or", ["ory"] = "or", ["od"] = "or", ["urd"] = "ur", ["nep"] = "ne", ["spa"] = "es",
            ["fra"] = "fr", ["fre"] = "fr", ["deu"] = "de", ["ger"] = "de", ["ita"] = "it", ["por"] = "pt", ["nld"] = "nl", ["dut"] = "nl",
            ["swe"] = "sv", ["dan"] = "da", ["nor"] = "no", ["nob"] = "no", ["nb"] = "no", ["fin"] = "fi", ["pol"] = "pl", ["ces"] = "cs",
            ["cze"] = "cs", ["ron"] = "ro", ["rum"] = "ro", ["hun"] = "hu", ["ell"] = "el", ["gre"] = "el", ["tur"] = "tr", ["rus"] = "ru",
            ["ukr"] = "uk", ["bul"] = "bg", ["ara"] = "ar", ["heb"] = "he", ["iw"] = "he", ["fas"] = "fa", ["per"] = "fa", ["ind"] = "id",
            ["msa"] = "ms", ["may"] = "ms", ["zlm"] = "ms", ["vie"] = "vi", ["tha"] = "th", ["fil"] = "tl", ["tgl"] = "tl", ["zho"] = "zh",
            ["chi"] = "zh", ["cmn"] = "zh", ["jpn"] = "ja", ["kor"] = "ko", ["swa"] = "sw",
        };

        /// "hi", "hi-IN", "hin", "Hindi" (any case) -> "hi"; null when it isn't a language in the list.
        public static string FromAnyCode(string any)
        {
            if (string.IsNullOrWhiteSpace(any)) return null;
            string s = any.Trim().ToLowerInvariant();
            int dash = s.IndexOfAny(new[] { '-', '_' });
            if (dash > 0) s = s.Substring(0, dash);
            if (Aliases.TryGetValue(s, out string mapped)) return mapped;
            foreach (var l in All)
                if (l.code == s || l.name.ToLowerInvariant() == s) return l.code;
            return null;
        }

        // Null for Latin letters, digits and punctuation.
        static string ScriptOf(char c)
        {
            if (c < 0x0370) return null;
            if (c <= 0x03FF) return "el";
            if (c <= 0x052F) return "ru";                      // Cyrillic (Ukrainian and Bulgarian share it - the menu's choice wins below)
            if (c >= 0x0590 && c <= 0x05FF) return "he";
            if (c >= 0x0600 && c <= 0x06FF) return "ar";       // also Urdu and Persian
            if (c >= 0x0900 && c <= 0x097F) return "hi";       // also Marathi and Nepali
            if (c >= 0x0980 && c <= 0x09FF) return "bn";
            if (c >= 0x0A00 && c <= 0x0A7F) return "pa";
            if (c >= 0x0A80 && c <= 0x0AFF) return "gu";
            if (c >= 0x0B00 && c <= 0x0B7F) return "or";
            if (c >= 0x0B80 && c <= 0x0BFF) return "ta";
            if (c >= 0x0C00 && c <= 0x0C7F) return "te";
            if (c >= 0x0C80 && c <= 0x0CFF) return "kn";
            if (c >= 0x0D00 && c <= 0x0D7F) return "ml";
            if (c >= 0x0E00 && c <= 0x0E7F) return "th";
            if (c >= 0x3040 && c <= 0x30FF) return "ja";
            if (c >= 0xAC00 && c <= 0xD7AF) return "ko";
            if (c >= 0x4E00 && c <= 0x9FFF) return "zh";
            return null;
        }

        /// Scripts shared by several languages: the one the user picked as "this is what I use" settles it
        /// (Devanagari: Hindi/Marathi/Nepali; Arabic script: Arabic/Urdu/Persian; Cyrillic: Russian/Ukrainian/Bulgarian).
        public static string Refine(string detected, string preferred)
        {
            if (string.IsNullOrEmpty(preferred) || preferred == detected) return detected;
            string[][] families =
            {
                new[] { "hi", "mr", "ne" }, new[] { "ar", "ur", "fa" }, new[] { "ru", "uk", "bg" },
            };
            foreach (var family in families)
                if (family.Contains(detected) && family.Contains(preferred)) return preferred;
            return detected;
        }
    }
}
