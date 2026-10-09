using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using ChatbotAI.Dialogue;
using UnityEngine;
using Whisper;
using Whisper.Utils;

namespace ChatbotAI.Audio
{
    /// Mic capture -> Whisper transcription. Two ways to listen (menu > Visitors > Listening & language):
    ///   Tap to talk - the visitor taps the mic button (or holds Space) and taps again / pauses to finish;
    ///   Automatic   - the mic stays open while the bot waits; a voice that stands out from the room's own level for
    ///                 ~0.2 s starts a question and a pause ends it (never while the bot speaks, so it doesn't hear itself).
    /// A background-noise filter (both ways) cuts rumble and ignores recordings with no clear voice - Whisper turned
    /// room noise into "Amen." / "Thank you very much." before. Dictate() records one answer for the menu's text boxes.
    ///
    /// Auto mode asks the general multilingual model (large-v3-turbo, in the
    /// whisperEnglish slot) how likely the audio is English vs Hindi (see
    /// WhisperLanguageDetector), then transcribes with that language forced -
    /// so output is only ever English or Devanagari text, never Urdu script.
    /// English is also primed with names/terms from the avatar's documents.
    public class SpeechInputController : MonoBehaviour
    {
        public enum InputLanguage { Auto, English, Hindi }

        public enum HindiRecognizer
        {
            [InspectorName("General model (turbo) - faster, cleaner")] General,
            [InspectorName("Hindi fine-tuned model")] FineTuned,
        }

        [SerializeField] MicrophoneRecord microphone;
        [Tooltip("The general multilingual model (large-v3-turbo). Handles English, the language check, and Hindi unless the fine-tuned model is selected.")]
        [SerializeField] WhisperManager whisperEnglish;
        [SerializeField] WhisperManager whisperHindi;
        [SerializeField] InputLanguage language = InputLanguage.Auto;

        [Tooltip("Which model transcribes Hindi. In testing the fine-tuned model added junk to the start of results, sometimes " +
                 "invented whole sentences, and was ~3x slower; the general model with Hindi forced was clean.")]
        [SerializeField] HindiRecognizer hindiRecognizer = HindiRecognizer.General;

        [Tooltip("Optional - supplies the active avatar's document vocabulary as a recognition hint.")]
        [SerializeField] DialogueController dialogueController;

        [Tooltip("whisper.unity never sets whisper.cpp's thread count, so it's stuck on the native default. Applied once per model after it loads.")]
        [SerializeField] int whisperThreads = 8;

        [Tunable("Listening", "Stop listening when you stop talking",
                 note = "Hands-free: after a pause it stops by itself - no need to tap again or let go of Space.")]
        [SerializeField] bool autoStop = false;
        [Tunable("Listening", "Pause before it stops", 0.5f, 5f, 0.1f, unit = "s", note = "With the above on: how long a silence ends the question.")]
        [SerializeField, Range(0.5f, 5f)] float autoStopSeconds = 1.5f;
        [Tunable("Listening", "Longest question", 5f, 60f, 1f, unit = "s", note = "Listening stops after this long, whatever happens.")]
        [SerializeField, Range(5, 60)] int maxListenSeconds = 30;

        [Tunable("Listening", "Background noise filter",
                 note = "Cuts low rumble (air conditioning, traffic) and ignores recordings without a clear voice - so a busy room isn't taken for a question.")]
        [SerializeField] bool noiseFilter = true;
        [Tunable("Listening", "Voice must stand out from the noise by (dB)", 3f, 20f, 1f,
                 note = "Noise filter: how much louder than the room's background the voice must be. Higher ignores more - and quiet speakers.")]
        [SerializeField, Range(3f, 20f)] float minVoiceOverNoiseDb = 6f;
        [Tunable("Listening", "Automatic: how loud a voice must be", 1.5f, 8f, 0.1f, unit = "x",
                 note = "Automatic listening: how many times louder than the room's background a voice must be to start a question. Higher = only people close to the screen.")]
        [SerializeField, Range(1.5f, 8f)] float autoStartRatio = 3f;
        [Tunable("Listening", "Automatic: pause that ends a question", 0.5f, 3f, 0.1f, unit = "s")]
        [SerializeField, Range(0.5f, 3f)] float autoEndSilence = 1f;

