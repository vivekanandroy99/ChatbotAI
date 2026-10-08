using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// Bots made in the app (menu > Bots > New bot / Duplicate) from the existing 3D characters. Each is a small file
    /// StreamingAssets/Bots/<id>.json: its id, name, 3D look, mode and the built-in bot it started from (its defaults).
    /// Everything changed afterwards in the menu is kept like for any bot (AvatarSettingsStore); its documents live in
    /// StreamingAssets/Knowledge/<id>. Being in StreamingAssets, bots made in the Editor go into the next build, and bots
    /// made on a kiosk stay on that kiosk (a new build replaces that folder).
    public static class CustomBots
    {
        [Serializable]
        public class Definition
        {
            public string id, displayName, basedOn, character;
            public AvatarProfile.ConversationMode mode;
            public string created;
        }

        public static string Folder => Path.Combine(Application.streamingAssetsPath, "Bots");

        static string PathOf(string id) => Path.Combine(Folder, id + ".json");

        public static bool IsCustom(AvatarProfile p) => p && File.Exists(PathOf(p.avatarId));

        public static List<Definition> LoadAll()
        {
            var list = new List<Definition>();
            if (!Directory.Exists(Folder)) return list;
            foreach (string file in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    var d = JsonUtility.FromJson<Definition>(File.ReadAllText(file));
                    if (d != null && !string.IsNullOrEmpty(d.id)) list.Add(d);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"CustomBots: couldn't read {file}: {e.Message}");
                }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.created, b.created));
            return list;
        }

        public static void Save(Definition d)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathOf(d.id), JsonUtility.ToJson(d, true));
        }

        /// A new id from the name: "bot_priya_1006153012" (safe as a folder name for its documents).
        public static string NewId(string name)
        {
            string slug = Regex.Replace((name ?? "").ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
            if (slug.Length == 0) slug = "bot";
            if (slug.Length > 24) slug = slug.Substring(0, 24);
            return $"bot_{slug}_{DateTime.Now:MMddHHmmss}";
        }

        /// Removes a made bot: its file and its documents go to the Recycle Bin, its saved settings are forgotten.
        public static bool Delete(AvatarProfile p)
        {
            if (!IsCustom(p)) return false;
            bool ok = ChatbotAI.UI.WindowsFiles.Recycle(PathOf(p.avatarId));
            if (Directory.Exists(p.KnowledgeFolderPath)) ChatbotAI.UI.WindowsFiles.Recycle(p.KnowledgeFolderPath);
            // In the Editor Unity keeps a .meta file next to each (in a build there are none).
            foreach (string meta in new[] { PathOf(p.avatarId) + ".meta", p.KnowledgeFolderPath + ".meta" })
                if (File.Exists(meta)) ChatbotAI.UI.WindowsFiles.Recycle(meta);
            AvatarSettingsStore.Forget(p.avatarId);
            PlayerPrefs.DeleteKey("stage/" + p.avatarId);
            PlayerPrefs.DeleteKey("light/" + p.avatarId);
            PlayerPrefs.Save();
            return ok;
        }
    }
}
