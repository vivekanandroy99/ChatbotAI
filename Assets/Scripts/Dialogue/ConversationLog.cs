using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// Every question and what the bot did with it, saved on this computer (never sent anywhere) - one JSON line
    /// per exchange in Conversations/<date>.jsonl (the project folder in the Editor, the app's data folder in a
    /// build). The menu's Learning page reads it to show what the bots couldn't answer, so the owner can teach
    /// the right answer (TaughtAnswers) - the bots never learn anything on their own.
    public static class ConversationLog
    {
        public enum Outcome
        {
            Answered,        // knowledge bot answered from its documents (or a taught answer)
            NotInDocuments,  // a customer question the documents didn't answer - worth teaching
            Partial,         // answered, but said "I don't have details on..." for part of it - worth teaching
            OffTopic,        // general-knowledge question, refused on purpose (weather, cricket...)
            SmallTalk,       // hello / thanks / who are you
            OpenChat,        // open-chat bot's free reply
            NotReady,        // the knowledge service wasn't up yet
        }

        [Serializable]
        public class Exchange
        {
            public string time;          // ISO 8601, local time
            public string avatarId;
            public string avatarName;
            public string question;      // as asked (typed or transcribed)
            public string english;       // the standalone English question (Hindi translated, follow-ups resolved)
            public bool hindi;
            public string outcome;       // Outcome name
            public string reply;         // as spoken
            public float score = -1;     // document match, 0-1 (-1 = not searched)

            public Outcome Kind => Enum.TryParse(outcome, out Outcome o) ? o : Outcome.Answered;
            public DateTime When => DateTime.TryParse(time, out var t) ? t : DateTime.MinValue;
            /// The question to teach an answer for: English (documents are searched in English).
            public string TeachQuestion => string.IsNullOrWhiteSpace(english) ? question : english;
        }

        const string EnabledKey = "learning/save-conversations";

        /// Saving conversations can be switched off in the menu (Learning).
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static string Folder => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Conversations"))
            : Path.Combine(Application.persistentDataPath, "Conversations");

        public static event Action<Exchange> OnAdded;

        static List<Exchange> recent;
        static HashSet<string> dismissed;
        const int KeepDays = 30;

        public static void Add(Exchange e)
        {
            e.time = DateTime.Now.ToString("s");
            if (Enabled)
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    File.AppendAllText(Path.Combine(Folder, DateTime.Now.ToString("yyyy-MM-dd") + ".jsonl"),
                                       JsonUtility.ToJson(e) + "\n", new UTF8Encoding(false));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"ConversationLog: couldn't save - {ex.Message}");
                }
            }
            Recent.Add(e);
            OnAdded?.Invoke(e);
        }

        /// The last 30 days, oldest first.
        public static List<Exchange> Recent
        {
            get
            {
                if (recent != null) return recent;
                recent = new List<Exchange>();
                if (!Directory.Exists(Folder)) return recent;
                var since = DateTime.Now.Date.AddDays(-KeepDays);
                foreach (string file in Directory.GetFiles(Folder, "*.jsonl").OrderBy(f => f))
                {
                    // "2026-09-30.jsonl", or "2026-09-30 (TV-PC).jsonl" imported from another computer (LearningImport).
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.Length < 10 || !DateTime.TryParse(name.Substring(0, 10), out var day) || day < since) continue;
                    foreach (string line in File.ReadAllLines(file))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try { recent.Add(JsonUtility.FromJson<Exchange>(line)); }
                        catch (ArgumentException) { }
                    }
                }
                return recent;
            }
        }

        /// Read the files again (after an import).
        public static void Reload()
        {
            recent = null;
            dismissed = null;
        }

        /// Same question, ignoring case, punctuation and spacing.
        public static string Key(string question) =>
            new string((question ?? "").ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray())
                .Replace("  ", " ").Trim();

        public static bool IsDismissed(string question) => Dismissed.Contains(Key(question));

        /// "Not worth teaching" - hidden from the Learning page (kept in the log).
        public static void Dismiss(string question)
        {
            if (!Dismissed.Add(Key(question))) return;
            try
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, "dismissed.txt"), Key(question) + "\n");
            }
            catch (IOException) { }
        }

        static HashSet<string> Dismissed
        {
            get
            {
                if (dismissed != null) return dismissed;
                string path = Path.Combine(Folder, "dismissed.txt");
                dismissed = File.Exists(path) ? new HashSet<string>(File.ReadAllLines(path)) : new HashSet<string>();
                return dismissed;
            }
        }

        public class Gap
        {
            public Exchange latest;
            public int times;
            public List<string> avatarNames = new List<string>();
        }

        /// Customer questions a knowledge bot couldn't answer, one per distinct question, most asked first -
        /// without dismissed ones and ones already taught.
        public static List<Gap> Unanswered(Func<Exchange, bool> taught)
        {
            var gaps = new Dictionary<string, Gap>();
            foreach (var e in Recent)
            {
                if (e.Kind != Outcome.NotInDocuments && e.Kind != Outcome.Partial) continue;
                string key = Key(e.TeachQuestion);
                if (key.Length == 0 || IsDismissed(e.TeachQuestion) || taught(e)) continue;
                if (!gaps.TryGetValue(key, out var gap)) gaps[key] = gap = new Gap();
                gap.latest = e;
                gap.times++;
                if (!gap.avatarNames.Contains(e.avatarName)) gap.avatarNames.Add(e.avatarName);
            }
            return gaps.Values.OrderByDescending(g => g.times).ThenByDescending(g => g.latest.When).ToList();
        }

        /// A readable summary (Markdown) for the owner, or to hand to a developer / AI assistant. Returns its path.
        public static string WriteReport(IEnumerable<TaughtAnswers.Entry> taught)
        {
            var all = Recent;
            var sb = new StringBuilder();
            sb.AppendLine($"# Conversation report - {DateTime.Now:d MMMM yyyy, HH:mm}");
            sb.AppendLine();
            sb.AppendLine($"Last {KeepDays} days: {all.Count} questions.");
            sb.AppendLine();
            sb.AppendLine("| Bot | Questions | Answered | Partly answered | Couldn't answer | Off-topic (refused on purpose) | Small talk | Open chat |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var bot in all.GroupBy(e => e.avatarName))
                sb.AppendLine($"| {bot.Key} | {bot.Count()} | {bot.Count(e => e.Kind == Outcome.Answered)} | {bot.Count(e => e.Kind == Outcome.Partial)} | {bot.Count(e => e.Kind == Outcome.NotInDocuments)} | " +
                              $"{bot.Count(e => e.Kind == Outcome.OffTopic)} | {bot.Count(e => e.Kind == Outcome.SmallTalk)} | {bot.Count(e => e.Kind == Outcome.OpenChat)} |");
            sb.AppendLine();

            sb.AppendLine("## Questions it couldn't answer (most asked first)");
            sb.AppendLine("Teach answers from the app: Menu > Learning. Or add the information to the bot's documents.");
            sb.AppendLine();
            var gaps = Unanswered(_ => false);
            if (gaps.Count == 0) sb.AppendLine("None.");
            foreach (var g in gaps)
                sb.AppendLine($"- {g.latest.TeachQuestion} - asked {g.times}x ({string.Join(", ", g.avatarNames)}), match {g.latest.score:0.00}" +
                              (g.latest.hindi ? $" - asked in Hindi: \"{g.latest.question}\"" : ""));
            sb.AppendLine();

            sb.AppendLine("## Off-topic questions (refused on purpose)");
            foreach (var g in all.Where(e => e.Kind == Outcome.OffTopic).GroupBy(e => Key(e.TeachQuestion)).OrderByDescending(g => g.Count()).Take(30))
                sb.AppendLine($"- {g.Last().TeachQuestion} ({g.Count()}x)");
            sb.AppendLine();

            sb.AppendLine("## Taught answers");
            bool any = false;
            foreach (var t in taught)
            {
                any = true;
                sb.AppendLine($"- **{t.question}** ({string.Join(", ", t.avatarNames)}) - {t.answer}");
            }
            if (!any) sb.AppendLine("None yet.");
            sb.AppendLine();

            sb.AppendLine("## Last 60 exchanges");
            foreach (var e in all.Skip(Math.Max(0, all.Count - 60)))
            {
                sb.AppendLine($"- {e.When:dd MMM HH:mm} - {e.avatarName} - {e.outcome}");
                sb.AppendLine($"  - Q: {e.question}" + (e.hindi && !string.IsNullOrEmpty(e.english) ? $" (English: {e.english})" : ""));
                sb.AppendLine($"  - A: {e.reply}");
            }

            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, $"Report {DateTime.Now:yyyy-MM-dd HH-mm}.md");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }
    }
}
