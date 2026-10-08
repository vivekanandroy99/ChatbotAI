using System.IO;
using System.IO.Compression;
using System.Linq;
using ChatbotAI.Dialogue;
using UnityEditor;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// Tools > ChatbotAI > Import Learning...: merges a zip exported on another computer (menu > Learning > Export,
    /// see LearningExport) into this project:
    ///   conversations -> Conversations/<date> (<computer>).jsonl - shown on the Learning page with this computer's,
    ///                    so its unanswered questions can be taught here. Re-importing the same zip replaces, never doubles.
    ///   taught answers -> each bot's StreamingAssets/Knowledge/<id>/ - used here at once and shipped in the next build.
    ///   report.md     -> Conversations/imported/<zip name>.md
    public static class LearningImport
    {
        [MenuItem("Tools/ChatbotAI/Import Learning...")]
        static void Import()
        {
            string zipPath = EditorUtility.OpenFilePanel("Import learning from another computer", "", "zip");
            if (string.IsNullOrEmpty(zipPath)) return;
            EditorUtility.DisplayDialog("Learning imported", ImportFrom(zipPath), "OK");
        }

        /// Merges the zip; returns a summary for the owner.
        public static string ImportFrom(string zipPath)
        {
            int conversations = 0, taught = 0, skipped = 0;
            string computer = "other computer";
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var info = zip.GetEntry("info.txt");
                if (info != null)
                    using (var r = new StreamReader(info.Open()))
                    {
                        string first = r.ReadLine() ?? "";
                        if (first.StartsWith("computer: ")) computer = first.Substring(10).Trim();
                    }
                string safeComputer = new string(computer.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
                var knowledgeRoot = Path.Combine(Application.streamingAssetsPath, "Knowledge");

                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith("/")) continue;
                    if (entry.FullName.StartsWith(LearningExport.ConversationsFolder + "/"))
                    {
                        string name = Path.GetFileName(entry.FullName);
                        Directory.CreateDirectory(ConversationLog.Folder);
                        if (name == "dismissed.txt")
                        {
                            using var r = new StreamReader(entry.Open());
                            foreach (string line in r.ReadToEnd().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
                                ConversationLog.Dismiss(line);
                            continue;
                        }
                        string target = Path.Combine(ConversationLog.Folder, $"{Path.GetFileNameWithoutExtension(name)} ({safeComputer}).jsonl");
                        entry.ExtractToFile(target, overwrite: true);
                        conversations++;
                    }
                    else if (entry.FullName.StartsWith(LearningExport.TaughtFolder + "/"))
                    {
                        var parts = entry.FullName.Split('/');
                        if (parts.Length != 3) continue;
                        string botFolder = Path.Combine(knowledgeRoot, parts[1]);
                        if (!Directory.Exists(botFolder)) { skipped++; continue; }  // a bot this project doesn't have
                        entry.ExtractToFile(Path.Combine(botFolder, parts[2]), overwrite: true);
                        taught++;
                    }
                    else if (entry.FullName == "report.md")
                    {
                        string folder = Path.Combine(ConversationLog.Folder, "imported");
                        Directory.CreateDirectory(folder);
                        entry.ExtractToFile(Path.Combine(folder, Path.GetFileNameWithoutExtension(zipPath) + ".md"), overwrite: true);
                    }
                }
            }
            ConversationLog.Reload();
            AssetDatabase.Refresh();
            return $"From {computer}:\n\n{conversations} day(s) of conversations - shown in the app's menu > Learning.\n" +
                   $"{taught} taught answer(s) added to the bots' knowledge - in the next build too." +
                   (skipped > 0 ? $"\n{skipped} taught answer(s) skipped: their bot isn't in this project." : "");
        }
    }
}