        public enum ListenMode { TapToTalk, Automatic }
        const string ModeKey = "companion/listen-mode";

        /// Tap to talk, or automatic listening (kept on this PC; travels with builds like the other menu settings).
        public ListenMode Mode
        {
            get => (ListenMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 1);
            set
            {
                PlayerPrefs.SetInt(ModeKey, (int)value);
                PlayerPrefs.Save();
                if (value != ListenMode.Automatic && autoArmed && !IsListening) DisarmAuto();
            }
        }

        /// Set by the app screen: whether automatic listening may wait for a visitor now (the bot is idle, the menu and
        /// the keyboard are closed...). Not set = never.
        public Func<bool> AutoAllowed;

        /// Automatic mode: the mic is open, waiting for someone to speak.
        public bool AutoWaiting => autoArmed && !IsListening;

        /// The last question was started by automatic listening (nothing heard is then not worth a message).
        public bool LastWasAutomatic { get; private set; }

        /// A recording had nothing in it worth answering (silence, or noise the filter threw away).
        public event Action OnNothingHeard;

        const string MicKey = "companion/microphone";

        /// The microphone to record from (menu > Language & listening); empty = the system default.
        public string MicrophoneDevice
        {
            get => PlayerPrefs.GetString(MicKey, "");
            set
            {
                PlayerPrefs.SetString(MicKey, value ?? "");
                PlayerPrefs.Save();
                if (!IsListening) UseMicrophone(value);
            }
        }

        // A saved microphone that's been unplugged falls back to the system default.
        void UseMicrophone(string device)
        {
            bool present = !string.IsNullOrEmpty(device) && System.Linq.Enumerable.Contains(microphone.AvailableMicDevices, device);
            microphone.SelectedMicDevice = present ? device : null;
        }

        public IEnumerable<string> MicrophoneDevices => microphone.AvailableMicDevices;

        /// The speech recognition model file in use (StreamingAssets/Models/Whisper).
        public string SpeechModelFile => System.IO.Path.GetFileName(whisperEnglish.ModelPath ?? "");

        string vocabularyPrompt;

        public event Action<string> OnTranscribed;
        public event Action<InputLanguage> OnLanguageDetected;
        public event Action OnListeningStarted;
        public event Action OnListeningStopped;

        /// The mic is recording.
        public bool IsListening { get; private set; }
        /// Recording has stopped and Whisper is working out what was said.
        public bool IsTranscribing { get; private set; }

        bool micUsesVad;
        float micChunkSeconds;

        void Awake()
        {
            microphone.OnRecordStop += HandleRecordStop;
            microphone.OnChunkReady += HandleChunk;
            microphone.echo = false;    // never play the visitor's own recording back
            micUsesVad = microphone.useVad;
            micChunkSeconds = microphone.chunksLengthSec;
            whisperHindi.language = "hi";
            // A speech model picked in the app's menu (ModelChoices) - it loads later (after the language model).
            string picked = ModelChoices.Get(ModelChoices.Ears);
            if (!string.IsNullOrEmpty(picked) && !whisperEnglish.IsLoaded && !whisperEnglish.IsLoading &&
                System.IO.File.Exists(System.IO.Path.Combine(WhisperModelLibrary.FolderPath, picked)))
            {
                whisperEnglish.ModelPath = WhisperModelLibrary.RelativeFolder + "/" + picked;
                whisperEnglish.IsModelPathInStreamingAssets = true;
            }
            UseMicrophone(MicrophoneDevice);
            // The fine-tuned Hindi model only loads if it's selected (~1.7 GB of graphics memory).
            if (hindiRecognizer != HindiRecognizer.FineTuned) WhisperLoading.SetLoadsOnStart(whisperHindi, false);
        }

        async void Start()
        {
            await ApplyThreadCountWhenLoaded(whisperEnglish);
            if (hindiRecognizer == HindiRecognizer.FineTuned) await ApplyThreadCountWhenLoaded(whisperHindi);
        }

        void OnDestroy()
        {
            microphone.OnRecordStop -= HandleRecordStop;
            microphone.OnChunkReady -= HandleChunk;
        }

