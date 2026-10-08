using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChatbotAI.Dialogue
{
    /// Numbers for the owner (menu > Visitors > Report) from the conversation log (last 30 days): questions per day,
    /// the most-asked questions, what couldn't be answered - and the same as an Excel file.
    public static class ConversationReport
    {
        public class Day
        {
            public DateTime date;
            public int total, answered, partly, unanswered, offTopic, smallTalk, openChat;
        }

        public class Asked
        {
            public string question;
            public int times;
            public DateTime last;
            public List<string> bots = new List<string>();
        }

        // Real questions - small talk ("hi", "thanks") isn't a topic.
        static IEnumerable<ConversationLog.Exchange> Questions =>
            ConversationLog.Recent.Where(e => e.Kind != ConversationLog.Outcome.SmallTalk && !string.IsNullOrWhiteSpace(e.TeachQuestion));

        public static List<Day> PerDay(int days)
        {
            var since = DateTime.Now.Date.AddDays(-(days - 1));
            var byDay = ConversationLog.Recent.Where(e => e.When >= since).GroupBy(e => e.When.Date).ToDictionary(g => g.Key, g => g.ToList());
            var list = new List<Day>();
            for (var d = since; d <= DateTime.Now.Date; d = d.AddDays(1))
            {
                var all = byDay.TryGetValue(d, out var l) ? l : new List<ConversationLog.Exchange>();
                list.Add(new Day
                {
                    date = d, total = all.Count,
                    answered = all.Count(e => e.Kind == ConversationLog.Outcome.Answered),
                    partly = all.Count(e => e.Kind == ConversationLog.Outcome.Partial),
                    unanswered = all.Count(e => e.Kind == ConversationLog.Outcome.NotInDocuments),
                    offTopic = all.Count(e => e.Kind == ConversationLog.Outcome.OffTopic),
                    smallTalk = all.Count(e => e.Kind == ConversationLog.Outcome.SmallTalk),
                    openChat = all.Count(e => e.Kind == ConversationLog.Outcome.OpenChat),
                });
            }
            return list;
        }

        /// The same question asked again (ignoring case and punctuation), most asked first.
        public static List<Asked> MostAsked(int top)
        {
            return Questions.GroupBy(e => ConversationLog.Key(e.TeachQuestion))
                .Where(g => g.Key.Length > 0)
                .Select(g => new Asked
                {
                    question = g.Last().TeachQuestion, times = g.Count(), last = g.Max(e => e.When),
                    bots = g.Select(e => e.avatarName).Distinct().ToList(),
                })
                .OrderByDescending(a => a.times).ThenByDescending(a => a.last).Take(top).ToList();
        }

        /// The whole report as an Excel file in `folder`; returns its path.
        public static string ExportExcel(string folder, Func<ConversationLog.Exchange, bool> taught)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"Altcore report - {Environment.MachineName} - {DateTime.Now:yyyy-MM-dd HH-mm}.xlsx");

            var perDay = new SimpleXlsx.Sheet { name = "Questions per day" };
            perDay.rows.Add(new object[] { "Date", "All", "Answered", "Partly answered", "Couldn't answer", "Off-topic (refused on purpose)", "Small talk", "Open chat" });
            foreach (var d in PerDay(30))
                perDay.rows.Add(new object[] { d.date.ToString("yyyy-MM-dd ddd"), d.total, d.answered, d.partly, d.unanswered, d.offTopic, d.smallTalk, d.openChat });

            var perBot = new SimpleXlsx.Sheet { name = "Per bot" };
            perBot.rows.Add(new object[] { "Bot", "All", "Answered", "Partly answered", "Couldn't answer", "Off-topic", "Small talk", "Open chat", "Asked in Hindi" });
            foreach (var bot in ConversationLog.Recent.GroupBy(e => e.avatarName))
                perBot.rows.Add(new object[]
                {
                    bot.Key, bot.Count(), bot.Count(e => e.Kind == ConversationLog.Outcome.Answered), bot.Count(e => e.Kind == ConversationLog.Outcome.Partial),
                    bot.Count(e => e.Kind == ConversationLog.Outcome.NotInDocuments), bot.Count(e => e.Kind == ConversationLog.Outcome.OffTopic),
                    bot.Count(e => e.Kind == ConversationLog.Outcome.SmallTalk), bot.Count(e => e.Kind == ConversationLog.Outcome.OpenChat), bot.Count(e => e.hindi),
                });

            var most = new SimpleXlsx.Sheet { name = "Most asked" };
            most.rows.Add(new object[] { "Question", "Times asked", "Bots", "Last asked" });
            foreach (var a in MostAsked(100))
                most.rows.Add(new object[] { a.question, a.times, string.Join(", ", a.bots), a.last.ToString("yyyy-MM-dd HH:mm") });

            var gaps = new SimpleXlsx.Sheet { name = "Couldn't answer" };
            gaps.rows.Add(new object[] { "Question", "Times asked", "Bots", "Partly answered", "Asked in Hindi as", "Last asked" });
            foreach (var g in ConversationLog.Unanswered(taught))
                gaps.rows.Add(new object[]
                {
                    g.latest.TeachQuestion, g.times, string.Join(", ", g.avatarNames), g.latest.Kind == ConversationLog.Outcome.Partial ? "yes" : "",
                    g.latest.hindi ? g.latest.question : "", g.latest.When.ToString("yyyy-MM-dd HH:mm"),
                });

            var all = new SimpleXlsx.Sheet { name = "All questions" };
            all.rows.Add(new object[] { "Time", "Bot", "Question", "In English", "Language", "Result", "Reply", "Document match" });
            foreach (var e in ConversationLog.Recent)
                all.rows.Add(new object[]
                {
                    e.When.ToString("yyyy-MM-dd HH:mm"), e.avatarName, e.question, e.hindi ? e.english : "", e.hindi ? "Hindi" : "English",
                    Describe(e.Kind), e.reply, e.score >= 0 ? (object)e.score : "",
                });

            SimpleXlsx.Write(path, new[] { perDay, perBot, most, gaps, all });
            return path;
        }

        public static string Describe(ConversationLog.Outcome o) => o switch
        {
            ConversationLog.Outcome.Answered => "Answered",
            ConversationLog.Outcome.Partial => "Partly answered",
            ConversationLog.Outcome.NotInDocuments => "Couldn't answer",
            ConversationLog.Outcome.OffTopic => "Off-topic (refused on purpose)",
            ConversationLog.Outcome.SmallTalk => "Small talk",
            ConversationLog.Outcome.OpenChat => "Open chat",
            _ => "Not ready yet",
        };
    }
}
