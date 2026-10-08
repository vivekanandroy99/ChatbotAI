using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// Answers the owner taught from the menu (Learning), for questions a bot couldn't answer or got wrong.
    /// Each is a small text file in the bot's knowledge folder (StreamingAssets/Knowledge/<id>/Taught answer -
    /// <question>.txt: the question on the first line, the answer below), so the document search finds it like any
    /// other document - from the next question on, no restart. Knowledge bots with the same documents (Maya and
    /// Ethan) are taught together. Delete a file (or use the menu) to forget an answer.
    public static class TaughtAnswers
    {
        public const string FilePrefix = "Taught answer - ";

        public class Entry
        {
            public string question, answer;
            public List<string> avatarNames = new List<string>();
            public List<string> paths = new List<string>();
        }

        /// Everything taught, one entry per question (with the bots that know it), newest first.
        public static List<Entry> All(IEnumerable<AvatarProfile> profiles)
        {
            var byQuestion = new Dictionary<string, Entry>();
            var order = new List<(string key, DateTime written)>();
            foreach (var p in profiles)
            {
                foreach (string path in Files(p))
                {
                    var (q, a) = Read(path);
                    if (q == null) continue;
                    string key = ConversationLog.Key(q);
                    if (!byQuestion.TryGetValue(key, out var e))
                    {
                        byQuestion[key] = e = new Entry { question = q, answer = a };
                        order.Add((key, File.GetLastWriteTime(path)));
                    }
                    e.avatarNames.Add(p.displayName);
                    e.paths.Add(path);
                }
            }
            return order.OrderByDescending(o => o.written).Select(o => byQuestion[o.key]).ToList();
        }

        public static bool HasAny(AvatarProfile p) => Files(p).Any();

        /// Is there a taught answer for this question (for this bot)?
        public static bool Knows(AvatarProfile p, string question)
        {
            string key = ConversationLog.Key(question);
            return Files(p).Any(f => ConversationLog.Key(Read(f).question) == key);
        }

        /// The bots that learn an answer taught to `p`: knowledge bots sharing p's documents learn together;
        /// an open-chat bot learns alone.
        public static List<AvatarProfile> LearnersWith(AvatarProfile p, IEnumerable<AvatarProfile> all)
        {
            if (p.IsOpenChat) return new List<AvatarProfile> { p };
            string docs = DocumentSet(p);
            return all.Where(o => o == p || (!o.IsOpenChat && DocumentSet(o) == docs && docs.Length > 0)).ToList();
        }

        /// Saves (or replaces) the answer to a question for p and the bots that learn with it.
        public static void Teach(AvatarProfile p, IEnumerable<AvatarProfile> all, string question, string answer)
        {
            question = OneLine(question);
            answer = answer.Trim();
            if (question.Length == 0 || answer.Length == 0) return;
            string key = ConversationLog.Key(question);
            foreach (var learner in LearnersWith(p, all))
            {
                string folder = learner.KnowledgeFolderPath;
                Directory.CreateDirectory(folder);
                string path = Files(learner).FirstOrDefault(f => ConversationLog.Key(Read(f).question) == key) ?? NewPath(folder, question);
                File.WriteAllText(path, question + "\n" + answer + "\n", new UTF8Encoding(false));
            }
            Debug.Log($"Taught answer saved: \"{question}\"");
        }

        /// Edits a taught answer everywhere it's kept (the question on the first line is what counts, not the file name).
        public static void Rewrite(Entry e, string question, string answer)
        {
            question = OneLine(question);
            answer = answer.Trim();
            if (question.Length == 0 || answer.Length == 0) return;
            foreach (string path in e.paths)
                File.WriteAllText(path, question + "\n" + answer + "\n", new UTF8Encoding(false));
        }

        public static void Forget(Entry e)
        {
            foreach (string path in e.paths)
                try { File.Delete(path); }
                catch (IOException ex) { Debug.LogWarning($"TaughtAnswers: couldn't delete {path} - {ex.Message}"); }
        }

        static IEnumerable<string> Files(AvatarProfile p)
        {
            string folder = p.KnowledgeFolderPath;
            return Directory.Exists(folder) ? Directory.GetFiles(folder, FilePrefix + "*.txt") : Array.Empty<string>();
        }

        static (string question, string answer) Read(string path)
        {
            try
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length == 0) return (null, null);
                return (lines[0].Trim(), string.Join("\n", lines.Skip(1)).Trim());
            }
            catch (IOException) { return (null, null); }
        }

        static string NewPath(string folder, string question)
        {
            string slug = new string(question.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();
            if (slug.Length > 50) slug = slug.Substring(0, 50).Trim();
            if (slug.Length == 0) slug = "answer";
            string path = Path.Combine(folder, $"{FilePrefix}{slug}.txt");
            for (int n = 2; File.Exists(path); n++) path = Path.Combine(folder, $"{FilePrefix}{slug} ({n}).txt");
            return path;
        }

        // The documents in the bot's knowledge folder (not the profile's list - its asset references don't exist in a build).
        static string DocumentSet(AvatarProfile p)
        {
            string folder = p.KnowledgeFolderPath;
            if (!Directory.Exists(folder)) return "";
            return string.Join("|", Directory.GetFiles(folder)
                .Select(Path.GetFileName)
                .Where(n => !n.StartsWith(".") && !n.StartsWith(FilePrefix) && !n.EndsWith(".meta"))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        }

        static string OneLine(string s) => string.Join(" ", (s ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
