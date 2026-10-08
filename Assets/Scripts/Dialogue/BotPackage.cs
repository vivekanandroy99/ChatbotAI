using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// A bot in one file (".altbot" - a zip) to carry to another kiosk on a USB stick: its name, 3D look, voices,
    /// personality and every setting, its backdrop choice, its documents and taught answers (with the document search's
    /// index, so it answers at once). Importing makes it a new bot there (menu > Bots > Import a bot), no rebuild needed.
    public static class BotPackage
    {
        public const string Extension = ".altbot";
        const string ManifestName = "bot.json";
        const string DocumentsFolder = "documents/";

        [Serializable]
        class Manifest
        {
            public int version = 1;
            public string displayName, basedOn, character, stageChoice, lightingChoice, exportedFrom, exportedAt;
            public AvatarProfile.ConversationMode mode;
            public List<string> settingNames = new List<string>();
            public List<string> settingValues = new List<string>();
        }

        /// Writes the bot's file into `folder`; returns its path.
        public static string Export(AvatarProfile bot, AvatarRegistry registry, string folder)
        {
            Directory.CreateDirectory(folder);
            string safe = new string(bot.displayName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
            string path = Path.Combine(folder, $"{(safe.Length > 0 ? safe : "Bot")} - {DateTime.Now:yyyy-MM-dd HH-mm}{Extension}");

            var (names, values) = AvatarSettingsStore.Snapshot(bot);
            var original = registry.OriginalOf(bot);
            var manifest = new Manifest
            {
                displayName = bot.displayName,
                // The built-in bot it starts from: itself, or the one a made bot was based on.
                basedOn = CustomBots.IsCustom(bot)
                    ? CustomBots.LoadAll().FirstOrDefault(d => d.id == bot.avatarId)?.basedOn ?? original.avatarId
                    : original.avatarId,
                character = bot.character ? bot.character.name : "",
                mode = bot.mode,
                stageChoice = PlayerPrefs.GetString("stage/" + bot.avatarId, ""),
                lightingChoice = PlayerPrefs.GetString("light/" + bot.avatarId, ""),
                exportedFrom = Environment.MachineName,
                exportedAt = DateTime.Now.ToString("s"),
                settingNames = names,
                settingValues = values,
            };

            using (var zip = new ZipArchive(new FileStream(path, FileMode.Create), ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry(ManifestName);
                using (var w = new StreamWriter(entry.Open())) w.Write(JsonUtility.ToJson(manifest, true));
                string docs = bot.KnowledgeFolderPath;
                if (Directory.Exists(docs))
                    foreach (string file in Directory.GetFiles(docs, "*", SearchOption.AllDirectories))
                    {
                        string relative = file.Substring(docs.Length).TrimStart('\\', '/').Replace('\\', '/');
                        if (relative.EndsWith(".meta") || relative == ".synced") continue;
                        zip.CreateEntryFromFile(file, DocumentsFolder + relative, System.IO.Compression.CompressionLevel.Fastest);
                    }
            }
            return path;
        }

        /// Makes a new bot from an exported file; returns it (null + a reason if the file isn't a bot).
        public static AvatarProfile Import(string path, AvatarRegistry registry, out string problem)
        {
            problem = null;
            try
            {
                using (var zip = ZipFile.OpenRead(path))
                {
                    var entry = zip.GetEntry(ManifestName);
                    if (entry == null) { problem = "That file isn't an exported bot."; return null; }
                    Manifest manifest;
                    using (var r = new StreamReader(entry.Open())) manifest = JsonUtility.FromJson<Manifest>(r.ReadToEnd());
                    if (manifest == null || string.IsNullOrWhiteSpace(manifest.displayName)) { problem = "That file isn't an exported bot."; return null; }

                    var bot = registry.Import(manifest.displayName, manifest.basedOn, manifest.character, manifest.mode,
                                              manifest.settingNames, manifest.settingValues);
                    if (!string.IsNullOrEmpty(manifest.stageChoice))
                    {
                        PlayerPrefs.SetString("stage/" + bot.avatarId, manifest.stageChoice);
                        PlayerPrefs.Save();
                    }
                    if (!string.IsNullOrEmpty(manifest.lightingChoice))
                    {
                        PlayerPrefs.SetString("light/" + bot.avatarId, manifest.lightingChoice);
                        PlayerPrefs.Save();
                    }
                    string target = Path.GetFullPath(bot.KnowledgeFolderPath);
                    foreach (var e in zip.Entries)
                    {
                        if (!e.FullName.StartsWith(DocumentsFolder) || e.FullName.EndsWith("/")) continue;
                        string dest = Path.GetFullPath(Path.Combine(target, e.FullName.Substring(DocumentsFolder.Length)));
                        if (!dest.StartsWith(target, StringComparison.OrdinalIgnoreCase)) continue;   // never outside its folder
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        e.ExtractToFile(dest, overwrite: true);
                    }
                    return bot;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                problem = $"Couldn't read that file - {e.Message}";
                return null;
            }
        }
    }
}
