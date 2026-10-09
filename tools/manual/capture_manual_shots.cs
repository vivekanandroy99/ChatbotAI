// Play mode (Unity CLI: unity command eval_file tools/manual/capture_manual_shots.cs): every screenshot for the staff manual
// (tools/manual/build_manual.py) -> tools/manual/shots/<id>.png + manifest.json (where each marked option is, 0-1 of the picture).
// Renders at 1440x2560 portrait (the kiosk's shape). Real questions are asked of the real bots for the conversation pictures
// (saving conversations is switched off meanwhile and the sample log below stands in for the visitors' real questions - nothing
// of a real visitor is shown). Progress: tools/manual/shots/_capture.txt ("ALLDONE" when finished).
// Re-run after the menu changes, then run build_manual.py.
string projectDir = System.IO.Directory.GetCurrentDirectory();
string outDir = System.IO.Path.Combine(projectDir, "tools/manual/shots");
string logPath = System.IO.Path.Combine(outDir, "_capture.txt");
System.IO.Directory.CreateDirectory(outDir);
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var sflags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
var ui = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.UI.CompanionUI>();
var menu = (ChatbotAI.UI.CompanionMenu)typeof(ChatbotAI.UI.CompanionUI).GetField("menu", flags).GetValue(ui);
var mt = typeof(ChatbotAI.UI.CompanionMenu);
var root = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>().rootVisualElement;
var sheet = UnityEngine.UIElements.UQueryExtensions.Q(root, "sheet");
var scroll = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.ScrollView>(root, "sheetScroll");
var registry = ChatbotAI.Dialogue.AvatarRegistry.Instance;
var dialogue = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Dialogue.DialogueController>();
var input = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Audio.SpeechInputController>();
var output = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Audio.SpeechOutputController>();
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(logPath, log.ToString()); }
// manifest.json: one line per picture, kept between runs (a run limited to some pages by shots/_only.txt only replaces those).
var manifestEntries = new System.Collections.Generic.SortedDictionary<string, string>(System.StringComparer.Ordinal);
string manifestPath = System.IO.Path.Combine(outDir, "manifest.json");
string[] only = System.IO.File.Exists(System.IO.Path.Combine(outDir, "_only.txt"))
    ? System.IO.File.ReadAllLines(System.IO.Path.Combine(outDir, "_only.txt")).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray() : null;
if (System.IO.File.Exists(manifestPath))
    foreach (string line in System.IO.File.ReadAllLines(manifestPath))
    {
        int a = line.IndexOf("\"id\":\"");
        if (a < 0) continue;
        a += 6;
        manifestEntries[line.Substring(a, line.IndexOf('"', a) - a)] = line.Trim().TrimEnd(',');
    }
void ForgetPictures(string id)
{
    foreach (string key in manifestEntries.Keys.Where(k => k == id || k.StartsWith(id + "_")).ToList())
    {
        manifestEntries.Remove(key);
        try { System.IO.File.Delete(System.IO.Path.Combine(outDir, key + ".png")); } catch { }
    }
}

var gameViewType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
var gameView = UnityEditor.EditorWindow.GetWindow(gameViewType, false, null, false);
var sizeIndex = gameViewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
int oldSize = (int)sizeIndex.GetValue(gameView);

bool Matches(string text, string mark) =>
    text != null && (mark.StartsWith("starts:") ? text.StartsWith(mark.Substring(7))
                   : mark.StartsWith("contains:") ? text.Contains(mark.Substring(9))
                   : text.Trim() == mark);

// What a mark outlines: a row (its whole row), a caption (with the group under it), a button, or "#name" = a named element.
UnityEngine.Rect? Target(string mark)
{
    if (mark == null) return null;
    if (mark.StartsWith("#"))
    {
        var named = UnityEngine.UIElements.UQueryExtensions.Q(root, mark.Substring(1));
        return named != null && named.resolvedStyle.display != UnityEngine.UIElements.DisplayStyle.None && named.worldBound.width > 1 ? named.worldBound : (UnityEngine.Rect?)null;
    }
    foreach (var t in UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.TextElement>(scroll.contentContainer).ToList())
    {
        if (!Matches(t.text, mark) || t.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.None) continue;
        if (t.ClassListContains("section__caption"))
        {
            var r = t.worldBound;
            int i = t.parent.IndexOf(t);
            if (i + 1 < t.parent.childCount)
            {
                var g = t.parent[i + 1].worldBound;
                r = UnityEngine.Rect.MinMaxRect(UnityEngine.Mathf.Min(r.xMin, g.xMin), r.yMin, UnityEngine.Mathf.Max(r.xMax, g.xMax), g.yMax);
            }
            return r;
        }
        UnityEngine.UIElements.VisualElement e = t;
        while (e != null && !e.ClassListContains("row") && !(e is UnityEngine.UIElements.Button) && e != scroll.contentContainer) e = e.parent;
        return (e == null || e == scroll.contentContainer ? t : e).worldBound;
    }
    return null;
}