        public void SetLanguage(InputLanguage lang) => language = lang;
        public InputLanguage Language => language;

        public void StartListening()
        {
            if (IsTranscribing) return;
            // Automatic mode, waiting: the visitor tapped the button anyway - their question starts now.
            if (autoArmed)
            {
                if (IsListening) return;
                float now = Time.realtimeSinceStartup;
                speechStartOffset = Mathf.Max(0f, now - recordStartedAt - 0.3f);
                lastVoiceAt = now + 2f;      // a moment to start talking before a pause can end it
                IsListening = true;
                LastWasAutomatic = false;
                OnListeningStarted?.Invoke();
                return;
            }
            RefreshVocabularyHint();
            microphone.useVad = micUsesVad;
            microphone.chunksLengthSec = micChunkSeconds;
            microphone.vadStop = autoStop || dictation != null;
            microphone.vadStopTime = dictation != null ? 2f : autoStopSeconds;
            microphone.maxLengthSec = maxListenSeconds;
            microphone.StartRecord();
            IsListening = true;
            LastWasAutomatic = false;
            if (dictation == null) OnListeningStarted?.Invoke();
        }

        public void StopListening()
        {
            if (!IsListening) return;
            IsListening = false;
            IsTranscribing = true;
            if (dictation == null) OnListeningStopped?.Invoke();
            microphone.StopRecord();
        }

        async void HandleRecordStop(AudioChunk recordedAudio)
        {
            bool auto = autoArmed;
            autoArmed = false;
            // Automatic mode, nobody spoke (or it was closed to wait afresh): nothing to do.
            if (discardNext || (auto && speechStartOffset < 0f))
            {
                discardNext = false;
                return;
            }
            var dictate = dictation;
            // Stopped by itself (a pause with "stop when you stop talking", or the time limit), not by StopListening.
            if (IsListening)
            {
                IsListening = false;
                IsTranscribing = true;
                if (dictate == null) OnListeningStopped?.Invoke();
            }
            float[] data = recordedAudio.Data;
            // Automatic: from just before the voice started (the wait before it is only room noise).
            if (auto) data = From(data, recordedAudio.Frequency * recordedAudio.Channels, speechStartOffset - 0.5f);
            string text;
            InputLanguage detected;
            try
            {
                (text, detected) = await TranscribeAsync(data, recordedAudio.Frequency, recordedAudio.Channels);
            }
            finally
            {
                IsTranscribing = false;
                dictation = null;
            }
            if (dictate != null)
            {
                dictate(text?.Trim() ?? "");
                return;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                OnNothingHeard?.Invoke();
                return;
            }

            OnLanguageDetected?.Invoke(detected);
            // The language an online service heard (Tamil, French...) goes with the question - the bot answers in it.
            Languages.HeardLanguage = lastHeardCode;
            lastHeardCode = null;
            OnTranscribed?.Invoke(text);
        }

        string lastHeardCode;

        static float[] From(float[] data, int samplesPerSecond, float seconds)
        {
            int skip = Mathf.Clamp(Mathf.RoundToInt(seconds * samplesPerSecond), 0, data.Length);
            if (skip == 0) return data;
            var rest = new float[data.Length - skip];
            Array.Copy(data, skip, rest, 0, rest.Length);
            return rest;
        }

        // ---------------- Automatic listening ----------------

        bool autoArmed, discardNext;
        float recordStartedAt, speechStartOffset = -1f, lastVoiceAt;
        float noiseFloor = -1f;   // the room's own level (RMS), kept between questions
        float speechPeak;         // the current speaker's level
        int loudChunks;

        static float Db(float rms) => 20f * Mathf.Log10(Mathf.Max(rms, 1e-5f));

        void Update()
        {
            if (stopRequested)
            {
                stopRequested = false;
                if (autoArmed && IsListening) StopListening();
            }
            if (Mode != ListenMode.Automatic || dictation != null)
            {
                if (autoArmed && !IsListening) DisarmAuto();
                return;
            }
            bool allowed = AutoAllowed != null && AutoAllowed();
            if (autoArmed)
            {
                // Waiting: closed again when it mustn't listen, and every 20 s (keeps the recording short).
                if (!IsListening && (!allowed || Time.realtimeSinceStartup - recordStartedAt > 20f)) DisarmAuto();
            }
            else if (allowed && !IsListening && !IsTranscribing && !microphone.IsRecording)
                ArmAuto();
        }

