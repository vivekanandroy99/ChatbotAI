using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChatbotAI
{
    /// Plug and play: the settings made in the Editor's menu (voices, personality edits, camera framings, stage, theme,
    /// languages, Advanced settings...) travel with a build. PortableBuild exports them into the build's
    /// StreamingAssets/default-settings.json; the first time the app starts on a PC they become its starting settings.
    /// Only once per PC - changes made later on the kiosk are kept - and never over a setting the PC already has.
    public static class DefaultSettings
    {
        public const string FileName = "default-settings.json";
        const string ImportedKey = "defaults/imported";

        /// Which saved settings travel. The microphone, speaker and screen stay behind (their devices belong to one PC), and so
        /// does the debug panel (a kiosk should never start with it showing).
        static readonly string[] Carried = { "companion/", "avatar-settings/", "camera/", "stage/", "tune/", "models/", "learning/", "staff/" };
        static readonly string[] Kept = { "companion/microphone", "companion/speaker", "companion/display", "companion/debug" };

        public static bool Travels(string key)
        {
            foreach (string k in Kept) if (key.StartsWith(k, StringComparison.Ordinal)) return false;
            foreach (string c in Carried) if (key.StartsWith(c, StringComparison.Ordinal)) return true;
            return false;
        }

        [Serializable]
        public class Entry
        {
            public string key, type, value;
        }

        [Serializable]
        public class SettingsFile
        {
            public string madeOn;
            public List<Entry> entries = new List<Entry>();
        }

        // Before the scene loads: CompanionUI, AvatarSettingsStore and Tunables read these as they start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ImportOnFirstStart()
        {
            if (Application.isEditor || PlayerPrefs.GetInt(ImportedKey, 0) == 1) return;
            string path = Path.Combine(Application.streamingAssetsPath, FileName);
            int count = 0;
            try
            {
                if (File.Exists(path))
                {
                    var file = JsonUtility.FromJson<SettingsFile>(File.ReadAllText(path));
                    foreach (var e in file?.entries ?? new List<Entry>())
                    {
                        if (string.IsNullOrEmpty(e.key) || PlayerPrefs.HasKey(e.key) || !Travels(e.key)) continue;
                        switch (e.type)
                        {
                            case "int": PlayerPrefs.SetInt(e.key, int.Parse(e.value, System.Globalization.CultureInfo.InvariantCulture)); break;
                            case "float": PlayerPrefs.SetFloat(e.key, float.Parse(e.value, System.Globalization.CultureInfo.InvariantCulture)); break;
                            default: PlayerPrefs.SetString(e.key, e.value ?? ""); break;
                        }
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DefaultSettings: couldn't read {path}: {ex.Message}");
            }
            PlayerPrefs.SetInt(ImportedKey, 1);
            PlayerPrefs.Save();
            if (count > 0) Debug.Log($"DefaultSettings: first start on this PC - {count} settings from the build.");
        }
    }
}