async System.Threading.Tasks.Task Frames(int n) { for (int i = 0; i < n; i++) await System.Threading.Tasks.Task.Yield(); }
async System.Threading.Tasks.Task Wait(int ms) { await System.Threading.Tasks.Task.Delay(ms); }

System.Collections.IEnumerator Grab(System.Threading.Tasks.TaskCompletionSource<UnityEngine.Texture2D> done)
{
    yield return new UnityEngine.WaitForEndOfFrame();
    done.SetResult(UnityEngine.ScreenCapture.CaptureScreenshotAsTexture());
}
System.Threading.Tasks.Task<UnityEngine.Texture2D> Capture()
{
    var done = new System.Threading.Tasks.TaskCompletionSource<UnityEngine.Texture2D>();
    ui.StartCoroutine(Grab(done));
    return done.Task;
}

float panelToPx = 2f;

// Crops `area` (panel units) out of the screenshot, saves <id>.png and adds it to the manifest with its marks.
void Save(string id, UnityEngine.Texture2D shot, UnityEngine.Rect area, System.Collections.Generic.List<(int n, string label, UnityEngine.Rect r, bool redact)> marks)
{
    int x0 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(area.xMin * panelToPx), 0, shot.width - 4);
    int w = UnityEngine.Mathf.Min(shot.width - x0, UnityEngine.Mathf.RoundToInt(area.width * panelToPx)) / 4 * 4;
    int yTop = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(area.yMin * panelToPx), 0, shot.height - 4);
    int h = UnityEngine.Mathf.Min(shot.height - yTop, UnityEngine.Mathf.RoundToInt(area.height * panelToPx)) / 4 * 4;
    var crop = new UnityEngine.Texture2D(w, h, UnityEngine.TextureFormat.RGB24, false);
    crop.SetPixels(shot.GetPixels(x0, shot.height - yTop - h, w, h));
    crop.Apply();
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, id + ".png"), crop.EncodeToPNG());
    UnityEngine.Object.Destroy(crop);
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var sb = new System.Text.StringBuilder();
    foreach (var (n, label, r, redact) in marks)
    {
        if (sb.Length > 0) sb.Append(",");
        sb.Append("{\"n\":" + n + ",\"label\":\"" + label.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"redact\":" + (redact ? "true" : "false") +
                  ",\"x\":" + ((r.xMin - area.xMin) / area.width).ToString("0.0000", inv) + ",\"y\":" + ((r.yMin - area.yMin) / area.height).ToString("0.0000", inv) +
                  ",\"w\":" + (r.width / area.width).ToString("0.0000", inv) + ",\"h\":" + (r.height / area.height).ToString("0.0000", inv) + "}");
    }
    manifestEntries[id] = "{\"id\":\"" + id + "\",\"w\":" + w + ",\"h\":" + h + ",\"marks\":[" + sb + "]}";
    System.IO.File.WriteAllText(manifestPath, "[\n" + string.Join(",\n", manifestEntries.Values.Select(e => "  " + e)) + "\n]\n");
    Log($"{id}: {w}x{h}, {marks.Count} marks");
}

// A whole-screen picture with these "#name" marks (menu closed).
async System.Threading.Tasks.Task FullShot(string id, params string[] marks)
{
    var list = new System.Collections.Generic.List<(int, string, UnityEngine.Rect, bool)>();
    int n = 0;
    foreach (string m in marks)
        if (Target(m) is UnityEngine.Rect r) list.Add((++n, m.TrimStart('#'), r, false)); else Log($"  {id}: '{m}' not found");
    await Frames(2);
    var shot = await Capture();
    Save(id, shot, root.panel.visualTree.layout, list);
    UnityEngine.Object.Destroy(shot);
}

