// Play mode: screenshots for the menu's guides (HelpGuides) -> Assets/Resources/Help/<id>.png + <id>-marks.json.
// Renders at the kiosk's portrait 4K (2160x3840), opens each guide's menu page (signed in as admin/admin), scrolls to
// the first option its steps point at, and records where each pointed-at option is (0-1 of the picture). The kiosk
// guide shows the main screen with the menu closed. Afterwards (edit mode): tools/import_help_shots.cs.
// Re-run after the menu changes. Progress in <scratchpad or project>/help_capture.txt.
string outDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets/Resources/Help");
string logPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Temp/help_capture.txt");
System.IO.Directory.CreateDirectory(outDir);
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var ui = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.UI.CompanionUI>();
var menu = (ChatbotAI.UI.CompanionMenu)typeof(ChatbotAI.UI.CompanionUI).GetField("menu", flags).GetValue(ui);
var mt = typeof(ChatbotAI.UI.CompanionMenu);
var root = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>().rootVisualElement;
var sheet = UnityEngine.UIElements.UQueryExtensions.Q(root, "sheet");
var scroll = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.ScrollView>(root, "sheetScroll");
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(logPath, log.ToString()); }

// The Game view's size, put back at the end.
var gameViewType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
var gameView = UnityEditor.EditorWindow.GetWindow(gameViewType, false, null, false);
var sizeIndex = gameViewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
int oldSize = (int)sizeIndex.GetValue(gameView);

bool Matches(string text, string mark) =>
    text != null && (mark.StartsWith("starts:") ? text.StartsWith(mark.Substring(7))
                   : mark.StartsWith("contains:") ? text.Contains(mark.Substring(9))
                   : text.Trim() == mark);

// What a mark outlines: a row (its whole row), a caption (with the group under it), a button, or a named element.
UnityEngine.Rect? Target(string mark)
{
    if (mark == null) return null;
    if (mark.StartsWith("#"))
    {
        var named = UnityEngine.UIElements.UQueryExtensions.Q(root, mark.Substring(1));
        return named != null && named.resolvedStyle.display != UnityEngine.UIElements.DisplayStyle.None ? named.worldBound : (UnityEngine.Rect?)null;
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

// A screenshot needs the end of a frame (else it comes back empty): a coroutine on the UI object takes it.
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

void Save(ChatbotAI.UI.HelpGuide g, UnityEngine.Texture2D shot, UnityEngine.Rect area, System.Collections.Generic.List<(int step, UnityEngine.Rect r)> marks, float panelToPx)
{
    // Panel units -> pixels (the texture's rows run bottom-up), cropped to `area`, a multiple of 4 for compression.
    int x0 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(area.xMin * panelToPx), 0, shot.width - 4);
    int w = UnityEngine.Mathf.Min(shot.width - x0, UnityEngine.Mathf.RoundToInt(area.width * panelToPx)) / 4 * 4;
    int yTop = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(area.yMin * panelToPx), 0, shot.height - 4);
    int h = UnityEngine.Mathf.Min(shot.height - yTop, UnityEngine.Mathf.RoundToInt(area.height * panelToPx)) / 4 * 4;
    var crop = new UnityEngine.Texture2D(w, h, UnityEngine.TextureFormat.RGB24, false);
    crop.SetPixels(shot.GetPixels(x0, shot.height - yTop - h, w, h));
    crop.Apply();
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, g.id + ".png"), crop.EncodeToPNG());
    UnityEngine.Object.Destroy(crop);
    var data = new ChatbotAI.UI.HelpGuides.Marks();
    foreach (var (step, r) in marks)
        data.marks.Add(new ChatbotAI.UI.HelpGuides.Mark
        {
            step = step, x = (r.xMin - area.xMin) / area.width, y = (r.yMin - area.yMin) / area.height,
            w = r.width / area.width, h = r.height / area.height,
        });
    System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, g.id + "-marks.json"), UnityEngine.JsonUtility.ToJson(data, true));
    Log($"{g.id}: {w}x{h}, {data.marks.Count}/{g.steps.Length} steps marked");
}

