using System.Collections.Generic;
using System.Linq;
using ChatbotAI.Audio;
using ChatbotAI.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Whisper;

namespace ChatbotAI.UI
{
    /// The app's main screen (UI Toolkit: Assets/UI/Companion), drawn over the 3D scene: the avatar's name on
    /// top, and at the bottom the last exchange (YOU / the avatar) and one big button whose look follows the state:
    ///   Getting ready (spinner, progress) -> Tap to talk (mic) -> Listening (stop) -> Thinking (spinner)
    ///   -> Speaking (speaker; tap to stop).
    /// Hold Space to talk; the keyboard button opens a text field. The hamburger opens CompanionMenu.
    [RequireComponent(typeof(UIDocument))]
    public class CompanionUI : MonoBehaviour, ITunableListener
    {
        public const string ListenLanguageKey = "companion/listen-language";
        const string DarkKey = "companion/dark";
        const string DebugKey = "companion/debug";
        const string KeyboardKey = "companion/show-keyboard";

        [Tunable("Display & performance", "Text & button size", 0.6f, 1.6f, 0.05f, unit = "x", note = "Everything on screen, bigger or smaller.")]
        [SerializeField, Range(0.6f, 1.6f)] float uiScale = 1f;
        [Tunable("Display & performance", "Frame rate cap", 20f, 120f, 5f,
                 note = "Lower frees the graphics card for the AI. 60 is smooth; above 60 can make the Hindi voice break up.")]
        [SerializeField, Range(20, 120)] int targetFps = 60;

        public enum GlassMode { Auto, Full, Light, Off }
        [Tunable("Display & performance", "Frosted glass",
                 note = "The blur behind the panels is drawn at the screen's full size - on a 4K screen it kept the graphics card " +
                        "~20% busier (slower answers). Auto: full up to 1440p, lighter on bigger screens. Off: a solid panel, fastest.")]
        [SerializeField] GlassMode glass = GlassMode.Auto;
        [Tunable("Display & performance", "Show the conversation text", note = "Off: only the buttons show - the question and answer are just heard.")]
        [SerializeField] bool showText = true;
        [Tooltip("Show the keyboard button (type instead of talk) until changed in the menu. Off: visitors only talk.")]
        [SerializeField] bool showKeyboardByDefault = false;

        public enum State { Starting, Ready, Listening, Thinking, Speaking, Error }


        [Tooltip("The camera that films the avatar - it clears to the theme's background colour until the scene has a backdrop of its own.")]
        [SerializeField] Camera avatarCamera;
        [Tooltip("Tall screens (a portrait TV, a phone): how wide the screen counts as, in UI units - the UI is scaled " +
                 "to the screen's width. Bigger = smaller text and buttons. Wide screens count as 720 units high.")]
        [SerializeField, Range(400, 1400)] int tallScreenWidthUnits = 720;
        [SerializeField] Key pushToTalkKey = Key.Space;
        [Tooltip("Keeps the speaking look through the short pauses between sentences.")]
        [SerializeField] float speakingHoldSeconds = 0.6f;

        UIDocument document;
        DialogueController dialogue;
        SpeechInputController input;
        SpeechOutputController output;
        AvatarRegistry registry;
        DebugOverlay debug;
        StartupProgress progress;
        CompanionMenu menu;

        VisualElement app, micPulse, youBlock, replyBlock, replyDivider, typeRow, debugCard;
        Label hint, youText, replyWho, replyText, caption;
        Button mainButton, keyboardButton;
        TextField typeField;
        ScrollView transcript, debugScroll;
        Glyph mainGlyph;

        // Loading screen
        VisualElement loading, loadingFill, loadingSheen;
        Label loadingPercent, loadingTitle, loadingStage;

        // Keyboard and sign-in
        VisualElement chatKeyboardPanel, login, loginCard;
        TextField loginUser, loginPassword;
        Label loginError;
        Button loginKeyboardToggle;
        AdminAccess access;
        (VisualElement row, Glyph icon)[] loadingSteps;
        float readyAt = -1f;
        bool loadingGone;
        [Tooltip("Keeps the loading screen up this long after everything has loaded, then fades it out.")]
        [Tunable("Display & performance", "Clear the conversation after", 0f, 120f, 2f, unit = "s",
                 note = "When nobody has talked for this long after the bot finished speaking, the question and answer disappear and the welcome line comes back. 0 = never.")]
        [SerializeField, Range(0f, 120f)] float clearConversationAfter = 6f;
        [Tunable("Display & performance", "Loading screen stays after loading", 0f, 3f, 0.1f, unit = "s")]
        [SerializeField] float loadingHoldSeconds = 0.5f;