ChatbotAI.Dialogue.AvatarProfile ProfileOf(string avatarId) { foreach (var p in registry.Profiles) if (p.avatarId == avatarId) return p; return null; }

async System.Threading.Tasks.Task UseBot(string avatarId)
{
    var p = ProfileOf(avatarId);
    if (p == null) { Log("no bot " + avatarId); return; }
    if (registry.Active != p)
    {
        if (menu.IsOpen) menu.Close();
        registry.SetActive(p);
        await Wait(3200);
    }
}

async System.Threading.Tasks.Task<bool> UntilState(ChatbotAI.UI.CompanionUI.State s, int timeoutMs)
{
    for (int t = 0; t < timeoutMs; t += 30) { if (ui.Current == s) return true; await Wait(30); }
    return false;
}

// Asks (typed) and captures the Thinking moment and, once it speaks, the Speaking picture.
async System.Threading.Tasks.Task Conversation(string idPrefix, string question, bool thinkingShot)
{
    if (output.IsBusy) output.Stop();
    await UntilState(ChatbotAI.UI.CompanionUI.State.Ready, 8000);
    dialogue.Ask(question);
    if (thinkingShot && await UntilState(ChatbotAI.UI.CompanionUI.State.Thinking, 4000))
    {
        await Wait(250);
        await FullShot(idPrefix + "_thinking", "#youBlock", "#stateCaption");
    }
    if (!await UntilState(ChatbotAI.UI.CompanionUI.State.Speaking, 60000)) { Log("  " + idPrefix + ": never spoke"); return; }
    var reply = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root, "replyText");
    for (int i = 0; i < 100 && (reply.text ?? "").Length < 25; i++) await Wait(100);
    await Wait(1800);
    await FullShot(idPrefix + "_speaking", "#youBlock", "#replyBlock", "#mainButton", "#stateCaption");
    output.Stop();
    await UntilState(ChatbotAI.UI.CompanionUI.State.Ready, 8000);
    await Wait(400);
}

// ---------------- Menu pages ----------------

string helpPageTitle = "Help & guides";
var helpField = mt.GetField("HelpPage", sflags);
if (helpField != null) helpPageTitle = (string)helpField.GetRawConstantValue();
string sampleDocument = System.IO.Path.Combine(outDir, "Sample brochure.pdf");

ChatbotAI.Dialogue.ConversationLog.Exchange Ex(int daysAgo, string name, string id, string q, string outcome, string reply) =>
    new ChatbotAI.Dialogue.ConversationLog.Exchange { time = System.DateTime.Now.AddDays(-daysAgo).AddMinutes(-daysAgo * 37 - 20).ToString("s"),
        avatarId = id, avatarName = name, question = q, english = q, outcome = outcome, reply = reply, score = 0.6f };

