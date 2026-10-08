using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChatbotAI.UI
{
    /// The menu's help: one short guide per menu page (Menu > ? on any page, or Help & guides). Each has a screenshot
    /// of its page with the options it talks about outlined and numbered like its steps - taken by
    /// tools/capture_help_shots.cs into Resources/Help (<id>.png + <id>-marks.json); a guide without them still shows
    /// its text. Kept short: people at a kiosk don't have long to read.
    public sealed class HelpGuide
    {
        public string id, title, intro;
        /// The menu page in the screenshot: a CompanionMenu Build... method (+ its arguments), null = the main screen.
        public string page;
        public object[] pageArgs = Array.Empty<object>();
        /// Each step's text, and what it points at on the screenshot: the text of a row, caption or button on that
        /// page ("Look & voice"), "starts:"/"contains:" + part of it, or "#name" for an element of the main screen.
        public (string text, string mark)[] steps;
        public string[] tips = Array.Empty<string>();
        /// Menu page titles (and Build... method names) that open this guide from their ? button.
        public string[] openedFrom = Array.Empty<string>();
    }

    public static class HelpGuides
    {
        public static readonly HelpGuide[] All =
        {
            new HelpGuide
            {
                id = "kiosk", title = "Using the kiosk", page = null,
                intro = "What visitors see, and how they talk to the bot. Nothing to install or sign in for visitors.",
                steps = new[]
                {
                    ("Tap the microphone and ask a question in English or Hindi. Tap again when done - or switch on Automatic listening and just talk.", "#mainButton"),
                    ("While the bot is speaking, tap the same button to stop it.", null),
                    ("The question and the answer show here. They clear a few seconds after the bot stops, ready for the next visitor.", "#dock"),
                    ("Staff only: tap the menu button and sign in to change anything.", "#menuButton"),
                },
                tips = new[]
                {
                    "A Hindi question gets a Hindi answer, an English one an English answer.",
                    "\"Didn't catch that\" means nothing clear was heard - speak a little closer to the microphone.",
                    "With a keyboard attached, holding Space also talks.",
                },
            },
            new HelpGuide
            {
                id = "menu", title = "The menu at a glance", page = "BuildRoot", openedFrom = new[] { "Settings" },
                intro = "Everything staff can change, in one list. Changes are saved at once and stay on this PC.",
                steps = new[]
                {
                    ("BOTS - tap a bot to bring it on screen. The tick shows who is talking now.", "BOTS"),
                    ("The pages of the bot on screen: its look and voice, personality, documents, camera and backdrop.", "Look & voice"),
                    ("VISITORS - how people talk to the bots, what they asked, and the report.", "Listening & language"),
                    ("DEVICES - the microphone, speaker and screen this PC uses.", "Microphone"),
                    ("ADVANCED - AI models, staff passwords and fine-tuning. Rarely needed.", "AI models"),
                },
                tips = new[]
                {
                    "Tap ? at the top of any page for its own guide.",
                    "\"Close the app\" (at the bottom) needs a second tap, so it can't happen by accident.",
                    "Sign out when you're done, so visitors can't open the menu.",
                },
            },
            new HelpGuide
            {
                id = "bots", title = "Switching and adding bots", page = "BuildRoot",
                intro = "Each bot has its own name, look, voices, personality and documents. Only one is on screen at a time.",
                steps = new[]
                {
                    ("Tap a bot's name to put it on screen - it takes about a second.", "BOTS"),
                    ("New bot - make another bot from one of the 3D characters.", "New bot…"),
                    ("Import a bot - load a bot exported from another kiosk (a .altbot file, e.g. from a USB drive).", "Import a bot…"),
                    ("Copy, export, reset or delete the bot on screen.", "Copy, export, reset or delete"),
                },
                tips = new[]
                {
                    "Knowledge-only bots answer only from their documents; Open chat bots talk about anything.",
                    "The app always starts with the first bot in the list.",
                },
            },
            new HelpGuide
            {
                id = "newbot", title = "Making a new bot", page = "BuildNewBot", openedFrom = new[] { "New bot" },
                intro = "A new bot in three choices. It starts with the voices and personality of the bot that has the same look.",
                steps = new[]
                {
                    ("Type its name (tap Speak to say it, or Keyboard to type on screen).", "NAME"),
                    ("Pick its look - one of the 3D characters. Several bots can share one.", "LOOK"),
                    ("Knowledge only (answers from documents you give it) or Open chat (talks about anything).", "Kind"),
                    ("Tap Create bot. Then add its documents and change its voice and personality on its own pages.", "Create bot"),
                },
                tips = new[] { "A Knowledge-only bot can't answer anything until it has documents - add them right after." },
            },
            new HelpGuide
            {
                id = "look", title = "Look & voice", page = "BuildLookVoice",
                openedFrom = new[] { "Look & voice", "English voice", "Hindi voice" },
                intro = "How the bot looks and sounds.",
                steps = new[]
                {
                    ("LOOK - tap a character to change the bot's body and face.", "LOOK"),
                    ("English voice - tap to see the list; tap a voice there to hear it and choose it.", "English voice"),
                    ("Hindi voice - the voice for Hindi answers. Veena voices sound most natural.", "Hindi voice"),
                    ("Speaking speed - slower or faster (Veena voices keep their own pace).", "Speaking speed"),
                },
                tips = new[]
                {
                    "Keep both voices the same gender - Hindi answers use matching verb forms.",
                    "Voices marked Veena are the same person in English and Hindi.",
                },
            },
            new HelpGuide
            {
                id = "personality", title = "Personality", page = "BuildPersonality", openedFrom = new[] { "Personality", "Tone" },
                intro = "Who the bot is and how it talks. This doesn't change what it knows - that comes from its documents.",
                steps = new[]
                {
                    ("NAME - what the bot calls itself.", "NAME"),
                    ("TOPICS - what it's about, separated by commas. Used in \"Ask me anything about...\".", "TOPICS"),
                    ("WHO IT IS - a few lines describing its character, in plain words.", "WHO IT IS"),
                    ("STYLE - tone, formality and humour change at once.", "STYLE"),
                },
                tips = new[]
                {
                    "Text boxes (name, who it is, how it talks, always, never) are kept only after you tap Save at the bottom.",
                    "ALWAYS / NEVER: short rules, e.g. \"never promise discounts\".",
                },
            },
            new HelpGuide
            {
                id = "conversation", title = "Conversation", page = "BuildConversation", openedFrom = new[] { "Conversation" },
                intro = "How the bot answers: only from its documents, or about anything.",
                steps = new[]
                {
                    ("Answers - Knowledge only refuses anything not in its documents; Open chat talks about anything.", "Answers"),
                    ("Reply length - from one line to detailed.", "Reply length"),
                    ("Topic strictness - higher refuses more questions that only loosely match the documents.", "Topic strictness"),
                    ("Answer checking - higher blocks more made-up answers (and a few good ones).", "Answer checking"),
                },
                tips = new[]
                {
                    "Company bots should stay on Knowledge only, or they may make up facts.",
                    "Leave strictness and checking as they are unless answers are refused or wrong too often.",
                },
            },
            new HelpGuide
            {
                id = "documents", title = "Documents", page = "BuildDocuments", openedFrom = new[] { "Documents", "Document" },
                intro = "What a Knowledge-only bot knows. It answers only from these files.",
                steps = new[]
                {
                    ("The bot's files. Tap one to see it or remove it.", "contains:DOCUMENTS"),
                    ("Add documents - pick PDF, Word, PowerPoint or text files from this PC or a USB drive.", "Add documents…"),
                    ("Same for... - bots sharing these documents get the same change, so they answer alike.", "starts:Same for"),
                    ("Open the documents folder - see the files in Windows.", "Open the documents folder"),
                },
                tips = new[]
                {
                    "New files are read in the background - give it a minute before asking about them.",
                    "For Hindi, Word or text files work best; Hindi PDFs often read back scrambled.",
                },
            },
            new HelpGuide
            {
                id = "camera", title = "Camera", page = "BuildCamera", openedFrom = new[] { "Camera" },
                intro = "How the bot is framed on screen. The page sits low so you can see the change while you make it.",
                steps = new[]
                {
                    ("Tap a shot to start from it - waist up, close-up, full body and more.", "Relaxed"),
                    ("POSITION - drag the sliders to move the camera closer, higher, sideways or round.", "Distance"),
                    ("Lens - smaller is flatter and more flattering (move back to fit).", "Lens"),
                    ("Reset camera - back to the character's own framing.", "Reset camera"),
                },
                tips = new[] { "Saved per character, and separately for tall and wide screens." },
            },
            new HelpGuide
            {
                id = "backdrop", title = "Backdrop", page = "BuildStage", openedFrom = new[] { "Stage" },
                intro = "What's behind the bot.",
                steps = new[]
                {
                    ("Studio - a soft photo-studio look with a shadow on the floor.", "Studio"),
                    ("Gradient, Picture or Flat colour - other backgrounds.", "Picture"),
                    ("Brightness - lighter or darker behind the bot.", "Brightness"),
                },
                tips = new[] { "Set for this bot only. Light and dark follow Display > Appearance." },
            },
            new HelpGuide
            {
                id = "lighting", title = "Lighting", page = "BuildLighting", openedFrom = new[] { "Lighting" },
                intro = "How the bot is lit. Changes show at once on the bot behind the menu.",
                steps = new[]
                {
                    ("Tap a look - Natural, Soft, Bright, Dramatic, Warm, Cool or Glow. Start here.", "starts:LOOKS"),
                    ("MAIN LIGHT - how bright it is, where it comes from, how high, and warm or cool.", "MAIN LIGHT"),
                    ("OTHER LIGHTS - soft fill, back light, the studio glow and how dark the shadows are.", "OTHER LIGHTS"),
                    ("Reset lighting - back to how it was delivered.", "Reset lighting"),
                },
                tips = new[]
                {
                    "Saved for the bot on screen - each bot can have its own lighting.",
                    "Faces look best with Soft or Natural. Dramatic is strong - keep it for effect.",
                    "The brightness of the background is in Backdrop.",
                },
            },            new HelpGuide
            {
                id = "manage", title = "Copy, export, reset or delete", page = "BuildManage", openedFrom = new[] { "BuildManage" },
                intro = "Look after the bot on screen.",
                steps = new[]
                {
                    ("Make a copy - a new bot with the same look, voices, personality and documents.", "starts:Make a copy"),
                    ("Export - one file to take the bot to another kiosk (Bots > Import a bot there).", "starts:Export"),
                    ("Reset - put its settings back to how they were delivered.", "starts:Reset"),
                    ("Delete - only bots made here (New bot or a copy) can be deleted.", "DELETE"),
                },
                tips = new[] { "Plug in a USB drive before exporting to save the file straight onto it." },
            },
            new HelpGuide
            {
                id = "listening", title = "Listening & language", page = "BuildLanguage", openedFrom = new[] { "Listening & language" },
                intro = "How visitors start talking, and which languages it hears and answers in. The same for every bot.",
                steps = new[]
                {
                    ("Listening - Tap the button (best in busy, noisy places) or Automatic (it starts when someone speaks).", "Listening"),
                    ("Listens for - Auto tells English and Hindi apart. Pick one if visitors only speak that.", "Listens for"),
                    ("Replies in - same as asked, or always English / always Hindi.", "Replies in"),
                },
                tips = new[] { "Noise filter and listening fine-tuning: Advanced > Listening." },
            },
            new HelpGuide
            {
                id = "learning", title = "Questions & answers", page = "BuildLearning",
                openedFrom = new[] { "Learning", "Teach an answer", "Correct an answer", "Taught answers", "Taught answer" },
                intro = "What visitors asked, and where you teach the bots better answers. Nothing is learnt without you.",
                steps = new[]
                {
                    ("COULDN'T ANSWER - questions the documents didn't cover. Tap one to teach the answer.", "COULDN'T ANSWER"),
                    ("RECENT ANSWERS - tap a wrong answer to correct it.", "RECENT ANSWERS"),
                    ("Taught answers - see, change or delete what you've taught.", "Taught answers"),
                    ("Export learning - take the conversations and taught answers to the development computer.", "Export learning to Desktop"),
                },
                tips = new[]
                {
                    "A taught answer is used from the very next question.",
                    "Weather, cricket and other off-topic questions are refused on purpose and not listed.",
                },
            },
            new HelpGuide
            {
                id = "report", title = "Report", page = "BuildReport", openedFrom = new[] { "Report" },
                intro = "How busy the kiosk was and what people asked.",
                steps = new[]
                {
                    ("QUESTIONS - today, this week, this month, and how many were answered from the documents.", "QUESTIONS"),
                    ("MOST ASKED - the top questions, per bot.", "MOST ASKED"),
                    ("Save to Desktop - everything as one Excel file (or onto a USB drive if one is plugged in).", "Save to Desktop"),
                },
                tips = new[] { "The Excel file covers the last 30 days, with every question and its answer." },
            },
            new HelpGuide
            {
                id = "microphone", title = "Microphone", page = "BuildMicrophones", openedFrom = new[] { "Microphone" },
                intro = "Which microphone the bot listens with.",
                steps = new[]
                {
                    ("System default - whatever Windows uses.", "System default"),
                    ("Or tap a microphone by name - the tick shows the one in use.", null),
                },
                tips = new[] { "If the chosen microphone is unplugged, it falls back to the system default." },
            },
            new HelpGuide
            {
                id = "speaker", title = "Speaker", page = "BuildSpeakers", openedFrom = new[] { "Speaker" },
                intro = "Where the bot's voice comes out.",
                steps = new[]
                {
                    ("System default - whatever Windows uses.", "System default"),
                    ("Or tap a speaker by name. Only the app's sound moves; other sounds stay where they are.", null),
                },
                tips = new[] { "Test it: Look & voice > English voice, tap a voice to hear it." },
            },
            new HelpGuide
            {
                id = "screen", title = "Screen", page = "BuildScreens", openedFrom = new[] { "Screen" },
                intro = "With more than one screen connected: which one shows the app.",
                steps = new[] { ("Tap a screen to move the app there. It's remembered for the next start.", "SHOW THE APP ON") },
                tips = new[] { "Works in the built app (not in Unity's Editor)." },
            },
            new HelpGuide
            {
                id = "display", title = "Display", page = "BuildRoot",
                intro = "How the screen looks, for every bot.",
                steps = new[]
                {
                    ("Appearance - light or dark.", "Appearance"),
                    ("Show keyboard - adds a keyboard button next to the microphone, so visitors can type.", "Show keyboard"),
                    ("Debug panel - timings and details for a developer. Keep it off for visitors.", "Debug panel"),
                },
                tips = new[] { "Text size, the conversation clearing time and more: Advanced > Display & performance." },
            },
            new HelpGuide
            {
                id = "models", title = "AI models", page = "BuildModels", openedFrom = new[] { "AI models", "Online AI" },
                intro = "The AI parts that run on this PC. Already set up - change only if told to.",
                steps = new[]
                {
                    ("BRAIN - the language model that writes the answers.", "BRAIN - LANGUAGE MODEL"),
                    ("EARS - speech recognition: turns what visitors say into text.", "EARS - SPEECH RECOGNITION"),
                    ("ANSWER CHECKER - picks the passages that really answer a question. On is best.", "DOCUMENT SEARCH - ANSWER CHECKER"),
                    ("VEENA - the natural Hindi voices. Off starts faster but Hindi sounds plainer.", "VEENA · NATURAL HINDI"),
                },
                tips = new[] { "Changes here are used from the next time the app starts." },
            },
            new HelpGuide
            {
                id = "staff", title = "Staff sign-in", page = "BuildStaff", openedFrom = new[] { "Staff sign-in", "Add a person" },
                intro = "Who can open the menu. Visitors can't without a username and password.",
                steps = new[]
                {
                    ("Tap a person to change their password or remove them.", "WHO CAN OPEN THIS MENU"),
                    ("Add a person - a username and password for each member of staff.", "Add a person…"),
                },
                tips = new[]
                {
                    "Change the default admin / admin password before the kiosk goes public.",
                    "Passwords are stored scrambled, on this PC only (and in your next build).",
                },
            },
            new HelpGuide
            {
                id = "tuning", title = "Fine-tuning (Advanced)", page = "BuildTunables", pageArgs = new object[] { "Display & performance" },
                intro = "Fine adjustments for the voice, listening, mouth, eyes, body, brain and screen. Rarely needed.",
                steps = new[]
                {
                    ("Each page holds sliders and switches with a one-line explanation underneath.", "Text & button size"),
                    ("Clear the conversation after - how soon the screen is ready for the next visitor.", "Clear the conversation after"),
                    ("Frame rate cap - keep 60; higher can make the Hindi voice break up.", "Frame rate cap"),
                    ("Reset this page - back to how it was delivered.", "Reset this page"),
                },
                tips = new[]
                {
                    "Listening page: the background noise filter and how loud a voice must be for Automatic listening.",
                    "\"1 changed\" next to a page in the menu means something there was adjusted.",
                },
            },
        };

        /// The guide for a menu page (its title, or its Build... method), or null.
        public static HelpGuide For(string pageTitle, string buildMethod)
        {
            foreach (var g in All)
                if (Array.IndexOf(g.openedFrom, pageTitle) >= 0 || (buildMethod != null && Array.IndexOf(g.openedFrom, buildMethod) >= 0))
                    return g;
            if (Tunables.Pages(Tunables.All()).Contains(pageTitle)) return Find("tuning");
            return null;
        }

        public static HelpGuide Find(string id) => Array.Find(All, g => g.id == id);

        // ---- Screenshots (Resources/Help) ----

        [Serializable]
        public class Mark
        {
            public int step;
            public float x, y, w, h;   // 0-1 of the screenshot, from its top-left
        }

        [Serializable]
        public class Marks
        {
            public List<Mark> marks = new List<Mark>();
        }

        public static Texture2D Shot(HelpGuide g) => Resources.Load<Texture2D>("Help/" + g.id);

        public static List<Mark> MarksOf(HelpGuide g)
        {
            var text = Resources.Load<TextAsset>("Help/" + g.id + "-marks");
            if (!text) return new List<Mark>();
            try { return JsonUtility.FromJson<Marks>(text.text)?.marks ?? new List<Mark>(); }
            catch (ArgumentException) { return new List<Mark>(); }
        }
    }
}
