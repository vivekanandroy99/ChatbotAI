using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// A word the voice says wrong, and how to spell it so it comes out right.
    [System.Serializable]
    public class Pronunciation
    {
        [Tooltip("The word or phrase as it appears in replies, e.g. Koregaon. Not case-sensitive.")]
        public string word;
        [Tooltip("How the English voice should say it, spelled the way it sounds, e.g. Koray-gown.")]
        public string sayAs;
        [Tooltip("Optional: how the Hindi voice should say it - usually just the word in Devanagari, e.g. कोरेगांव. " +
                 "Left empty, the Hindi voice uses Say As.")]
        public string sayAsInHindi;
    }

    /// A name and the other ways visitors say it (AvatarProfile.heardAs).
    [System.Serializable]
    public class NameVariants
    {
        [Tooltip("The name as the documents write it, e.g. P&I.")]
        public string name;
        [Tooltip("Other ways it is said or heard, e.g. PNI, P and I, पीएनआई. Not case-sensitive.")]
        public List<string> variants = new List<string>();

        const string Edge = @"[\p{L}\p{M}\p{N}]";   // Devanagari vowel signs are marks, so \w/\b don't do here

        /// A variant as a regex: whole words only; an all-capitals variant of 2-5 letters (PNI) also matches spaced or
        /// dotted letters (P N I, P.N.I.); spaces match any run of spaces.
        public static string Pattern(string variant)
        {
            if (string.IsNullOrWhiteSpace(variant)) return null;
            string v = variant.Trim();
            string body;
            if (Regex.IsMatch(v, "^[A-Z]{2,5}$"))
                body = string.Join(@"[\s.\-]*", v.ToCharArray().Select(c => Regex.Escape(c.ToString()))) + @"\.?";
            else
                body = string.Join(@"\s+", v.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape));
            return $@"(?<!{Edge}){body}(?!{Edge})";
        }
    }

    /// One customizable character: persona, knowledge documents, refusal
    /// lines, voice and 3D character (AvatarCharacter). Create new ones via
    /// Assets > Create > ChatbotAI > Avatar Profile, drop them in
    /// Resources/Avatars, and AvatarRegistry picks them up automatically.
    [CreateAssetMenu(fileName = "NewAvatarProfile", menuName = "ChatbotAI/Avatar Profile")]
    public class AvatarProfile : ScriptableObject
    {
        public static readonly string[] SupportedDocumentTypes = { ".pdf", ".docx", ".pptx", ".txt", ".md" };

        [Header("Identity")]
        public string avatarId = "default";
        public string displayName = "Nova";

        public enum ConversationMode
        {
            [Tooltip("Answers only from its knowledge documents; anything else gets a refusal line (the customer-service bot).")]
            KnowledgeOnly,
            [Tooltip("Talks about anything, in its persona, from what the language model knows - no documents, no topic gate.")]
            OpenChat,
        }

        [Header("Conversation")]
        [Tooltip("Knowledge Only: the customer-service bot - answers only from the documents below, refuses the rest. " +
                 "Open Chat: a general companion - talks about anything in its persona, remembers the last few exchanges; " +
                 "documents, thresholds and refusal lines are not used.")]
        public ConversationMode mode = ConversationMode.KnowledgeOnly;

        [Tooltip("Open Chat: how many previous exchanges it remembers (more = better memory, slower replies).")]
        [Range(0, 12)] public int openChatMemory = 6;

        [Tooltip("Open Chat: 0 = plain and predictable, 1 = more varied and playful.")]
        [Range(0f, 1.2f)] public float openChatCreativity = 0.7f;

        public bool IsOpenChat => mode == ConversationMode.OpenChat;

        [Header("Persona (system prompt)")]
        [Tooltip("Personality and tone only. What the avatar knows comes from its knowledge documents.")]
        [TextArea(5, 12)]
        public string personaPrompt =
            "You are Nova, a friendly front-desk assistant. You are upbeat, a little cheeky, " +
            "and keep answers short and conversational, as if spoken out loud.";

        public enum Tone { Warm, Professional, Playful, Calm, Enthusiastic }
        public enum Formality { Casual, Balanced, Formal }
        public enum ReplyLength { VeryShort, Short, Medium, Detailed }

        [Header("Personality details (added to the persona)")]
        public Tone tone = Tone.Warm;
        public Formality formality = Formality.Balanced;
        [Tooltip("0 = no jokes, 1 = plenty of light humour.")]
        [Range(0f, 1f)] public float humour = 0.3f;
        [Tooltip("How long its replies are, in both modes.")]
        public ReplyLength replyLength = ReplyLength.Short;
        [Tooltip("How it talks, in your own words - e.g. \"simple Indian English, says 'ji' to be polite\".")]
        [TextArea(2, 4)] public string speakingStyle = "";
        [Tooltip("Things it should always do - e.g. \"end by offering more help\".")]
        [TextArea(2, 4)] public string alwaysDo = "";
        [Tooltip("Things it must never do or say - e.g. \"never talk about competitors\".")]
        [TextArea(2, 4)] public string neverDo = "";

        [Header("Open chat details")]
        [Tooltip("Open Chat: now and then asks the visitor something back.")]
        public bool asksQuestionsBack = true;
        [Tooltip("Open Chat: subjects it politely steers away from - e.g. \"politics, religion\".")]
        public string avoidTopics = "";

        /// "One or two short sentences" - the reply length, for the rules.
        public string ReplyLengthPhrase(bool capitalised)
        {
            string s = replyLength switch
            {
                ReplyLength.VeryShort => "one short, complete sentence",
                ReplyLength.Medium => "two or three short, complete sentences",
                ReplyLength.Detailed => "three to five complete sentences",
                _ => "one or two short, complete sentences",
            };
            return capitalised ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s;
        }

        /// The personality details as instructions for the language model (empty if nothing to add).
        public string StyleRules()
        {
            var lines = new List<string>();
            lines.Add(tone switch
            {
                Tone.Professional => "Your tone is professional, clear and courteous.",
                Tone.Playful => "Your tone is playful and lively.",
                Tone.Calm => "Your tone is calm, gentle and reassuring.",
                Tone.Enthusiastic => "Your tone is upbeat and enthusiastic.",
                _ => "Your tone is warm and friendly.",
            });
            if (formality == Formality.Casual) lines.Add("Talk casually, like a friendly person, not an official.");
            if (formality == Formality.Formal) lines.Add("Speak formally and politely.");
            if (humour < 0.15f) lines.Add("Don't make jokes.");
            else if (humour > 0.65f) lines.Add("Add light, friendly humour where it fits.");
            if (!string.IsNullOrWhiteSpace(speakingStyle)) lines.Add("How you talk: " + speakingStyle.Trim());
            if (!string.IsNullOrWhiteSpace(alwaysDo)) lines.Add("Always: " + alwaysDo.Trim());
            if (!string.IsNullOrWhiteSpace(neverDo)) lines.Add("Never: " + neverDo.Trim());
            if (IsOpenChat && !string.IsNullOrWhiteSpace(avoidTopics))
                lines.Add($"Politely steer the conversation away from: {avoidTopics.Trim()}.");
            return string.Join(" ", lines);
        }

        [Header("Knowledge base")]
        [Tooltip("The documents this avatar knows (.pdf, .docx, .pptx, .txt, .md) - it only answers from these. " +
                 "Drag files here from the Project window, or drop them from Windows Explorer onto the box below.")]
        public List<Object> knowledgeDocuments = new List<Object>();

        [Tooltip("How closely a question must match the documents before the LLM is even asked (0-1). " +
                 "Watch the Console's 'Knowledge gate' lines to tune: raise it if off-topic questions get through, lower it if real questions get refused.")]
        [Range(0f, 1f)] public float relevanceThreshold = 0.45f;

        [Tooltip("How closely the drafted reply must match the documents before it's spoken (0-1).")]
        [Range(0f, 1f)] public float replyGroundingThreshold = 0.45f;

        [Tooltip("How many document passages the LLM is shown per question.")]
        [Range(1, 10)] public int passagesPerQuestion = 6;

        [Tooltip("Search the documents with this bot's topic names replaced by \"the company\" (the relevance gate still " +
                 "scores the question as asked). For a name the search models can't read: with P&I the right passage " +
                 "was among the 6 in 25 of 26 questions, against 17. Maya's Altscape/Altcore searched worse without them.")]
        public bool searchWithoutTopicNames;

        [Tooltip("Reorder the closest passages with the reranker (when Menu > AI models > answer checker is on). Helped " +
                 "Maya (right passage first 16 -> 21 of 35); not the P&I bots (23 vs 25 of 26 without it).")]
        public bool rerankPassages = true;

        /// The question as it should be searched for ranking passages (searchWithoutTopicNames), or null.
        public string SearchText(string question)
        {
            if (!searchWithoutTopicNames || string.IsNullOrEmpty(question)) return null;
            string text = question;
            foreach (string name in EffectiveTopicNames)
                text = Regex.Replace(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(name)}(?<own>['’]s)?(?![\p{{L}}\p{{N}}])",
                                     m => m.Groups["own"].Success ? "the company's" : "the company", RegexOptions.IgnoreCase);
            return text == question ? null : text;
        }

        [Tooltip("Said if a question comes in before the knowledge service has finished starting up.")]
        public string notReadyLine = "Give me a moment - I'm still loading my knowledge.";

        [Tooltip("The brands/products this avatar is about (e.g. Altscape, Altcore). Off-topic questions are steered " +
                 "back to them ('...but ask me anything about Altscape and Altcore'), and they're always kept in English letters. " +
                 "Leave empty to detect them from the documents.")]
        public List<string> topicNames = new List<string>();

        /// Filled at runtime from the documents when topicNames is empty.
        [System.NonSerialized] public List<string> detectedTopicNames;

        public List<string> EffectiveTopicNames
        {
            get
            {
                var set = topicNames?.FindAll(n => !string.IsNullOrWhiteSpace(n)).ConvertAll(n => n.Trim());
                if (set != null && set.Count > 0) return set;
                return detectedTopicNames ?? new List<string>();
            }
        }

        [Header("Refusal bank")]
        [Tooltip("Randomly rotated in-character lines for off-topic English questions, spoken as written. " +
                 "{topics} becomes the Topic Names above.")]
        public List<string> refusalLines = new List<string>
        {
            "That one's outside my lane, I'm afraid - but ask me anything about {topics}!",
            "I can't help with that one, but I'd be happy to tell you all about {topics}.",
            "Hmm, that's not something I know about. I'm your go-to for {topics}, though!",
            "Good question, but it's beyond what I cover. Want to hear about {topics} instead?",
            "I'll have to pass on that one. Ask me anything about {topics} and I'm all yours.",
            "That's outside my expertise - I'm here for {topics}. What would you like to know?",
            "Ha, you've found the edge of my map! I'm much better on {topics}.",
            "I don't have an answer for that one, but I know {topics} inside out.",
            "Not my department, I'm afraid! I'm the one to ask about {topics}, though.",
            "That's out of syllabus for me. Try me on {topics} instead!",
        };

        [Tooltip("The same for questions asked in Hindi - everyday spoken Hindi. {topics} works here too.")]
        public List<string> hindiRefusalLines = new List<string>
        {
            "इस बारे में तो मेरे पास जानकारी नहीं है, लेकिन {topics} के बारे में जो चाहें पूछिए!",
            "अरे, ये तो मेरे बस की बात नहीं है! हाँ, {topics} के बारे में कुछ भी पूछिए।",
            "इसका जवाब तो मेरे पास नहीं है। {topics} के बारे में कुछ जानना हो तो बताइए!",
            "हम्म, ये मेरे topic से बाहर है। चलिए, {topics} की बात करते हैं?",
            "सवाल बढ़िया है, पर इसका जवाब मेरे पास नहीं है। {topics} के बारे में पूछिए ना!",
            "ये मेरी expertise नहीं है, लेकिन {topics} के बारे में जो भी जानना हो, पूछिए।",
            "ओह, ये तो syllabus से बाहर है! {topics} के बारे में कुछ पूछिए।",
        };

        [Header("Voice")]
        [Tooltip("Voice for English replies - one of the voices that come with the speech model. Pick from the list; Preview plays it (Play mode).")]
        public string englishVoice = "af_heart";

        [Tooltip("Voice for Hindi replies - one of the voices that come with the speech model. Keep it the same gender as the English voice.")]
        public string hindiVoice = "hf_alpha";

        [Range(0.5f, 2f)] public float speechSpeed = 1f;

        [Tooltip("Words the voice mispronounces (place names, brand names...) and how to spell them so they sound right. " +
                 "Only changes what's spoken - the reply text on screen stays as it is.")]
        public List<Pronunciation> pronunciations = new List<Pronunciation>();

        [Tooltip("Names visitors say - or speech recognition writes - in other ways (P&I heard as \"PNI\", \"P and I\"). The " +
                 "question is changed to the name as the documents write it before the bot reads it, so search and " +
                 "answers find it. Letter-by-letter variants (PNI) also match \"P N I\", \"P.N.I.\".")]
        public List<NameVariants> heardAs = new List<NameVariants>();

        /// The visitor's text with every heardAs variant replaced by its name (whole words, any case).
        public string Understood(string text)
        {
            if (heardAs == null || string.IsNullOrEmpty(text)) return text;
            foreach (var n in heardAs)
            {
                if (n == null || string.IsNullOrWhiteSpace(n.name) || n.variants == null) continue;
                foreach (string v in n.variants)
                {
                    string pattern = NameVariants.Pattern(v);
                    if (pattern != null) text = Regex.Replace(text, pattern, n.name.Trim(), RegexOptions.IgnoreCase);
                }
            }
            return text;
        }

        [Header("Appearance")]
        [Tooltip("The 3D character (model + animations + lip-sync tuning) this avatar appears as. Empty = the one set on " +
                 "the Avatar Stage in the scene.")]
        public ChatbotAI.Avatar.AvatarCharacter character;
        [Tooltip("The stage behind this avatar (studio, gradient, picture or flat colour). Empty = the Stage Backdrop's " +
                 "default in the scene. The app's menu (Stage) can change the style, picture and brightness too.")]
        public ChatbotAI.Avatar.StageLook stage;

        /// The runtime copy of knowledgeDocuments lives in StreamingAssets/Knowledge/<this>/
        /// (kept in sync by the editor - see KnowledgeSync). Restricted to characters the
        /// knowledge service accepts.
        public string KnowledgeFolderName
        {
            get
            {
                string name = Regex.Replace(avatarId ?? "", @"[^A-Za-z0-9_\- ]", "_").Trim();
                return string.IsNullOrEmpty(name) ? "default" : name;
            }
        }

        public string KnowledgeFolderPath => Path.Combine(Application.streamingAssetsPath, "Knowledge", KnowledgeFolderName);

        /// Hindi first-person verbs have to agree with the voice (सकती हूँ / सकता हूँ).
        public bool HasFemaleVoice => !ChatbotAI.Audio.VoiceCatalog.TryGet(hindiVoice, out var voice) || voice.female;

        /// "Altscape and Altcore" / "Altscape और Altcore"; a generic phrase if no names are set.
        public string TopicsPhrase(bool hindi)
        {
            var names = EffectiveTopicNames;
            if (names.Count == 0) return hindi ? "हमारे काम" : "what we do here";
            if (names.Count == 1) return names[0];
            string and = hindi ? " और " : " and ";
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + and + names[names.Count - 1];
        }

        /// The text as it should be spoken: pronunciation fixes applied (whole words, any case).
        public string ForSpeech(string text, bool hindiVoice)
        {
            if (pronunciations == null || string.IsNullOrEmpty(text)) return text;
            foreach (var p in pronunciations)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.word)) continue;
                string say = hindiVoice && !string.IsNullOrWhiteSpace(p.sayAsInHindi) ? p.sayAsInHindi : p.sayAs;
                if (string.IsNullOrWhiteSpace(say)) continue;
                text = Regex.Replace(text, $@"(?<![\w@./]){Regex.Escape(p.word.Trim())}(?![\w])", say.Trim(), RegexOptions.IgnoreCase);
            }
            return text;
        }

        public static bool IsSupportedDocument(string path) =>
            System.Array.IndexOf(SupportedDocumentTypes, Path.GetExtension(path).ToLowerInvariant()) >= 0;
    }
}