// Page id -> (the title Push shows, what builds it).
(string title, System.Action build)? PageOf(string kind, ChatbotAI.Dialogue.AvatarProfile bot)
{
    System.Action Call(string method, params object[] args) { var m = mt.GetMethod(method, flags); return () => m.Invoke(menu, args); }
    object Lang(string name) => System.Enum.Parse(mt.GetMethod("BuildVoices", flags).GetParameters()[0].ParameterType, name);
    var teach = Ex(0, "Maya", "altcore_female", "Do you have an office in Mumbai?", "NotInDocuments", "I can't help with that one, but ask me anything about Altscape and Altcore!");
    var correct = Ex(0, "Maya", "altcore_female", "What does a demo cost?", "Answered", "A demo costs nothing - book one on the website.");
    switch (kind)
    {
        case "help": return (helpPageTitle, Call("BuildGuideList"));
        case "guide": return ("Listening & language", Call("BuildGuide", ChatbotAI.UI.HelpGuides.Find("listening")));
        case "newbot": return ("New bot", Call("BuildNewBot"));
        case "lookvoice": return ("Look & voice", Call("BuildLookVoice"));
        case "voices-en": return ("English voice", Call("BuildVoices", Lang("English")));
        case "voices-hi": return ("Hindi voice", Call("BuildVoices", Lang("Hindi")));
        case "personality": return ("Personality", Call("BuildPersonality"));
        case "tone": return ("Tone", Call("BuildTone"));
        case "conversation": return ("Conversation", Call("BuildConversation"));
        case "documents": return ("Documents", Call("BuildDocuments"));
        case "document": return ("Document", Call("BuildDocument", sampleDocument));
        case "camera": return ("Camera", Call("BuildCamera"));
        case "stage": return ("Stage", Call("BuildStage"));
        case "lighting": return ("Lighting", Call("BuildLighting"));
        case "manage": return (bot.displayName, Call("BuildManage"));
        case "language": return ("Listening & language", Call("BuildLanguage"));
        case "learning": return ("Learning", Call("BuildLearning"));
        case "teach": return ("Teach an answer", Call("BuildTeach", teach, ""));
        case "correct": return ("Correct an answer", Call("BuildTeach", correct, correct.reply));
        case "taughtedit":
            var entry = new ChatbotAI.Dialogue.TaughtAnswers.Entry { question = "What are your opening hours?", answer = "Example: we're open from 10 am to 6 pm on weekdays." };
            entry.avatarNames.Add("Maya"); entry.avatarNames.Add("Ethan");
            return ("Taught answer", Call("BuildTaughtEdit", entry));
        case "report": return ("Report", Call("BuildReport"));
        case "mics": return ("Microphone", Call("BuildMicrophones"));
        case "speakers": return ("Speaker", Call("BuildSpeakers"));
        case "screens": return ("Screen", Call("BuildScreens"));
        case "models": return ("AI models", Call("BuildModels"));
        case "online": return ("Online AI", Call("BuildOnlineAi"));
        case "staff": return ("Staff sign-in", Call("BuildStaff"));
        case "staffadd": return ("Add a person", Call("BuildStaffMember", new object[] { null }));
        default:
            if (kind.StartsWith("tun:")) { string page = kind.Substring(4); return (page, Call("BuildTunables", page)); }
            return null;
    }
}