        State state = (State)(-1);
        float lastSpeaking = -10f, lastActivity;
        bool youPending, replyStreaming, pushToTalkHeld;
        Color cameraBackground;
        ChatbotAI.Avatar.StageBackdrop backdrop;
        PanelSettings runtimePanel;
        (Vector2Int, PanelScreenMatchMode, float) savedScaling;
        float nextDebugRefresh;
        readonly List<(DebugOverlay.LineKind kind, string text)> debugLines = new List<(DebugOverlay.LineKind, string)>();
        readonly List<Label> debugLabels = new List<Label>();

        public State Current => state;

        public bool Dark
        {
            get => app != null && app.ClassListContains("theme-dark");
            set
            {
                app.EnableInClassList("theme-dark", value);
                app.EnableInClassList("theme-light", !value);
                if (backdrop) backdrop.Dark = value;
                PlayerPrefs.SetInt(DarkKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// The keyboard button (type instead of talk) and, with it, the on-screen keyboard. Off by default.
        public bool ShowKeyboard
        {
            get => app != null && !app.ClassListContains("keyboard-off");
            set
            {
                app.EnableInClassList("keyboard-off", !value);
                if (!value && typeRow != null && typeRow.ClassListContains("type-row--open")) ToggleTyping();
                PlayerPrefs.SetInt(KeyboardKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public AdminAccess Access => access;

        public void TunableChanged(string field)
        {
            if (field == nameof(uiScale)) tallLayout = null;  // re-fit on the next frame
            if (field == nameof(targetFps)) Application.targetFrameRate = targetFps;
            if (field == nameof(showText)) app?.EnableInClassList("text-off", !showText);
            if (field == nameof(glass)) glassShown = null;
        }

        public bool DebugVisible
        {
            get => debug && debug.Visible;
            set
            {
                if (debug) debug.Visible = value;
                PlayerPrefs.SetInt(DebugKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        void Awake()
        {
            document = GetComponent<UIDocument>();
            // One screen only. A PanelRenderer (Unity 6.6's newer UI component) turned up on this object next to the
            // UIDocument and drew a second copy of the screen that no code updates: frozen at "Getting ready" in the
            // light theme over the avatar, and it broke the rounded clipping (menu groups drew solid white).
            foreach (var duplicate in GetComponents<PanelRenderer>())
            {
                Debug.LogWarning("Companion UI: removed a PanelRenderer that drew a second, frozen copy of the screen.");
                Destroy(duplicate);
            }
            // The tall-screen scaling is set on the document's own panel settings; their saved values are put back when
            // the app stops, so Play mode never changes the asset. (It used a runtime COPY before: Unity 6.6 kept the
            // original panel drawing a frozen first frame - light theme, "Getting ready", wide-screen scale - over the
            // avatar, and the two panels broke each other's rounded clipping: menu groups drew solid white.)
            runtimePanel = document.panelSettings;
            if (runtimePanel)
                savedScaling = (runtimePanel.referenceResolution, runtimePanel.screenMatchMode, runtimePanel.match);
            dialogue = FindAnyObjectByType<DialogueController>();
            input = FindAnyObjectByType<SpeechInputController>();
            output = FindAnyObjectByType<SpeechOutputController>();
            registry = FindAnyObjectByType<AvatarRegistry>();
            debug = FindAnyObjectByType<DebugOverlay>();
            // Sign-in before the menu: the scene's AdminAccess (users set in its Inspector); if it's missing, the
            // default one (admin / admin) - the menu is never left open to visitors by accident.
            access = GetComponent<AdminAccess>();
            if (!access) access = FindAnyObjectByType<AdminAccess>();
            if (!access) access = gameObject.AddComponent<AdminAccess>();
            backdrop = FindAnyObjectByType<ChatbotAI.Avatar.StageBackdrop>();
            if (!avatarCamera) avatarCamera = Camera.main;
            if (avatarCamera) cameraBackground = avatarCamera.backgroundColor;
            var speechModels = FindObjectsByType<WhisperManager>(FindObjectsInactive.Include);
            var engine = FindAnyObjectByType<TTSProcessManager>(FindObjectsInactive.Include);
            progress = new StartupProgress(dialogue, speechModels, engine);
        }

        void OnEnable()
        {
            var root = document.rootVisualElement;
            app = root.Q("app");
            // Hover highlights only for a real mouse: after a finger tap the touched button stayed lit on a touch screen.
            app.AddToClassList("pointer-mouse");
            void TrackPointer(IPointerEvent e) => app.EnableInClassList("pointer-mouse", e.pointerType == UnityEngine.UIElements.PointerType.mouse);
            root.RegisterCallback<PointerDownEvent>(e => TrackPointer(e), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(e => TrackPointer(e), TrickleDown.TrickleDown);

            hint = root.Q<Label>("hint");
            youBlock = root.Q("youBlock");
            youText = root.Q<Label>("youText");
            replyDivider = root.Q("replyDivider");
            replyBlock = root.Q("replyBlock");
            replyWho = root.Q<Label>("replyWho");
            replyText = root.Q<Label>("replyText");
            caption = root.Q<Label>("stateCaption");
            transcript = root.Q<ScrollView>("transcript");
            transcript.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            transcript.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            TouchScrollbar.AddTo(transcript);
            DragScroll.AddTo(transcript);
            typeRow = root.Q("typeRow");
            typeField = root.Q<TextField>("typeField");
            typeField.textEdition.placeholder = "Type a message";
            micPulse = root.Q("micPulse");
            debugCard = root.Q("debugCard");
            debugScroll = root.Q<ScrollView>("debugScroll");
            debugScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            debugScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            DragScroll.AddTo(debugScroll);

            loading = root.Q("loading");
            loadingPercent = root.Q<Label>("loadingPercent");
            loadingTitle = root.Q<Label>("loadingTitle");
            loadingStage = root.Q<Label>("loadingStage");
            // Anything this PC is missing (no microphone, a small graphics card...) - staff see it while it loads.
            var problems = StartupCheck.AtStart;
            if (problems.Count > 0)
            {
                var note = root.Q<Label>("loadingNote");
                note.text = "Check this PC:\n• " + string.Join("\n• ", problems);
                note.AddToClassList("loading__note--warning");
            }
            loadingFill = root.Q("loadingFill");
            loadingSheen = root.Q("loadingSheen");
            var stepsRow = root.Q("loadingSteps");
            stepsRow.Clear();
            loadingSteps = new[] { "Brain", "Listening", "Voice" }.Select(name =>
            {
                var row = new VisualElement().WithClass("loading__step");
                var icon = new Glyph(Glyph.Kind.Dot).WithClass("loading__step-icon");
                row.Add(icon);
                row.Add(new Label(name).WithClass("loading__step-label"));
                stepsRow.Add(row);
                return (row, icon);
            }).ToArray();

            var menuButton = root.Q<Button>("menuButton");
            menuButton.Add(new Glyph(Glyph.Kind.Menu));
            mainButton = root.Q<Button>("mainButton");
            mainGlyph = new Glyph(Glyph.Kind.Mic);
            mainButton.Add(mainGlyph);
            mainButton.clicked += MainButton;
            keyboardButton = root.Q<Button>("keyboardButton");
            keyboardButton.Add(new Glyph(Glyph.Kind.Keyboard));
            keyboardButton.clicked += ToggleTyping;
            var send = root.Q<Button>("sendButton");
            send.Add(new Glyph(Glyph.Kind.Send));
            send.clicked += SendTyped;
            typeField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    SendTyped();
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape) ToggleTyping();
            }, TrickleDown.TrickleDown);

            root.Q<Button>("debugStopVoice").clicked += () => { if (debug) debug.StopVoice(); };
            root.Q<Button>("debugClear").clicked += () => { if (debug) debug.ClearLog(); };

            menu = new CompanionMenu(root, this, registry, dialogue, output, input);
            menu.Closed += () => { if (access) access.MenuClosed(); };
            menuButton.clicked += MenuButton;
            SetUpKeyboardAndSignIn(root);
            // Space is push-to-talk: a focused button would also "press" on it.
            root.Query<Button>().ForEach(b => b.focusable = false);

            app.RegisterCallback<CustomStyleResolvedEvent>(MatchCameraBackground);

            Dark = PlayerPrefs.GetInt(DarkKey, 1) == 1;
            ShowKeyboard = PlayerPrefs.GetInt(KeyboardKey, showKeyboardByDefault ? 1 : 0) == 1;
            if (debug)
            {
                debug.DrawOwnPanel = false;
                debug.Visible = PlayerPrefs.GetInt(DebugKey, 0) == 1;
            }
            if (input) input.SetLanguage((SpeechInputController.InputLanguage)PlayerPrefs.GetInt(ListenLanguageKey, 0));

            if (dialogue)
            {
                dialogue.OnQuestion += ShowQuestion;
                dialogue.OnReplySentence += AddReplySentence;
                dialogue.OnReply += ShowReply;
            }
            if (input)
            {
                input.OnListeningStarted += ClearExchange;
                input.OnListeningStopped += ShowTranscribing;
                input.OnTranscribed += Ask;
                input.OnNothingHeard += NothingHeard;
                // Automatic listening waits only while the bot is idle and nobody is using the menu or the keyboard;
                // not right after it spoke either, so the end of its own voice isn't taken for a visitor.
                input.AutoAllowed = () => state == State.Ready && !menu.IsOpen && !LoginOpen &&
                                          !typeRow.ClassListContains("type-row--open") &&
                                          Time.realtimeSinceStartup - lastSpeaking > 1f;
            }
            if (registry) registry.OnActiveChanged += AvatarChanged;

            AvatarEdited();
            ClearExchange();
        }

        // The registry loads its avatars in its own Awake, which may run after this OnEnable.
        void Start()
        {
            AvatarEdited();
            Application.targetFrameRate = targetFps;
            app.EnableInClassList("text-off", !showText);
        }

        // ---------------- Keyboard & sign-in ----------------

        OnScreenKeyboard chatKeyboard, loginKeyboard;

        void SetUpKeyboardAndSignIn(VisualElement root)
        {
            chatKeyboardPanel = root.Q("chatKeyboard");
            chatKeyboard = new OnScreenKeyboard { Target = () => typeField };
            chatKeyboard.Enter += SendTyped;
            chatKeyboardPanel.Add(chatKeyboard);

            login = root.Q("login");
            loginCard = root.Q("loginCard");
            loginUser = root.Q<TextField>("loginUser");
            loginPassword = root.Q<TextField>("loginPassword");
            loginError = root.Q<Label>("loginError");
            loginUser.textEdition.placeholder = "Username";
            loginPassword.textEdition.placeholder = "Password";
            loginPassword.isPasswordField = true;
            loginPassword.maskChar = '•';
            // Tapping a field puts the caret at the end instead of selecting everything (the typed chat text showed
            // highlighted in blue).
            foreach (var field in new[] { typeField, loginUser, loginPassword })
            {
                field.textSelection.selectAllOnFocus = false;
                field.textSelection.selectAllOnMouseUp = false;
            }
            root.Q<Button>("loginCancel").clicked += CloseLogin;
            root.Q<Button>("loginSubmit").clicked += SubmitLogin;
            loginKeyboardToggle = root.Q<Button>("loginKeyboardToggle");
            loginKeyboardToggle.Add(new Glyph(Glyph.Kind.Keyboard));
            loginKeyboardToggle.clicked += () =>
            {
                bool on = !loginCard.ClassListContains("login__card--with-keyboard");
                loginCard.EnableInClassList("login__card--with-keyboard", on);
                loginKeyboardToggle.EnableInClassList("login__keyboard-toggle--on", on);
            };
            // Typed on the on-screen keyboard: into the field last tapped (username first).
            TextField loginTarget = loginUser;
            loginUser.RegisterCallback<FocusInEvent>(_ => loginTarget = loginUser);
            loginPassword.RegisterCallback<FocusInEvent>(_ => loginTarget = loginPassword);
            loginKeyboard = new OnScreenKeyboard { Target = () => loginTarget };
            loginKeyboard.Enter += () =>
            {
                if (loginTarget == loginUser) { loginTarget = loginPassword; loginPassword.Focus(); }
                else SubmitLogin();
            };
            root.Q("loginKeyboard").Add(loginKeyboard);
            foreach (var field in new[] { loginUser, loginPassword })
                field.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        if (field == loginUser) loginPassword.Focus();
                        else SubmitLogin();
                        e.StopPropagation();
                    }
                    else if (e.keyCode == KeyCode.Escape) CloseLogin();
                }, TrickleDown.TrickleDown);
            // Clicking the dimmed area outside the card closes it.
            login.RegisterCallback<ClickEvent>(e => { if (e.target == login) CloseLogin(); });
            root.Query<Button>().ForEach(b => b.focusable = false);
        }

        bool LoginOpen => login != null && login.ClassListContains("login--open");

        void MenuButton()
        {
            if (menu.IsOpen) { menu.Close(); return; }
            if (access && !access.IsSignedIn) { ShowLogin(); return; }
            OpenMenu();
        }

        void OpenMenu()
        {
            if (access) access.MenuOpened();
            menu.Open();
        }

        void ShowLogin()
        {
            loginUser.value = "";
            loginPassword.value = "";
            loginError.text = "";
            login.AddToClassList("login--open");
            loginUser.schedule.Execute(() => loginUser.Focus());
        }

        void CloseLogin()
        {
            login.RemoveFromClassList("login--open");
            loginUser.Blur();
            loginPassword.Blur();
        }

        void SubmitLogin()
        {
            if (access.LockedForSeconds > 0f)
            {
                loginError.text = $"Too many tries - wait {Mathf.CeilToInt(access.LockedForSeconds)} s.";
                Shake();
                return;
            }
            if (access.TrySignIn(loginUser.value, loginPassword.value))
            {
                CloseLogin();
                OpenMenu();
                return;
            }
            loginError.text = access.LockedForSeconds > 0f
                ? $"Too many tries - wait {Mathf.CeilToInt(access.LockedForSeconds)} s."
                : "Wrong username or password.";
            loginPassword.value = "";
            Shake();
        }

        // A small side-to-side shake of the card for a wrong password.
        void Shake()
        {
            for (int i = 0; i < 6; i++)
            {
                bool on = i % 2 == 0;
                loginCard.schedule.Execute(() => loginCard.EnableInClassList("login__card--shake", on)).StartingIn(i * 55);
            }
        }

        void OnDisable()
        {
            if (dialogue)
            {
                dialogue.OnQuestion -= ShowQuestion;
                dialogue.OnReplySentence -= AddReplySentence;
                dialogue.OnReply -= ShowReply;
            }
            if (input)
            {
                input.OnListeningStarted -= ClearExchange;
                input.OnListeningStopped -= ShowTranscribing;
                input.OnTranscribed -= Ask;
                input.OnNothingHeard -= NothingHeard;
                input.AutoAllowed = null;
            }
            if (registry) registry.OnActiveChanged -= AvatarChanged;
            if (debug) debug.DrawOwnPanel = true;

            if (avatarCamera) avatarCamera.backgroundColor = cameraBackground;
        }

        void OnDestroy()
        {
            if (runtimePanel)
                (runtimePanel.referenceResolution, runtimePanel.screenMatchMode, runtimePanel.match) = savedScaling;
        }

        // ---------------- State ----------------

        void Update()
        {
            if (progress == null) return;   // scripts recompiled during Play mode - restart Play to get the screen back
            progress.Update();
            SetState(Compute());
            HandleKeys();
            FitToScreenShape();
            ApplyGlass();

            if (state == State.Starting) caption.text = $"{progress.Stage}…  {Mathf.RoundToInt(progress.Shown * 100)}%";
            if (state == State.Ready) caption.text = ReadyCaption;   // the listening mode can change in the menu
            UpdateLoading();
            if (state == State.Listening)
            {
                // A ripple spreading out from the red button while the mic is on.
                float t = Time.realtimeSinceStartup * 0.8f % 1f;
                float s = 1f + 0.55f * t;
                micPulse.style.scale = new Scale(new Vector3(s, s, 1f));
                micPulse.style.opacity = 0.45f * (1f - t);
            }

            // The question and answer are a guide while talking - after a quiet spell the screen goes back to the
            // welcome line, so the next visitor doesn't see the last one's conversation.
            float now = Time.realtimeSinceStartup;
            if (state != State.Ready || menu.IsOpen || typeRow.ClassListContains("type-row--open")) lastActivity = now;
            if (clearConversationAfter > 0f && now - lastActivity > clearConversationAfter &&
                (youBlock.style.display == DisplayStyle.Flex || replyBlock.style.display == DisplayStyle.Flex))
                ClearExchange();

            // Transcription came back empty: nothing was heard.
            if (youPending && input && !input.IsListening && !input.IsTranscribing && !(dialogue && dialogue.IsThinking))
            {
                youPending = false;
                youText.text = "Didn't catch that - try again.";
            }

            RefreshDebug();
        }

        // The loading screen: ring + percentage, what's loading, a chip per part (dot = waiting, spinner = loading,
        // tick = ready). Once all is loaded it holds a moment, then fades to the character already in its idle motion.
        void UpdateLoading()
        {
            if (loadingGone) return;
            loadingFill.style.width = Length.Percent(progress.Shown * 100f);
            // The light sweeping along the filled part, once every 1.6 s - trimmed to the fill by hand (no clipping).
            float fillWidth = loadingFill.resolvedStyle.width;
            float sweep = Time.realtimeSinceStartup / 1.6f % 1f;
            float start = sweep * (fillWidth + 80f) - 80f;
            float from = Mathf.Clamp(start, 0f, fillWidth), to = Mathf.Clamp(start + 80f, 0f, fillWidth);
            loadingSheen.style.left = from;
            loadingSheen.style.width = to - from;
            if (progress.HasFailed)
            {
                loading.AddToClassList("loading--error");
                loadingTitle.text = "Couldn't start";
                loadingStage.text = progress.FailureMessage;
                loadingPercent.text = "";
                return;
            }
            loadingPercent.text = $"{Mathf.RoundToInt(progress.Shown * 100)}%";
            loadingStage.text = progress.IsReady ? "Ready" : progress.Stage + "…";
            // Speech recognition waits for the language model (they share the graphics card); the voice loads alongside.
            SetStep(0, progress.BrainReady, true);
            SetStep(1, progress.EarsReady, progress.BrainReady);
            SetStep(2, progress.VoiceReady, true);

            if (!progress.IsReady) return;
            float now = Time.realtimeSinceStartup;
            if (readyAt < 0f) readyAt = now;
            if (now - readyAt >= loadingHoldSeconds) loading.AddToClassList("loading--done");
            if (now - readyAt >= loadingHoldSeconds + 0.9f)
            {
                loading.AddToClassList("loading--gone");
                loadingGone = true;
            }
        }

        void SetStep(int i, bool done, bool started)
        {
            var (row, icon) = loadingSteps[i];
            row.EnableInClassList("loading__step--done", done);
            row.EnableInClassList("loading__step--active", !done && started);
            icon.Shape = done ? Glyph.Kind.Check : started ? Glyph.Kind.Spinner : Glyph.Kind.Dot;
        }

        State Compute()
        {
            if (progress.HasFailed) return State.Error;
            if (!progress.IsReady) return State.Starting;
            if (input && input.IsListening) return State.Listening;
            float now = Time.realtimeSinceStartup;
            if (output && output.IsSpeaking) lastSpeaking = now;
            if (output && output.IsBusy && now - lastSpeaking < speakingHoldSeconds) return State.Speaking;
            if ((input && input.IsTranscribing) || (dialogue && dialogue.IsThinking) || (output && output.IsBusy)) return State.Thinking;
            return State.Ready;
        }

        void SetState(State next)
        {
            if (next == state) return;
            if (state >= 0) app.RemoveFromClassList(ClassOf(state));
            state = next;
            app.AddToClassList(ClassOf(state));

            switch (state)
            {
                case State.Starting:
                    mainGlyph.Shape = Glyph.Kind.Spinner;
                    break;
                case State.Ready:
                    mainGlyph.Shape = Glyph.Kind.Mic;
                    // A touch kiosk usually has no keyboard - don't mention Space there (it still works, push-to-talk).
                    caption.text = ReadyCaption;
                    break;
                case State.Listening:
                    mainGlyph.Shape = Glyph.Kind.Mic;
                    caption.text = input && input.LastWasAutomatic ? "Listening…" : "Listening…  ·  tap to finish";
                    break;
                case State.Thinking:
                    mainGlyph.Shape = Glyph.Kind.Spinner;
                    caption.text = "Thinking…";
                    break;
                case State.Speaking:
                    mainGlyph.Shape = Glyph.Kind.Speaker;
                    caption.text = "Speaking  ·  tap to stop";
                    break;
                case State.Error:
                    mainGlyph.Shape = Glyph.Kind.Mic;
                    caption.text = progress.FailureMessage;
                    break;
            }
            mainButton.tooltip = caption.text;
        }

        string ReadyCaption => input && input.Mode == SpeechInputController.ListenMode.Automatic ? "Just start talking" : "Tap to talk";

        static string ClassOf(State s) => "state-" + s.ToString().ToLowerInvariant();

        bool? tallLayout;

        // Tall window (portrait TV, phone): its own arrangement, scaled to the screen's width - scaled to the
        // height like a wide screen, the text took up a third of a portrait TV. The stage re-frames the camera itself.
        void FitToScreenShape()
        {
            bool tall = Screen.height > Screen.width;
            if (tallLayout == tall) return;
            tallLayout = tall;
            app.EnableInClassList("layout-portrait", tall);
            if (!runtimePanel) return;
            // Text & button size: fewer reference units = everything drawn bigger.
            float scale = Mathf.Clamp(uiScale, 0.3f, 3f);
            runtimePanel.referenceResolution = tall
                ? new Vector2Int(Mathf.RoundToInt(tallScreenWidthUnits / scale), Mathf.RoundToInt(tallScreenWidthUnits / scale * 16f / 9f))
                : new Vector2Int(Mathf.RoundToInt(1280 / scale), Mathf.RoundToInt(720 / scale));
            runtimePanel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            runtimePanel.match = tall ? 0f : 1f;
        }

        GlassMode? glassShown;

        // Frosted glass: Auto = the full blur up to ~1440p (4 million pixels), the lighter one above (4K: 62% -> 49% busy).
        void ApplyGlass()
        {
            var mode = glass;
            if (mode == GlassMode.Auto) mode = (float)Screen.width * Screen.height > 4.2e6f ? GlassMode.Light : GlassMode.Full;
            if (glassShown == mode) return;
            glassShown = mode;
            app.EnableInClassList("glass-light", mode == GlassMode.Light);
            app.EnableInClassList("glass-off", mode == GlassMode.Off);
        }

        // ---------------- Actions ----------------

        void MainButton()
        {
            switch (state)
            {
                case State.Ready: StartListening(); break;
                case State.Listening: input.StopListening(); break;
                case State.Speaking: output.Stop(); break;
            }
        }

        void StartListening()
        {
            if (!input) return;
            if (output && output.IsBusy) output.Stop();
            input.StartListening();
        }

        void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame && menu.IsOpen) menu.Close();
            if (keyboard.escapeKey.wasPressedThisFrame && LoginOpen) CloseLogin();
            if (LoginOpen) return;

            // Push-to-talk, unless a text field has the keyboard.
            bool typing = document.rootVisualElement.panel?.focusController?.focusedElement is TextField ||
                          document.rootVisualElement.panel?.focusController?.focusedElement is VisualElement v && v.GetFirstAncestorOfType<TextField>() != null;
            var key = keyboard[pushToTalkKey];
            if (key.wasPressedThisFrame && !typing && !menu.IsOpen && (state == State.Ready || state == State.Speaking))
            {
                pushToTalkHeld = true;
                StartListening();
            }
            if (key.wasReleasedThisFrame && pushToTalkHeld)
            {
                pushToTalkHeld = false;
                if (input && input.IsListening) input.StopListening();
            }
        }

        void ToggleTyping()
        {
            bool open = !typeRow.ClassListContains("type-row--open");
            typeRow.EnableInClassList("type-row--open", open);
            chatKeyboardPanel.EnableInClassList("osk-panel--open", open);
            keyboardButton.EnableInClassList("round-button--on", open);
            if (open) typeField.schedule.Execute(() => typeField.Focus());
            else typeField.Blur();
        }

        void SendTyped()
        {
            string text = typeField.value?.Trim();
            if (string.IsNullOrEmpty(text) || !(state == State.Ready || state == State.Speaking)) return;
            if (output && output.IsBusy) output.Stop();
            typeField.value = "";
            Ask(text);
            typeField.schedule.Execute(() => typeField.Focus());
        }

        void Ask(string text)
        {
            if (!dialogue || string.IsNullOrWhiteSpace(text)) return;
            dialogue.Ask(text);
        }

        // ---------------- Transcript ----------------

        void ClearExchange()
        {
            youPending = replyStreaming = false;
            youText.text = replyText.text = "";
            Hide(youBlock);
            Hide(replyBlock);
            Hide(replyDivider);
            Reveal(hint);
        }

        // Nothing worth answering was heard. Automatic listening: just go back to waiting (in a busy room this happens
        // often - no message). Tap to talk: Update shows "Didn't catch that".
        void NothingHeard()
        {
            if (input && input.LastWasAutomatic) ClearExchange();
        }

        void ShowTranscribing()
        {
            Hide(hint);
            youText.text = "…";
            youText.AddToClassList("message__text--pending");
            Reveal(youBlock);
            youPending = true;
        }

        void ShowQuestion(string text)
        {
            youPending = replyStreaming = false;
            replyText.text = "";
            Hide(hint);
            Hide(replyBlock);
            Hide(replyDivider);
            youText.RemoveFromClassList("message__text--pending");
            youText.text = text;
            Reveal(youBlock);
            transcript.scrollOffset = Vector2.zero;
        }

        void AddReplySentence(string sentence, bool isLast)
        {
            if (!replyStreaming)
            {
                replyStreaming = true;
                replyText.text = "";
            }
            replyText.text = (replyText.text + " " + sentence).Trim();
            ShowReplyBlock();
        }

        void ShowReply(string text)
        {
            replyStreaming = false;
            replyText.text = text;
            ShowReplyBlock();
        }

        void ShowReplyBlock()
        {
            youPending = false;
            Hide(hint);
            if (youBlock.style.display == DisplayStyle.Flex) Reveal(replyDivider);
            Reveal(replyBlock);
        }

        static void Hide(VisualElement e) => e.style.display = DisplayStyle.None;

        // Shows a hidden element with a short fade-in (.message--enter is removed a frame later, so the
        // transition runs); an element already showing is left alone.
        static void Reveal(VisualElement e)
        {
            if (e.style.display == DisplayStyle.Flex) return;
            e.AddToClassList("message--enter");
            e.style.display = DisplayStyle.Flex;
            e.schedule.Execute(() => e.RemoveFromClassList("message--enter")).StartingIn(30);
        }

        // ---------------- Avatar ----------------

        void AvatarChanged(AvatarProfile _)
        {
            AvatarEdited();
            ClearExchange();
        }

        /// The active avatar's name, topics or mode may have changed.
        public void AvatarEdited()
        {
            var profile = registry ? registry.Active : null;
            string name = profile ? profile.displayName : "Avatar";
            replyWho.text = name.ToUpperInvariant();
            hint.text = profile && !profile.IsOpenChat
                ? $"Ask me anything about {profile.TopicsPhrase(false)}."
                : "Ask me anything - I'm all ears.";
        }

        // ---------------- Scene ----------------

        static readonly CustomStyleProperty<Color> CameraBackground = new CustomStyleProperty<Color>("--camera-background");

        // Behind the avatar: the theme's background colour (until the scene gets a stage backdrop).
        void MatchCameraBackground(CustomStyleResolvedEvent e)
        {
            if (avatarCamera && e.customStyle.TryGetValue(CameraBackground, out var color)) avatarCamera.backgroundColor = color;
        }

        // ---------------- Debug card ----------------

        void RefreshDebug()
        {
            bool show = debug && debug.Visible;
            debugCard.EnableInClassList("debug-card--open", show);
            if (!show || Time.unscaledTime < nextDebugRefresh) return;
            nextDebugRefresh = Time.unscaledTime + 0.1f;

            debug.Describe(debugLines);
            var logs = debug.LogLines().ToList();
            int needed = debugLines.Count + 1 + logs.Count;
            while (debugLabels.Count < needed)
            {
                var label = new Label().WithClass("debug-line");
                debugLabels.Add(label);
                debugScroll.Add(label);
            }
            int i = 0;
            foreach (var (kind, text) in debugLines)
                SetDebugLine(debugLabels[i++], kind == DebugOverlay.LineKind.Header ? text.ToUpperInvariant() : text,
                             kind == DebugOverlay.LineKind.Header ? "debug-line--header" : kind == DebugOverlay.LineKind.Detail ? "debug-line--detail" : null);
            SetDebugLine(debugLabels[i++], "LOG", "debug-line--header");
            foreach (string line in logs) SetDebugLine(debugLabels[i++], line, "debug-line--log");
            for (; i < debugLabels.Count; i++) debugLabels[i].style.display = DisplayStyle.None;
        }

        static readonly string[] DebugKinds = { "debug-line--header", "debug-line--detail", "debug-line--log" };

        static void SetDebugLine(Label label, string text, string kind)
        {
            label.style.display = DisplayStyle.Flex;
            if (label.text != text) label.text = text;
            foreach (string k in DebugKinds) label.EnableInClassList(k, k == kind);
        }
    }
}
