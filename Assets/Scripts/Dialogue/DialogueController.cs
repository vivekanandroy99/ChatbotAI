using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// The dialogue brain. The avatar only talks about what's in its knowledge
    /// documents, enforced in three steps:
    ///   1. Before the LLM: the question is matched against the documents. No
    ///      close match -> refusal line, the LLM is never asked.
    ///   2. The LLM only sees the matching passages and is told to answer from
    ///      them alone, or say NO_ANSWER -> refusal line.
    ///   3. After the LLM: the draft reply is matched against the documents
    ///      again. If it drifted away from them -> refusal line.
    ///
    /// Before that, a router agent spots small talk (greetings, thanks, "who are
    /// you" - answered with written lines, see SmallTalk) and rewrites everything
    /// else as a standalone English question, resolving follow-ups against the
    /// previous one.
    ///
    /// The brain always answers in English; for a Hindi question a separate
    /// translator agent (same model, own instructions) turns the checked
    /// English answer into everyday spoken Hindi, keeping brand/team names in
    /// English. Asked to do both at once, the model kept answering Hindi
    /// questions in English.
    ///
    /// Avatars in Open Chat mode (AvatarProfile.mode) skip all of the above except
    /// the translation: the persona talks about anything, remembering the last few
    /// exchanges (AskOpen).
    public class DialogueController : MonoBehaviour
    {
        const string NoAnswerMarker = "NO_ANSWER";

        // Replies may only contain English letters, Devanagari and ordinary punctuation
        // (plus ₹ and ™). Qwen models occasionally slide into Chinese mid-reply; with
        // this grammar the sampler can't produce those characters at all.
        const string ReplyGrammar =
            @"root ::= [\t\n\r\x20-\x7E -ÿऀ-ॿ​-‍‐-‧₹™]*";

        // The router picks a kind of small talk (the reply itself is written - see SmallTalk)
        // or writes the question in English letters (accented ones too: café).
        static readonly string RouterGrammar =
            "root ::= (\"ASK: \" | \"GENERAL: \") [\\x20-\\x7E\\u00C0-\\u00FF]+ | \"SMALLTALK: \" (" +
            string.Join(" | ", SmallTalk.Kinds.Select(k => $"\"{k}\"")) + ")";

        enum RouteKind { SmallTalk, CustomerQuestion, GeneralQuestion }

        // The "not covered / not included" clause matters: without it the small
        // model treats a negative answer in the documents as no answer at all.
        const string GroundingRules =
            "Answer using ONLY the information in the CONTEXT given with each question. Never use outside knowledge, even if you know the answer. " +
            "If the CONTEXT answers the question - including when it says something is not covered, not included or not available - give that answer. " +
            "If it only answers part of the question, or something closely related, say in a few words what you don't have details on, " +
            "then share what the CONTEXT does say that helps - never guess or infer beyond what it actually says. If the CONTEXT " +
            "answers the question fully, just answer - don't say you lack details. " +
            // Tested 2026-09-30: "Does it work on mobile phones?" got a general description of the product, and "How
            // much does it cost?" an answer about what home buyers pay - answers to other questions.
            "Answer the exact question asked, never a different one. If it asks whether something works, exists or is " +
            "included (does it, can I, is there), or asks for a price, cost or number, and the CONTEXT doesn't say, begin " +
            "with 'I don't have details on' followed by that exact thing; then, if the CONTEXT gives a way to get in touch " +
            "(a website, email or phone), you may point to it. " +
            "Keep any conditions or exceptions the CONTEXT attaches to the answer (like 'waived if', 'only for', 'above Rs X'). " +
            "Only if nothing in the CONTEXT is relevant to the question, reply with exactly: " + NoAnswerMarker + ". " +
            "Answer like a knowledgeable, warm company representative: the direct answer first; add a supporting detail from the " +
            "CONTEXT only if it helps with what was asked - never unrelated extras, slogans or marketing flourishes; state facts plainly. " +
            "{length} in plain, simple words, natural to say out loud - " +
            "no lists, no markdown, no marketing jargon. Say 'I don't have details on...', never 'the information does not say'. " +
            "Speak in your own voice as the company's representative - never 'it is described as', 'according to', documents, " +
            "files, file names or the word CONTEXT.";

        // Open Chat avatars (AvatarProfile.mode): no documents, no gate - the persona talks about anything.
        const string OpenChatRules =
            "You're talking out loud with someone, face to face, as the character described above. Talk about anything they " +
            "bring up - everyday life, science, films, food, travel, sport, tech, ideas - with real opinions and personality{askBack}. " +
            "Keep each reply to {length} " +
            "that sound natural spoken aloud - no lists, no markdown, no emojis, no stage directions. Always reply in English, " +
            "even when they write in Hindi (your reply is translated for them). You know a lot - share well-known facts, " +
            "ideas and recommendations freely and confidently (asked for a fun fact about India, give one). You can't look " +
            "anything up and don't know today's news, weather, scores or prices - say so honestly instead of guessing. " +
            "Never invent specific numbers, dates or quotes you aren't sure of.";

        // Open Chat: a Hindi message is put into English before the persona sees it. Shown the Hindi itself, a persona
        // written as a Brit answered in character: "मुझे डर है मैं Hindi नहीं बोलता" (I'm afraid I don't speak Hindi).
        const string ToEnglishPrompt =
            "You translate what someone said out loud - in Hindi, Hinglish or English, transcribed from speech - into one " +
            "natural, everyday English message with the same meaning, names and tone. Never answer it or add anything. " +
            "Output only the English.";

        const string EnglishGrammar = @"root ::= [\x20-\x7E]+";

        static readonly List<ChatMessage> ToEnglishHistory = new (string hindi, string english)[]
        {
            ("आप कैसे हो?", "How are you?"),
            ("तुम्हें कौन सा खाना सबसे ज़्यादा पसंद है?", "Which food do you like the most?"),
            ("मोदी जी के बारे में आप क्या सोचते हो", "What do you think about Modi ji?"),
            ("weekend पे आपका क्या plan है", "What are your plans for the weekend?"),
            ("मुझे एक joke सुनाओ", "Tell me a joke."),
            ("मेरा नाम राहुल है, मैं Pune से हूँ", "My name is Rahul, I'm from Pune."),
        }.SelectMany(e => new[] { new ChatMessage("user", "HINDI: " + e.hindi), new ChatMessage("assistant", e.english) }).ToList();

        const string TranslatorPrompt =
            "You turn short spoken customer-service replies from English into everyday spoken Hindi - the simple, casual Hindi " +
            "people in India actually talk in (bol-chaal ki Hindi), never formal, bookish or shuddh Hindi. Everyday words become simple " +
            "Hindi: शुरू हुई (not स्थापित or established), बात करें (not संपर्क करें or contact), मदद (not सहायता), मिलता है (not उपलब्ध है), " +
            "देते हैं (not प्रदान करते हैं), आसान (not सरल), के ज़रिए (not के माध्यम से), इस्तेमाल (not उपयोग), ज़्यादा (not अधिक), सिर्फ़ (not केवल), कीमत (not लागत), होता है (not आयोजित किया जाता है). " +
            "Technical and business terms that Indians say in English anyway (solutions, services, features, clients, team, project, " +
            "platform, app, website, online, demo, real-time, interactive, plan, booking) stay in English letters, as do brand and " +
            "product names, team names, numbers, phone numbers, email addresses and websites. Names of plans, categories, products " +
            "and features that start with a capital letter (like Premium Plan, Home Loans, Interior Design) are copied exactly. " +
            "Never write an English word in Devanagari. If you are not sure of the everyday Hindi word for something, keep the English word - that is how people really talk; " +
            "never reach for rare, formal or Sanskrit-heavy words. Keep sentences short and easy to say. " +
            "Do not add, remove or change any facts. Each message is 'ENGLISH: <text>' - translate the text, never answer it, " +
            "even when it is a question. Output only the Hindi reply, nothing else.";

        // Short questions back to the listener, which the translator answered ("What about you?" ->
        // "मैं करता हूँ reading") or flipped ("और मैं?") - Open Chat replies end with them all the time.
        static readonly Dictionary<string, string> FixedPhrases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["What about you?"] = "और आप?", ["How about you?"] = "और आप?", ["And you?"] = "और आप?",
            ["You?"] = "और आप?", ["What do you think?"] = "आपको क्या लगता है?", ["You know?"] = "पता है?",
            ["Right?"] = "है ना?", ["Isn't it?"] = "है ना?", ["Pretty wild, isn't it?"] = "कमाल है ना?",
            ["How about yourself?"] = "और आप?", ["What about yourself?"] = "और आप?",
        };

        // {topics}, {first} and {last} (first/last topic name) are filled in per avatar.
        const string RouterPrompt =
            "You sort the messages a voice assistant for {topics} receives. The MESSAGE is English or Hindi, transcribed from speech. " +
            "Output exactly one line.\n" +
            "SMALLTALK: <kind> - only when the whole message is small talk with no request for information. Kinds: GREETING (hello), " +
            "HOW_ARE_YOU, THANKS, GOODBYE, WHO_ARE_YOU (about the assistant itself: who are you, what can you do), COMPLIMENT.\n" +
            "GENERAL: <question> - a general-knowledge question that isn't about any business or its customers: news, weather, " +
            "sports results, politics, famous people, jokes, maths, trivia.\n" +
            "ASK: <question> - everything else: anything a customer might ask {topics} or a business like it (what it is and does, " +
            "products, prices, policies, visiting, contact...), even if you don't know the answer.\n" +
            "For GENERAL and ASK, rewrite the message as one standalone English question that keeps its meaning and wording as close as possible: translate Hindi to " +
            "English, spell brand names as {topics}, and if a PREVIOUS QUESTION is given, replace words like it, they, this or that using it. " +
            "'You guys', 'your company' and 'your product' mean {topics}. Never answer the question - only rewrite it.";

        // Words that refer back to the previous question ("does it...", "क्या ये...", "... भी").
        static readonly System.Text.RegularExpressions.Regex FollowUpWords = new System.Text.RegularExpressions.Regex(
            @"\b(it|its|it's|they|them|their|this|that|these|those|there|also|too|same|more|what about|how about)\b|" +
            @"(^|\s)(ये|यह|इस|इसका|इसकी|इसके|इसे|इसमें|इससे|इन|इनका|इनकी|इनके|इन्हें|वो|वह|उस|उसका|उसकी|उसके|उसे|उनका|उनकी|उनके|उन्हें|भी|और)(?=\s|$|[?।,!.])",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        const string SearchHelperPrompt =
            "Given a customer's question about a company's products or services, write one or two short sentences, in English, " +
            "that a brochure answering it might contain. Use the plain, general words a company would write (for example " +
            "accessories for a phone case, payment options for UPI, branches for a city). Output only the sentences.";

        [SerializeField] LLMAgent agent;
        [Tooltip("Second agent on the same LLM, used only to turn English answers into everyday Hindi for Hindi questions.")]
        [SerializeField] LLMAgent translator;
        [Tooltip("Third agent on the same LLM: writes a guessed answer to search with when the first search found nothing usable.")]
        [SerializeField] LLMAgent searchHelper;
        [Tooltip("Fourth agent on the same LLM: tells small talk apart from questions, and rewrites questions as standalone English.")]
        [SerializeField] LLMAgent router;
        [SerializeField] AvatarRegistry avatarRegistry;
        [SerializeField] string sidecarUrl = "http://127.0.0.1:8765";

        [Tooltip("Customer questions scoring this close below the avatar's relevance threshold are still shown to the " +
                 "model with the best passages, which answers or refuses - catches documents that use other words.")]
        [Tunable("Brain", "Second chance for near-miss questions", 0f, 0.3f, 0.01f,
                 note = "Knowledge bots: a customer question scoring this close below Topic strictness still gets checked against the documents.")]
        [SerializeField, Range(0f, 0.3f)] float borderlineMargin = 0.12f;

        [Tooltip("A question within this many seconds of the last answer can refer back to it ('does it work on mobile?'). " +
                 "Longer gaps are treated as a new visitor.")]
        [Tunable("Brain", "New visitor after", 20f, 600f, 10f, unit = "s",
                 note = "After this long without a question, follow-ups (\"does it...\") and open-chat memory start fresh.")]
        [SerializeField] float followUpWindowSeconds = 120f;

        [Tooltip("Open-chat bots: how closely a question must match an answer the owner taught (menu > Learning) for " +
                 "the bot to be given it. Knowledge bots find taught answers like any other document.")]
        [Tunable("Brain", "Taught answers: how close a question must be", 0.3f, 0.95f, 0.01f,
                 note = "Open-chat bots: how closely a question must match a taught answer to use it.")]
        [SerializeField, Range(0.3f, 0.95f)] float taughtMatchScore = 0.6f;

        string appliedPersonaId;
        List<ChatMessage> routerHistory, translatorHistory;
        string lastQuestion;
        float lastAnswerTime = float.NegativeInfinity;

        public AvatarProfile ActiveProfile => avatarRegistry.Active;

        public enum ReplyLanguage { SameAsQuestion, English, Hindi }
        const string ReplyLanguageKey = "companion/reply-language";

        /// The language replies are spoken in (menu > Language & listening): the one asked in, or always one.
        public ReplyLanguage RepliesIn
        {
            get => (ReplyLanguage)PlayerPrefs.GetInt(ReplyLanguageKey, 0);
            set
            {
                PlayerPrefs.SetInt(ReplyLanguageKey, (int)value);
                PlayerPrefs.Save();
            }
        }
        public string SidecarUrl => sidecarUrl;

        public event Action<string> OnReply;

        /// A question arrived (the text as asked) - for the debug overlay's timings.
        public event Action<string> OnQuestion;

        // Open Chat: the conversation so far (English), newest last.
        readonly List<ChatMessage> openHistory = new List<ChatMessage>();

        // Hindi replies, one sentence at a time as each is translated (isLast on the
        // final one); OnReply still follows with the whole reply.
        public event Action<string, bool> OnReplySentence;

        // Fires once the LLM (CUDA) has finished loading and warming up.
        // Whisper's GPU (Vulkan) init is deliberately held off until this
        // fires - initializing two different GPU backends at the same
        // instant crashed the whole PC (a driver-level fault), not just
        // Unity. See StaggeredGpuBootstrap.
        public event Action OnWarmupComplete;

        /// Working on an answer: from a question arriving until its reply (or first sentence) is ready.
        /// For the avatar's "thinking" animation.
        public bool IsThinking { get; private set; }

        void Awake()
        {
            OnReply += _ => IsThinking = false;
            OnReplySentence += (_, _) => IsThinking = false;
        }

        void Reset()
        {
            agent = GetComponent<LLMAgent>();
        }

        async void Start()
        {
            // The first Chat() call after a model loads has to process the
            // system prompt into the model's cache - that's a several-second
            // one-time cost. Pay it now, in the background, instead of
            // making the user's first real question eat it live.
            searchHelper.systemPrompt = SearchHelperPrompt;
            searchHelper.grammar = ReplyGrammar;
            // Sorting and translating want the single most likely output, not variety.
            router.temperature = 0f;
            translator.temperature = 0f;
            await SyncPersona();
            await agent.Warmup();
            await router.Warmup();
            await translator.Warmup();
            await searchHelper.Warmup();
            IsWarmedUp = true;
            OnWarmupComplete?.Invoke();
            avatarRegistry.OnActiveChanged += _ => WarmNewPersona();
        }

        /// The language model is loaded and warmed up.
        public bool IsWarmedUp { get; private set; }

        /// The active avatar's persona, mode or voice gender was edited (in-app settings): rebuild its prompts.
        public void PersonaChanged()
        {
            appliedPersonaId = null;
            if (IsWarmedUp) WarmNewPersona();
        }

        // Another avatar picked: its persona is processed now, not on its first question.
        async void WarmNewPersona()
        {
            await SyncPersona();
            await agent.Warmup();
            router.chat = routerHistory;
            await router.Warmup();
        }

        /// Avatars that don't list their brands get them from their documents. The
        /// knowledge service starts after the LLM, so this happens on the first question.
        async Task DetectTopicNames(AvatarProfile profile)
        {
            if (profile.topicNames.Any(n => !string.IsNullOrWhiteSpace(n)) || profile.detectedTopicNames != null) return;
            var names = await KnowledgeClient.GetTopics(sidecarUrl, profile.KnowledgeFolderName);
            if (names == null) return;  // not up yet - try again next question
            profile.detectedTopicNames = names.ToList();
            appliedPersonaId = null;    // rebuild the prompts that name them
            Debug.Log($"Topic names detected from the documents: {string.Join(", ", names)}");
        }

        async Task SyncPersona()
        {
            var profile = avatarRegistry.Active;
            if (profile == null) return;
            if (appliedPersonaId == profile.avatarId) return;

            // The persona, its personality settings (tone, humour, always/never...) and the mode's rules, with the reply
            // length and (open chat) asking back filled in from the profile.
            string rules = (profile.IsOpenChat ? OpenChatRules : GroundingRules)
                .Replace("{length}", profile.ReplyLengthPhrase(capitalised: !profile.IsOpenChat))
                .Replace("{askBack}", profile.asksQuestionsBack ? ", and now and then ask something back to keep the conversation going" : "");
            string style = profile.StyleRules();
            agent.systemPrompt = profile.personaPrompt + (style.Length > 0 ? "\n\n" + style : "") + "\n\n" + rules;
            openHistory.Clear();
            agent.grammar = ReplyGrammar;
            await agent.ClearHistory();

            // Open Chat has no routing: the router agent puts Hindi messages into English instead.
            router.systemPrompt = profile.IsOpenChat ? ToEnglishPrompt : FillAvatar(RouterPrompt, profile);
            router.grammar = profile.IsOpenChat ? EnglishGrammar : RouterGrammar;
            routerHistory = profile.IsOpenChat ? ToEnglishHistory : BuildRouterHistory(profile);

            // Names that must survive translation, and first-person verbs matching the voice.
            string names = string.Join(", ", profile.EffectiveTopicNames.Append(profile.displayName));
            translator.systemPrompt = TranslatorPrompt +
                $" These names always stay exactly as written, in English letters: {names}." +
                (profile.HasFemaleVoice
                    ? " The speaker is a woman: use feminine first-person verbs (मैं बता सकती हूँ, मैं करती हूँ)."
                    : " The speaker is a man: use masculine first-person verbs (मैं बता सकता हूँ, मैं करता हूँ).");
            translator.grammar = ReplyGrammar;
            translatorHistory = BuildTranslatorHistory(profile.HasFemaleVoice);

            lastQuestion = null;
            appliedPersonaId = profile.avatarId;
        }

        public async void Ask(string userText)
        {
            var profile = avatarRegistry.Active;
            if (profile == null)
            {
                Debug.LogError("DialogueController: no active AvatarProfile in the registry.");
                return;
            }
            // Names said or heard another way ("PNI" for P&I) -> as the documents write them (AvatarProfile.heardAs).
            string understood = profile.Understood(userText);
            if (understood != userText)
            {
                Debug.Log($"Heard \"{userText}\" - understood as \"{understood}\"");
                userText = understood;
            }

            IsThinking = true;
            OnQuestion?.Invoke(userText);
            bool hindi = RefusalBank.IsHindi(userText);
            // Replies in the language asked, unless the menu fixes it (Language & listening > Replies in).
            bool hindiReply = RepliesIn == ReplyLanguage.SameAsQuestion ? hindi : RepliesIn == ReplyLanguage.Hindi;
            // What happened to this question, for the Learning page (ConversationLog).
            var record = new ConversationLog.Exchange
            {
                avatarId = profile.avatarId, avatarName = profile.displayName, question = userText, hindi = hindi,
            };
            void Finish(string text, ConversationLog.Outcome outcome)
            {
                record.reply = text;
                record.outcome = outcome.ToString();
                ConversationLog.Add(record);
                OnReply?.Invoke(text);
            }

            if (profile.IsOpenChat)
            {
                await SyncPersona();
                await AskOpen(userText, profile, hindi, hindiReply, record, Finish);
                return;
            }
            await DetectTopicNames(profile);
            await SyncPersona();
            string folder = profile.KnowledgeFolderName;

            // Small talk gets a friendly reply straight away; everything else becomes
            // a standalone English question (translated, follow-ups resolved), which
            // both the document search and the answering agent handle far better
            // than raw transcribed Hindi.
            var (kind, routed) = await Route(userText);
            // Naming a topic means it's a question ("hi, what's Altcore?"), whatever the
            // router thought - except for thanks and goodbyes.
            if (kind == RouteKind.SmallTalk && routed != "THANKS" && routed != "GOODBYE" && MentionsTopic(profile, userText))
            {
                Debug.Log($"Router: said {routed}, but the message names a topic - treating it as a question.");
                (kind, routed) = (RouteKind.CustomerQuestion, userText);
            }
            if (kind == RouteKind.SmallTalk)
            {
                Debug.Log($"Router: small talk ({routed})");
                Finish(SmallTalk.Pick(routed, profile, hindiReply), ConversationLog.Outcome.SmallTalk);
                return;
            }
            string question = routed;
            Debug.Log($"Router: {(kind == RouteKind.GeneralQuestion ? "general" : "customer")} question -> \"{question}\"");
            int followUpAt = question.IndexOf(" (Follow-up to: ", StringComparison.Ordinal);
            record.english = followUpAt > 0 ? question.Substring(0, followUpAt) : question;
            // Refused on purpose (general knowledge) or a gap in the documents worth teaching.
            var refused = kind == RouteKind.GeneralQuestion ? ConversationLog.Outcome.OffTopic : ConversationLog.Outcome.NotInDocuments;

            var match = await KnowledgeClient.Retrieve(sidecarUrl, folder, question, profile.passagesPerQuestion,
                                                      profile.rerankPassages, profile.SearchText(question));
            if (match == null)
            {
                Finish(profile.notReadyLine, ConversationLog.Outcome.NotReady);
                return;
            }
            record.score = match.best_score;

            Debug.Log($"Knowledge gate (question): score {match.best_score:F2} / needs {profile.relevanceThreshold:F2} (best sentence {match.best_sentence_score:F2})");
            // A customer question that only just misses may still be answered in other
            // words ("pets are welcome" for "can I bring my dog?") - the model reads the
            // best passages and decides. General-knowledge questions get no such benefit
            // of the doubt: that's how a weather question would end up answered from a
            // passage about sunlight.
            bool borderline = kind == RouteKind.CustomerQuestion && match.best_score < profile.relevanceThreshold &&
                              match.best_score >= profile.relevanceThreshold - borderlineMargin;
            if (borderline) Debug.Log("Knowledge gate (question): borderline customer question - the model checks the passages.");
            if (match.chunks == null || match.chunks.Length == 0 || (match.best_score < profile.relevanceThreshold && !borderline))
            {
                Finish(RefusalBank.Pick(profile, hindiReply), refused);
                return;
            }

            var passages = match.chunks;
            string reply = await AnswerFrom(passages, question, hindiReply);
            if (reply == null) return;

            if (reply.Contains(NoAnswerMarker) && !borderline)
            {
                // Second chance: the documents may answer it in other words (a
                // "mobile" question answered by a "web app usable from anywhere").
                // Only on-topic questions that came back empty pay for this.
                var retry = await SearchWithGuess(profile, folder, question);
                if (retry?.chunks != null && retry.chunks.Length > 0)
                {
                    passages = retry.chunks;
                    reply = await AnswerFrom(passages, question, hindiReply) ?? NoAnswerMarker;
                }
            }

            if (reply.Contains(NoAnswerMarker))
            {
                Debug.Log("Knowledge gate (reply): LLM found no answer in the documents.");
                Finish(RefusalBank.Pick(profile, hindiReply), refused);
                return;
            }

            // Question and reply together: a short correct answer ("2024") matches
            // nothing on its own, but the pair still reveals a drifted reply.
            var grounding = await KnowledgeClient.Retrieve(sidecarUrl, folder, question + "\n" + reply, 1, rerank: false);
            float groundingScore = grounding?.best_score ?? 0f;
            Debug.Log($"Knowledge gate (reply): score {groundingScore:F2} / needs {profile.replyGroundingThreshold:F2}");
            if (groundingScore < profile.replyGroundingThreshold)
            {
                Debug.Log($"Knowledge gate (reply): rejected draft - \"{reply}\"");
                Finish(RefusalBank.Pick(profile, hindiReply), refused);
                return;
            }

            lastQuestion = record.english;
            lastAnswerTime = Time.realtimeSinceStartup;
            // "I don't have details on ..." (GroundingRules' wording for what the documents don't say): a gap to teach.
            bool partial = PartialAnswer.IsMatch(reply);
            if (hindiReply) Debug.Log($"Answer before translation: \"{reply}\"");
            reply = hindiReply ? await ToHindi(reply, profile) : CanonicalNames(profile, reply);
            Finish(reply, partial ? ConversationLog.Outcome.Partial : ConversationLog.Outcome.Answered);
        }

        /// Open Chat: the persona answers from the model's own knowledge, with the conversation so far
        /// (reset after a pause as long as a follow-up window - a new visitor). The model reads Hindi
        /// fine but writes it badly, so it always answers in English and Hindi questions get the
        /// same translation as in Knowledge Only mode.
        async Task AskOpen(string userText, AvatarProfile profile, bool hindi, bool hindiReply, ConversationLog.Exchange record,
                           Action<string, ConversationLog.Outcome> finish)
        {
            if (Time.realtimeSinceStartup - lastAnswerTime > followUpWindowSeconds) openHistory.Clear();
            // Hindi is put into English first (see ToEnglishPrompt); the persona and the memory only ever see English.
            string message = userText;
            if (hindi)
            {
                router.chat = routerHistory;
                string english = (await router.Chat("HINDI: " + userText, null, null, addToHistory: false))?.Trim();
                Debug.Log($"Open chat: heard in Hindi -> \"{english}\"");
                // Given raw Hindi, the model started its reply with "(Translating: Do you like cooking?)".
                message = string.IsNullOrWhiteSpace(english)
                    ? userText + "\n\n(Reply in English, in character - don't translate or repeat their message.)"
                    : english;
                if (!string.IsNullOrWhiteSpace(english)) record.english = english;
            }
            // An answer the owner taught (menu > Learning) for this question is given to the persona to use.
            string prompt = message;
            if (TaughtAnswers.HasAny(profile))
            {
                var taught = await KnowledgeClient.Retrieve(sidecarUrl, profile.KnowledgeFolderName, message, 1, rerank: false);
                if (taught?.chunks != null && taught.chunks.Length > 0 && taught.best_score >= taughtMatchScore)
                {
                    Debug.Log($"Open chat: using a taught answer (match {taught.best_score:F2})");
                    // Only the answer (the file's first line is the question), framed as the bot's own - given the
                    // whole Q&A, Iris answered as if the visitor had said it ("Teal! That's brilliant...").
                    string taughtText = taught.chunks[0].text.Trim();
                    int firstBreak = taughtText.IndexOf('\n');
                    string taughtAnswer = firstBreak > 0 ? taughtText.Substring(firstBreak + 1).Trim() : taughtText;
                    prompt = message + "\n\n[Private note for you, not from them: your answer to this is \"" + taughtAnswer +
                             "\" Give exactly that answer, plainly and first, in character - don't hedge, change or contradict it.]";
                }
            }
            agent.chat = new List<ChatMessage>(openHistory);
            float temperature = agent.temperature;
            agent.temperature = profile.openChatCreativity;
            string reply;
            try
            {
                reply = SpokenOpenReply(await agent.Chat(prompt, null, null, addToHistory: false));
            }
            finally
            {
                agent.temperature = temperature;
            }
            if (string.IsNullOrEmpty(reply))
            {
                IsThinking = false;
                return;
            }
            Debug.Log($"Open chat ({openHistory.Count / 2} earlier exchanges remembered): \"{reply}\"");
            openHistory.Add(new ChatMessage("user", message));
            openHistory.Add(new ChatMessage("assistant", reply));
            int keep = Mathf.Max(0, profile.openChatMemory) * 2;
            if (openHistory.Count > keep) openHistory.RemoveRange(0, openHistory.Count - keep);
            lastAnswerTime = Time.realtimeSinceStartup;
            finish(hindiReply ? await ToHindi(reply, profile) : reply, ConversationLog.Outcome.OpenChat);
        }

        static readonly System.Text.RegularExpressions.Regex PartialAnswer = new System.Text.RegularExpressions.Regex(
            @"\bI (?:do not|don['’]t) have (?:any )?(?:details|information|specifics)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        static readonly System.Text.RegularExpressions.Regex TranslationNote =new System.Text.RegularExpressions.Regex(
            @"^\s*\((?:translat|in english|in hindi)[^)]*\)\s*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// A free-form reply as it should be said: no "(Translating: ...)" preface, no markdown
        /// emphasis or headings (the voice would read the symbols), one paragraph.
        // Stage directions at the start or end of a sentence - "(Smiling warmly)", "*laughs*" - were read out loud.
        static readonly System.Text.RegularExpressions.Regex StageDirection = new System.Text.RegularExpressions.Regex(
            @"(?:^|(?<=[.!?…]\s*))\s*(?:\([^()\d]{1,40}\)|\*[^*\d]{1,40}\*)|\s*(?:\([^()\d]{1,40}\)|\*[^*\d]{1,40}\*)\s*$");

        static string SpokenOpenReply(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return null;
            reply = TranslationNote.Replace(reply, "");
            reply = StageDirection.Replace(reply, "");
            // The whole reply in quotation marks ("“Right then! ...”") - the voice doesn't need them.
            reply = reply.Replace("“", "").Replace("”", "").Replace("\"", "");
            reply = System.Text.RegularExpressions.Regex.Replace(reply, @"[*_#`]+", "");
            reply = System.Text.RegularExpressions.Regex.Replace(reply, @"\s+", " ").Trim();
            return reply.Length == 0 ? null : reply;
        }

        /// Topic names always spelled the one way (documents write "altcore" and "Altcore"),
        /// so they read - and are pronounced - consistently.
        static string CanonicalNames(AvatarProfile profile, string text)
        {
            foreach (var name in profile.EffectiveTopicNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                // Not inside email addresses or websites (contact@altcore.co stays as it is).
                text = System.Text.RegularExpressions.Regex.Replace(text,
                    $@"(?<![\w@./]){System.Text.RegularExpressions.Regex.Escape(name.Trim())}(?!\w|\.[A-Za-z])",
                    name.Trim(), System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            return text;
        }

        /// Searches with the question plus a guessed answer, which reads like the passage
        /// being looked for and so finds it even when the documents use other words.
        async Task<KnowledgeClient.Result> SearchWithGuess(AvatarProfile profile, string folder, string question)
        {
            searchHelper.chat = SearchHelperHistory;
            string guess = (await searchHelper.Chat(question, null, null, addToHistory: false))?.Trim();
            string text = question + "\n" + guess;
            var result = await KnowledgeClient.Retrieve(sidecarUrl, folder, text, profile.passagesPerQuestion,
                                                        profile.rerankPassages, profile.SearchText(text));
            Debug.Log($"Knowledge gate: second search with \"{guess}\" - score {result?.best_score ?? 0f:F2}, best sentence {result?.best_sentence_score ?? 0f:F2}");
            return result;
        }

        static bool MentionsTopic(AvatarProfile profile, string text) =>
            profile.EffectiveTopicNames.Any(n =>
                text.IndexOf(n.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

        /// Smaller models sometimes leave a follow-up unresolved ("When was it started?"). If the
        /// rewrite lost the brand the previous question was about, the previous question rides along.
        string WithFollowUpContext(string question, bool followUp)
        {
            if (!followUp) return question;
            var names = avatarRegistry.Active.EffectiveTopicNames;
            bool previousNamed = names.Any(n => lastQuestion.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
            bool rewriteNamed = names.Any(n => question.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!previousNamed || rewriteNamed) return question;
            // "Does it work on mobile phones?" after "What is Altscape?" -> "Does Altscape work on mobile phones?".
            // With the previous question only appended, the answer agent answered THAT one again (tested 2026-09-30).
            string topic = names.Where(n => lastQuestion.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                                .OrderByDescending(n => n.Length).First().Trim();
            var pronoun = System.Text.RegularExpressions.Regex.Match(question, @"\b(it|its|they|them|their)\b",
                                                                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (pronoun.Success)
            {
                string word = pronoun.Value.ToLowerInvariant();
                string name = word == "its" || word == "their" ? topic + "'s" : topic;
                return question.Substring(0, pronoun.Index) + name + question.Substring(pronoun.Index + pronoun.Length);
            }
            return $"{question} (Follow-up to: {lastQuestion})";
        }

        /// The kind of message, and the small-talk kind or the standalone English question.
        async Task<(RouteKind kind, string text)> Route(string userText)
        {
            // The previous question is only shown for messages that point back at it:
            // shown every time, the model kept rewriting new questions into the old one.
            string message = "MESSAGE: " + userText;
            bool followUp = lastQuestion != null && Time.realtimeSinceStartup - lastAnswerTime < followUpWindowSeconds && FollowUpWords.IsMatch(userText);
            if (followUp) message = "PREVIOUS QUESTION: " + lastQuestion + "\n" + message;

            router.chat = routerHistory;
            string routed = (await router.Chat(message, null, null, addToHistory: false))?.Trim() ?? "";
            if (routed.StartsWith("SMALLTALK:")) return (RouteKind.SmallTalk, routed.Substring(10).Trim());
            if (routed.StartsWith("ASK:") && routed.Length > 5) return (RouteKind.CustomerQuestion, WithFollowUpContext(routed.Substring(4).Trim(), followUp));
            if (routed.StartsWith("GENERAL:") && routed.Length > 9) return (RouteKind.GeneralQuestion, WithFollowUpContext(routed.Substring(8).Trim(), followUp));
            Debug.LogWarning($"Router gave an unexpected answer, using the question as heard: \"{routed}\"");
            return (RouteKind.CustomerQuestion, userText);
        }

        /// Answer from the given passages; null if the LLM returned nothing. English -
        /// except that a Hindi question answered from Hindi documents may come back in the
        /// documents' own Hindi (natural wording; ToHindi then leaves it as it is).
        async Task<string> AnswerFrom(KnowledgeClient.Passage[] passages, string question, bool hindiQuestion)
        {
            // Worked examples go in front of every question (the model follows
            // them far more reliably than instructions). Set each time because
            // LLMUnity clears history when it (re)connects to the model; the
            // prefix never changes, so it stays cached. Each question is
            // independent (addToHistory: false), so latency doesn't grow.
            agent.chat = ExampleHistory;
            string prompt = BuildPrompt(passages, question, hindiQuestion);
            string reply = await agent.Chat(prompt, null, null, addToHistory: false);
            return string.IsNullOrWhiteSpace(reply) ? null : WithoutDocumentNotes(reply.Trim(), passages);
        }

        // Documents written for a chatbot carry notes to it - record codes (SV01, CO02, P037), "Sources: ...",
        // "Retrieve ...", "Do not describe ..." - and quoted model answers. The answer agent copied them ("Add relevant
        // scope from SV01 if requested.", 2026-10-07 P&I document): such sentences are dropped, and the quote marks.
        // So are placeholders ("[insert website address here]" - the translator then filled in riverside.example) and
        // the worked examples' made-up library ("the Riverside Library" turned up in a museum list).
        static readonly System.Text.RegularExpressions.Regex RecordCode =
            new System.Text.RegularExpressions.Regex(@"\b(?:CO|SV|CAP|LD|OF|CL|PJ)\d{2}\b|\b[SQ]\d{2}\b|\b[PC]\d{3}\b|\[[A-Za-z][^\]]*\]| \| ");   // [insert ...] (not [1]); " | " = a raw table row
        static readonly System.Text.RegularExpressions.Regex NoteSentence = new System.Text.RegularExpressions.Regex(
            @"^(?:Sources?:|Status:|Use (?:the )?[A-Z]{2,4}\d* to\b|(?:Do not|Don't)\s|(?:Retrieve|Provide|Treat|Attribute|Select|Search|Route|Preserve)\s)");

        /// The worked examples' made-up library, in text whose source doesn't mention it.
        static bool LeakedExample(string text, string source) =>
            text.IndexOf("riverside", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (source ?? "").IndexOf("riverside", StringComparison.OrdinalIgnoreCase) < 0;

        static string WithoutDocumentNotes(string reply, KnowledgeClient.Passage[] passages)
        {
            if (reply == NoAnswerMarker) return reply;
            string context = string.Join("\n", passages.Select(p => p.text));
            string unquoted = reply.Replace("“", "").Replace("”", "").Trim();
            var kept = SentenceEnd.Split(unquoted)
                .Where(s => !RecordCode.IsMatch(s) && !NoteSentence.IsMatch(s.Trim()) && !LeakedExample(s, context));
            string result = string.Join(" ", kept).Trim();
            if (result.Length == 0) return unquoted;
            if (result != unquoted) Debug.Log($"Dropped a note from the documents: \"{unquoted}\" -> \"{result}\"");
            return result;
        }

        // Sentence ends: ". " / "! " / "? " followed by a capital or digit (not "e.g. the").
        static readonly System.Text.RegularExpressions.Regex SentenceEnd =
            new System.Text.RegularExpressions.Regex(@"(?<=[.!?])\s+(?=[A-Z0-9""'])");

        /// Translated one sentence at a time: the model's Hindi falls apart on long,
        /// multi-sentence input, and the shared prefix stays cached between sentences.
        /// Each sentence goes out through OnReplySentence as soon as it's ready, so
        /// the voice starts on the first one.
        async Task<string> ToHindi(string english, AvatarProfile profile)
        {
            var sentences = SentenceEnd.Split(english.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var parts = new List<string>();
            for (int i = 0; i < sentences.Count; i++)
            {
                // Already Hindi (answered from Hindi documents, in their own words): keep it.
                if (MostlyHindi(sentences[i]))
                {
                    string kept = CanonicalNames(profile, sentences[i].Trim());
                    parts.Add(kept);
                    OnReplySentence?.Invoke(kept, i == sentences.Count - 1);
                    continue;
                }
                string hindi = await TranslateSentence(sentences[i].Trim());
                // The model garbles English words when it writes them in Devanagari
                // (commercial -> कॉर्पोरेट); anything of the English answer it wrote
                // that way goes back to English letters, so it's also pronounced right.
                hindi = CanonicalNames(profile, (await KnowledgeClient.RestoreEnglish(sidecarUrl, english, hindi, namesOnly: profile.IsOpenChat)).Trim());
                // The translator sometimes runs the two scripts together ("ये एकsurprisingly") - the voice slurred them.
                hindi = ScriptBoundary.Replace(hindi, " ");
                parts.Add(hindi);
                OnReplySentence?.Invoke(hindi, i == sentences.Count - 1);
            }
            return string.Join(" ", parts);
        }

        static readonly System.Text.RegularExpressions.Regex ScriptBoundary = new System.Text.RegularExpressions.Regex(
            @"(?<=[ऀ-ॣ०-ॿ])(?=[A-Za-z])|(?<=[A-Za-z])(?=[ऀ-ॣ०-ॿ])");

        async Task<string> TranslateSentence(string english)
        {
            if (FixedPhrases.TryGetValue(english.Trim().Replace('’', '\''), out string fixedHindi)) return fixedHindi;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                translator.chat = translatorHistory;
                // Labelled, so a question reads as text to translate, not one to answer.
                string hindi = await translator.Chat("ENGLISH: " + english, null, null, addToHistory: false);
                if (!string.IsNullOrWhiteSpace(hindi) && RefusalBank.IsHindi(hindi) && !LeakedExample(hindi, english)) return hindi.Trim();
                Debug.Log($"Translator returned no Devanagari (attempt {attempt + 1}): \"{hindi}\"");
            }
            return english;  // an English sentence in a Hindi reply is still understood
        }

        // Examples are about a made-up library so there are no facts to borrow.
        static readonly (string question, string answer)[] Examples =
        {
            ("CONTEXT:\n[1] The Riverside Library opened in 2019 in the heart of Pune. It is open from 9 AM to 6 PM on weekdays.\n" +
             "[2] Members can borrow up to five books at a time.\n\nQUESTION: When did the library open?",
             "The Riverside Library opened in 2019, right in the heart of Pune. It's open from 9 AM to 6 PM on weekdays if you'd like to drop by."),
            // A "no" the CONTEXT implies is a full answer - not a "don't have details".
            ("CONTEXT:\n[1] Home delivery of books is available only in Pune and Nashik.\n\nQUESTION: Do you deliver books in Delhi?",
             "No, home delivery is only available in Pune and Nashik for now."),
            ("CONTEXT:\n[1] Members can reserve study rooms and renew books online, from any device, at any time.\n" +
             "[2] The Riverside Library is open from 9 AM to 6 PM on weekdays.\n\nQUESTION: Is there a Riverside Library app for my phone?",
             "I don't have details on a dedicated phone app, but members can reserve study rooms and renew books online from any device, at any time."),
            ("CONTEXT:\n[1] The Riverside Library is open from 9 AM to 6 PM on weekdays.\n\nQUESTION: Does the library have a cafeteria?",
             NoAnswerMarker),
            // Documents written for a chatbot hold notes and answer templates for it (2026-10-07, P&I): the notes are
            // never repeated ("Do not describe the founder as..." was read out), and a template's fallback ("no office
            // in that city") is used only when the list really has no match (Kolkata was listed, the fallback was said).
            ("CONTEXT:\n[1] LB01 — Founder | PRIMARY | Source S02\nThe library was founded by the late Mrs. A. Rao in 1985. " +
             "Say \"since 1985\". Do not describe the founder as a current trustee.\n\nQUESTION: Who started the library?",
             "The library was founded by the late Mrs. A. Rao, back in 1985."),
            ("CONTEXT:\n[1] Q05 — \"Is there a branch in my city?\" Give the matching row of the branch list. If there is no matching " +
             "branch: \"I don't have a verified branch listing for that city.\" Sources: BR01.\n[2] Branch | Address\n" +
             "Lakeshore | 4 Mill Road, Lakeshore\nHillford | 12 College Road, Hillford\n\nQUESTION: Do you have a branch in Hillford?",
             "Yes, there's a branch at 12 College Road, Hillford."),
            // ...and when the list has no match, the fallback - never another city's address (a Pune question got the
            // Noida head office as "our Pune office" with only the example above).
            ("CONTEXT:\n[1] Q05 — \"Is there a branch in my city?\" Give the matching row of the branch list. If there is no matching " +
             "branch: \"I don't have a verified branch listing for that city.\" Sources: BR01.\n[2] Branch | Address\n" +
             "Lakeshore | 4 Mill Road, Lakeshore\nHillford | 12 College Road, Hillford\n\nQUESTION: Is there a branch in Greenfield?\n(The CONTEXT doesn't mention Greenfield.)",
             "I don't have a verified branch listing for Greenfield. The branches I know of are in Lakeshore and Hillford."),
        };

        static readonly List<ChatMessage> ExampleHistory = Examples
            .SelectMany(e => new[] { new ChatMessage("user", e.question), new ChatMessage("assistant", e.answer) })
            .ToList();

        static readonly List<ChatMessage> SearchHelperHistory = new List<ChatMessage>
        {
            new ChatMessage("user", "Can I book a study room from home?"),
            new ChatMessage("assistant", "Members can reserve study rooms online from any device, anytime, without visiting the library."),
        };

        static string FillAvatar(string template, AvatarProfile profile)
        {
            var names = profile.EffectiveTopicNames;
            return template
                .Replace("{name}", string.IsNullOrWhiteSpace(profile.displayName) ? "the assistant" : profile.displayName)
                .Replace("{topics}", profile.TopicsPhrase(false))
                .Replace("{first}", names.Count > 0 ? names[0].Trim() : "the company")
                .Replace("{last}", names.Count > 1 ? names[names.Count - 1].Trim() : "the team")
                .Replace("{persona}", profile.personaPrompt);
        }

        // Uses the avatar's real topic names so brand questions are clearly questions;
        // shows translation, close-to-the-words rewrites and follow-up resolution.
        static List<ChatMessage> BuildRouterHistory(AvatarProfile profile)
        {
            var examples = new (string message, string routed)[]
            {
                ("MESSAGE: hello", "SMALLTALK: GREETING"),
                ("MESSAGE: नमस्ते, कैसे हो आप?", "SMALLTALK: HOW_ARE_YOU"),
                ("MESSAGE: who are you and what can you do", "SMALLTALK: WHO_ARE_YOU"),
                ("MESSAGE: आप कौन हो? अपने बारे में कुछ बताओ", "SMALLTALK: WHO_ARE_YOU"),
                ("MESSAGE: बहुत बढ़िया, धन्यवाद", "SMALLTALK: THANKS"),
                ("MESSAGE: आज मौसम कैसा है?", "GENERAL: What is the weather like today?"),
                ("MESSAGE: what is {first}", "ASK: What is {first}?"),
                ("MESSAGE: {last} क्या है? कुछ बताइए", "ASK: What is {last}? Can you tell me about it?"),
                ("MESSAGE: hi there, what does {last} do", "ASK: What does {last} do?"),
                ("MESSAGE: {first} किन sectors के लिए बना है?", "ASK: Which sectors is {first} built for?"),
                ("MESSAGE: भारत के प्रधानमंत्री कौन हैं?", "GENERAL: Who is the Prime Minister of India?"),
                ("MESSAGE: can you tell me a joke", "GENERAL: Can you tell me a joke?"),
                ("MESSAGE: क्या आपके यहाँ parking है?", "ASK: Is there parking available?"),
                ("PREVIOUS QUESTION: What does {first} do?\nMESSAGE: क्या ये मोबाइल पे भी चलता है?", "ASK: Does {first} work on mobile phones?"),
                ("PREVIOUS QUESTION: What is {last}?\nMESSAGE: और ये कब शुरू हुआ था?", "ASK: When did {last} start?"),
                // A new question stands on its own, whatever came before.
                ("PREVIOUS QUESTION: Does {first} work on mobile phones?\nMESSAGE: और {last} के बारे में बताइए", "ASK: Can you tell me about {last}?"),
            };
            return examples
                .SelectMany(e => new[] { new ChatMessage("user", FillAvatar(e.message, profile)), new ChatMessage("assistant", FillAvatar(e.routed, profile)) })
                .ToList();
        }

        // Hindi first-person verbs are gendered, so the self-introduction example
        // matches the avatar's voice.
        static List<ChatMessage> BuildTranslatorHistory(bool female)
        {
            string can = female ? "सकती" : "सकता";
            var examples = new (string english, string hindi)[]
            {
                ("The Riverside Library was established in 2019 and is open from 9 AM to 6 PM on weekdays.",
                 "Riverside Library 2019 में शुरू हुई थी, और हफ़्ते के दिनों में सुबह 9 बजे से शाम 6 बजे तक खुली रहती है।"),
                ("You can contact the Riverside Library at +91 98765 43210 or email hello@riverside.example.",
                 "आप Riverside Library से +91 98765 43210 पर बात कर सकते हैं, या hello@riverside.example पर email कर सकते हैं।"),
                ("Our events team helps members plan reading sessions, and you can book a study room through our website from any device.",
                 "हमारी events team members को reading sessions plan करने में मदद करती है, और आप हमारी website के ज़रिए किसी भी device से study room book कर सकते हैं।"),
                ("The library offers interactive learning solutions and a digital archive with real-time search, which makes research much easier.",
                 "Library interactive learning solutions और real-time search वाला एक digital archive देती है, जिससे research करना काफ़ी आसान हो जाता है।"),
                ("I don't have details on a dedicated phone app, but members can reserve study rooms online from any device.",
                 "Phone app के बारे में मेरे पास details नहीं हैं, लेकिन members किसी भी device से online study room reserve कर सकते हैं।"),
                ("Our AI assistant helps staff answer members' questions faster, which cuts waiting time and gives every visitor a " +
                 "consistent, memorable experience across all our branches.",
                 "हमारा AI assistant staff को members के सवालों के जवाब जल्दी देने में मदद करता है, जिससे waiting time कम होता है और " +
                 "हर visitor को सभी branches में एक जैसा, यादगार experience मिलता है।"),
                ("The library is built for students, researchers and book clubs.",
                 "ये library students, researchers और book clubs के लिए बनी है।"),
                ("I can tell you about our timings, events and memberships.",
                 $"मैं आपको हमारी timings, events और memberships के बारे में बता {can} हूँ।"),
                // Conversation (Open Chat avatars): questions back to the listener stay about them.
                ("Have you ever been there?", "क्या आप कभी वहाँ गए हैं?"),
                ("What do you enjoy doing on weekends?", "आपको weekends पर क्या करना अच्छा लगता है?"),
            };
            return examples
                .SelectMany(e => new[] { new ChatMessage("user", "ENGLISH: " + e.english), new ChatMessage("assistant", e.hindi) })
                .ToList();
        }

        /// More Devanagari letters than English ones.
        static bool MostlyHindi(string text) =>
            text.Count(ch => ch >= '\u0900' && ch <= '\u097F') > text.Count(ch => ch < 128 && char.IsLetter(ch));

        static readonly System.Text.RegularExpressions.Regex CapitalisedWord =
            new System.Text.RegularExpressions.Regex(@"\b[A-Z][A-Za-z0-9&\-]{2,}\b");
        static readonly HashSet<string> NotNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "What", "Which", "Who", "Whom", "Whose", "Where", "When", "Why", "How", "Is", "Are", "Was", "Were", "Do", "Does", "Did",
            "Can", "Could", "Will", "Would", "Should", "Has", "Have", "Had", "Tell", "Please", "The", "And", "You", "Your", "Our",
            "Any", "Also", "Hello", "Hi", "Thanks", "Okay", "Yes", "No", "Not", "About", "Give", "Show", "List", "Explain",
        };

        /// Capitalised words in the question (names: Pune, Infosys) that appear nowhere in the passages.
        static List<string> UnmentionedNames(string question, string context)
        {
            int followUp = question.IndexOf(" (Follow-up to: ", StringComparison.Ordinal);
            if (followUp > 0) question = question.Substring(0, followUp);
            var missing = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in CapitalisedWord.Matches(question))
            {
                string word = m.Value;
                if (NotNames.Contains(word) || missing.Contains(word)) continue;
                if (context.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0) missing.Add(word);
            }
            return missing;
        }

        static string BuildPrompt(KnowledgeClient.Passage[] passages, string question, bool hindiQuestion)
        {
            var sb = new StringBuilder("CONTEXT:\n");
            // Answers the owner taught (menu > Learning) come first and win over older document text.
            static bool Taught(KnowledgeClient.Passage p) => p.source != null && p.source.StartsWith(TaughtAnswers.FilePrefix);
            var ordered = passages.OrderBy(p => Taught(p) ? 0 : 1).ToArray();
            for (int i = 0; i < ordered.Length; i++)
            {
                sb.Append('[').Append(i + 1).Append("] ");
                if (Taught(ordered[i])) sb.Append("(Confirmed, most up to date - use this over anything else.) ");
                sb.Append(ordered[i].text.Trim()).Append("\n\n");
            }
            sb.Append("QUESTION: ").Append(question);
            // Names in the question that no passage mentions: Gemma-4B answered "Is there an office in Pune?" with the
            // Mumbai office even with an example of the "no match" case (2026-10-07, P&I). Told outright, it says so.
            var missing = UnmentionedNames(question, string.Join("\n", passages.Select(p => p.text)));
            if (missing.Count > 0) sb.Append("\n(The CONTEXT doesn't mention ").Append(string.Join(", ", missing)).Append(".)");
            // The model answers in the language of the passages it reads; an English
            // question over Hindi documents got a Hindi answer without this. A Hindi
            // question answered from Hindi documents is best in the documents' own words:
            // translating an English answer back turned केसर पिस्ता (saffron pistachio) into
            // "white and almond".
            if (passages.Length > 0 && MostlyHindi(passages[0].text))
                sb.Append(hindiQuestion ? "\n(Answer in Hindi, in the CONTEXT's own words.)" : "\n(Answer in English.)");
            return sb.ToString();
        }
    }
}
