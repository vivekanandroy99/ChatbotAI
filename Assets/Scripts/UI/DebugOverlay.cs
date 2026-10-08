using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using ChatbotAI.Audio;
using ChatbotAI.Avatar;
using ChatbotAI.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChatbotAI.UI
{
    /// In-app debug panel (F1, the companion screen's menu, or - on its own - the "Debug" button bottom-right):
    /// what the avatar is doing and why. The companion screen draws Describe()'s lines in its own card.
    ///   Avatar     - which one, its mode, character, voices, lip-sync profile
    ///   Now        - mic / brain / voice state, body animation, frame rate
    ///   Last turn  - what was heard (and its language), the reply, and timings: question -> reply
    ///                ready -> first sound -> done, plus mic stop -> transcript
    ///   Lip-sync   - sound heard, loudness, jaw opening, live
    ///   Log        - the brain's own lines as they happen (router decision, knowledge-gate scores,
    ///                open-chat replies, speech language, lip-sync profile, warnings and errors)
    /// Everything is read from the existing components and their events; nothing else changes when it's shown.
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] bool visible;
        [SerializeField] Key toggleKey = Key.F1;
        [Tooltip("Log lines starting with these are shown (warnings and errors always are).")]
        [SerializeField] string[] logPrefixes =
        {
            "Router", "Knowledge gate", "Open chat", "Speech language", "Lip-sync profile", "Pronunciation",
            "Answer before translation", "Topic names", "SpeechOutputController", "AvatarStage",
        };
        [SerializeField] int logLines = 12;

        DialogueController dialogue;
        SpeechInputController input;
        SpeechOutputController output;
        AvatarStage stage;
        AvatarAnimator body;
        AvatarLipSync lips;
        uLipSync.uLipSync hearing;

        readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        readonly List<string> log = new List<string>();
        string heard = "-", heardLanguage = "", asked = "-", reply = "-";
        float askedAt = -1, replyAt = -1, soundAt = -1, doneAt = -1, micStopAt = -1, transcribedAt = -1;
        float fps;
        GUIStyle box, label, small, header;

        void OnEnable() => Application.logMessageReceivedThreaded += Capture;
        void OnDisable() => Application.logMessageReceivedThreaded -= Capture;

        void Start()
        {
            dialogue = FindAnyObjectByType<DialogueController>();
            input = FindAnyObjectByType<SpeechInputController>();
            output = FindAnyObjectByType<SpeechOutputController>();
            stage = FindAnyObjectByType<AvatarStage>();
            if (stage)
            {
                body = stage.GetComponent<AvatarAnimator>();
                lips = stage.GetComponent<AvatarLipSync>();
            }
            if (output) hearing = output.GetComponentInChildren<uLipSync.uLipSync>();

            if (dialogue)
            {
                dialogue.OnQuestion += q => { asked = q; askedAt = Now; replyAt = soundAt = doneAt = -1; reply = "..."; };
                dialogue.OnReplySentence += (s, _) => { if (replyAt < 0) replyAt = Now; };
                dialogue.OnReply += r => { reply = r; if (replyAt < 0) replyAt = Now; };
            }
            if (output)
            {
                output.OnSpeechStarted += () => { if (soundAt < 0) soundAt = Now; };
                output.OnSpeechFinished += () => doneAt = Now;
                output.OnFailed += e => Add($"VOICE FAILED: {e}");
            }
            if (input)
            {
                input.OnListeningStopped += () => { micStopAt = Now; transcribedAt = -1; };
                input.OnTranscribed += t => { heard = t; transcribedAt = Now; };
                input.OnLanguageDetected += l => heardLanguage = l.ToString();
            }
        }

        static float Now => Time.realtimeSinceStartup;

        void Capture(string message, string stackTrace, LogType type)
        {
            bool wanted = type != LogType.Log;
            if (!wanted)
                foreach (string p in logPrefixes)
                    if (message.StartsWith(p)) { wanted = true; break; }
            if (!wanted) return;
            string line = message.Replace("\n", " ");
            if (line.Length > 220) line = line.Substring(0, 220) + "...";
            string tag = type == LogType.Log ? "" : type == LogType.Warning ? "[warn] " : "[ERROR] ";
            incoming.Enqueue($"{System.DateTime.Now:HH:mm:ss} {tag}{line}");
        }

        void Add(string line) => incoming.Enqueue($"{System.DateTime.Now:HH:mm:ss} {line}");

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame) visible = !visible;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            while (incoming.TryDequeue(out string line))
            {
                log.Add(line);
                if (log.Count > 200) log.RemoveAt(0);
            }
        }

        /// Shown or hidden (F1 toggles it too).
        public bool Visible { get => visible; set => visible = value; }

        /// False when another UI shows Describe()'s lines itself (the companion screen's debug card).
        public bool DrawOwnPanel { get; set; } = true;

        public enum LineKind { Header, Text, Detail }

        /// The panel's contents, top to bottom, without the log (see LogLines).
        public void Describe(List<(LineKind kind, string text)> lines)
        {
            lines.Clear();
            var profile = dialogue ? dialogue.ActiveProfile : null;
            lines.Add((LineKind.Header, $"Avatar   ·   {fps:0} fps"));
            if (profile)
            {
                string mode = profile.IsOpenChat ? $"open chat (memory {profile.openChatMemory}, creativity {profile.openChatCreativity:0.0})" : $"knowledge only ({profile.TopicsPhrase(false)})";
                lines.Add((LineKind.Text, $"{profile.displayName} [{profile.avatarId}] - {mode}"));
                lines.Add((LineKind.Detail, $"Character: {(stage && stage.Shown ? stage.Shown.displayName : "-")}   Voices: {profile.englishVoice} / {profile.hindiVoice}" +
                                            $"   Lip-sync profile: {(hearing && hearing.profile ? hearing.profile.name.Replace("uLipSync-Profile-", "") : "-")}"));
            }

            string mic = input == null ? "-" : input.IsListening ? "LISTENING" : input.IsTranscribing ? "transcribing" : "idle";
            string brain = dialogue && dialogue.IsThinking ? "THINKING" : "idle";
            string voice = output == null ? "-" : output.IsSpeaking ? "SPEAKING" : output.IsBusy ? "preparing" : "idle";
            lines.Add((LineKind.Text, $"Now: mic {mic}  |  brain {brain}  |  voice {voice}  |  body {(body ? body.CurrentState : "-")}"));

            lines.Add((LineKind.Header, "Last turn"));
            lines.Add((LineKind.Text, $"Reply ready {Since(askedAt, replyAt)}  |  first sound {Since(askedAt, soundAt)}  |  done {Since(askedAt, doneAt)}"));
            lines.Add((LineKind.Detail, $"Heard{(heardLanguage != "" ? $" ({heardLanguage})" : "")}: {heard}" +
                                        (micStopAt > 0 && transcribedAt > micStopAt ? $"   (transcribed in {transcribedAt - micStopAt:0.00} s)" : "")));
            lines.Add((LineKind.Detail, $"Asked: {asked}"));
            lines.Add((LineKind.Detail, $"Reply: {(reply.Length > 160 ? reply.Substring(0, 160) + "..." : reply)}"));

            if (lips && hearing)
            {
                float level = lips.Level;
                lines.Add((LineKind.Header, "Lip-sync"));
                lines.Add((LineKind.Text, $"Sound '{hearing.result.phoneme}'   level {Bar(level)} {level:0.00}   jaw {lips.JawDegrees:0.0} deg"));
            }
        }

        /// The newest log lines, oldest first.
        public IEnumerable<string> LogLines()
        {
            for (int i = Mathf.Max(0, log.Count - logLines); i < log.Count; i++) yield return log[i];
        }

        public void ClearLog() => log.Clear();
        public void StopVoice() { if (output) output.Stop(); }

        readonly List<(LineKind kind, string text)> described = new List<(LineKind, string)>();

        void OnGUI()
        {
            if (!DrawOwnPanel) return;
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = 720f;
            Styles();

            if (GUI.Button(new Rect(width - 116, height * 0.78f - 40, 100, 30), visible ? "Hide debug" : "Debug (F1)")) visible = !visible;
            if (!visible) return;

            var area = new Rect(12, height * 0.28f, Mathf.Clamp(width * 0.36f, 260, 520), height * 0.49f);
            GUI.Box(area, GUIContent.none, box);
            GUILayout.BeginArea(new Rect(area.x + 10, area.y + 8, area.width - 20, area.height - 16));
            // Vertical scrolling only: lines wrap to the panel's width.
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            lineWidth = area.width - 40;

            Describe(described);
            foreach (var (kind, text) in described)
            {
                if (kind == LineKind.Header) GUILayout.Space(4);
                Line(text, kind == LineKind.Header ? header : kind == LineKind.Text ? label : small);
            }

            GUILayout.Space(4);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Log", header);
                if (GUILayout.Button("Stop voice", GUILayout.Width(90))) StopVoice();
                if (GUILayout.Button("Clear", GUILayout.Width(60))) ClearLog();
            }
            foreach (string line in LogLines()) Line(line, small);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        Vector2 scroll;
        float lineWidth = 300;

        void Line(string text, GUIStyle style) => GUILayout.Label(text, style, GUILayout.Width(lineWidth));

        static string Since(float from, float to) => from < 0 || to < from ? "-" : $"{to - from:0.0} s";

        static string Bar(float v)
        {
            int n = Mathf.RoundToInt(Mathf.Clamp01(v) * 10);
            return new StringBuilder().Append('|', n).Append('.', 10 - n).ToString();
        }

        Texture2D panelTexture;

        void Styles()
        {
            if (box != null && panelTexture) return;
            panelTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            panelTexture.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.08f, 0.85f));
            panelTexture.Apply();
            box = new GUIStyle();
            box.normal.background = panelTexture;
            label = Text(13, Color.white, FontStyle.Normal);
            small = Text(12, new Color(0.8f, 0.85f, 0.9f), FontStyle.Normal);
            header = Text(14, new Color(0.45f, 0.75f, 1f), FontStyle.Bold);
        }

        static GUIStyle Text(int size, Color color, FontStyle fontStyle)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, fontStyle = fontStyle };
            style.normal.textColor = color;
            style.hover.textColor = color;
            return style;
        }

        void OnDestroy()
        {
            if (panelTexture) Destroy(panelTexture);
        }
    }
}