// id, bot, kind, whole-screen picture (the camera-like pages sit low over the bot), most pictures, the marks.
var pages = new (string id, string bot, string kind, bool full, int maxViews, string[] marks)[]
{
    ("root", "altcore_female", "root", false, 4, new[] { "Help & guides", "BOTS", "New bot…", "Import a bot…", "MAYA", "Look & voice", "Camera", "Copy, export, reset or delete",
        "VISITORS", "DEVICES", "DISPLAY", "ADVANCED", "starts:Sign out", "!starts:Sign out", "Close the app" }),
    ("help", "altcore_female", "help", false, 1, new[] { "Using the kiosk" }),
    ("guide", "altcore_female", "guide", false, 1, new string[0]),
    ("newbot", "altcore_female", "newbot", false, 2, new[] { "NAME", "LOOK", "Kind", "Create bot" }),
    ("lookvoice", "altcore_female", "lookvoice", false, 2, new[] { "LOOK", "VOICE", "English voice", "Hindi voice", "Speaking speed" }),
    ("voices_en", "altcore_female", "voices-en", false, 1, new[] { "FEMALE" }),
    ("voices_hi", "altcore_female", "voices-hi", false, 1, new[] { "FEMALE" }),
    ("personality", "altcore_female", "personality", false, 3, new[] { "NAME", "TOPICS", "WHO IT IS", "STYLE", "Tone", "HOW IT TALKS", "ALWAYS", "NEVER", "Save" }),
    ("tone", "altcore_female", "tone", false, 1, new[] { "Warm" }),
    ("conversation", "altcore_female", "conversation", false, 2, new[] { "MODE", "Answers", "Reply length", "KNOWLEDGE", "Topic strictness", "Answer checking", "Passages read per question" }),
    ("conversation_chat", "chat_female", "conversation", false, 2, new[] { "Answers", "Reply length", "OPEN CHAT", "Memory", "Creativity", "Asks questions back", "Topics to avoid" }),
    ("documents", "altcore_female", "documents", false, 2, new[] { "contains:DOCUMENTS", "Add documents…", "starts:Same for", "Open the documents folder" }),
    ("document", "altcore_female", "document", false, 1, new[] { "starts:Remove from" }),
    ("camera", "altcore_female", "camera", true, 4, new[] { "starts:SHOTS", "POSITION", "Distance", "LENS & MOVEMENT", "Lens", "Follow the character", "Reset camera" }),
    ("stage", "altcore_female", "stage", true, 2, new[] { "BACKDROP", "Studio", "ADJUST", "Brightness" }),
    ("lighting", "altcore_female", "lighting", true, 3, new[] { "starts:LOOKS", "MAIN LIGHT", "Brightness", "Warmth", "OTHER LIGHTS", "Reset lighting" }),
    ("manage", "altcore_female", "manage", false, 2, new[] { "COPY", "TAKE IT TO ANOTHER KIOSK", "RESET", "DELETE" }),
    ("language", "altcore_female", "language", false, 1, new[] { "Listening", "Listens for", "Replies in" }),
    ("learning", "altcore_female", "learning", false, 3, new[] { "COULDN'T ANSWER", "RECENT ANSWERS", "Taught answers", "Save conversations", "Save a report", "Open the conversations folder", "starts:Export learning" }),
    ("teach", "altcore_female", "teach", false, 1, new[] { "QUESTION", "THE RIGHT ANSWER", "Teach this answer", "Not worth teaching - hide it" }),
    ("correct", "altcore_female", "correct", false, 1, new[] { "QUESTION", "contains:SAID", "THE RIGHT ANSWER", "Teach this answer" }),
    ("taughtedit", "altcore_female", "taughtedit", false, 1, new[] { "QUESTION", "ANSWER", "Save", "Forget this answer" }),
    ("report", "altcore_female", "report", false, 3, new[] { "QUESTIONS", "LAST 7 DAYS", "MOST ASKED", "COULDN'T ANSWER", "EXPORT TO EXCEL", "starts:Save to" }),
    ("mics", "altcore_female", "mics", false, 1, new[] { "System default" }),
    ("speakers", "altcore_female", "speakers", false, 1, new[] { "System default" }),
    ("screens", "altcore_female", "screens", false, 1, new[] { "SHOW THE APP ON" }),
    ("models", "altcore_female", "models", false, 4, new[] { "BRAIN - LANGUAGE MODEL", "EARS - SPEECH RECOGNITION", "DOCUMENT SEARCH - ANSWER CHECKER", "starts:VOICE - KOKORO", "VEENA · NATURAL HINDI", "ONLINE AI", "Use the models set in the Inspector" }),
    ("online", "altcore_female", "online", false, 3, new[] { "THE BRAIN", "SERVICE", "MODEL", "starts:Choose from the service", "API KEY", "Save the key", "Test the connection" }),
    ("staff", "altcore_female", "staff", false, 1, new[] { "WHO CAN OPEN THIS MENU", "Add a person…", "!admin" }),
    ("staffadd", "altcore_female", "staffadd", false, 1, new[] { "NAME", "PASSWORD", "Add" }),
    ("tun_display", "altcore_female", "tun:Display & performance", false, 3, new[] { "Text & button size", "Frame rate cap", "Show the conversation text", "Clear the conversation after", "Reset this page" }),
    ("tun_listening", "altcore_female", "tun:Listening", false, 2, new[] { "Stop listening when you stop talking", "Background noise filter", "starts:Automatic: how loud", "Reset this page" }),
    ("tun_voice", "altcore_female", "tun:Voice & sound", false, 1, new[] { "Voice volume", "Voice buffer before each sentence", "Reset this page" }),
    ("tun_mouth", "altcore_female", "tun:Mouth & lip-sync", false, 1, new[] { "Lip movement", "Jaw opening", "Mouth speed" }),
    ("tun_eyes", "altcore_female", "tun:Eyes", false, 1, new[] { "Blinking", "Glances away", "Eyelids" }),
    ("tun_body", "altcore_female", "tun:Body", false, 1, new[] { "Body motion speed", "Smoothness between motions" }),
    ("tun_brain", "altcore_female", "tun:Brain", false, 1, new[] { "New visitor after", "starts:Second chance", "starts:Taught answers" }),
};