        void ArmAuto()
        {
            microphone.useVad = false;            // our own detector (HandleChunk) decides
            microphone.vadStop = false;
            microphone.chunksLengthSec = 0.1f;
            microphone.maxLengthSec = 60;
            loudChunks = 0;
            speechPeak = 0f;
            speechStartOffset = -1f;
            autoArmed = true;
            recordStartedAt = Time.realtimeSinceStartup;
            microphone.StartRecord();
        }

        void DisarmAuto()
        {
            discardNext = true;
            microphone.StopRecord();   // HandleRecordStop throws it away
            autoArmed = false;
            discardNext = false;
        }

        // Every 0.1 s of audio while automatic listening waits or listens.
        void HandleChunk(AudioChunk chunk)
        {
            if (!autoArmed || chunk.Data == null || chunk.Data.Length == 0) return;
            float rms = Rms(chunk.Data, 0, chunk.Data.Length);
            float now = Time.realtimeSinceStartup;
            if (noiseFloor < 0f) noiseFloor = Mathf.Max(rms, 1e-4f);
            if (!IsListening)
            {
                bool voice = rms > noiseFloor * autoStartRatio && rms > 0.005f;
                loudChunks = voice ? loudChunks + 1 : 0;
                speechPeak = voice ? Mathf.Max(speechPeak, rms) : 0f;
                // The room's level: follows quieter moments quickly, louder ones slowly (a voice doesn't become "the room").
                noiseFloor = Mathf.Max(1e-4f, Mathf.Lerp(noiseFloor, rms, rms < noiseFloor ? 0.3f : 0.02f));
                // 0.3 s of it - a click, a cough or a dropped key is shorter.
                if (loudChunks < 3) return;
                speechStartOffset = Mathf.Max(0f, now - recordStartedAt - 0.35f);
                lastVoiceAt = now;
                IsListening = true;
                LastWasAutomatic = true;
                Debug.Log($"Automatic listening: a voice at {Db(speechPeak):0} dB (the room {Db(noiseFloor):0} dB).");
                RefreshVocabularyHint();
                OnListeningStarted?.Invoke();
                return;
            }
            // Still talking while it's louder than the room - and not far below the speaker's own level (a quiet room
            // estimate alone kept a question open for many seconds of headset hiss).
            speechPeak = Mathf.Max(speechPeak * 0.995f, rms);
            if (rms > Mathf.Max(noiseFloor * autoStartRatio * 0.6f, speechPeak * 0.18f)) lastVoiceAt = Mathf.Max(lastVoiceAt, now);
            // Stopped in Update, not here: this runs inside the recorder's own loop over its audio clip.
            if (now - lastVoiceAt > autoEndSilence || now - (recordStartedAt + speechStartOffset) > maxListenSeconds)
                stopRequested = true;
        }

        bool stopRequested;

        // ---------------- Dictation (menu text boxes) ----------------

        Action<string> dictation;

        /// Recording or writing down a dictation now.
        public bool IsDictating => dictation != null;

        /// Records one spoken answer and hands back its text (empty if nothing was heard) - for the menu's text boxes.
        /// It stops by itself after a 2 s pause, or with StopListening. False if the mic is busy.
        public bool Dictate(Action<string> onText)
        {
            if (onText == null || IsListening || IsTranscribing) return false;
            if (autoArmed) DisarmAuto();
            dictation = onText;
            StartListening();
            return true;
        }

        // ---------------- Background noise filter ----------------

        static float Rms(float[] data, int start, int count)
        {
            double sum = 0;
            for (int i = start; i < start + count; i++) sum += data[i] * data[i];
            return count > 0 ? (float)Math.Sqrt(sum / count) : 0f;
        }

