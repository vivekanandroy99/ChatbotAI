using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// Brings what a bot learnt on another computer (a showcase TV running the exe) back to the development
    /// project, so the next build knows it. On that computer: menu > Learning > Export - one zip with the
    /// conversations, the answers taught there and a readable report, saved to the Desktop or a USB drive. Here:
    /// Unity menu Tools > ChatbotAI > Import Learning... (LearningImport) merges it in: conversations appear on the
    /// Learning page, taught answers go into the bots' knowledge folders (and so into the next build).
    public static class LearningExport
    {
        public const string ConversationsFolder = "conversations";
        public const string TaughtFolder = "taught";

        /// Writes the zip into `destinationFolder`; returns its path.
        public static string Export(string destinationFolder, IEnumerable<AvatarProfile> profiles)
        {
            var list = profiles.ToList();
            Directory.CreateDirectory(destinationFolder);
            string path = Path.Combine(destinationFolder,
                $"Learning - {Sanitize(Environment.MachineName)} - {DateTime.Now:yyyy-MM-dd HH-mm}.zip");
            string report = ConversationLog.WriteReport(TaughtAnswers.All(list));

            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            if (Directory.Exists(ConversationLog.Folder))
                foreach (string file in Directory.GetFiles(ConversationLog.Folder))
                    if (file.EndsWith(".jsonl") || Path.GetFileName(file) == "dismissed.txt")
                        zip.CreateEntryFromFile(file, $"{ConversationsFolder}/{Path.GetFileName(file)}");
            foreach (var p in list)
            {
                string folder = p.KnowledgeFolderPath;
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, TaughtAnswers.FilePrefix + "*.txt"))
                    zip.CreateEntryFromFile(file, $"{TaughtFolder}/{p.avatarId}/{Path.GetFileName(file)}");
            }
            zip.CreateEntryFromFile(report, "report.md");
            var info = zip.CreateEntry("info.txt");
            using (var w = new StreamWriter(info.Open(), new UTF8Encoding(false)))
            {
                w.WriteLine($"computer: {Environment.MachineName}");
                w.WriteLine($"exported: {DateTime.Now:s}");
                w.WriteLine($"app: {Application.productName} {Application.version}");
                w.WriteLine("Import in the Unity project: Tools > ChatbotAI > Import Learning...");
            }
            return path;
        }

        /// Where the export can go: the Desktop, then any USB drive plugged in.
        public static List<(string name, string folder)> Destinations()
        {
            var result = new List<(string, string)> { ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)) };
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                    if (d.DriveType == DriveType.Removable && d.IsReady)
                        result.Add(($"USB drive {d.Name.TrimEnd('\\')} {(string.IsNullOrEmpty(d.VolumeLabel) ? "" : "(" + d.VolumeLabel + ")")}".Trim(), d.RootDirectory.FullName));
            }
            catch (IOException) { }
            return result;
        }

        static string Sanitize(string s) => new string(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
    }
}