async System.Threading.Tasks.Task CapturePage((string id, string bot, string kind, bool full, int maxViews, string[] marks) spec)
{
    ForgetPictures(spec.id);
    await UseBot(spec.bot);
    var bot = ProfileOf(spec.bot);
    if (!menu.IsOpen)
    {
        ui.Access.TrySignIn("admin", "admin");
        typeof(ChatbotAI.UI.CompanionUI).GetMethod("OpenMenu", flags).Invoke(ui, null);
        await Wait(900);
    }
    mt.GetMethod("ShowRoot", flags).Invoke(menu, null);
    if (spec.kind != "root")
    {
        var page = PageOf(spec.kind, bot);
        if (page == null) { Log("unknown page " + spec.kind); return; }
        mt.GetMethod("Push", flags).Invoke(menu, new object[] { page.Value.title, page.Value.build, null });
    }
    await Wait(spec.full ? 1100 : 750);
    var viewport = scroll.worldBound;
    var sheetBox = sheet.worldBound;
    float contentH = scroll.contentContainer.worldBound.height;
    float stepH = UnityEngine.Mathf.Max(200f, viewport.height - 150f);
    var offsets = new System.Collections.Generic.List<float> { 0f };
    float maxOffset = UnityEngine.Mathf.Max(0f, contentH - viewport.height);
    while (offsets[offsets.Count - 1] < maxOffset - 1f && offsets.Count < spec.maxViews)
        offsets.Add(UnityEngine.Mathf.Min(maxOffset, offsets[offsets.Count - 1] + stepH));
    int number = 0;
    var numbered = new System.Collections.Generic.Dictionary<string, int>();
    for (int v = 0; v < offsets.Count; v++)
    {
        scroll.scrollOffset = new UnityEngine.Vector2(0, offsets[v]);
        await Wait(520);
        viewport = scroll.worldBound;
        var marks = new System.Collections.Generic.List<(int, string, UnityEngine.Rect, bool)>();
        foreach (string m0 in spec.marks)
        {
            bool redact = m0.StartsWith("!");
            string m = redact ? m0.Substring(1) : m0;
            if (!(Target(m) is UnityEngine.Rect r)) continue;
            if (r.yMin < viewport.yMin - 1 || r.yMax > viewport.yMax + 1)
            {
                // A section taller than the page: outlined as far as it shows, if its caption is in view.
                if (r.height <= 300f || r.yMin < viewport.yMin - 1 || r.yMin > viewport.yMax - 60f) continue;
                r = UnityEngine.Rect.MinMaxRect(r.xMin, UnityEngine.Mathf.Max(r.yMin, viewport.yMin), r.xMax, UnityEngine.Mathf.Min(r.yMax, viewport.yMax));
            }
            if (redact) { marks.Add((0, m, r, true)); continue; }
            if (!numbered.TryGetValue(m, out int n)) { n = ++number; numbered[m] = n; }
            marks.Add((n, m, r, false));
        }
        UnityEngine.Rect area;
        if (spec.full) area = root.panel.visualTree.layout;
        else
        {
            float bottom = sheetBox.yMax;
            float contentBottom = scroll.contentContainer.worldBound.yMax + 36f;
            if (contentBottom < bottom) bottom = UnityEngine.Mathf.Max(contentBottom, sheetBox.yMin + sheetBox.width * 0.8f);
            area = UnityEngine.Rect.MinMaxRect(sheetBox.xMin, sheetBox.yMin, sheetBox.xMax, UnityEngine.Mathf.Min(bottom, sheetBox.yMax));
        }
        await Frames(2);
        var shot = await Capture();
        Save(offsets.Count == 1 ? spec.id : spec.id + "_" + (v + 1), shot, area, marks);
        UnityEngine.Object.Destroy(shot);
    }
    foreach (string m0 in spec.marks) { string m = m0.TrimStart('!'); if (!m0.StartsWith("!") && !numbered.ContainsKey(m)) Log($"  {spec.id}: '{m}' not found"); }
}

System.Collections.Generic.List<ChatbotAI.Dialogue.ConversationLog.Exchange> SampleLog() => new System.Collections.Generic.List<ChatbotAI.Dialogue.ConversationLog.Exchange>
{
    Ex(6, "Maya", "altcore_female", "What is Altscape?", "Answered", "Altscape is an immersive sales platform for real estate."),
    Ex(5, "Ethan", "altcore_male", "What is Altscape?", "Answered", "Altscape is an immersive sales platform for real estate."),
    Ex(4, "Maya", "altcore_female", "How can I contact Altcore?", "Answered", "You can reach Altcore at contact@altcore.co."),
    Ex(4, "Maya", "altcore_female", "Hello!", "SmallTalk", "Hello, and welcome! I'm Maya."),
    Ex(3, "Pearl", "pni_female", "Which museums has P&I worked on?", "Answered", "P&I has worked on the Craft Museum in Varanasi and more."),
    Ex(3, "Maya", "altcore_female", "Do you have an office in Mumbai?", "NotInDocuments", "I can't help with that one, but ask me anything about Altscape and Altcore!"),
    Ex(2, "Iris", "chat_female", "Any good films this weekend?", "OpenChat", "Oh, I'd pick something cosy - do you like thrillers or comedies?"),
    Ex(1, "Pearl", "pni_female", "What did P&I do for G20?", "Answered", "P&I lists the G20 Leaders' Summit in its portfolio."),
    Ex(1, "Maya", "altcore_female", "What does a demo cost?", "Partial", "I don't have details on pricing, but you can book a demo."),
    Ex(0, "Maya", "altcore_female", "What is Altscape?", "Answered", "Altscape is an immersive sales platform for real estate."),
    Ex(0, "Ethan", "altcore_male", "Do you have an office in Mumbai?", "NotInDocuments", "I can't help with that one, but ask me anything about Altscape and Altcore!"),
    Ex(0, "Maya", "altcore_female", "Hello!", "SmallTalk", "Hello, and welcome! I'm Maya."),
};

