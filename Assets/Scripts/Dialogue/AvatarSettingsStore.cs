using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// Settings changed in the app's menu (voices, speed, mode, persona, personality...), kept per avatar across
    /// sessions (PlayerPrefs). Only the fields changed in the app are stored, so later edits to the Avatar Profile
    /// asset still come through for everything else. The registry runs on copies of the assets, so nothing here
    /// writes to them. Any AvatarProfile field of type string, float, int, bool or enum - and the 3D look (character,
    /// by name) - can be saved by name.
    public static class AvatarSettingsStore
    {
        [Serializable]
        class Saved
        {
            public List<string> changed = new List<string>();
            public List<string> values = new List<string>();  // same order as changed
            // Before 2026-09-30 values were kept in these fields - still read.
            public string displayName, personaPrompt, englishVoice, hindiVoice;
            public AvatarProfile.ConversationMode mode;
            public float speechSpeed, openChatCreativity, relevanceThreshold;
            public int openChatMemory;
        }

        static string Key(AvatarProfile p) => "avatar-settings/" + p.avatarId;

        /// The 3D look (AvatarProfile.character) is saved by the character asset's name; this finds it again (the
        /// registry fills it with every character the bots use).
        public static Func<string, ChatbotAI.Avatar.AvatarCharacter> FindCharacter;

        /// Forgets everything saved for this avatar id (a deleted bot).
        public static void Forget(string avatarId)
        {
            PlayerPrefs.DeleteKey("avatar-settings/" + avatarId);
            PlayerPrefs.Save();
        }

        static Saved Read(AvatarProfile p)
        {
            string json = PlayerPrefs.GetString(Key(p), "");
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<Saved>(json); }
            catch (ArgumentException) { return null; }
        }

        /// Puts the stored changes onto the profile.
        public static void Load(AvatarProfile p)
        {
            var s = Read(p);
            if (s == null) return;
            bool legacy = s.values == null || s.values.Count != s.changed.Count;
            for (int i = 0; i < s.changed.Count; i++)
            {
                var field = Field(s.changed[i]);
                if (field == null) continue;
                if (!legacy)
                {
                    try
                    {
                        object value = Parse(s.values[i], field.FieldType);
                        if (value != null || !field.FieldType.IsSubclassOf(typeof(UnityEngine.Object))) field.SetValue(p, value);
                    }
                    catch (FormatException) { }
                }
                else
                {
                    var old = typeof(Saved).GetField(s.changed[i]);
                    if (old != null) field.SetValue(p, old.GetValue(s));
                }
            }
            UpgradeVoices(p);
        }

        /// A saved/imported voice of a removed engine -> its replacement (VoiceCatalog.Current).
        static void UpgradeVoices(AvatarProfile p)
        {
            p.englishVoice = ChatbotAI.Audio.VoiceCatalog.Current(p.englishVoice);
            p.hindiVoice = ChatbotAI.Audio.VoiceCatalog.Current(p.hindiVoice);
        }

        /// Stores the profile's current value of each named field (AvatarProfile field names).
        public static void Save(AvatarProfile p, params string[] fields)
        {
            var s = Read(p) ?? new Saved();
            var values = new Dictionary<string, string>();
            // Keep what was saved before (converting the old format), then add the new fields.
            bool legacy = s.values == null || s.values.Count != s.changed.Count;
            foreach (string name in s.changed)
            {
                var field = Field(name);
                if (field != null) values[name] = legacy ? Format(field.GetValue(p)) : s.values[s.changed.IndexOf(name)];
            }
            foreach (string name in fields)
            {
                var field = Field(name);
                if (field != null) values[name] = Format(field.GetValue(p));
            }
            var fresh = new Saved();
            foreach (var kv in values)
            {
                fresh.changed.Add(kv.Key);
                fresh.values.Add(kv.Value);
            }
            PlayerPrefs.SetString(Key(p), JsonUtility.ToJson(fresh));
            PlayerPrefs.Save();
        }

        public static bool HasChanges(AvatarProfile p) => PlayerPrefs.HasKey(Key(p));

        /// Every storable setting of a bot as text (for a bot export) - except its id.
        public static (List<string> names, List<string> values) Snapshot(AvatarProfile p)
        {
            var names = new List<string>();
            var values = new List<string>();
            foreach (var f in typeof(AvatarProfile).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.Name == nameof(AvatarProfile.avatarId) || Field(f.Name) == null) continue;
                names.Add(f.Name);
                values.Add(Format(f.GetValue(p)));
            }
            return (names, values);
        }

        /// Puts exported settings onto a bot and keeps them as its own. Unknown or unreadable ones are skipped.
        public static void Apply(AvatarProfile p, List<string> names, List<string> values)
        {
            var applied = new List<string>();
            for (int i = 0; i < names.Count && i < values.Count; i++)
            {
                var field = Field(names[i]);
                if (field == null || names[i] == nameof(AvatarProfile.avatarId)) continue;
                try
                {
                    object value = Parse(values[i], field.FieldType);
                    if (value == null && field.FieldType.IsSubclassOf(typeof(UnityEngine.Object))) continue;
                    field.SetValue(p, value);
                    applied.Add(names[i]);
                }
                catch (FormatException) { }
            }
            UpgradeVoices(p);
            if (applied.Count > 0) Save(p, applied.ToArray());
        }

        /// Forgets the app's changes and copies the asset's values back onto the profile.
        public static void Reset(AvatarProfile p, AvatarProfile original)
        {
            var s = Read(p);
            PlayerPrefs.DeleteKey(Key(p));
            PlayerPrefs.Save();
            if (!original || original == p || s == null) return;
            foreach (string name in s.changed)
            {
                var field = Field(name);
                if (field != null) field.SetValue(p, field.GetValue(original));
            }
        }

        static FieldInfo Field(string name)
        {
            var f = typeof(AvatarProfile).GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (f == null) return null;
            var t = f.FieldType;
            return t == typeof(string) || t == typeof(List<string>) || t == typeof(float) || t == typeof(int) || t == typeof(bool) || t.IsEnum ||
                   t == typeof(ChatbotAI.Avatar.AvatarCharacter) ? f : null;
        }

        static string Format(object value) => value switch
        {
            ChatbotAI.Avatar.AvatarCharacter c => c ? c.name : "",
            List<string> list => string.Join("\n", list),  // one item per line
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            null => "",
            Enum e => Convert.ToInt32(e).ToString(CultureInfo.InvariantCulture),
            bool b => b ? "1" : "0",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };

        static object Parse(string s, Type t)
        {
            if (t == typeof(string)) return s;
            if (t == typeof(List<string>)) return new List<string>(s.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            if (t == typeof(ChatbotAI.Avatar.AvatarCharacter)) return FindCharacter?.Invoke(s);
            if (t == typeof(float)) return float.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(int)) return int.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return s == "1";
            return Enum.ToObject(t, int.Parse(s, CultureInfo.InvariantCulture));
        }
    }
}
