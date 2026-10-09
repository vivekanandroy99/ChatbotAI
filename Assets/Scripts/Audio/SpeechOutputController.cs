using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatbotAI.Audio
{
    /// Speaks reply text through the local voice server (TTSServer/server.py,
    /// localhost only). The reply is split into sentences and each one is
    /// generated while the previous one plays; audio is played as it streams in
    /// (PcmStream), so a voice that generates while speaking (Veena) starts
    /// within half a second. A new reply interrupts whatever is still being spoken.
    public class SpeechOutputController : MonoBehaviour, ITunableListener
    {
        // Fragments shorter than this ride along with the next sentence rather
        // than costing their own request and pause.
        const int MinSentenceChars = 20;
        static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?।])\s+");

        [SerializeField] AudioSource audioSource;
        [SerializeField] string ttsUrl = "http://127.0.0.1:8765/speak";
        [SerializeField] TTSProcessManager processManager;

        public event Action OnGenerating;
        public event Action OnSpeechStarted;
        public event Action OnSpeechFinished;
        public event Action<string> OnFailed;
        /// A sentence finished playing: its text, voice, audio and sample rate (for review recording).
        public event Action<string, string, float[], int> OnSentenceSpoken;

        public bool IsBusy { get; private set; }
        /// A sentence is actually playing right now (IsBusy also covers waiting for the next sentence).
        /// Not just audioSource.isPlaying: a source with Play On Awake reports playing with no clip at all,
        /// which had the avatar in Talk from the moment the app started.
        public bool IsSpeaking => PlayingStream != null && audioSource && audioSource.isPlaying;

        /// The sentence playing now and the audio-clock time it started (read on the audio thread by
        /// LipSyncLookahead, which analyses the speech slightly ahead of what's audible).
        public PcmStream PlayingStream { get; private set; }
        /// The voice of the reply being spoken (or last spoken) - LipSyncVoiceProfiles picks uLipSync's profile by it.
        public string CurrentVoice { get; private set; }
        public double PlayingStartDsp { get; private set; }
        public bool EngineReady => processManager == null || processManager.IsReady;

        /// Asks the voice server to load an optional engine (IndicF5) now rather than on the first reply.
        public void WarmVoice(string voice)
        {
            string json = JsonUtility.ToJson(new WarmRequest { voice = voice });
            var req = new UnityWebRequest(ttsUrl.Replace("/speak", "/warm_voice"), "POST")
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SendWebRequest().completed += _ => req.Dispose();
        }

        [Serializable]
        class WarmRequest
        {
            public string voice;
        }

        // Gives up on a reply whose next sentence never arrives.
        const float StreamTimeoutSeconds = 20f;

        [Tooltip("Audio buffered before a sentence that's still being generated (Veena) starts playing. Unity reads " +
                 "~0.86 s ahead the moment a streamed clip starts; with less buffered than that, it plays a gap ~0.5 s " +
                 "in. Voices that arrive whole (Kokoro) don't wait.")]
        [Tunable("Voice & sound", "Voice volume", 0f, 1f, 0.05f, unit = "%", note = "The app's own volume, on top of Windows' volume.")]
        [SerializeField, Range(0f, 1f)] float volume = 1f;

        [Tunable("Voice & sound", "Voice buffer before each sentence", 0.3f, 3f, 0.1f, unit = "s",
                 note = "Veena voices: audio collected before a sentence starts. Raise it if the voice breaks up on a busy computer.")]
        [SerializeField] float streamPrebufferSeconds = 0.9f;

        Coroutine speaking;
        UnityWebRequest current, next;  // the sentence playing, and the one after it
        List<string> sentences = new List<string>();
        bool moreComing;
        float lastAppendTime;

        /// Speaks text, interrupting anything still being spoken. moreToCome: the
        /// reply continues through Append() calls (a reply that's still being written).
        public void Speak(string text, string voice, float speed, bool moreToCome = false)
        {
            if (processManager != null && !processManager.IsReady && !OnlineVoice.Ready)
            {
                const string error = "voice engine is still starting up - try again in a few seconds";
                Debug.LogWarning($"SpeechOutputController: {error}");
                OnFailed?.Invoke(error);
                return;
            }

            Stop();
            CurrentVoice = voice;
            sentences = SplitSentences(text);
            moreComing = moreToCome;
            lastAppendTime = Time.realtimeSinceStartup;
            if (sentences.Count == 0 && !moreToCome) return;
            speaking = StartCoroutine(SpeakRoutine(voice, speed));
        }

        /// Adds the next part of the reply being spoken; ignored once it's been interrupted.
        public void Append(string text, bool moreToCome)
        {
            if (speaking == null) return;
            sentences.AddRange(SplitSentences(text));
            moreComing = moreToCome;
            lastAppendTime = Time.realtimeSinceStartup;
        }

        public void Stop()
        {
            if (speaking != null) StopCoroutine(speaking);
            speaking = null;
            moreComing = false;
            Dispose(ref current);
            Dispose(ref next);
            PlayingStream = null;
            audioSource.Stop();
            ReleaseClip();
            IsBusy = false;
        }

        public void TunableChanged(string field)
        {
            if (field == nameof(volume)) audioSource.volume = volume;
        }

        void Awake()
        {
            // Only plays what Speak sends it.
            audioSource.playOnAwake = false;
            audioSource.volume = volume;
            audioSource.Stop();
        }

        void OnDisable() => Stop();

        IEnumerator SpeakRoutine(string voice, float speed)
        {
            IsBusy = true;
            OnGenerating?.Invoke();

            for (int i = 0; ; i++)
            {
                // Waiting for the next sentence of a reply that's still being written.
                while (i >= sentences.Count && moreComing && Time.realtimeSinceStartup - lastAppendTime < StreamTimeoutSeconds)
                    yield return null;
                if (i >= sentences.Count) break;

                current = next ?? Send(sentences[i], voice, speed);
                next = null;
                var request = current;
                var stream = (PcmStream)request.downloadHandler;
                string sentence = sentences[i];
                string rateKey = stream.Online ? "online/" + voice : voice;
                float asked = Time.realtimeSinceStartup;
                // Start once enough audio is buffered that the rest arrives before it's needed - no gap mid-word.
                yield return new WaitUntil(() => request.isDone ||
                                                 (request.responseCode == 200 && ReadyToPlay(stream, sentence, rateKey)));
                bool failed = request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError ||
                              (request.isDone && stream.SampleRate == 0);
                if (failed && stream.Online)
                {
                    // The online voice didn't work: this sentence is spoken by the voice on this PC.
                    OnlineVoice.NoteFailure(request.responseCode, stream.Head, request.error);
                    Dispose(ref current);
                    current = Send(sentence, voice, speed, local: true);
                    request = current;
                    stream = (PcmStream)request.downloadHandler;
                    rateKey = voice;
                    yield return new WaitUntil(() => request.isDone ||
                                                     (request.responseCode == 200 && ReadyToPlay(stream, sentence, rateKey)));
                    failed = request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError ||
                             (request.isDone && stream.SampleRate == 0);
                }
                else if (stream.Online && stream.SampleRate > 0) OnlineVoice.NoteSuccess(Time.realtimeSinceStartup - asked);
                if (failed)
                {
                    string error = $"TTS request failed - {request.error} (HTTP {request.responseCode})";
                    Dispose(ref current);
                    Debug.LogError($"SpeechOutputController: {error}");
                    IsBusy = false;
                    speaking = null;
                    OnFailed?.Invoke(error);
                    yield break;
                }

                ReleaseClip();
                audioSource.clip = stream.CreateClip();
                // The audio-clock time it starts (to within one audio buffer, ~20 ms), for lip-sync.
                // Not PlayScheduled: with it, the streamed clip read seconds ahead and played silence
                // (up to 3.9 s) where generation hadn't caught up yet.
                PlayingStream = stream;
                PlayingStartDsp = AudioSettings.dspTime;
                audioSource.Play();
                if (i == 0) OnSpeechStarted?.Invoke();

                while (true)
                {
                    // The next sentence is requested once this one has fully arrived, so the
                    // server writes it while this one is still playing.
                    if (request.isDone && next == null && i + 1 < sentences.Count) next = Send(sentences[i + 1], voice, speed);
                    int end = stream.EndSample;
                    if (!audioSource.isPlaying || (end >= 0 && audioSource.timeSamples >= end)) break;
                    yield return null;
                }
                audioSource.Stop();
                PlayingStream = null;
                OnSentenceSpoken?.Invoke(sentences[i], voice, stream.Samples(), stream.SampleRate);
                if (stream.Complete) LearnLength(rateKey, sentences[i], stream.ReceivedSeconds);
                if (stream.StarvedSeconds > 0.05f)
                    Debug.LogWarning($"SpeechOutputController: the voice fell behind - {stream.StarvedSeconds:0.00} s of silence mid-sentence ({stream.StarvedAt}).");
                Dispose(ref current);
            }

            ReleaseClip();
            IsBusy = false;
            speaking = null;
            OnSpeechFinished?.Invoke();
        }

        // Seconds of speech per character of text, per voice - learnt from the sentences spoken, to guess how long
        // the one arriving will be.
        readonly Dictionary<string, float> secondsPerChar = new Dictionary<string, float>();
        const float DefaultSecondsPerChar = 0.08f;

        float EstimateSeconds(string voice, string text) =>
            text.Length * (secondsPerChar.TryGetValue(voice, out float s) ? s : DefaultSecondsPerChar);

        void LearnLength(string voice, string text, float seconds)
        {
            if (text.Length < 12 || seconds <= 0.3f) return;
            float measured = seconds / text.Length;
            secondsPerChar[voice] = secondsPerChar.TryGetValue(voice, out float s) ? Mathf.Lerp(s, measured, 0.3f) : measured;
        }

        /// A sentence still being generated plays once what's buffered covers the rest: at arrival rate r (seconds
        /// of audio per second) and length D, playback never runs dry if R >= D(1 - r) + r * prebuffer. With the
        /// graphics card busy, Veena came at ~0.35x real time and every Hindi sentence broke up after 1.2 s; now
        /// it waits longer before a slow sentence instead of stopping mid-word.
        bool ReadyToPlay(PcmStream stream, string text, string voice)
        {
            float received = stream.BufferedSeconds;
            if (received < streamPrebufferSeconds) return false;
            float? rate = stream.ArrivalRate;
            if (rate == null) return false;
            float length = EstimateSeconds(voice, text) * 1.15f;  // a guess - err long
            float needed = length * (1f - rate.Value) + rate.Value * streamPrebufferSeconds;
            return received >= needed;
        }

        UnityWebRequest Send(string text, string voice, float speed, bool local = false)
        {
            if (!local)
            {
                var online = OnlineVoice.Request(text, voice, speed);
                if (online != null) return online;
            }
            string json = JsonUtility.ToJson(new SpeakRequest { text = text, voice = voice, speed = speed });
            var req = new UnityWebRequest(ttsUrl + "_stream", "POST")
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
                downloadHandler = new PcmStream()
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SendWebRequest();
            return req;
        }

        static void Dispose(ref UnityWebRequest request)
        {
            if (request == null) return;
            if (!request.isDone) request.Abort();
            request.Dispose();
            request = null;
        }

        void ReleaseClip()
        {
            if (audioSource.clip == null) return;
            var old = audioSource.clip;
            audioSource.clip = null;
            Destroy(old);
        }

        // Long sentences are split into clauses: voices get less clear on long text, and the first
        // clause can play while the rest is generated. Only at the comma or conjunction nearest the
        // middle, never leaving a piece shorter than MinClauseChars - splitting at every comma made
        // fragments like "जिसमें residential," that Veena turned into babble.
        const int LongSentenceChars = 100;
        const int MinClauseChars = 35;
        static readonly Regex ClauseBreak = new Regex(
            @"(?<=[,;:])\s+|\s(?=(?:और|जो|जबकि|ताकि|लेकिन|क्योंकि|जिसमें|जिससे|या|and|but|which|while|so)\s)");

        static IEnumerable<string> Pieces(string text)
        {
            foreach (string sentence in SentenceEnd.Split(text.Trim()))
            foreach (string clause in Clauses(sentence.Trim()))
                yield return clause;
        }

        static IEnumerable<string> Clauses(string sentence)
        {
            if (sentence.Length <= LongSentenceChars)
            {
                yield return sentence;
                yield break;
            }
            int cut = -1;
            foreach (Match m in ClauseBreak.Matches(sentence))
            {
                int at = m.Index + m.Length;
                if (at < MinClauseChars || sentence.Length - at < MinClauseChars) continue;
                if (cut < 0 || Math.Abs(at - sentence.Length / 2) < Math.Abs(cut - sentence.Length / 2)) cut = at;
            }
            if (cut < 0)
            {
                yield return sentence;
                yield break;
            }
            foreach (string part in Clauses(sentence.Substring(0, cut).Trim())) yield return part;
            foreach (string part in Clauses(sentence.Substring(cut).Trim())) yield return part;
        }

        static List<string> SplitSentences(string text)
        {
            var result = new List<string>();
            string carry = "";
            foreach (string part in Pieces(text))
            {
                string sentence = (carry + " " + part).Trim();
                if (sentence.Length < MinSentenceChars)
                {
                    carry = sentence;
                    continue;
                }
                result.Add(sentence);
                carry = "";
            }
            if (carry.Length > 0)
            {
                if (result.Count > 0) result[result.Count - 1] += " " + carry;
                else result.Add(carry);
            }
            return result;
        }

        [Serializable]
        class SpeakRequest
        {
            public string text;
            public string voice;
            public float speed;
        }
    }
}