async void Run()
{
    var logType = typeof(ChatbotAI.Dialogue.ConversationLog);
    bool wasLogging = ChatbotAI.Dialogue.ConversationLog.Enabled;
    bool wasKeyboard = ui.ShowKeyboard;
    bool wasDark = ui.Dark;
    var startBot = registry.Active;
    try
    {
        for (int i = 0; i < 600 && ui.Current == ChatbotAI.UI.CompanionUI.State.Starting; i++) await Wait(300);
        // Optional: shots/_light.txt = take the pictures in the app's LIGHT appearance (less ink when printed). The owner's
        // appearance is put back at the end (and kept in `wasDark` - if a run is stopped half way, check Menu > Appearance).
        if (System.IO.File.Exists(System.IO.Path.Combine(outDir, "_light.txt")))
        {
            ui.Dark = false;
            await Wait(800);
        }
        // Made-up example questions instead of this kiosk's real visitors (in memory only); real asks aren't saved either.
        logType.GetField("recent", sflags).SetValue(null, SampleLog());
        logType.GetField("dismissed", sflags).SetValue(null, new System.Collections.Generic.HashSet<string>());
        ChatbotAI.Dialogue.ConversationLog.Enabled = false;
        System.IO.File.WriteAllBytes(sampleDocument, new byte[40 * 1024]);

        UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1440, 2560, "Manual capture (portrait)");
        await Wait(2500);
        panelToPx = UnityEngine.Screen.width / root.panel.visualTree.layout.width;
        Log($"screen {UnityEngine.Screen.width}x{UnityEngine.Screen.height}, {panelToPx:0.00} px per unit" + (only != null ? "  (only: " + string.Join(", ", only) + ")" : ""));

        if (menu.IsOpen) menu.Close();
        ui.Access.SignOut();
        await UseBot("altcore_female");
        await Wait(1500);

        if (only == null)
        {
            // ---- The main screen ----
            try
            {
                await FullShot("main_ready", "#menuButton", "#dock", "#hint", "#mainButton", "#stateCaption");
                input.StartListening();
                if (await UntilState(ChatbotAI.UI.CompanionUI.State.Listening, 3000))
                {
                    await Wait(1300);
                    await FullShot("main_listening", "#mainButton", "#stateCaption");
                    input.StopListening();
                    var you = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root, "youText");
                    for (int i = 0; i < 200 && !(you.text ?? "").StartsWith("Didn't catch"); i++) await Wait(100);
                    if ((you.text ?? "").StartsWith("Didn't catch")) await FullShot("main_nocatch", "#youBlock", "#mainButton");
                    else Log("  main_nocatch: not shown (" + you.text + ")");
                }
                else Log("  main_listening: never listened");
                if (output.IsBusy) output.Stop();
                await UntilState(ChatbotAI.UI.CompanionUI.State.Ready, 20000);
                await Wait(7500);   // the "didn't catch that" text clears by itself
            }
            catch (System.Exception e) { Log("main states ERROR " + e); }

            // ---- The keyboard, the sign-in card, the loading screen ----
            try
            {
                ui.ShowKeyboard = true;
                await Wait(600);
                typeof(ChatbotAI.UI.CompanionUI).GetMethod("ToggleTyping", flags).Invoke(ui, null);
                await Wait(900);
                await FullShot("main_typing", "#keyboardButton", "#typeField", "#sendButton");
                typeof(ChatbotAI.UI.CompanionUI).GetMethod("ToggleTyping", flags).Invoke(ui, null);
                ui.ShowKeyboard = wasKeyboard;
                await Wait(500);
            }
            catch (System.Exception e) { Log("typing ERROR " + e); }
            try
            {
                typeof(ChatbotAI.UI.CompanionUI).GetMethod("ShowLogin", flags).Invoke(ui, null);
                await Wait(1000);
                await FullShot("login", "#loginUser", "#loginPassword", "#loginSubmit");
                typeof(ChatbotAI.UI.CompanionUI).GetMethod("CloseLogin", flags).Invoke(ui, null);
                await Wait(500);
            }
            catch (System.Exception e) { Log("login ERROR " + e); }
            try
            {
                var loading = UnityEngine.UIElements.UQueryExtensions.Q(root, "loading");
                loading.RemoveFromClassList("loading--gone");
                loading.RemoveFromClassList("loading--done");
                await Wait(800);
                await FullShot("loading", "#loadingPercent", "#loadingSteps");
                loading.AddToClassList("loading--done");
                loading.AddToClassList("loading--gone");
            }
            catch (System.Exception e) { Log("loading ERROR " + e); }

            // ---- Real questions ----
            try { await Conversation("maya_en", "What is Altscape?", true); } catch (System.Exception e) { Log("maya_en ERROR " + e); }
            try { await Conversation("maya_refuse", "Who won the cricket match yesterday?", false); } catch (System.Exception e) { Log("maya_refuse ERROR " + e); }
            try { await Conversation("maya_hi", "अल्टस्केप क्या है?", false); } catch (System.Exception e) { Log("maya_hi ERROR " + e); }

            // ---- Every bot ----
            foreach (string id in new[] { "altcore_male", "chat_female", "chat_male", "pni_female", "pni_male" })
            {
                try
                {
                    await UseBot(id);
                    await Wait(8000);   // the last answer clears from the screen
                    await FullShot("bot_" + id, "#hint");
                    if (id == "chat_female") await Conversation("iris_chat", "Can you recommend a good film for the weekend?", false);
                    if (id == "pni_female") await Conversation("pearl_pni", "What does PNI do?", false);
                }
                catch (System.Exception e) { Log(id + " ERROR " + e); }
            }
            await UseBot("altcore_female");
            await Wait(8000);
            await FullShot("bot_altcore_female", "#hint");
        }

        // A run limited to some pictures (shots/_only.txt) can also redo single conversation pictures by their id.
        if (only != null)
            foreach (var (id, bot, question, thinking) in new[]
            {
                ("maya_en", "altcore_female", "What is Altscape?", true),
                ("maya_refuse", "altcore_female", "Who won the cricket match yesterday?", false),
                ("maya_hi", "altcore_female", "अल्टस्केप क्या है?", false),
                ("iris_chat", "chat_female", "Can you recommend a good film for the weekend?", false),
                ("pearl_pni", "pni_female", "What does PNI do?", false),
            })
                if (System.Array.IndexOf(only, id) >= 0)
                {
                    try { await UseBot(bot); await Wait(7000); await Conversation(id, question, thinking); }
                    catch (System.Exception e) { Log(id + " ERROR " + e); }
                }

        // ---- The menu: every page (with the example log again, and conversations being saved as usual) ----
        logType.GetField("recent", sflags).SetValue(null, SampleLog());
        logType.GetField("dismissed", sflags).SetValue(null, new System.Collections.Generic.HashSet<string>());
        ChatbotAI.Dialogue.ConversationLog.Enabled = wasLogging;
        foreach (var spec in pages)
        {
            if (only != null && System.Array.IndexOf(only, spec.id) < 0) continue;
            try { await CapturePage(spec); }
            catch (System.Exception e) { Log(spec.id + " ERROR " + e); }
        }
        if (menu.IsOpen) menu.Close();
        ui.Access.SignOut();
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        try { if (startBot != null) registry.SetActive(startBot); } catch { }
        ChatbotAI.Dialogue.ConversationLog.Enabled = wasLogging;
        ChatbotAI.Dialogue.ConversationLog.Reload();
        ui.ShowKeyboard = wasKeyboard;
        ui.Dark = wasDark;
        try { System.IO.File.Delete(sampleDocument); } catch { }
        sizeIndex.SetValue(gameView, oldSize);
        Log("ALLDONE");
    }
}
Run();
return null;