        /// Removes rumble below ~100 Hz (air conditioning, traffic, footsteps) - a voice has almost nothing there.
        static float[] HighPass(float[] data, int frequency, float cutoff = 100f)
        {
            var output = new float[data.Length];
            float rc = 1f / (2f * Mathf.PI * cutoff), dt = 1f / frequency, a = rc / (rc + dt);
            float prevIn = 0f, prevOut = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                prevOut = a * (prevOut + data[i] - prevIn);
                prevIn = data[i];
                output[i] = prevOut;
            }
            return output;
        }

        /// How far the loudest parts (a voice) stand above the quiet parts (the room), in dB; loudest = their level.
        static float VoiceOverNoiseDb(float[] data, int frequency, out float loudest)
        {
            int frame = Mathf.Max(1, frequency / 50);   // 20 ms
            var levels = new List<float>();
            for (int i = 0; i + frame <= data.Length; i += frame) levels.Add(Rms(data, i, frame));
            loudest = 0f;
            if (levels.Count < 5) return 0f;
            levels.Sort();
            float noise = Mathf.Max(levels[(int)(levels.Count * 0.2f)], 1e-5f);
            loudest = levels[Mathf.Min(levels.Count - 1, (int)(levels.Count * 0.95f))];
            return 20f * Mathf.Log10(Mathf.Max(loudest, 1e-5f) / noise);
        }

