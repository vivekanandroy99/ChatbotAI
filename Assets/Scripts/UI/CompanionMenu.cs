using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChatbotAI.Audio;
using ChatbotAI.Avatar;
using ChatbotAI.Dialogue;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChatbotAI.UI
{
    /// The companion screen's settings sheet (hamburger, top right), iOS Settings style (2026-10-06 layout):
    ///   Bots          - tap one to talk to it; New bot (name, 3D look, kind -> CustomBots)
    ///   <active bot>  - Look & voice, Personality, Conversation (mode, reply length, open chat / knowledge
    ///                   settings), Documents, Camera, Backdrop, Copy/reset/delete
    ///   Visitors      - Language (listens for / replies in), Questions & answers (learning)
    ///   Devices       - microphone, speaker, screen
    ///   Display       - light / dark, keyboard, debug panel
    ///   Advanced      - AI models and the [Tunable] fine-tuning pages
    /// Bot changes are kept per bot (AvatarSettingsStore); screen settings in PlayerPrefs.
    public class CompanionMenu
    {
        readonly VisualElement sheet, scrim;
        readonly ScrollView scroll;
        readonly Label title;
        readonly Button back;
        readonly AvatarRegistry registry;
        readonly DialogueController dialogue;
        readonly SpeechOutputController output;
        readonly SpeechInputController input;
        readonly CompanionUI ui;
        readonly AvatarStage stage;
        readonly StageBackdrop backdrop;
        readonly Stack<(string title, Action build)> pages = new Stack<(string, Action)>();
        readonly OnScreenKeyboard keyboard;
        TextField typing;

        public bool IsOpen { get; private set; }

        // A touch screen, or the owner asked for the keyboard (Display > Show keyboard).
        bool WantsKeyboard => ui.ShowKeyboard || UnityEngine.InputSystem.Touchscreen.current != null;

        void ShowKeyboard(TextField field)
        {
            typing = field;
            sheet.AddToClassList("sheet--typing");
            // The list gets shorter: keep the box being typed in visible.
            scroll.schedule.Execute(() => { if (typing == field) scroll.ScrollTo(field); }).StartingIn(60);
        }

        void HideKeyboard()
        {
            var field = typing;
            typing = null;
            sheet.RemoveFromClassList("sheet--typing");
            field?.Blur();
        }

        /// The menu was closed (the sign-in then lasts AdminAccess's Stay Signed In minutes).
        public event Action Closed;

        public CompanionMenu(VisualElement root, CompanionUI ui, AvatarRegistry registry, DialogueController dialogue,
                             SpeechOutputController output, SpeechInputController input)
        {
            this.ui = ui;
            this.registry = registry;
            this.dialogue = dialogue;
            this.output = output;
            this.input = input;
            stage = UnityEngine.Object.FindAnyObjectByType<AvatarStage>();
            backdrop = UnityEngine.Object.FindAnyObjectByType<StageBackdrop>();
            sheet = root.Q("sheet");
            sheet.AddToClassList("sheet--gone");
            scrim = root.Q("scrim");
            scroll = root.Q<ScrollView>("sheetScroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            TouchScrollbar.AddTo(scroll);
            DragScroll.AddTo(scroll);
            // On-screen keyboard for the menu's text boxes (touch screens have no keyboard), docked under the list.
            keyboard = new OnScreenKeyboard { Target = () => typing, DoneKey = true }.WithClass("sheet__keyboard");
            keyboard.Enter += HideKeyboard;
            keyboard.Done += HideKeyboard;
            sheet.Add(keyboard);
            scroll.RegisterCallback<FocusInEvent>(e =>
            {
                var field = e.target as TextField ?? (e.target as VisualElement)?.GetFirstAncestorOfType<TextField>();
                if (field != null && WantsKeyboard) ShowKeyboard(field);
            }, TrickleDown.TrickleDown);
            title = root.Q<Label>("sheetTitle");
            back = root.Q<Button>("sheetBack");
            back.Add(new Glyph(Glyph.Kind.Back));
            back.clicked += Back;
            var close = root.Q<Button>("sheetClose");
            close.Add(new Glyph(Glyph.Kind.Close));
            close.clicked += Close;
            // ? - the guide for the page on screen (or the list of guides).
            help = root.Q<Button>("sheetHelp");
            if (help != null)
            {
                help.Add(new Glyph(Glyph.Kind.Help));
                help.focusable = false;
                help.clicked += OpenHelp;
            }
            scrim.RegisterCallback<ClickEvent>(_ => Close());
            if (registry)
            {
                registry.OnActiveChanged += _ => { if (IsOpen) ShowRoot(); };
                registry.OnProfilesChanged += () => { if (IsOpen && pages.Count == 1) Render(); };
            }
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            IsOpen = true;
            ShowRoot();
            // Shown first, slid in a frame later so the transition runs.
            sheet.RemoveFromClassList("sheet--gone");
            sheet.schedule.Execute(() => { if (IsOpen) sheet.AddToClassList("sheet--open"); });
            scrim.AddToClassList("scrim--open");
            scrim.pickingMode = PickingMode.Position;
        }

        public void Close()
        {
            if (IsOpen) Closed?.Invoke();
            IsOpen = false;
            importNote = null;
            HideKeyboard();
            sheet.RemoveFromClassList("sheet--open");
            scrim.RemoveFromClassList("scrim--open");
            scrim.pickingMode = PickingMode.Ignore;
            sheet.Blur();
            // Off the screen once it has slid away (0.3 s): a closed sheet still cost GPU time every frame.
            sheet.schedule.Execute(() => { if (!IsOpen) sheet.AddToClassList("sheet--gone"); }).StartingIn(350);
        }

        void ShowRoot()
        {
            pages.Clear();
            pageGuides.Clear();
            resumeAt.Clear();
            Push("Settings", BuildRoot);
        }

        void Push(string pageTitle, Action build, string guideId = null)
        {
            // Each page's guide (HelpGuides); a page without its own (a document, a person) has its parent page's.
            var guide = pageTitle == HelpPage ? null : HelpGuides.For(pageTitle, build?.Method?.Name);
            pageGuides.Push(guideId ?? guide?.id ?? (pageGuides.Count > 0 ? pageGuides.Peek() : null));
            // Where the page being left was scrolled to, so going back returns to the same place.
            if (pages.Count > 0) resumeAt.Push(scroll.scrollOffset);
            pages.Push((pageTitle, build));
            Render(Vector2.zero);   // a page opened from another starts at its top
        }

        void Back()
        {
            if (pages.Count > 1)
            {
                pages.Pop();
                pageGuides.Pop();
                Render(resumeAt.Count > 0 ? resumeAt.Pop() : Vector2.zero);
                return;
            }
            Render(null);
        }

        // Scroll positions of the pages left behind (top = the page we came from).
        readonly Stack<Vector2> resumeAt = new Stack<Vector2>();

        // restore: the scroll position to show (null = keep the current one, e.g. a switch redrawing its own page).
        void Render(Vector2? restore = null)
        {
            Vector2 keep = scroll.scrollOffset;
            HideKeyboard();
            var (pageTitle, build) = pages.Peek();
            title.text = pageTitle;
            back.EnableInClassList("sheet__header-button--hidden", pages.Count <= 1);
            sheet.EnableInClassList("sheet--root", pages.Count <= 1);   // the Altcore wordmark instead of "Settings"
            help?.EnableInClassList("sheet__header-button--hidden", pageTitle == HelpPage);
            bool cameraPage = pageTitle == CameraPage || pageTitle == StagePage || pageTitle == LightingPage;
            sheet.EnableInClassList("sheet--camera", cameraPage);
            scrim.EnableInClassList("scrim--clear", cameraPage);
            scroll.Clear();
            build();
            // Set now and again once the new content has been laid out (the scroll can't go past the content's size yet).
            Vector2 to = restore ?? keep;
            scroll.scrollOffset = to;
            foreach (long delay in new long[] { 30, 120, 300 })
                scroll.schedule.Execute(() => scroll.scrollOffset = to).StartingIn(delay);
        }

        AvatarProfile Profile => registry ? registry.Active : null;

        void Saved(params string[] fields)
        {
            if (Profile) AvatarSettingsStore.Save(Profile, fields);
        }

        // ---------------- Pages ----------------

        // The first page: which bot, then that bot's pages, then the things that are the same for every bot.
        void BuildRoot()
        {
            // Anything this PC is missing right now (checked each time the menu opens - devices get plugged in).
            var problems = StartupCheck.Problems();
            if (problems.Count > 0)
            {
                var check = Section("CHECK THIS PC");
                foreach (string p in problems)
                    Row(check, stack: true).Add(new Label(p).WithClass("row__title", "row__title--warning"));
            }

            // New here? The guides (also on every page's ? button).
            var helpRow = Section(null);
            TapRow(helpRow, "Help & guides", "New here? Short guides to every page", () => Push(HelpPage, BuildGuideList));

            // Bots: tap one to talk to it; New bot makes one from the existing 3D characters.
            var bots = Section("BOTS");
            if (registry)
            {
                foreach (var p in registry.Profiles)
                {
                    var profile = p;
                    // The look only when it isn't the bot's own name (Maya looks like Maya).
                    string look = profile.character && profile.character.displayName != profile.displayName
                        ? "Looks like " + profile.character.displayName + "  ·  " : "";
                    string what = profile.IsOpenChat ? "Open chat" : $"Knows {profile.TopicsPhrase(false)}";
                    ChoiceRow(bots, profile.displayName, look + what, profile == Profile, () => registry.SetActive(profile));
                }
                ButtonRow(bots, "New bot…", destructive: false, () => { newBot = null; Push("New bot", BuildNewBot); });
                ButtonRow(bots, "Import a bot…", destructive: false, ImportBot);
                if (importNote != null) Footnote(importNote);
            }
            var profileNow = Profile;
            if (!profileNow) return;

            // The active bot.
            var bot = Section(profileNow.displayName.ToUpperInvariant());
            NavRow(bot, "Look & voice", profileNow.character ? profileNow.character.displayName : "", () => Push("Look & voice", BuildLookVoice));
            NavRow(bot, "Personality", ToneName(profileNow.tone), () => Push("Personality", BuildPersonality));
            NavRow(bot, "Conversation", profileNow.IsOpenChat ? "Open chat" : "Knowledge only", () => Push("Conversation", BuildConversation));
            if (!profileNow.IsOpenChat)
            {
                int documents = Documents(profileNow).Count;
                NavRow(bot, "Documents", documents == 0 ? "None" : documents.ToString(), () => { documentsNote = null; Push("Documents", BuildDocuments); });
            }
            if (stage && stage.Shown)
                NavRow(bot, "Camera", stage.FramingChanged ? "Adjusted" : "Default", () => Push(CameraPage, BuildCamera));
            if (backdrop)
                NavRow(bot, "Backdrop", StyleName(backdrop.CurrentStyle), () => Push(StagePage, BuildStage));
            if (backdrop)
            {
                int look = StageBackdrop.MatchingLighting(backdrop.CurrentLighting);
                NavRow(bot, "Lighting", look >= 0 ? StageBackdrop.LightingPresets[look].name : "Custom", () => Push(LightingPage, BuildLighting));
            }
            NavRow(bot, "Copy, export, reset or delete", "", () => { exportNote = null; Push(profileNow.displayName, BuildManage); });
            if (!profileNow.IsOpenChat && Documents(profileNow).Count == 0)
                Footnote($"{profileNow.displayName} has no documents yet - it can only answer from documents. Add some in Documents.");

            // Visitors: the same for every bot.
            var visitors = Section("VISITORS");
            NavRow(visitors, "Listening & language", LanguageSummary(), () => Push("Listening & language", BuildLanguage));
            int toReview = Unanswered().Count;
            NavRow(visitors, "Questions & answers", toReview == 0 ? "" : $"{toReview} to review", () => Push(LearningPage, BuildLearning));
            int today = ConversationLog.Recent.Count(e => e.When.Date == DateTime.Now.Date);
            NavRow(visitors, "Report", today == 0 ? "" : $"{today} today", () => { exportNote = null; Push("Report", BuildReport); });

            // This computer's devices.
            var devices = Section("DEVICES");
            if (input)
                NavRow(devices, "Microphone", string.IsNullOrEmpty(input.MicrophoneDevice) ? "System default" : Shorten(input.MicrophoneDevice, 22),
                       () => Push("Microphone", BuildMicrophones));
            string speaker = AudioOutputDevice.Chosen;
            string speakerName = string.IsNullOrEmpty(speaker) ? "System default"
                : AudioOutputDevice.List().Find(d => d.id == speaker).name ?? "Not connected";
            NavRow(devices, "Speaker", Shorten(speakerName, 22), () => Push("Speaker", BuildSpeakers));
            var screens = DisplayChoice.List();
            if (screens.Count > 1 || Application.isEditor)
            {
                int here = screens.FindIndex(DisplayChoice.IsCurrent);
                NavRow(devices, "Screen", here < 0 ? "" : Shorten(DisplayChoice.NameOf(screens[here], here), 22), () => Push("Screen", BuildScreens));
            }

            // Display
            var display = Section("DISPLAY");
            SegmentRow(display, "Appearance", new[] { "Light", "Dark" }, ui.Dark ? 1 : 0, i => ui.Dark = i == 1);
            SwitchRow(display, "Show keyboard", ui.ShowKeyboard, on => ui.ShowKeyboard = on,
                      note: "Adds a keyboard button next to the microphone: it opens a text box and an on-screen keyboard.");
            SwitchRow(display, "Debug panel", ui.DebugVisible, on => ui.DebugVisible = on, note: "F1 also shows or hides it.");

            // Advanced: the AI models, and every [Tunable] Inspector setting of the scene, by page
            var tunables = Tunables.All();
            var advanced = Section("ADVANCED");
            NavRow(advanced, "AI models", "", () => Push("AI models", BuildModels));
            if (ui.Access)
                NavRow(advanced, "Staff sign-in", StaffAccounts.Any ? $"{StaffAccounts.Usernames.Count} people" : "Default",
                       () => { staffNote = null; Push("Staff sign-in", BuildStaff); });
            foreach (string page in Tunables.Pages(tunables))
            {
                string p = page;
                int changedCount = tunables.Count(t => t.info.page == p && Tunables.IsChanged(t));
                NavRow(advanced, p, changedCount > 0 ? $"{changedCount} changed" : "", () => Push(p, () => BuildTunables(p)));
            }
            Footnote("Fine-tuning for the voice, mouth, eyes, body, brain and screen - the same settings as in Unity's Inspector.");

            // Signed in - and closing the app (with the sign-out, so only staff can; a second tap confirms)
            var account = Section(null);
            if (ui.Access && ui.Access.RequiresSignIn)
                ButtonRow(account, $"Sign out{(ui.Access.SignedInAs != null ? " (" + ui.Access.SignedInAs + ")" : "")}", destructive: false, () =>
                {
                    ui.Access.SignOut();
                    Close();
                });
            VisualElement closeRow = null;
            bool armed = false;
            closeRow = ButtonRow(account, "Close the app", destructive: true, () =>
            {
                if (!armed)
                {
                    armed = true;
                    closeRow.Q<Label>().text = "Tap again to close the app";
                    // Back to normal if not confirmed within a few seconds.
                    closeRow.schedule.Execute(() => { armed = false; closeRow.Q<Label>().text = "Close the app"; }).StartingIn(4000);
                    return;
                }
                QuitApp();
            });
        }

        static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- Conversation: documents (what a Knowledge only bot answers from) ----------------

        // The bot's folder StreamingAssets/Knowledge/<bot> is read by the voice server's document search, which notices
        // added and removed files by itself. Files added here stay on this PC (in the Editor, KnowledgeSync only ever
        // removes its own copies of the Inspector's document list).
        string documentsNote;
        bool documentsForAll = true;

        static List<string> Documents(AvatarProfile p)
        {
            try
            {
                if (!Directory.Exists(p.KnowledgeFolderPath)) return new List<string>();
                return Directory.GetFiles(p.KnowledgeFolderPath)
                    .Where(f => AvatarProfile.IsSupportedDocument(f) && !Path.GetFileName(f).StartsWith(".") &&
                                !Path.GetFileName(f).StartsWith(TaughtAnswers.FilePrefix))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch { return new List<string>(); }
        }

        // Copies of the Inspector's document list (KnowledgeSync's manifest) - in the Editor they come back on the next
        // Play, so they're removed in Unity instead.
        static bool FromInspector(string path)
        {
            if (!Application.isEditor) return false;
            string manifest = Path.Combine(Path.GetDirectoryName(path), ".synced");
            return File.Exists(manifest) && File.ReadAllLines(manifest).Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        }

        // Bots that answer from the same documents (Maya and Ethan) get the same changes, so they stay alike.
        List<AvatarProfile> DocumentSharers(AvatarProfile p) =>
            registry && documentsForAll ? TaughtAnswers.LearnersWith(p, registry.Profiles) : new List<AvatarProfile> { p };

        void DocumentsChanged(IEnumerable<AvatarProfile> bots)
        {
            foreach (var bot in bots) bot.detectedTopicNames = null;   // brand names are read from the documents again
            dialogue.PersonaChanged();
        }

        void BuildDocuments()
        {
            var profile = Profile;
            if (profile == null) return;
            var sharers = registry ? TaughtAnswers.LearnersWith(profile, registry.Profiles) : new List<AvatarProfile> { profile };
            var others = sharers.Where(b => b != profile).Select(b => b.displayName).ToList();

            if (documentsNote != null)
            {
                var noteGroup = Section(null);
                Row(noteGroup, stack: true).Add(new Label(documentsNote).WithClass("row__title"));
            }

            var list = Section($"{profile.displayName.ToUpperInvariant()}'S DOCUMENTS");
            var files = Documents(profile);
            if (files.Count == 0) Row(list).Add(new Label("No documents yet.").WithClass("row__title", "row__title--muted"));
            foreach (string path in files)
            {
                string file = path;
                var info = new FileInfo(file);
                string size = info.Length < 1024 * 1024 ? $"{Math.Max(1, info.Length / 1024)} KB" : $"{info.Length / (1024f * 1024f):0.0} MB";
                TapRow(list, info.Name, $"{size}  ·  {info.LastWriteTime:d MMM yyyy}", () => Push("Document", () => BuildDocument(file)));
            }
            ButtonRow(list, "Add documents…", destructive: false, () =>
            {
                var picked = WindowsFiles.PickFiles("Add documents for " + profile.displayName,
                    ("Documents", "*.pdf;*.docx;*.pptx;*.txt;*.md"));
                int added = 0, skipped = 0;
                var bots = DocumentSharers(profile);
                foreach (string source in picked)
                {
                    if (!AvatarProfile.IsSupportedDocument(source) || Path.GetFileName(source).StartsWith(TaughtAnswers.FilePrefix)) { skipped++; continue; }
                    try
                    {
                        foreach (var bot in bots)
                        {
                            Directory.CreateDirectory(bot.KnowledgeFolderPath);
                            File.Copy(source, Path.Combine(bot.KnowledgeFolderPath, Path.GetFileName(source)), overwrite: true);
                        }
                        added++;
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        skipped++;
                        Debug.LogWarning($"Documents: couldn't add {source}: {e.Message}");
                    }
                }
                if (picked.Count == 0) return;
                if (added > 0) DocumentsChanged(bots);
                documentsNote = (added > 0 ? $"Added {added} document{(added == 1 ? "" : "s")} - {string.Join(" and ", bots.Select(b => b.displayName))} can answer from {(added == 1 ? "it" : "them")} within a minute (longer for big files)." : "")
                              + (skipped > 0 ? $" {skipped} couldn't be added - only PDF, Word, PowerPoint and text files." : "");
                documentsNote = documentsNote.Trim();
                Render();
            });
            if (others.Count > 0)
                SwitchRow(list, $"Same for {string.Join(", ", others)}", documentsForAll, on => documentsForAll = on,
                          note: "Uses the same documents - adding or removing changes both, so they keep answering alike.");
            Footnote(("PDF, Word (.docx), PowerPoint (.pptx) and text (.txt, .md) files - from this PC or a USB drive. " +
                      "For Hindi documents Word or text files work best (Hindi PDFs often read back scrambled). " +
                      (profile.IsOpenChat ? "This bot is in Open chat, which doesn't use documents - switch Mode to Knowledge only." : "")).TrimEnd());

            var folder = Section(null);
            ButtonRow(folder, "Open the documents folder", destructive: false, () =>
            {
                Directory.CreateDirectory(profile.KnowledgeFolderPath);
                Application.OpenURL("file:///" + profile.KnowledgeFolderPath.Replace('\\', '/'));
            });
        }

        void BuildDocument(string path)
        {
            var profile = Profile;
            var info = new FileInfo(path);
            var group = Section(null);
            var row = Row(group, stack: true);
            row.Add(new Label(info.Name).WithClass("row__title"));
            row.Add(new Label(info.Exists ? $"{info.Length / 1024} KB  ·  added {info.LastWriteTime:d MMM yyyy, HH:mm}" : "Already removed")
                .WithClass("row__subtitle"));
            if (!info.Exists || profile == null) return;

            if (FromInspector(path))
            {
                Footnote("This one comes from the bot's document list in Unity (Inspector) - remove it there. In the built app it can be removed here.");
                return;
            }
            var bots = DocumentSharers(profile);
            var remove = Section(null);
            VisualElement removeRow = null;
            bool armed = false;
            string label = $"Remove from {string.Join(" and ", bots.Select(b => b.displayName))}";
            removeRow = ButtonRow(remove, label, destructive: true, () =>
            {
                if (!armed)
                {
                    armed = true;
                    removeRow.Q<Label>().text = "Tap again to remove";
                    removeRow.schedule.Execute(() => { armed = false; removeRow.Q<Label>().text = label; }).StartingIn(4000);
                    return;
                }
                int failed = 0;
                foreach (var bot in bots)
                {
                    string copy = Path.Combine(bot.KnowledgeFolderPath, info.Name);
                    if (File.Exists(copy) && !WindowsFiles.Recycle(copy)) failed++;
                }
                DocumentsChanged(bots);
                documentsNote = failed == 0 ? $"Removed {info.Name} (it's in the Recycle Bin)." : $"Couldn't remove {info.Name} - is it open in another program?";
                Back();
            });
            Footnote("Goes to the Recycle Bin, so it can be restored from there.");
        }

        // ---------------- The active bot's pages ----------------

        // Look & voice: which 3D character it appears as, and how it sounds.
        void BuildLookVoice()
        {
            var profile = Profile;
            if (!profile) return;
            if (registry && registry.Characters.Count > 0)
            {
                var looks = Section("LOOK");
                foreach (var c in registry.Characters)
                {
                    var look = c;
                    var users = registry.Profiles.Where(p => p != profile && p.character == look).Select(p => p.displayName).ToList();
                    ChoiceRow(looks, look.displayName, users.Count == 0 ? null : "Also used by " + string.Join(", ", users), profile.character == look, () =>
                    {
                        if (profile.character == look) return;
                        profile.character = look;          // the stage swaps the model by itself
                        Saved(nameof(AvatarProfile.character));
                        ui.AvatarEdited();
                        Render();
                    });
                }
                Footnote("The 3D character it appears as - its body, face and movements. Pick voices that suit it below.");
            }

            var voice = Section("VOICE");
            NavRow(voice, "English voice", Pretty(profile.englishVoice), () => Push("English voice", () => BuildVoices(VoiceCatalog.Language.English)));
            NavRow(voice, "Hindi voice", Pretty(profile.hindiVoice), () => Push("Hindi voice", () => BuildVoices(VoiceCatalog.Language.Hindi)));
            SliderRow(voice, "Speaking speed", 0.7f, 1.3f, profile.speechSpeed, 0.05f, v => $"{v:0.00}x",
                v => profile.speechSpeed = v, () => Saved(nameof(AvatarProfile.speechSpeed)));
            static bool FixedPace(string v) => v.StartsWith("veena");
            if (FixedPace(profile.englishVoice) || FixedPace(profile.hindiVoice))
                Footnote("Veena voices always speak at their natural pace.");
        }

        // Conversation: knowledge only or open chat, and how it answers.
        void BuildConversation()
        {
            var profile = Profile;
            if (!profile) return;
            var mode = Section("MODE");
            SegmentRow(mode, "Answers", new[] { "Knowledge only", "Open chat" }, profile.IsOpenChat ? 1 : 0, i =>
            {
                profile.mode = i == 1 ? AvatarProfile.ConversationMode.OpenChat : AvatarProfile.ConversationMode.KnowledgeOnly;
                Saved(nameof(AvatarProfile.mode));
                dialogue.PersonaChanged();
                ui.AvatarEdited();
                Render();
            });
            SegmentRow(mode, "Reply length", new[] { "1 line", "Short", "Medium", "Detailed" }, (int)profile.replyLength, i =>
            {
                profile.replyLength = (AvatarProfile.ReplyLength)i;
                Saved(nameof(AvatarProfile.replyLength));
                dialogue.PersonaChanged();
            });
            bool hasDocuments = HasDocuments(profile);
            Footnote(profile.IsOpenChat
                ? hasDocuments
                    ? $"Open chat doesn't use its documents - it will make up answers about {profile.TopicsPhrase(false)}. Switch back to Knowledge only for real answers."
                    : "Talks about anything from what the language model knows; it can be wrong on facts."
                : "Knowledge only: answers only from its documents and politely refuses everything else.");

            if (profile.IsOpenChat)
            {
                var chat = Section("OPEN CHAT");
                SliderRow(chat, "Memory", 0, 12, profile.openChatMemory, 1, v => v < 0.5f ? "off" : $"{v:0} exchanges",
                    v => profile.openChatMemory = Mathf.RoundToInt(v), () => Saved(nameof(AvatarProfile.openChatMemory)),
                    note: "How many earlier exchanges it remembers.");
                SliderRow(chat, "Creativity", 0f, 1.2f, profile.openChatCreativity, 0.05f, v => v < 0.3f ? $"{v:0.00} · plain" : v < 0.85f ? $"{v:0.00} · balanced" : $"{v:0.00} · playful",
                    v => profile.openChatCreativity = v, () => Saved(nameof(AvatarProfile.openChatCreativity)),
                    note: "How varied its replies are.");
                SwitchRow(chat, "Asks questions back", profile.asksQuestionsBack, on =>
                {
                    profile.asksQuestionsBack = on;
                    Saved(nameof(AvatarProfile.asksQuestionsBack));
                    dialogue.PersonaChanged();
                });
                NavRow(chat, "Topics to avoid", string.IsNullOrWhiteSpace(profile.avoidTopics) ? "None" : Shorten(profile.avoidTopics, 24),
                       () => Push("Personality", BuildPersonality));
            }
            else
            {
                var knowledge = Section("KNOWLEDGE");
                int documents = Documents(profile).Count;
                NavRow(knowledge, "Documents", documents == 0 ? "None" : documents.ToString(), () => { documentsNote = null; Push("Documents", BuildDocuments); });
                SliderRow(knowledge, "Topic strictness", 0.2f, 0.8f, profile.relevanceThreshold, 0.01f, v => $"{v:0.00}",
                    v => profile.relevanceThreshold = v, () => Saved(nameof(AvatarProfile.relevanceThreshold)),
                    note: "How closely a question must match its documents before it answers. Higher refuses more.");
                SliderRow(knowledge, "Answer checking", 0.2f, 0.8f, profile.replyGroundingThreshold, 0.01f, v => $"{v:0.00}",
                    v => profile.replyGroundingThreshold = v, () => Saved(nameof(AvatarProfile.replyGroundingThreshold)),
                    note: "How closely its answer must match the documents before it's spoken. Higher blocks more made-up answers - and some good ones.");
                SliderRow(knowledge, "Passages read per question", 1, 10, profile.passagesPerQuestion, 1, v => $"{v:0}",
                    v => profile.passagesPerQuestion = Mathf.RoundToInt(v), () => Saved(nameof(AvatarProfile.passagesPerQuestion)),
                    note: "More finds answers spread over several places, but is a little slower.");
            }
        }

        // Copy, reset or delete: kept off the first page (a reset there was easy to tap by mistake). Deleting asks twice.
        void BuildManage()
        {
            var profile = Profile;
            if (!profile || !registry) return;
            bool custom = CustomBots.IsCustom(profile);

            var copy = Section("COPY");
            ButtonRow(copy, $"Make a copy of {profile.displayName}", destructive: false, () =>
            {
                var made = registry.Duplicate(profile, profile.displayName + " 2");
                registry.SetActive(made);       // the menu goes back to its first page, now on the copy
            });
            Footnote("A new bot with the same look, voices, personality, settings and documents - then change what you like.");

            ExportSection(profile);

            var reset = Section("RESET");
            bool changed = AvatarSettingsStore.HasChanges(profile);
            var resetRow = ButtonRow(reset, $"Reset {profile.displayName}'s settings", destructive: true, () =>
            {
                AvatarSettingsStore.Reset(profile, registry.OriginalOf(profile));
                dialogue.PersonaChanged();
                ui.AvatarEdited();
                Back();
            });
            resetRow.SetEnabled(changed);
            resetRow.EnableInClassList("row--disabled", !changed);
            Footnote(!changed ? $"{profile.displayName}'s settings are already the starting ones."
                : custom ? $"Puts {profile.displayName}'s voices, personality and settings back to how it was made. Its documents, the camera and Advanced pages aren't touched."
                : $"Puts {profile.displayName}'s look, voices, personality, mode and conversation settings back to the values set in Unity's Inspector. Other bots, the camera and Advanced pages aren't touched.");

            var delete = Section("DELETE");
            if (!custom)
            {
                Row(delete).Add(new Label($"{profile.displayName} is built in and can't be deleted").WithClass("row__title", "row__title--muted"));
                Footnote("Only bots made with New bot or Make a copy can be deleted.");
                return;
            }
            VisualElement deleteRow = null;
            bool armed = false;
            string label = $"Delete {profile.displayName}";
            deleteRow = ButtonRow(delete, label, destructive: true, () =>
            {
                if (!armed)
                {
                    armed = true;
                    deleteRow.Q<Label>().text = $"Tap again to delete {profile.displayName}";
                    deleteRow.schedule.Execute(() => { armed = false; deleteRow.Q<Label>().text = label; }).StartingIn(4000);
                    return;
                }
                registry.Delete(profile);
                ShowRoot();
            });
            Footnote("Its documents and taught answers go to the Recycle Bin, so they can be restored from there.");
        }

        // ---------------- Advanced: staff sign-in ----------------

        string staffNote;

        void BuildStaff()
        {
            var access = ui.Access;
            if (!access) return;
            if (staffNote != null)
                Row(Section(null), stack: true).Add(new Label(staffNote).WithClass("row__title"));

            var people = Section("WHO CAN OPEN THIS MENU");
            if (StaffAccounts.Any)
                foreach (string name in StaffAccounts.Usernames)
                {
                    string n = name;
                    TapRow(people, n, n == access.SignedInAs ? "You" : null, () => { staffNote = null; Push(n, () => BuildStaffMember(n)); });
                }
            else
                foreach (var u in access.InspectorUsers)
                    if (u != null && !string.IsNullOrWhiteSpace(u.username))
                    {
                        string n = u.username.Trim();
                        TapRow(people, n, "Password set in Unity" + (u.password == "admin" ? " - still the default \"admin\"" : ""),
                               () => { staffNote = null; Push(n, () => BuildStaffMember(n)); });
                    }
            ButtonRow(people, "Add a person…", destructive: false, () => { staffNote = null; Push("Add a person", () => BuildStaffMember(null)); });
            Footnote(StaffAccounts.Any
                ? "Each person signs in with their own name and password. Passwords are stored only as a secure fingerprint, never as text."
                : "These come from Unity's Inspector. Change a password or add someone here and this list takes over (stored securely, on this PC and in your next build).");
        }

        // Add a person (name = null), or change someone's password / remove them.
        void BuildStaffMember(string name)
        {
            var access = ui.Access;
            if (!access) return;
            TextField nameField = null;
            if (name == null)
            {
                var who = Section("NAME");
                nameField = new TextField().WithClass("text-line");
                nameField.textEdition.placeholder = "e.g. reception";
                var whoRow = Row(who, stack: true);
                whoRow.Add(nameField);
                InputTools(whoRow, nameField, speak: false);
            }
            // Passwords: the keyboard only - never spoken out loud.
            var pass = Section(name == null ? "PASSWORD" : "NEW PASSWORD");
            var first = new TextField { isPasswordField = true, maskChar = '•' }.WithClass("text-line");
            first.textEdition.placeholder = "Password";
            var firstRow = Row(pass, stack: true);
            firstRow.Add(first);
            InputTools(firstRow, first, speak: false);
            var again = new TextField { isPasswordField = true, maskChar = '•' }.WithClass("text-line");
            again.textEdition.placeholder = "The same again";
            var againRow = Row(pass, stack: true);
            againRow.Add(again);
            InputTools(againRow, again, speak: false);
            var problem = new Label("").WithClass("section__footnote");
            scroll.Add(problem);

            var save = new Button(() =>
            {
                string who = name ?? nameField.value;
                if (first.value != again.value) { problem.text = "The two passwords aren't the same."; return; }
                StaffAccounts.TakeOver(access.InspectorUsers);
                if (!StaffAccounts.Set(who, first.value, out string why)) { problem.text = why; return; }
                staffNote = name == null ? $"{who.Trim()} can now sign in." : $"{who}'s password is changed.";
                Back();
            }) { text = name == null ? "Add" : "Change password" }.WithClass("save-button");
            save.focusable = false;
            scroll.Add(save);

            if (name == null) return;
            var remove = Section(null);
            int count = StaffAccounts.Any ? StaffAccounts.Usernames.Count : access.InspectorUsers.Count;
            if (count <= 1)
            {
                Footnote("The only person who can sign in can't be removed - add someone else first.");
                return;
            }
            VisualElement removeRow = null;
            bool armed = false;
            string label = $"Remove {name}";
            removeRow = ButtonRow(remove, label, destructive: true, () =>
            {
                if (!armed)
                {
                    armed = true;
                    removeRow.Q<Label>().text = $"Tap again to remove {name}";
                    removeRow.schedule.Execute(() => { armed = false; removeRow.Q<Label>().text = label; }).StartingIn(4000);
                    return;
                }
                StaffAccounts.TakeOver(access.InspectorUsers);
                StaffAccounts.Remove(name);
                staffNote = $"{name} can no longer sign in.";
                Back();
            });
        }

        // ---------------- Export / import a bot (another kiosk, via a USB drive) ----------------

        string importNote;

        void ImportBot()
        {
            var picked = WindowsFiles.PickFiles("Import a bot", ("Exported bots", "*" + BotPackage.Extension));
            if (picked.Count == 0 || !registry) return;
            var bot = BotPackage.Import(picked[0], registry, out string problem);
            if (bot == null)
            {
                importNote = problem;
                Render();
                return;
            }
            importNote = $"Imported {bot.displayName} - it's now the active bot.";
            registry.SetActive(bot);      // back to the first page, on the new bot (the note shows until the menu closes)
        }

        void ExportSection(AvatarProfile profile)
        {
            var export = Section("TAKE IT TO ANOTHER KIOSK");
            foreach (var (name, folder) in LearningExport.Destinations())
            {
                string f = folder, n = name;
                ButtonRow(export, $"Export {profile.displayName} to {n}", destructive: false, () =>
                {
                    try { exportNote = "Saved: " + BotPackage.Export(profile, registry, f); }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { exportNote = $"Couldn't save there - {e.Message}"; }
                    Render();
                });
            }
            Footnote(exportNote ?? $"One file with {profile.displayName}'s look, voices, personality, settings, backdrop, documents and taught " +
                                   "answers. On the other kiosk: Menu > Bots > Import a bot. Plug in a USB drive to see it here.");
        }

        // ---------------- New bot ----------------

        class NewBotDraft
        {
            public string name = "";
            public AvatarCharacter look;
            public AvatarProfile.ConversationMode mode;
        }

        NewBotDraft newBot;

        // Name, look and kind - then it's made and switched to; everything else is set on its own pages.
        void BuildNewBot()
        {
            if (!registry || registry.Characters.Count == 0) return;
            newBot ??= new NewBotDraft { look = Profile && Profile.character ? Profile.character : registry.Characters[0] };
            var draft = newBot;

            var nameGroup = Section("NAME");
            var nameField = new TextField { value = draft.name }.WithClass("text-line");
            nameField.textEdition.placeholder = "e.g. Priya";
            nameField.RegisterValueChangedCallback(e => draft.name = e.newValue);
            var newNameRow = Row(nameGroup, stack: true);
            newNameRow.Add(nameField);
            InputTools(newNameRow, nameField);

            var looks = Section("LOOK");
            foreach (var c in registry.Characters)
            {
                var look = c;
                var users = registry.Profiles.Where(p => p.character == look).Select(p => p.displayName).ToList();
                ChoiceRow(looks, look.displayName, users.Count == 0 ? null : "Used by " + string.Join(", ", users), draft.look == look, () =>
                {
                    draft.look = look;
                    Render();
                });
            }
            Footnote("Its 3D character. Several bots can share one - each keeps its own name, voices and personality.");

            var kind = Section("ANSWERS");
            SegmentRow(kind, "Kind", new[] { "Knowledge only", "Open chat" }, draft.mode == AvatarProfile.ConversationMode.OpenChat ? 1 : 0,
                i => draft.mode = i == 1 ? AvatarProfile.ConversationMode.OpenChat : AvatarProfile.ConversationMode.KnowledgeOnly);
            Footnote("Knowledge only: answers from the documents you give it (next step). Open chat: talks about anything.");

            var create = new Button(() =>
            {
                string name = (draft.name ?? "").Trim();
                if (name.Length == 0) name = draft.look.displayName + " " + (registry.Profiles.Count + 1);
                var made = registry.CreateBot(name, draft.look, draft.mode);
                newBot = null;
                registry.SetActive(made);      // the menu goes back to its first page, now on the new bot
                if (!made.IsOpenChat)
                {
                    documentsNote = $"{made.displayName} is ready. Add the documents it should answer from.";
                    Push("Documents", BuildDocuments);
                }
            }) { text = "Create bot" }.WithClass("save-button");
            create.focusable = false;
            scroll.Add(create);
            Footnote("It starts with the voices and personality of the bot that uses this look - change them on its own pages.");
        }

        // ---------------- Visitors: language ----------------

        string LanguageSummary()
        {
            if (!input) return "";
            string how = input.Mode == SpeechInputController.ListenMode.Automatic ? "Automatic" : "Tap to talk";
            string listens = input.Language switch
            {
                SpeechInputController.InputLanguage.English => "English",
                SpeechInputController.InputLanguage.Hindi => "Hindi",
                _ => "",
            };
            return listens.Length == 0 ? how : $"{how} · {listens}";
        }

        void BuildLanguage()
        {
            if (input)
            {
                var how = Section("HOW VISITORS TALK");
                SegmentRow(how, "Listening", new[] { "Tap the button", "Automatic" }, (int)input.Mode, i =>
                {
                    input.Mode = (SpeechInputController.ListenMode)i;
                    Render();
                });
                Footnote(input.Mode == SpeechInputController.ListenMode.Automatic
                    ? "Automatic: whenever the bot is waiting, it listens - visitors just start talking, and a short pause ends their " +
                      "question. It doesn't listen while it speaks or while the menu is open. The button still works too. In a loud room, " +
                      "make it need a louder voice: Advanced > Listening > \"Automatic: how loud a voice must be\"."
                    : "Visitors tap the microphone, talk, and tap again (or hold Space). Best for busy, noisy places.");
            }
            var listen = Section("LANGUAGE");
            if (input)
                SegmentRow(listen, "Listens for", new[] { "Auto", "English", "Hindi" }, (int)input.Language, i =>
                {
                    input.SetLanguage((SpeechInputController.InputLanguage)i);
                    PlayerPrefs.SetInt(CompanionUI.ListenLanguageKey, i);
                    PlayerPrefs.Save();
                }, note: "Auto tells English and Hindi apart by itself. Pick one if people only speak that language - it's never confused then.");
            SegmentRow(listen, "Replies in", new[] { "Same as asked", "English", "Hindi" }, (int)dialogue.RepliesIn, i =>
                dialogue.RepliesIn = (DialogueController.ReplyLanguage)i,
                note: "Same as asked: a Hindi question gets a Hindi answer. More languages are planned.");
            Footnote("For every bot. The background noise filter and the listening fine-tuning are in Advanced > Listening.");
        }

        // ---------------- Visitors: report ----------------

        string exportNote;

        void BuildReport()
        {
            var days = ConversationReport.PerDay(30);
            int Sum(int last) => days.Skip(days.Count - last).Sum(d => d.total);
            var summary = Section("QUESTIONS");
            ValueRow(summary, "Today", Sum(1).ToString());
            ValueRow(summary, "Last 7 days", Sum(7).ToString());
            ValueRow(summary, "Last 30 days", Sum(30).ToString());
            int asked = days.Sum(d => d.answered + d.partly + d.unanswered);
            if (asked > 0) ValueRow(summary, "Answered from the documents", $"{100 * days.Sum(d => d.answered) / asked}%");

            var week = Section("LAST 7 DAYS");
            foreach (var d in days.Skip(days.Count - 7).Reverse())
                ValueRow(week, d.date == DateTime.Now.Date ? "Today" : d.date.ToString("ddd d MMM"), d.total.ToString(),
                         d.total == 0 ? null : $"{d.answered} answered  ·  {d.unanswered + d.partly} not fully  ·  {d.smallTalk} small talk  ·  {d.openChat} open chat");

            var most = Section("MOST ASKED");
            var top = ConversationReport.MostAsked(10);
            if (top.Count == 0) Row(most).Add(new Label("No questions yet.").WithClass("row__title", "row__title--muted"));
            foreach (var a in top)
                ValueRow(most, a.question, $"{a.times}×", $"{string.Join(", ", a.bots)}  ·  last {Ago(a.last)}");

            var gaps = Section("COULDN'T ANSWER");
            int toReview = Unanswered().Count;
            NavRow(gaps, "Questions to review", toReview.ToString(), () => Push(LearningPage, BuildLearning));

            var export = Section("EXPORT TO EXCEL");
            foreach (var (name, folder) in LearningExport.Destinations())
            {
                string f = folder, n = name;
                ButtonRow(export, $"Save to {n}", destructive: false, () =>
                {
                    try { exportNote = "Saved: " + ConversationReport.ExportExcel(f, TaughtFor); }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { exportNote = $"Couldn't save there - {e.Message}"; }
                    Render();
                });
            }
            Footnote(exportNote ?? "One Excel file: questions per day, per bot, the most asked, what it couldn't answer and every question " +
                                   "with its reply (last 30 days). Plug in a USB drive to see it here.");
        }

        bool TaughtFor(ConversationLog.Exchange e)
        {
            var p = ProfileById(e.avatarId);
            return p && TaughtAnswers.Knows(p, e.TeachQuestion);
        }

        void BuildVoices(VoiceCatalog.Language language)
        {
            var profile = Profile;
            if (!profile) return;
            string current = language == VoiceCatalog.Language.English ? profile.englishVoice : profile.hindiVoice;
            var voices = VoiceCatalog.For(language).Where(v => v.engine == null || VoiceEngineDownloads.IsDownloaded(v.engine)).ToArray();
            foreach (bool female in new[] { true, false })
            {
                var group = Section(female ? "FEMALE" : "MALE");
                var list = voices.Where(v => v.female == female).ToArray();
                for (int i = 0; i < list.Length; i++)
                {
                    var v = list[i];
                    ChoiceRow(group, Pretty(v.id), v.description, v.id == current, () => PickVoice(language, v));
                }
            }
            Footnote("Tap a voice to hear it. Keep both voices the same gender - Hindi replies use matching verb forms.");
        }

        void PickVoice(VoiceCatalog.Language language, VoiceCatalog.Voice v)
        {
            var profile = Profile;
            bool wasFemale = profile.HasFemaleVoice;
            if (language == VoiceCatalog.Language.English) profile.englishVoice = v.id;
            else profile.hindiVoice = v.id;
            Saved(language == VoiceCatalog.Language.English ? nameof(AvatarProfile.englishVoice) : nameof(AvatarProfile.hindiVoice));
            if (profile.HasFemaleVoice != wasFemale) dialogue.PersonaChanged();
            if (output && output.EngineReady)
            {
                output.Stop();
                output.Speak(VoiceCatalog.PreviewLines[language], v.id, profile.speechSpeed);
            }
            ui.AvatarEdited();
            Render();
        }

        void BuildPersonality()
        {
            var profile = Profile;
            if (!profile) return;
            var group = Section("NAME");
            var nameRow = Row(group, stack: true);
            var nameField = new TextField { value = profile.displayName }.WithClass("text-line");
            nameRow.Add(nameField);
            InputTools(nameRow, nameField);

            TextField topics = null;
            if (!profile.IsOpenChat)
            {
                var topicsGroup = Section("TOPICS");
                topics = new TextField { value = string.Join(", ", profile.topicNames.FindAll(n => !string.IsNullOrWhiteSpace(n))) }
                    .WithClass("text-line");
                var found = profile.detectedTopicNames;
                topics.textEdition.placeholder = found != null && found.Count > 0 ? string.Join(", ", found) : "e.g. Altscape, Altcore";
                var topicsRow = Row(topicsGroup, stack: true);
                topicsRow.Add(topics);
                InputTools(topicsRow, topics);
                Footnote("What it's about, separated by commas - its welcome line says \"Ask me anything about …\" these, and so " +
                         "does a polite no to an off-topic question. Leave empty to take them from its documents.");
            }

            var personaGroup = Section("WHO IT IS");
            var persona = TextBox(personaGroup, profile.personaPrompt, small: false);
            Footnote(profile.IsOpenChat
                ? "Who it is, its background and how it talks. It answers anything in this voice."
                : "Personality only - what it knows comes from its documents.");

            // Style: applies at once.
            var style = Section("STYLE");
            NavRow(style, "Tone", ToneName(profile.tone), () => Push("Tone", BuildTone));
            SegmentRow(style, "Formality", new[] { "Casual", "Balanced", "Formal" }, (int)profile.formality, i =>
            {
                profile.formality = (AvatarProfile.Formality)i;
                Saved(nameof(AvatarProfile.formality));
                dialogue.PersonaChanged();
            });
            SliderRow(style, "Humour", 0f, 1f, profile.humour, 0.05f, v => v < 0.15f ? "none" : v <= 0.65f ? "a little" : "plenty",
                v => profile.humour = v, () =>
                {
                    Saved(nameof(AvatarProfile.humour));
                    dialogue.PersonaChanged();
                });

            // In its own words: saved with the button below.
            var howGroup = Section("HOW IT TALKS");
            var speakingStyle = TextBox(howGroup, profile.speakingStyle, small: true,
                                        hint: "e.g. simple Indian English, says 'ji' to be polite, uses the visitor's name");
            var alwaysGroup = Section("ALWAYS");
            var alwaysDo = TextBox(alwaysGroup, profile.alwaysDo, small: true,
                                   hint: "e.g. end by offering more help, mention the free demo when it fits");
            var neverGroup = Section("NEVER");
            var neverDo = TextBox(neverGroup, profile.neverDo, small: true, hint: "e.g. talk about competitors, promise discounts");
            TextField avoid = null;
            if (profile.IsOpenChat)
            {
                var avoidGroup = Section("TOPICS TO AVOID");
                avoid = new TextField { value = profile.avoidTopics }.WithClass("text-line");
                avoid.textEdition.placeholder = "e.g. politics, religion";
                var avoidRow = Row(avoidGroup, stack: true);
                avoidRow.Add(avoid);
                InputTools(avoidRow, avoid);
                Footnote("Subjects it politely steers away from.");
            }

            var save = new Button(() =>
            {
                string newName = nameField.value.Trim();
                if (newName.Length > 0) profile.displayName = newName;
                profile.personaPrompt = persona.value;
                profile.speakingStyle = speakingStyle.value;
                profile.alwaysDo = alwaysDo.value;
                profile.neverDo = neverDo.value;
                if (avoid != null) profile.avoidTopics = avoid.value;
                if (topics != null)
                    profile.topicNames = topics.value.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
                Saved(nameof(AvatarProfile.displayName), nameof(AvatarProfile.personaPrompt), nameof(AvatarProfile.speakingStyle),
                      nameof(AvatarProfile.alwaysDo), nameof(AvatarProfile.neverDo), nameof(AvatarProfile.avoidTopics),
                      nameof(AvatarProfile.topicNames));
                dialogue.PersonaChanged();
                ui.AvatarEdited();
                Back();
            }) { text = "Save" }.WithClass("save-button");
            save.focusable = false;
            scroll.Add(save);
        }

        TextField TextBox(VisualElement group, string value, bool small, string hint = null)
        {
            var field = new TextField { multiline = true, value = value ?? "" }.WithClass("text-area");
            if (small) field.AddToClassList("text-area--small");
            if (hint != null) field.textEdition.placeholder = hint;
            field.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var row = Row(group, stack: true);
            row.Add(field);
            InputTools(row, field);
            return field;
        }

        static string ToneName(AvatarProfile.Tone t) => t switch
        {
            AvatarProfile.Tone.Professional => "Professional",
            AvatarProfile.Tone.Playful => "Playful",
            AvatarProfile.Tone.Calm => "Calm",
            AvatarProfile.Tone.Enthusiastic => "Enthusiastic",
            _ => "Warm",
        };

        void BuildTone()
        {
            var profile = Profile;
            if (!profile) return;
            var group = Section(null);
            var descriptions = new Dictionary<AvatarProfile.Tone, string>
            {
                [AvatarProfile.Tone.Warm] = "Friendly and kind",
                [AvatarProfile.Tone.Professional] = "Clear, courteous, businesslike",
                [AvatarProfile.Tone.Playful] = "Lively, a bit cheeky",
                [AvatarProfile.Tone.Calm] = "Gentle and reassuring",
                [AvatarProfile.Tone.Enthusiastic] = "Upbeat and energetic",
            };
            foreach (var kv in descriptions)
            {
                var tone = kv.Key;
                ChoiceRow(group, ToneName(tone), kv.Value, profile.tone == tone, () =>
                {
                    profile.tone = tone;
                    Saved(nameof(AvatarProfile.tone));
                    dialogue.PersonaChanged();
                    Back();
                });
            }
        }

        void BuildMicrophones()
        {
            if (!input) return;
            var group = Section(null);
            string current = input.MicrophoneDevice;
            ChoiceRow(group, "System default", "Whatever Windows uses", string.IsNullOrEmpty(current), () =>
            {
                input.MicrophoneDevice = "";
                Back();
            });
            foreach (string device in input.MicrophoneDevices)
            {
                string d = device;
                ChoiceRow(group, d, null, d == current, () =>
                {
                    input.MicrophoneDevice = d;
                    Back();
                });
            }
            Footnote("A microphone that's unplugged later falls back to the system default.");
        }

        // Which speakers the voice plays through - only this app moves (AudioOutputDevice); the PC's default stays.
        void BuildSpeakers()
        {
            var group = Section(null);
            string current = AudioOutputDevice.Chosen;
            string failed = null;
            void Pick(string id, string name)
            {
                if (AudioOutputDevice.Use(id))
                {
                    if (output) output.Stop();
                    Back();
                }
                else
                {
                    failed = name;
                    Render();
                }
            }
            ChoiceRow(group, "System default", "Whatever Windows uses", string.IsNullOrEmpty(current), () => Pick("", "System default"));
            foreach (var device in AudioOutputDevice.List())
            {
                var d = device;
                ChoiceRow(group, d.name, null, d.id == current, () => Pick(d.id, d.name));
            }
            Footnote(failed != null
                ? $"Windows didn't allow switching to {failed} - choose it in Windows: Settings > Sound > Volume mixer."
                : "Only this app's sound moves - the PC's other sounds stay where they are. A speaker that's unplugged later " +
                  "falls back to the system default. Tap a voice in Voice to test.");
        }

        // ---------------- Display: which monitor ----------------

        void BuildScreens()
        {
            var group = Section("SHOW THE APP ON");
            var screens = DisplayChoice.List();
            for (int i = 0; i < screens.Count; i++)
            {
                var d = screens[i];
                int index = i;
                ChoiceRow(group, DisplayChoice.NameOf(d, i), DisplayChoice.Describe(d), DisplayChoice.IsCurrent(d), () =>
                {
                    if (!DisplayChoice.CanMove) return;
                    DisplayChoice.Use(d, index, () => { if (IsOpen) Render(); });
                });
            }
            Footnote(DisplayChoice.CanMove
                ? "The app moves there now and fills it, and starts there next time. If that screen is unplugged, it opens where Windows puts it."
                : "In Unity's Editor the Game view can't be moved - this works in the built app.");
        }

        // ---------------- Advanced: AI models ----------------

        // Each model loads once at startup, so a pick here (ModelChoices) is used from the next start.
        void BuildModels()
        {
            var selector = UnityEngine.Object.FindAnyObjectByType<LLMModelSelector>();
            var voiceEngine = UnityEngine.Object.FindAnyObjectByType<TTSProcessManager>(FindObjectsInactive.Include);

            var brain = Section("BRAIN - LANGUAGE MODEL");
            string runningBrain = selector ? selector.RunningModelFile : "";
            string pickedBrain = Picked(ModelChoices.Brain, selector ? selector.ModelFile : "");
            foreach (var m in LLMModelLibrary.List())
            {
                string file = m.fileName;
                ChoiceRow(brain, file, $"{m.SizeText}  ·  {LLMModelLibrary.DescribedAs(file)}{(file == runningBrain ? "  ·  running now" : "")}",
                          file == pickedBrain, () => { ModelChoices.Set(ModelChoices.Brain, file); Render(); });
            }
            AddModelRow(brain, "Add a language model…", LLMModelLibrary.FolderPath);
            Footnote("Answers questions and translates. Gemma-3-4B was chosen after testing (best Hindi, fastest). " +
                     "Any GGUF chat model works (Q4_K_M, up to ~8 GB): put the .gguf file in the folder that opens, then come back here.");

            var ears = Section("EARS - SPEECH RECOGNITION");
            string runningEars = input ? input.SpeechModelFile : "";
            string pickedEars = Picked(ModelChoices.Ears, runningEars);
            foreach (var m in WhisperModelLibrary.List())
            {
                string file = m.fileName;
                ChoiceRow(ears, file, $"{m.SizeText}  ·  {WhisperModelLibrary.DescribedAs(file)}{(file == runningEars ? "  ·  running now" : "")}",
                          file == pickedEars, () => { ModelChoices.Set(ModelChoices.Ears, file); Render(); });
            }
            AddModelRow(ears, "Add a speech recognition model…", WhisperModelLibrary.FolderPath);
            Footnote("Whisper models in GGML format (ggml-*.bin, from the whisper.cpp project).");

            if (voiceEngine)
            {
                var search = Section("DOCUMENT SEARCH - ANSWER CHECKER");
                bool rerankRunning = voiceEngine.UsesReranker;
                bool rerankOn = ModelChoices.Get(ModelChoices.RerankerOff) is string r && r.Length > 0 ? r != "1" : rerankRunning;
                SwitchRow(search, rerankOn ? "On" : "Off", rerankOn, value => { ModelChoices.Set(ModelChoices.RerankerOff, value ? "0" : "1"); Render(); },
                          (rerankOn ? "Picks the passages that really answer the question" : "Off - passages in plain search order") +
                          (rerankOn != rerankRunning ? "  ·  from the next start" : ""));
                Footnote("Knowledge bots: after the search finds the closest passages, this reads the question with each one and " +
                         "hands the brain the ones that answer it best. ~0.1 s per question, ~1 GB graphics memory. It doesn't " +
                         "change which questions are refused.");
            }

            if (voiceEngine)
            {
                // One group per voice engine. Kokoro is always on (English, and it speaks for any engine that's off);
                // every other engine has its own switch, and a file choice only when it has more than one file.
                var models = VoiceModelLibrary.List();
                var kokoro = Section("VOICE - KOKORO · ENGLISH (ALWAYS ON)");
                VoiceFiles(kokoro, voiceEngine, VoiceModelLibrary.Engine.Kokoro, models, ModelChoices.Kokoro);
                Footnote("Speaks English, and stands in (same gender) for any voice engine turned off below.");

                foreach (var engine in VoiceModelLibrary.Optional)
                {
                    var files = models.Where(m => m.engine == engine).ToList();
                    if (files.Count == 0) continue;   // not downloaded
                    var group = Section(EngineCaption(engine));
                    string offKey = ModelChoices.OffKey(engine);
                    bool runningOn = voiceEngine.IsSwitchedOn(engine);
                    bool on = ModelChoices.Get(offKey) is string off && off.Length > 0 ? off != "1" : runningOn;
                    string size = $"{files.Sum(f => f.bytes) / (1024f * 1024f * 1024f):0.0} GB";
                    string note = (on ? UsedBy(engine) : "Off - its voices speak with Kokoro") + $"  ·  {size}" +
                                  (on != runningOn ? "  ·  from the next start" : "");
                    SwitchRow(group, on ? "On" : "Off", on, value => { ModelChoices.Set(offKey, value ? "0" : "1"); Render(); }, note);
                    if (on && files.Count > 1 && engine == VoiceModelLibrary.Engine.Veena)
                        VoiceFiles(group, voiceEngine, engine, files, ModelChoices.Veena);
                }
                Footnote("Turning an engine off keeps it on disk but never loads it - a faster start and more free graphics memory. " +
                         "Its voices speak with a Kokoro voice of the same gender until it's on again.");

                var add = Section(null);
                AddModelRow(add, "Add a voice model…", VoiceModelLibrary.ModelsFolder);
                Footnote("Kokoro .onnx files, and Veena .gguf files (name containing \"veena\"). Other kinds of voice model need support in the voice server first.");
            }

            var online = Section("ONLINE AI");
            NavRow(online, "Online models & API keys", "Preview", () => Push("Online AI", BuildOnlineAi));

            Footnote("Changes here are used from the next time the app starts. To add or delete model files, use Unity's " +
                     "Inspector (LLM Model Selector, Speech Input, TTS Process Manager).");
            var reset = Section(null);
            ButtonRow(reset, "Use the models set in the Inspector", destructive: true, () =>
            {
                foreach (string k in new[] { ModelChoices.Brain, ModelChoices.Ears, ModelChoices.Kokoro, ModelChoices.Veena, ModelChoices.RerankerOff })
                    ModelChoices.Clear(k);
                foreach (var engine in VoiceModelLibrary.Optional) ModelChoices.Clear(ModelChoices.OffKey(engine));
                Render();
            });
        }

        // An engine's model files: a choice (tick = the one it runs) when there are several, else just the file.
        void VoiceFiles(VisualElement group, TTSProcessManager voiceEngine, VoiceModelLibrary.Engine engine,
                        List<VoiceModelLibrary.ModelFile> models, string key)
        {
            var files = models.Where(m => m.engine == engine).ToList();
            string running = voiceEngine.SelectedModel(engine);
            string picked = Picked(key, running);
            foreach (var m in files)
            {
                string file = m.fileName;
                string subtitle = $"{VoiceModelLibrary.DescribedAs(file)}  ·  {m.SizeText}{(file == running ? "  ·  running now" : "")}";
                if (files.Count == 1) Texts(Row(group), file, subtitle);
                else ChoiceRow(group, file, subtitle, file == picked, () => { ModelChoices.Set(key, file); Render(); });
            }
        }

        static string EngineCaption(VoiceModelLibrary.Engine engine) => engine switch
        {
            VoiceModelLibrary.Engine.Veena => "VEENA · NATURAL HINDI",
            VoiceModelLibrary.Engine.KokoroV11 => "KOKORO V1.1 · THREE MORE ENGLISH VOICES",
            _ => "INDICF5 · EXPERIMENTAL HINDI",
        };

        // "Used by Maya (Hindi), Ethan (Hindi)" - which bots speak with this engine right now.
        string UsedBy(VoiceModelLibrary.Engine engine)
        {
            var users = new List<string>();
            if (registry)
                foreach (var p in registry.Profiles)
                    foreach (var (id, language) in new[] { (p.englishVoice, "English"), (p.hindiVoice, "Hindi") })
                        if (VoiceCatalog.TryGet(id, out var v) && VoiceModelLibrary.EngineOf(v) == engine)
                            users.Add($"{p.displayName} ({language})");
            return users.Count == 0 ? "No bot uses it now" : "Used by " + string.Join(", ", users);
        }

        // Models are added by putting the file in the model folder (no file picker in the app): opens it in Explorer.
        void AddModelRow(VisualElement group, string title, string folder)
        {
            ButtonRow(group, title, destructive: false, () =>
            {
                System.IO.Directory.CreateDirectory(folder);
                Application.OpenURL("file:///" + System.IO.Path.GetFullPath(folder).Replace('\\', '/'));
            });
        }

        // ---------------- Advanced: online AI (a preview - not connected yet) ----------------

        static readonly string[] BrainProviders = { "OpenAI", "Anthropic Claude", "Google Gemini" };
        static readonly string[] EarsProviders = { "OpenAI", "Deepgram", "Sarvam AI" };
        static readonly string[] VoiceProviders = { "ElevenLabs", "Sarvam AI", "OpenAI" };

        // The planned hybrid mode: each part offline or online, with its provider and API key. A design preview -
        // nothing here is saved or sent anywhere, and the app keeps running fully offline.
        void BuildOnlineAi()
        {
            var banner = Section(null);
            var note = Row(banner, stack: true);
            note.Add(new Label("Preview - not working yet").WithClass("row__title", "row__title--warning"));
            note.Add(new Label("This is how online AI will be set up. Nothing here is saved or connected; every part of the app " +
                               "still runs offline on this computer.").WithClass("row__subtitle", "row__note"));

            void Part(string caption, string what, string[] providers)
            {
                var group = Section(caption);
                SegmentRow(group, what, new[] { "Offline", "Online" }, 0, _ => { });
                SegmentRow(group, "Provider", providers, 0, _ => { });
                var keyRow = Row(group, stack: true);
                keyRow.Add(new Label("API key").WithClass("row__title"));
                var key = new TextField { isPasswordField = true, maskChar = '•' }.WithClass("text-line");
                key.textEdition.placeholder = "Paste the key from the provider's website";
                keyRow.Add(key);
            }
            Part("BRAIN", "Answers", BrainProviders);
            Footnote("Online: smarter, longer answers and better Hindi; needs internet, costs per question, and questions leave this computer.");
            Part("EARS", "Speech recognition", EarsProviders);
            Footnote("Online: better with accents and noisy rooms; the recording is sent to the provider.");
            Part("VOICE", "Speaking voice", VoiceProviders);
            Footnote("Online: very natural voices (Sarvam's Bulbul has Indian voices); reply text is sent to the provider.");
            var fallback = Section(null);
            SwitchRow(fallback, "Fall back to offline when there's no internet", true, _ => { },
                      note: "Planned: if a provider is unreachable, that part switches to its offline model for the moment.");
        }

        static string Picked(string key, string fallback)
        {
            string picked = ModelChoices.Get(key);
            return string.IsNullOrEmpty(picked) ? fallback : picked;
        }

        // ---------------- Advanced: the [Tunable] Inspector settings ----------------

        void BuildTunables(string page)
        {
            var items = Tunables.All().Where(t => t.info.page == page).ToList();
            var group = Section(null);
            foreach (var item in items) TunableRow(group, item);
            var reset = Section(null);
            bool changed = items.Any(Tunables.IsChanged);
            var row = ButtonRow(reset, "Reset this page", destructive: true, () =>
            {
                foreach (var t in items) Tunables.Reset(t);
                Render();
            });
            row.SetEnabled(changed);
            row.EnableInClassList("row--disabled", !changed);
            Footnote("Kept on this computer. Reset goes back to the values set in Unity's Inspector.");
        }

        void TunableRow(VisualElement group, Tunables.Item t)
        {
            var info = t.info;
            if (t.Type == typeof(bool))
            {
                SwitchRow(group, info.label, (bool)t.Value, on => Tunables.Set(t, on), info.note);
                return;
            }
            if (t.Type.IsEnum)
            {
                var names = Enum.GetNames(t.Type);
                SegmentRow(group, info.label, names, Array.IndexOf(Enum.GetValues(t.Type), t.Value),
                           i => Tunables.Set(t, Enum.GetValues(t.Type).GetValue(i)), info.note);
                return;
            }
            bool isInt = t.Type == typeof(int);
            float value = isInt ? (int)t.Value : (float)t.Value;
            string Format(float v) => info.unit switch
            {
                "s" => isInt || info.step >= 1f ? $"{v:0} s" : $"{v:0.00} s",
                "x" => $"{v:0.00}×",
                "%" => $"{v * 100f:0}%",
                "°" => $"{v:0.#}°",
                _ => isInt ? $"{v:0}" : $"{v:0.00}",
            };
            SliderRow(group, info.label, info.min, info.max, value, info.step, Format,
                v => Tunables.Set(t, isInt ? (object)Mathf.RoundToInt(v) : v, save: false),
                () => Tunables.Set(t, t.Value, save: true), info.note);
        }

        // ---------------- Learning (the owner teaches; the bots never learn on their own) ----------------

        const string LearningPage = "Learning";

        AvatarProfile ProfileById(string id) => registry ? registry.Profiles.FirstOrDefault(p => p.avatarId == id) : null;

        List<ConversationLog.Gap> Unanswered() => ConversationLog.Unanswered(e =>
        {
            var p = ProfileById(e.avatarId);
            return p && TaughtAnswers.Knows(p, e.TeachQuestion);
        });

        void BuildLearning()
        {
            var gaps = Unanswered();
            var group = Section("COULDN'T ANSWER");
            if (gaps.Count == 0) Row(group).Add(new Label("Nothing to review.").WithClass("row__title", "row__title--muted"));
            foreach (var gap in gaps.Take(40))
            {
                var g = gap;
                string times = g.times > 1 ? $"asked {g.times}×  ·  " : "";
                bool partly = g.latest.Kind == ConversationLog.Outcome.Partial;
                TapRow(group, g.latest.TeachQuestion,
                       $"{string.Join(", ", g.avatarNames)}  ·  {(partly ? "partly answered  ·  " : "")}{times}{Ago(g.latest.When)}",
                       () => Push("Teach an answer", () => BuildTeach(g.latest, "")));
            }
            Footnote("Questions about the bot's topics that its documents didn't answer. Tap one to teach the answer - " +
                     "the bot uses it from the next question. Off-topic questions (weather, cricket...) are refused on purpose and not listed.");

            var recentGroup = Section("RECENT ANSWERS");
            var answered = ConversationLog.Recent
                .Where(e => e.Kind == ConversationLog.Outcome.Answered || e.Kind == ConversationLog.Outcome.OpenChat)
                .Reverse().Take(15).ToList();
            if (answered.Count == 0) Row(recentGroup).Add(new Label("No answers yet.").WithClass("row__title", "row__title--muted"));
            foreach (var ex in answered)
            {
                var e = ex;
                TapRow(recentGroup, e.question, $"{e.avatarName}  ·  {Ago(e.When)}  ·  {Shorten(e.reply, 70)}",
                       () => Push("Correct an answer", () => BuildTeach(e, e.reply)));
            }
            Footnote("Tap an answer that was wrong to teach the right one.");

            var taughtGroup = Section("TAUGHT");
            int taughtCount = registry ? TaughtAnswers.All(registry.Profiles).Count : 0;
            NavRow(taughtGroup, "Taught answers", taughtCount.ToString(), () => Push("Taught answers", BuildTaughtList));

            var records = Section("RECORDS");
            SwitchRow(records, "Save conversations", ConversationLog.Enabled, on => ConversationLog.Enabled = on);
            ButtonRow(records, "Save a report", destructive: false, () =>
            {
                string path = ConversationLog.WriteReport(registry ? TaughtAnswers.All(registry.Profiles) : new List<TaughtAnswers.Entry>());
                Application.OpenURL("file:///" + path.Replace('\\', '/'));
            });
            ButtonRow(records, "Open the conversations folder", destructive: false, () =>
            {
                Directory.CreateDirectory(ConversationLog.Folder);
                Application.OpenURL("file:///" + ConversationLog.Folder.Replace('\\', '/'));
            });
            Footnote($"Kept only on this computer, in {ConversationLog.Folder}. The report lists what visitors asked, " +
                     "what the bots couldn't answer and what you've taught - easy to share with a developer or an AI assistant.");

            var export = Section("TAKE IT TO ANOTHER COMPUTER");
            Label exported = null;
            foreach (var (name, folder) in LearningExport.Destinations())
            {
                string f = folder, n = name;
                ButtonRow(export, $"Export learning to {n}", destructive: false, () =>
                {
                    try
                    {
                        string path = LearningExport.Export(f, registry ? (IEnumerable<AvatarProfile>)registry.Profiles : new List<AvatarProfile>());
                        exported.text = $"Saved: {path}";
                    }
                    catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
                    {
                        exported.text = $"Couldn't save there - {e.Message}";
                    }
                });
            }
            exported = new Label("One zip with the conversations, taught answers and the report. On the development computer, " +
                                 "open the Unity project and use Tools > ChatbotAI > Import Learning... - the next build then knows " +
                                 "everything taught here.").WithClass("section__footnote");
            scroll.Add(exported);
        }

        // Teach the answer to a question it couldn't answer (isGap), or correct one it gave.
        void BuildTeach(ConversationLog.Exchange e, string answer)
        {
            var profile = ProfileById(e.avatarId);
            if (!profile)
            {
                Footnote("That bot no longer exists.");
                return;
            }
            var learners = registry ? TaughtAnswers.LearnersWith(profile, registry.Profiles) : new List<AvatarProfile> { profile };

            var qGroup = Section("QUESTION");
            var qRow = Row(qGroup, stack: true);
            var questionField = new TextField { value = e.TeachQuestion }.WithClass("text-line");
            qRow.Add(questionField);
            InputTools(qRow, questionField);
            if (e.hindi) Footnote($"Asked in Hindi: \"{e.question}\". Hindi questions are matched in English, so keep it in English.");
            else Footnote("Edit it into the general form visitors would ask, if you like.");

            bool isGap = e.Kind == ConversationLog.Outcome.NotInDocuments || e.Kind == ConversationLog.Outcome.Partial;
            if (e.Kind != ConversationLog.Outcome.NotInDocuments)
            {
                var saidGroup = Section($"{e.avatarName.ToUpperInvariant()} SAID");
                Row(saidGroup, stack: true).Add(new Label(e.reply).WithClass("row__title", "row__title--muted"));
            }

            var aGroup = Section("THE RIGHT ANSWER");
            var aRow = Row(aGroup, stack: true);
            var answerField = new TextField { multiline = true, value = answer }.WithClass("text-area");
            answerField.textEdition.placeholder = "Type the answer the bot should give";
            answerField.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            aRow.Add(answerField);
            InputTools(aRow, answerField);
            Footnote($"Write it - or say it - the way the bot should say it, in one or two sentences. " +
                     $"{string.Join(" and ", learners.Select(p => p.displayName))} will use it from the next question.");

            var save = new Button(() =>
            {
                if (string.IsNullOrWhiteSpace(questionField.value) || string.IsNullOrWhiteSpace(answerField.value)) return;
                TaughtAnswers.Teach(profile, learners, questionField.value, answerField.value);
                Back();
            }) { text = "Teach this answer" }.WithClass("save-button");
            save.focusable = false;
            scroll.Add(save);

            if (isGap)
            {
                var skip = Section(null);
                ButtonRow(skip, "Not worth teaching - hide it", destructive: true, () =>
                {
                    ConversationLog.Dismiss(e.TeachQuestion);
                    Back();
                });
            }
        }

        void BuildTaughtList()
        {
            var all = registry ? TaughtAnswers.All(registry.Profiles) : new List<TaughtAnswers.Entry>();
            var group = Section(null);
            if (all.Count == 0) Row(group).Add(new Label("Nothing taught yet.").WithClass("row__title", "row__title--muted"));
            foreach (var entry in all)
            {
                var t = entry;
                TapRow(group, t.question, $"{string.Join(", ", t.avatarNames)}  ·  {Shorten(t.answer, 70)}",
                       () => Push("Taught answer", () => BuildTaughtEdit(t)));
            }
            Footnote("Each is a small text file in the bot's knowledge folder (\"Taught answer - ...\") - you can also edit or delete them there.");
        }

        void BuildTaughtEdit(TaughtAnswers.Entry t)
        {
            var qGroup = Section("QUESTION");
            var questionField = new TextField { value = t.question }.WithClass("text-line");
            var qRow = Row(qGroup, stack: true);
            qRow.Add(questionField);
            InputTools(qRow, questionField);
            var aGroup = Section("ANSWER");
            var answerField = new TextField { multiline = true, value = t.answer }.WithClass("text-area");
            answerField.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var aRow = Row(aGroup, stack: true);
            aRow.Add(answerField);
            InputTools(aRow, answerField);
            Footnote($"Known by {string.Join(" and ", t.avatarNames)}.");
            var save = new Button(() =>
            {
                if (string.IsNullOrWhiteSpace(questionField.value) || string.IsNullOrWhiteSpace(answerField.value)) return;
                TaughtAnswers.Rewrite(t, questionField.value, answerField.value);
                Back();
            }) { text = "Save" }.WithClass("save-button");
            save.focusable = false;
            scroll.Add(save);
            var forget = Section(null);
            ButtonRow(forget, "Forget this answer", destructive: true, () =>
            {
                TaughtAnswers.Forget(t);
                Back();
            });
        }

        static string Ago(DateTime when)
        {
            var span = DateTime.Now - when;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} h ago";
            if (span.TotalDays < 2) return "yesterday";
            return when.ToString("d MMM");
        }

        static string Shorten(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s ?? "" : s.Substring(0, max).TrimEnd() + "…";

        // A row that opens something, with a title and a grey line under it.
        void TapRow(VisualElement group, string titleText, string subtitle, Action open)
        {
            var row = Row(group).WithClass("row--tappable");
            Texts(row, titleText, subtitle);
            row.Add(new Glyph(Glyph.Kind.Chevron).WithClass("row__accessory"));
            row.RegisterCallback<ClickEvent>(_ => open());
        }

        const string StagePage = "Stage";

        static string StyleName(StageLook.Style s) => s switch
        {
            StageLook.Style.Studio => "Studio",
            StageLook.Style.Gradient => "Gradient",
            StageLook.Style.Picture => "Picture",
            _ => "Flat colour",
        };

        // The backdrop behind the active avatar: style, picture, brightness - kept per avatar (PlayerPrefs), the Stage Look
        // asset in Unity is never edited. The scene stays undimmed (like the Camera page) so each change shows at once.
        void BuildStage()
        {
            var profile = Profile;
            if (!backdrop || !profile) return;
            var look = backdrop.LookFor(profile);
            if (!look)
            {
                Footnote("No stage is set up in Unity (Stage Backdrop > Default Look), so the flat theme colour is used.");
                return;
            }
            var choice = StageBackdrop.LoadChoice(profile);
            var style = choice.style >= 0 ? (StageLook.Style)choice.style : look.style;

            void Change(Action<StageBackdrop.Choice> edit)
            {
                edit(choice);
                StageBackdrop.SaveChoice(profile, choice);
                backdrop.Refresh();
            }

            var styles = Section("BACKDROP");
            var options = new (StageLook.Style s, string text)[]
            {
                (StageLook.Style.Studio, "A photo studio: soft glow behind, light and a shadow on the floor"),
                (StageLook.Style.Gradient, "A plain colour gradient"),
                (StageLook.Style.Picture, "A picture, softly out of focus"),
                (StageLook.Style.FlatColour, "The plain theme colour, no stage"),
            };
            foreach (var (s, text) in options)
                ChoiceRow(styles, StyleName(s), text, style == s, () =>
                {
                    Change(c => c.style = (int)s);
                    Render();
                });

            if (style == StageLook.Style.Picture)
            {
                var files = StageBackdrop.PictureFiles();
                var pictures = Section("PICTURE");
                if (look.picture)
                    ChoiceRow(pictures, look.picture.name, "Set in Unity", choice.picture.Length == 0, () =>
                    {
                        Change(c => c.picture = "");
                        Render();
                    });
                foreach (string file in files)
                    ChoiceRow(pictures, Path.GetFileNameWithoutExtension(file), null,
                              choice.picture == file || (choice.picture.Length == 0 && !look.picture && file == files[0]), () =>
                    {
                        Change(c => c.picture = file);
                        Render();
                    });
                Footnote(look.picture || files.Count > 0
                    ? $"Add more: put .jpg or .png pictures in the \"{StageBackdrop.PicturesFolder}\" folder (StreamingAssets - it's in the app's Data folder after building)."
                    : $"No pictures yet - put .jpg or .png pictures in the \"{StageBackdrop.PicturesFolder}\" folder (StreamingAssets - in the app's Data folder after building). The studio shows until then.");
            }

            if (style != StageLook.Style.FlatColour)
            {
                var adjust = Section("ADJUST");
                SliderRow(adjust, "Brightness", 0.3f, 2f, choice.brightness > 0f ? choice.brightness : look.brightness, 0.05f, v => $"{v:0.00}",
                          v =>
                          {
                              choice.brightness = v;
                              StageBackdrop.SaveChoice(profile, choice);
                              backdrop.Refresh();
                          }, () => { });
            }

            if (StageBackdrop.HasChoice(profile))
            {
                var reset = Section(null);
                ButtonRow(reset, "Use the stage set in Unity", destructive: true, () =>
                {
                    StageBackdrop.ForgetChoice(profile);
                    backdrop.Refresh();
                    Render();
                });
            }
            Footnote($"For {profile.displayName} only. Colours follow Display > Appearance (light / dark); change them, the glow " +
                     "and the shadow on the Stage Look asset in Unity.");
        }

        const string CameraPage = "Camera";

        // Moves the camera live while a slider is dragged (the scene isn't dimmed on this page); saved on release,
        // per character and per screen shape - a tall portrait TV and a wide screen frame differently.
        void BuildCamera()
        {
            if (!stage || !stage.Shown) return;
            string who = stage.Shown.displayName;
            string shape = stage.IsTallScreen ? "tall" : "wide";
            var f = stage.Framing;
            // Ready-made shots: tap one to use it (saved at once), then fine-tune below if you like.
            var shots = Section($"SHOTS · {who.ToUpperInvariant()} · {shape.ToUpperInvariant()} SCREEN");
            int current = CameraFraming.MatchingShot(f, stage.IsTallScreen);
            for (int i = 0; i < CameraFraming.Shots.Length; i++)
            {
                var shot = CameraFraming.Shots[i];
                ChoiceRow(shots, shot.name, shot.description, i == current, () =>
                {
                    stage.PreviewFraming(shot.For(stage.IsTallScreen));
                    stage.SaveFraming();
                    Render();
                });
            }
            Footnote(current < 0 ? "Fine-tuned by hand - tap a shot to start again from it." : "Tap a shot, then fine-tune it below if you like.");
            var group = Section("POSITION");
            VisualElement resetRow = null;
            void Preview() => stage.PreviewFraming(f);
            void Commit()
            {
                stage.SaveFraming();
                resetRow?.SetEnabled(true);
                resetRow?.RemoveFromClassList("row--disabled");
            }
            static string Cm(float metres) => $"{Mathf.Abs(metres) * 100f:0} cm";

            SliderRow(group, "Distance", 1f, 6f, f.distance, 0.05f, v => $"{v:0.00} m",
                v => { f.distance = v; Preview(); }, Commit);
            SliderRow(group, "Height", -0.4f, 1.2f, f.aimBelowEyes, 0.01f,
                v => Mathf.Abs(v) < 0.005f ? "at the eyes" : v > 0 ? $"aims {Cm(v)} below the eyes" : $"aims {Cm(v)} above the eyes",
                v => { f.aimBelowEyes = v; Preview(); }, Commit);
            SliderRow(group, "Sideways", -0.8f, 0.8f, f.side, 0.01f,
                v => Mathf.Abs(v) < 0.005f ? "centred" : v > 0 ? $"{Cm(v)} right" : $"{Cm(v)} left",
                v => { f.side = v; Preview(); }, Commit);
            SliderRow(group, "Angle", -45f, 45f, f.turn, 1f,
                v => Mathf.Abs(v) < 0.5f ? "front" : v > 0 ? $"{v:0}° from the right" : $"{-v:0}° from the left",
                v => { f.turn = v; Preview(); }, Commit);
            SliderRow(group, "Tilt", -20f, 30f, f.tilt, 1f,
                v => Mathf.Abs(v) < 0.5f ? "level" : v > 0 ? $"{v:0}° from above" : $"{-v:0}° from below",
                v => { f.tilt = v; Preview(); }, Commit);

            var lens = Section("LENS & MOVEMENT");
            float ownFov = UnityEngine.Camera.main ? UnityEngine.Camera.main.fieldOfView : 30f;
            SliderRow(lens, "Lens", 10f, 60f, f.fieldOfView > 1f ? f.fieldOfView : ownFov, 1f, v => $"{v:0}°",
                v => { f.fieldOfView = v; Preview(); }, Commit,
                note: "Smaller = telephoto: flatter, more flattering faces (move further back to fit). Larger = wide angle.");
            SwitchRow(lens, "Follow the character", f.follow, on => { f.follow = on; Preview(); Commit(); },
                      note: "The camera gently re-centres as the character moves, instead of staying fixed.");
            Footnote($"Saved for {who} on {shape} screens - wide and tall screens are set separately.");

            var reset = Section(null);
            resetRow = ButtonRow(reset, "Reset camera", destructive: true, () =>
            {
                stage.ResetFraming();
                Render();
            });
            bool changed = stage.FramingChanged;
            resetRow.SetEnabled(changed);
            resetRow.EnableInClassList("row--disabled", !changed);
        }

        const string LightingPage = "Lighting";

        // The lights on the active bot: a ready-made look, then the main light (brightness, direction, height, warmth) and the
        // others. Changes show at once (the scene isn't dimmed on this page) and are kept per bot on release.
        void BuildLighting()
        {
            var profile = Profile;
            if (!backdrop || !profile) return;
            var l = backdrop.CurrentLighting.Copy();
            var looks = Section($"LOOKS · {profile.displayName.ToUpperInvariant()}");
            int current = StageBackdrop.MatchingLighting(l);
            for (int i = 0; i < StageBackdrop.LightingPresets.Length; i++)
            {
                var preset = StageBackdrop.LightingPresets[i];
                ChoiceRow(looks, preset.name, preset.description, i == current, () =>
                {
                    var values = preset.values.Copy();
                    backdrop.PreviewLighting(values);
                    StageBackdrop.SaveLighting(profile, values);
                    Render();
                });
            }
            Footnote(current < 0 ? "Fine-tuned by hand - tap a look to start again from it." : "Tap a look, then fine-tune it below if you like.");

            void Preview() => backdrop.PreviewLighting(l);
            // Saved on release; the page is drawn again (a moment later) so the ticked look follows.
            void Commit()
            {
                StageBackdrop.SaveLighting(profile, l);
                scroll.schedule.Execute(() => { if (IsOpen && pages.Count > 0 && pages.Peek().title == LightingPage) Render(); });
            }
            static string Percent(float v) => $"{v * 100f:0}%";

            var main = Section("MAIN LIGHT");
            SliderRow(main, "Brightness", 0.2f, 2f, l.key, 0.05f, v => Percent(v),
                v => { l.key = v; Preview(); }, Commit);
            SliderRow(main, "Direction", -90f, 90f, l.turn, 1f,
                v => Mathf.Abs(v) < 0.5f ? "from the front" : v > 0 ? $"{v:0}° from the right" : $"{-v:0}° from the left",
                v => { l.turn = v; Preview(); }, Commit);
            SliderRow(main, "Height", -30f, 40f, l.height, 1f,
                v => Mathf.Abs(v) < 0.5f ? "as set up" : v > 0 ? $"{v:0}° higher" : $"{-v:0}° lower",
                v => { l.height = v; Preview(); }, Commit);
            SliderRow(main, "Warmth", -1f, 1f, l.warmth, 0.05f,
                v => Mathf.Abs(v) < 0.03f ? "neutral" : v > 0 ? $"warmer {v * 100f:0}%" : $"cooler {-v * 100f:0}%",
                v => { l.warmth = v; Preview(); }, Commit);

            var others = Section("OTHER LIGHTS");
            SliderRow(others, "Soft light", 0f, 2f, l.soft, 0.05f, v => Percent(v),
                v => { l.soft = v; Preview(); }, Commit,
                note: "Fills the shadow side of the face. More = even and gentle, less = dramatic.");
            SliderRow(others, "Back light", 0f, 2f, l.back, 0.05f, v => Percent(v),
                v => { l.back = v; Preview(); }, Commit,
                note: "A rim of light round the edges, from behind. It never goes above the main light.");
            SliderRow(others, "Studio glow", 0f, 2f, l.studio, 0.05f, v => Percent(v),
                v => { l.studio = v; Preview(); }, Commit,
                note: "The studio's soft light from all round and its shine in eyes and hair.");
            SliderRow(others, "Shadows", 0f, 1.5f, l.shadows, 0.05f, v => Percent(v),
                v => { l.shadows = v; Preview(); }, Commit,
                note: "How dark the shadows are. 100% = as set up.");
            Footnote($"Saved for {profile.displayName}. The brightness of what's behind the bot is in Backdrop.");

            var reset = Section(null);
            var resetRow = ButtonRow(reset, "Reset lighting", destructive: true, () =>
            {
                StageBackdrop.ForgetLighting(profile);
                backdrop.PreviewLighting(new StageBackdrop.Lighting());
                Render();
            });
            bool changed = StageBackdrop.HasLighting(profile);
            resetRow.SetEnabled(changed);
            resetRow.EnableInClassList("row--disabled", !changed);
        }

        // ---------------- Building blocks ----------------

        VisualElement Section(string caption)
        {
            if (caption != null) scroll.Add(new Label(caption).WithClass("section__caption"));
            else scroll.Add(new VisualElement().WithClass("section__caption"));
            var group = new VisualElement().WithClass("group");
            scroll.Add(group);
            return group;
        }

        void Footnote(string text) => scroll.Add(new Label(text).WithClass("section__footnote"));

        static VisualElement Row(VisualElement group, bool stack = false)
        {
            // The previous row keeps its separator; mark this one last until another follows.
            if (group.childCount > 0) group[group.childCount - 1].RemoveFromClassList("row--last");
            var row = new VisualElement().WithClass("row", "row--last");
            if (group.childCount == 0) row.AddToClassList("row--first");
            if (stack) row.AddToClassList("row--stack");
            group.Add(row);
            return row;
        }

        static VisualElement Texts(VisualElement row, string titleText, string subtitle)
        {
            var texts = new VisualElement().WithClass("row__texts");
            texts.pickingMode = PickingMode.Ignore;
            texts.Add(new Label(titleText) { pickingMode = PickingMode.Ignore }.WithClass("row__title"));
            if (!string.IsNullOrEmpty(subtitle)) texts.Add(new Label(subtitle) { pickingMode = PickingMode.Ignore }.WithClass("row__subtitle"));
            row.Add(texts);
            return texts;
        }

        void ChoiceRow(VisualElement group, string titleText, string subtitle, bool selected, Action pick)
        {
            var row = Row(group).WithClass("row--tappable");
            Texts(row, titleText, subtitle);
            var check = new Glyph(selected ? Glyph.Kind.Check : Glyph.Kind.None).WithClass("row__accessory", "row__accessory--check");
            row.Add(check);
            row.RegisterCallback<ClickEvent>(_ => pick());
        }

        // ---------------- Typing on a touch screen: Speak / Keyboard under a text box ----------------

        TextField dictatingInto;

        // Finger-sized buttons under a text box (inside its row): Speak - say it and the words are written in (stops after
        // a 2 s pause, or tap again; replaces the text the page opened with - e.g. the wrong answer being corrected - and
        // adds to anything typed or spoken since); Keyboard - opens the on-screen keyboard for this box (on a touch screen
        // it also opens by itself when the box is tapped). Password boxes get the keyboard only.
        void InputTools(VisualElement row, TextField field, bool speak = true)
        {
            var bar = new VisualElement().WithClass("input-tools");
            row.Add(bar);
            // Tapping into the box puts the cursor there - it must not select everything (the first key would wipe it).
            field.textSelection.selectAllOnFocus = false;
            field.textSelection.selectAllOnMouseUp = false;

            if (speak && input)
            {
                var (mic, micIcon, micLabel) = Pill(bar, Glyph.Kind.Mic, "Speak");
                string opened = field.value;
                string message = null;
                mic.RegisterCallback<ClickEvent>(_ =>
                {
                    if (input.IsDictating)
                    {
                        if (dictatingInto == field && input.IsListening) input.StopListening();
                        return;
                    }
                    message = null;
                    if (typing != null) HideKeyboard();
                    dictatingInto = field;
                    bool started = input.Dictate(text =>
                    {
                        dictatingInto = null;
                        if (text.Length == 0) { message = "Didn't catch that"; return; }
                        field.value = field.value == opened || string.IsNullOrWhiteSpace(field.value) ? text : field.value.TrimEnd() + " " + text;
                    });
                    if (!started)
                    {
                        dictatingInto = null;
                        message = "Microphone busy";
                    }
                });
                // In step with the microphone: Speak -> Listening (red) -> Writing it down -> Speak.
                mic.schedule.Execute(() =>
                {
                    bool mine = dictatingInto == field && input.IsDictating;
                    bool live = mine && input.IsListening;
                    mic.EnableInClassList("tool-pill--live", live);
                    micIcon.Shape = live ? Glyph.Kind.Stop : mine ? Glyph.Kind.Spinner : Glyph.Kind.Mic;
                    micLabel.text = live ? "Stop" : mine ? "Writing…" : message ?? "Speak";
                }).Every(120);
            }

            var (kb, _, kbLabel) = Pill(bar, Glyph.Kind.Keyboard, "Keyboard");
            kb.RegisterCallback<ClickEvent>(_ =>
            {
                if (typing == field) HideKeyboard();
                else
                {
                    field.Focus();
                    // Type after what's there (the cursor at the end, nothing selected).
                    field.schedule.Execute(() => field.SelectRange(field.value.Length, field.value.Length));
                    ShowKeyboard(field);
                }
            });
            kb.schedule.Execute(() =>
            {
                kb.EnableInClassList("tool-pill--on", typing == field);
                kbLabel.text = typing == field ? "Hide keyboard" : "Keyboard";
            }).Every(150);
        }

        // A pill button: icon + label. Not focusable, so tapping it leaves the text box's cursor where it is.
        static (VisualElement pill, Glyph icon, Label label) Pill(VisualElement parent, Glyph.Kind kind, string text)
        {
            var pill = new VisualElement { focusable = false }.WithClass("tool-pill");
            var icon = new Glyph(kind) { pickingMode = PickingMode.Ignore }.WithClass("tool-pill__icon");
            var label = new Label(text) { pickingMode = PickingMode.Ignore }.WithClass("tool-pill__label");
            pill.Add(icon);
            pill.Add(label);
            parent.Add(pill);
            return (pill, icon, label);
        }

        // A title (and subtitle) with a value on the right - nothing to tap.
        void ValueRow(VisualElement group, string titleText, string value, string subtitle = null)
        {
            var row = Row(group);
            Texts(row, titleText, subtitle);
            row.Add(new Label(value) { pickingMode = PickingMode.Ignore }.WithClass("row__value"));
        }

        void NavRow(VisualElement group, string titleText, string value, Action open)
        {
            var row = Row(group).WithClass("row--tappable");
            Texts(row, titleText, null);
            row.Add(new Label(value) { pickingMode = PickingMode.Ignore }.WithClass("row__value"));
            row.Add(new Glyph(Glyph.Kind.Chevron).WithClass("row__accessory"));
            row.RegisterCallback<ClickEvent>(_ => open());
        }

        void SliderRow(VisualElement group, string titleText, float min, float max, float value, float step,
                       Func<float, string> format, Action<float> apply, Action commit, string note = null)
        {
            var row = Row(group, stack: true);
            var head = new VisualElement().WithClass("row__head");
            head.Add(new Label(titleText).WithClass("row__title", "row__texts"));
            var shown = new Label(format(value)).WithClass("row__value");
            head.Add(shown);
            row.Add(head);
            if (!string.IsNullOrEmpty(note)) row.Add(new Label(note).WithClass("row__subtitle", "row__note"));
            var slider = new ValueSlider(min, max, value, step);
            slider.Changed += v =>
            {
                apply(v);
                shown.text = format(v);
            };
            slider.Committed += _ => commit();
            row.Add(slider);
        }

        void SegmentRow(VisualElement group, string titleText, string[] options, int selected, Action<int> changed, string note = null)
        {
            var row = Row(group, stack: true);
            row.Add(new Label(titleText).WithClass("row__title"));
            if (!string.IsNullOrEmpty(note)) row.Add(new Label(note).WithClass("row__subtitle", "row__note"));
            var seg = new Segmented(options, selected);
            seg.Changed += changed;
            row.Add(seg);
        }

        void SwitchRow(VisualElement group, string titleText, bool on, Action<bool> changed, string note = null)
        {
            var row = Row(group);
            Texts(row, titleText, note);
            var sw = new Switch(on);
            sw.Changed += changed;
            row.Add(sw);
        }

        VisualElement ButtonRow(VisualElement group, string titleText, bool destructive, Action click)
        {
            var row = Row(group).WithClass("row--tappable");
            row.AddToClassList(destructive ? "row--destructive" : "row--action");
            row.Add(new Label(titleText) { pickingMode = PickingMode.Ignore }.WithClass("row__title"));
            row.RegisterCallback<ClickEvent>(_ => click());
            return row;
        }

        // ---------------- Help: a short guide per page (HelpGuides), opened with ? or from Help & guides ----------------

        const string HelpPage = "Help";
        readonly Stack<string> pageGuides = new Stack<string>();
        readonly Button help;

        void OpenHelp()
        {
            var guide = pageGuides.Count > 0 && pageGuides.Peek() != null ? HelpGuides.Find(pageGuides.Peek()) : null;
            if (guide != null) Push(HelpPage, () => BuildGuide(guide));
            else Push(HelpPage, BuildGuideList);
        }

        static readonly (string caption, string[] ids)[] GuideSections =
        {
            ("GETTING STARTED", new[] { "kiosk", "menu", "bots" }),
            ("THE BOT ON SCREEN", new[] { "newbot", "look", "personality", "conversation", "documents", "camera", "backdrop", "lighting", "manage" }),
            ("VISITORS", new[] { "listening", "learning", "report" }),
            ("THIS PC", new[] { "microphone", "speaker", "screen", "display" }),
            ("ADVANCED", new[] { "models", "staff", "tuning" }),
        };

        void BuildGuideList()
        {
            Footnote("Short guides to every page of this menu. Tap ? at the top of any page for its own guide.");
            foreach (var (caption, ids) in GuideSections)
            {
                var group = Section(caption);
                foreach (string id in ids)
                {
                    var guide = HelpGuides.Find(id);
                    if (guide != null) TapRow(group, guide.title, guide.intro, () => Push(HelpPage, () => BuildGuide(guide)));
                }
            }
        }

        void BuildGuide(HelpGuide guide)
        {
            var top = new VisualElement().WithClass("guide__top");
            var all = new Button(() => Push(HelpPage, BuildGuideList)).WithClass("guide__all");
            all.focusable = false;
            all.Add(new Glyph(Glyph.Kind.Back).WithClass("guide__all-icon"));
            all.Add(new Label("All guides"));
            top.Add(all);
            scroll.Add(top);
            scroll.Add(new Label(guide.title).WithClass("guide__title"));
            scroll.Add(new Label(guide.intro).WithClass("guide__intro"));

            // The page as it looks, with what the steps point at outlined and numbered.
            var shot = HelpGuides.Shot(guide);
            if (shot)
            {
                var frame = new VisualElement().WithClass("guide__shot");
                frame.style.backgroundImage = new StyleBackground(shot);
                float ratio = (float)shot.height / shot.width;
                frame.RegisterCallback<GeometryChangedEvent>(e =>
                {
                    float h = e.newRect.width * ratio;
                    if (Mathf.Abs(frame.resolvedStyle.height - h) > 0.5f) frame.style.height = h;
                });
                foreach (var m in HelpGuides.MarksOf(guide))
                {
                    var box = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("guide__mark");
                    box.style.left = Length.Percent(m.x * 100f);
                    box.style.top = Length.Percent(m.y * 100f);
                    box.style.width = Length.Percent(m.w * 100f);
                    box.style.height = Length.Percent(m.h * 100f);
                    frame.Add(box);
                    var badge = new Label((m.step + 1).ToString()) { pickingMode = PickingMode.Ignore }.WithClass("guide__badge", "brand-fill");
                    badge.style.left = Length.Percent(m.x * 100f);
                    badge.style.top = Length.Percent(m.y * 100f);
                    frame.Add(badge);
                }
                scroll.Add(frame);
                scroll.Add(new Label(guide.page == null ? "What visitors see" : "What this page looks like").WithClass("guide__caption"));
            }

            for (int i = 0; i < guide.steps.Length; i++)
            {
                var step = new VisualElement().WithClass("guide__step");
                step.Add(new Label((i + 1).ToString()).WithClass("guide__number", "brand-fill"));
                step.Add(new Label(guide.steps[i].text).WithClass("guide__text"));
                scroll.Add(step);
            }

            if (guide.tips.Length > 0)
            {
                var tips = new VisualElement().WithClass("guide__tips");
                tips.Add(new Label("Good to know").WithClass("guide__tips-title"));
                foreach (string tip in guide.tips) tips.Add(new Label("•  " + tip).WithClass("guide__tip"));
                scroll.Add(tips);
            }

            // From the list: straight to the page it's about (its own page is one Back away when opened with ?).
            bool fromItsPage = pages.Count > 1 && pageGuides.Count > 1 && pageGuides.ElementAt(1) == guide.id;
            var method = guide.page != null && guide.page != nameof(BuildRoot)
                ? GetType().GetMethod(guide.page, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) : null;
            if (method != null && !fromItsPage && (guide.id != "documents" || (Profile && !Profile.IsOpenChat)))
            {
                var go = Section(null);
                // The page's own title (so its ? opens this guide again): the first it's opened from.
                string pageTitle = guide.pageArgs.Length > 0 ? guide.pageArgs[0].ToString()
                    : guide.id == "manage" && Profile ? Profile.displayName
                    : guide.openedFrom.FirstOrDefault(t => !t.StartsWith("Build")) ?? guide.title;
                ButtonRow(go, "Go to this page", destructive: false, () => Push(pageTitle, () => method.Invoke(this, guide.pageArgs), guide.id));
            }
        }

        // ---------------- Helpers ----------------

        /// "af_heart" -> "Heart", "veena_kavya" -> "Kavya (Veena)", "indicf5_hin_male2" -> "Hin Male2 (IndicF5)".
        static string Pretty(string id)
        {
            if (string.IsNullOrEmpty(id)) return "-";
            string[] parts = id.Split('_');
            string engine = parts[0] switch
            {
                "veena" => "Veena",
                "indicf5" => "IndicF5",
                _ => null,
            };
            var words = parts.Skip(1).Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w.Substring(1));
            string name = string.Join(" ", words);
            return engine != null ? $"{name} ({engine})" : name;
        }

        static bool HasDocuments(AvatarProfile profile)
        {
            try
            {
                return Directory.Exists(profile.KnowledgeFolderPath) &&
                       Directory.EnumerateFiles(profile.KnowledgeFolderPath).Any(AvatarProfile.IsSupportedDocument);
            }
            catch { return false; }
        }
    }
}
