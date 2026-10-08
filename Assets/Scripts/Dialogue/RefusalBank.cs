using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    public static class RefusalBank
    {
        static readonly Regex Devanagari = new Regex(@"\p{IsDevanagari}");
        static string lastLine;

        public static bool IsHindi(string text) => Devanagari.IsMatch(text);

        /// A random in-character refusal that steers back to the avatar's topics, in
        /// Hindi if the question was asked in Hindi. Never the same line twice in a row.
        public static string Pick(AvatarProfile profile, bool hindi)
        {
            List<string> lines = hindi && profile.hindiRefusalLines.Count > 0 ? profile.hindiRefusalLines : profile.refusalLines;
            string line;
            if (lines == null || lines.Count == 0)
                line = hindi ? "माफ़ कीजिए, इस बारे में मेरे पास जानकारी नहीं है।" : "Sorry, I can't help with that.";
            else
            {
                int i = Random.Range(0, lines.Count);
                if (lines.Count > 1 && lines[i] == lastLine) i = (i + 1 + Random.Range(0, lines.Count - 1)) % lines.Count;
                line = lines[i];
            }
            lastLine = line;
            return line.Replace("{topics}", profile.TopicsPhrase(hindi));
        }
    }
}