        public async Task<(string text, InputLanguage language)> TranscribeAsync(float[] samples, int frequency, int channels)
        {
            InputLanguage chosen = language;
            float overNoise = 99f, loudest = 1f;
            if (noiseFilter && channels == 1 && samples.Length > 0)
            {
                samples = HighPass(samples, frequency);
                overNoise = VoiceOverNoiseDb(samples, frequency, out loudest);
                if (loudest < 0.003f || overNoise < minVoiceOverNoiseDb)
                {
                    Debug.Log($"Noise filter: no clear voice (loudest {20f * Mathf.Log10(Mathf.Max(loudest, 1e-5f)):0} dB, " +
                              $"{overNoise:0} dB over the background) - ignored.");
                    return ("", chosen == InputLanguage.Auto ? InputLanguage.English : chosen);
                }
            }
            // Online ears: the service writes the words down and says which language it heard; if it can't be used,
            // Whisper on this PC listens to this question.
            lastHeardCode = null;
            if (OnlineEars.Ready && channels == 1 && samples.Length > 0)
            {
                string hint = chosen == InputLanguage.English ? "en" : chosen == InputLanguage.Hindi ? "hi" : OnlineEars.ListenCode;
                var heard = await OnlineEars.Transcribe(samples, frequency, hint, vocabularyPrompt);
                if (heard.text != null)
                {
                    InputLanguage which = heard.language == "hi" ? InputLanguage.Hindi : InputLanguage.English;
                    if (noiseFilter && IsSilenceInvention(heard.text) && (overNoise < 20f || loudest < 0.03f))
                    {
                        Debug.Log($"Noise filter: \"{heard.text}\" from a quiet recording - made up from silence - ignored.");
                        return ("", which);
                    }
                    lastHeardCode = heard.language;
                    return (heard.text, which);
                }
            }
            if (chosen == InputLanguage.Auto)
            {
                chosen = InputLanguage.English;
                if (frequency == WhisperWrapper.WhisperSampleRate && channels == 1 && whisperEnglish.IsLoaded)
                {
                    var probs = await Task.Run(() => WhisperLanguageDetector.Detect(whisperEnglish, samples, whisperThreads));
                    if (probs.ok)
                    {
                        chosen = probs.IsEnglish ? InputLanguage.English : InputLanguage.Hindi;
                        Debug.Log($"Speech language: English {probs.english:P0} vs Hindi {probs.hindi:P0} -> {chosen}");
                        // Neither language is likely: room noise (it came out as "Amen." / "Yeah." at 6-11%).
                        if (noiseFilter && Mathf.Max(probs.english, probs.hindi) < 0.25f)
                        {
                            Debug.Log("Noise filter: doesn't sound like English or Hindi speech - ignored.");
                            return ("", chosen);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("SpeechInputController: language check failed - assuming English.");
                    }
                }
            }

            WhisperManager model;
            if (chosen == InputLanguage.Hindi && hindiRecognizer == HindiRecognizer.FineTuned)
            {
                model = whisperHindi;
                if (!model.IsLoaded)
                {
                    await model.InitModel();
                    await ApplyThreadCountWhenLoaded(model);
                }
            }
            else
            {
                model = whisperEnglish;
                model.language = chosen == InputLanguage.Hindi ? "hi" : "en";
                // The name hint is Latin script - it helps English but garbles Hindi.
                model.initialPrompt = chosen == InputLanguage.Hindi ? null : vocabularyPrompt;
            }

            WhisperResult result = await model.GetTextAsync(samples, frequency, channels);
            string text = result?.Result?.Trim();
            // Whisper's favourite inventions for near-silence ("Thank you very much." from a headset's hiss, 2026-10-06):
            // dropped unless the voice was clearly there.
            if (noiseFilter && IsSilenceInvention(text) && (overNoise < 20f || loudest < 0.03f))
            {
                Debug.Log($"Noise filter: \"{text}\" from a quiet recording ({overNoise:0} dB over the background) - " +
                          "Whisper makes that up from silence - ignored.");
                return ("", chosen);
            }
            return (text, chosen);
        }

        static readonly HashSet<string> SilenceInventions = new HashSet<string>
        {
            "thank you", "thank you very much", "thanks", "thanks for watching", "thank you for watching", "amen", "bye",
            "bye bye", "you", "yeah", "okay", "ok", "so", "hmm", "um", "uh", "oh", "please subscribe", "the end",
        };

        static bool IsSilenceInvention(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var letters = new System.Text.StringBuilder();
            foreach (char c in text.ToLowerInvariant()) if (char.IsLetter(c) || c == ' ') letters.Append(c);
            string key = string.Join(" ", letters.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            return SilenceInventions.Contains(key);
        }

        async void RefreshVocabularyHint()
        {
            if (dialogueController == null || dialogueController.ActiveProfile == null) return;
            var profile = dialogueController.ActiveProfile;
            // The bot's own names first: the documents' vocabulary had no "P&I" (its words come from file names and
            // CamelCase/digit words), and "P and I" was heard as "DNA" (2026-10-08).
            var names = new List<string>();
            foreach (string n in profile.EffectiveTopicNames)
                if (!names.Contains(n.Trim())) names.Add(n.Trim());
            if (profile.heardAs != null)
                foreach (var n in profile.heardAs)
                    if (n != null && !string.IsNullOrWhiteSpace(n.name) && !names.Contains(n.name.Trim())) names.Add(n.name.Trim());
            var words = await KnowledgeClient.GetVocabulary(dialogueController.SidecarUrl, profile.KnowledgeFolderName);
            if (words == null && names.Count == 0) return;
            foreach (string w in words ?? Array.Empty<string>())
                if (!names.Contains(w)) names.Add(w);
            // Whisper treats this as text it has "already heard", which biases it
            // toward these spellings.
            vocabularyPrompt = names.Count == 0 ? null : string.Join(", ", names) + ".";
        }

        async Task ApplyThreadCountWhenLoaded(WhisperManager manager)
        {
            while (manager != null && !manager.IsLoaded) await Task.Delay(100);
            if (manager == null) return;

            var paramsField = typeof(WhisperManager).GetField("_params", BindingFlags.NonPublic | BindingFlags.Instance);
            if (paramsField?.GetValue(manager) is WhisperParams whisperParams)
            {
                whisperParams.ThreadsCount = whisperThreads;
            }
            else
            {
                Debug.LogWarning($"SpeechInputController: could not set thread count on {manager.name} (whisper.unity internals may have changed).");
            }
        }
    }

    /// whisper.unity loads a model when its GameObject wakes up unless told not to;
    /// that switch isn't public.
    static class WhisperLoading
    {
        static readonly FieldInfo InitOnAwake = typeof(WhisperManager).GetField("initOnAwake", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool LoadsOnStart(WhisperManager manager) => InitOnAwake == null || (bool)InitOnAwake.GetValue(manager);

        public static void SetLoadsOnStart(WhisperManager manager, bool load)
        {
            if (InitOnAwake != null) InitOnAwake.SetValue(manager, load);
            else Debug.LogWarning("SpeechInputController: can't skip loading an unused Whisper model (whisper.unity internals changed).");
        }
    }
}