async void Run()
{
    try
    {
        for (int i = 0; i < 400 && ui.Current == ChatbotAI.UI.CompanionUI.State.Starting; i++) await System.Threading.Tasks.Task.Delay(300);
        // Made-up example questions instead of this kiosk's real visitors (in memory only - nothing is written; the
        // real log is read again at the end), so the pictures shipped in the app show no real conversations.
        var logType = typeof(ChatbotAI.Dialogue.ConversationLog);
        ChatbotAI.Dialogue.ConversationLog.Exchange Ex(int daysAgo, string name, string id, string q, string outcome, string reply) =>
            new ChatbotAI.Dialogue.ConversationLog.Exchange { time = System.DateTime.Now.AddDays(-daysAgo).AddMinutes(-daysAgo * 37 - 20).ToString("s"),
                avatarId = id, avatarName = name, question = q, english = q, outcome = outcome, reply = reply, score = 0.6f };
        var samples = new System.Collections.Generic.List<ChatbotAI.Dialogue.ConversationLog.Exchange>
        {
            Ex(3, "Maya", "altcore_female", "What is Altscape?", "Answered", "Altscape is an immersive sales platform for real estate."),
            Ex(2, "Maya", "altcore_female", "How can I contact Altcore?", "Answered", "You can reach Altcore at contact@altcore.co."),
            Ex(2, "Maya", "altcore_female", "Hello!", "SmallTalk", "Hello, and welcome! I'm Maya."),
            Ex(1, "Pearl", "pni_female", "Which museums has P&I worked on?", "Answered", "P&I has worked on the Craft Museum in Varanasi and more."),
            Ex(1, "Maya", "altcore_female", "Do you have an office in Mumbai?", "NotInDocuments", "I can't help with that one, but ask me anything about Altscape and Altcore!"),
            Ex(0, "Pearl", "pni_female", "What did P&I do for G20?", "Answered", "P&I lists the G20 Leaders' Summit in its portfolio."),
            Ex(0, "Maya", "altcore_female", "What does a demo cost?", "Partial", "I don't have details on pricing, but you can book a demo."),
        };
        logType.GetField("recent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, samples);
        logType.GetField("dismissed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, new System.Collections.Generic.HashSet<string>());
        UnityEditor.PlayModeWindow.SetCustomRenderingResolution(2160, 3840, "Help capture (portrait 4K)");
        await System.Threading.Tasks.Task.Delay(1500);
        float panelToPx = UnityEngine.Screen.width / root.panel.visualTree.layout.width;
        Log($"screen {UnityEngine.Screen.width}x{UnityEngine.Screen.height}, {panelToPx:0.00} px per unit");

        foreach (var g in ChatbotAI.UI.HelpGuides.All)
        {
            if (g.page == null)
            {
                // The main screen: menu closed.
                if (menu.IsOpen) menu.Close();
                await System.Threading.Tasks.Task.Delay(800);
                var marksMain = new System.Collections.Generic.List<(int, UnityEngine.Rect)>();
                for (int s = 0; s < g.steps.Length; s++) if (Target(g.steps[s].mark) is UnityEngine.Rect r) marksMain.Add((s, r));
                await Frames(2);
                var full = await Capture();
                Save(g, full, root.panel.visualTree.layout, marksMain, panelToPx);
                UnityEngine.Object.Destroy(full);
                continue;
            }
            if (!menu.IsOpen)
            {
                ui.Access.TrySignIn("admin", "admin");
                typeof(ChatbotAI.UI.CompanionUI).GetMethod("OpenMenu", flags).Invoke(ui, null);
                await System.Threading.Tasks.Task.Delay(800);
            }
            mt.GetMethod("ShowRoot", flags).Invoke(menu, null);
            if (g.page != "BuildRoot")
            {
                var build = mt.GetMethod(g.page, flags);
                string title = g.pageArgs.Length > 0 ? g.pageArgs[0].ToString() : g.title;
                mt.GetMethod("Push", flags).Invoke(menu, new object[] { title, (System.Action)(() => build.Invoke(menu, g.pageArgs)), g.id });
            }
            await System.Threading.Tasks.Task.Delay(700);
            // Scroll so the first option a step points at sits near the top.
            UnityEngine.Rect? first = null;
            foreach (var st in g.steps) if (Target(st.mark) is UnityEngine.Rect fr) { first = fr; break; }
            if (first.HasValue)
            {
                float inContent = first.Value.yMin - scroll.contentContainer.worldBound.yMin;
                scroll.scrollOffset = new UnityEngine.Vector2(0, UnityEngine.Mathf.Max(0, inContent - 24));
                await System.Threading.Tasks.Task.Delay(400);
            }
            var view = scroll.worldBound;
            var sheetBox = sheet.worldBound;
            var marks = new System.Collections.Generic.List<(int, UnityEngine.Rect)>();
            float top = float.MaxValue, bottom = float.MinValue;
            for (int s = 0; s < g.steps.Length; s++)
            {
                if (!(Target(g.steps[s].mark) is UnityEngine.Rect r)) { if (g.steps[s].mark != null) Log($"  {g.id} step {s + 1}: '{g.steps[s].mark}' not found"); continue; }
                // Only what's on screen, within twice the sheet's width from the first.
                if (r.yMin < view.yMin - 1 || r.yMax > view.yMax + 1 || (top < float.MaxValue && r.yMax - top > sheetBox.width * 2.4f))
                { Log($"  {g.id} step {s + 1}: '{g.steps[s].mark}' out of the picture"); continue; }
                marks.Add((s, r));
                top = UnityEngine.Mathf.Min(top, r.yMin);
                bottom = UnityEngine.Mathf.Max(bottom, r.yMax);
            }
            // The picture: the sheet's width, from just above the first mark (or the sheet's top) to just below the last.
            float y0 = marks.Count == 0 ? sheetBox.yMin : (top - sheetBox.yMin < 140 ? sheetBox.yMin : top - 36);
            float y1 = marks.Count == 0 ? sheetBox.yMin + sheetBox.width * 1.2f : bottom + 36;
            if (y1 - y0 < sheetBox.width * 0.6f) y1 = y0 + sheetBox.width * 0.6f;
            y1 = UnityEngine.Mathf.Min(y1, sheetBox.yMax);
            await Frames(2);
            var shot = await Capture();
            Save(g, shot, UnityEngine.Rect.MinMaxRect(sheetBox.xMin, y0, sheetBox.xMax, y1), marks, panelToPx);
            UnityEngine.Object.Destroy(shot);
        }
        menu.Close();
        ui.Access.SignOut();
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        ChatbotAI.Dialogue.ConversationLog.Reload();
        sizeIndex.SetValue(gameView, oldSize);
        Log("ALLDONE");
    }
}
Run();
return null;
