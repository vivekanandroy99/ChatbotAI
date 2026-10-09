using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChatbotAI.Audio
{
    /// Testing aid: saves every spoken sentence (WAV + its text) to SpeechReview/<session>/
    /// in the project folder, and F8 flags the last reply as sounding wrong. The flagged
    /// clips can then be checked automatically (what a recogniser hears vs. what was meant),
    /// since the assistant helping build this can't listen to audio itself.
    public class SpeechReviewRecorder : MonoBehaviour
    {
        [SerializeField] SpeechOutputController speechOutput;
        [Tunable("Voice & sound", "Save spoken sentences for review", note = "Each sentence as a WAV in SpeechReview/ - lets a developer or AI check the voice. Off by default: the folder grows with every answer.")]
        [SerializeField] bool record = false;
        [Tooltip("Press in Play mode to flag the last reply as sounding wrong.")]
        [SerializeField] Key flagKey = Key.F8;

        // One folder per Play session. NonSerialized: Unity restored it as "" otherwise.
        [NonSerialized] string folder;
        [NonSerialized] int reply, sentence;

        void OnEnable()
        {
            speechOutput.OnGenerating += NewReply;
            speechOutput.OnSentenceSpoken += Save;
        }

        void OnDisable()
        {
            speechOutput.OnGenerating -= NewReply;
            speechOutput.OnSentenceSpoken -= Save;
        }

        void NewReply() => reply++;

        void Save(string text, string voice, float[] samples, int sampleRate)
        {
            if (!record || samples.Length == 0) return;
            if (string.IsNullOrEmpty(folder)) folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SpeechReview", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(folder);
            string file = $"r{reply:000}_s{++sentence:000}.wav";
            File.WriteAllBytes(Path.Combine(folder, file), Wav(samples, sampleRate));
            Append(new Entry { file = file, reply = reply, voice = voice, text = text, time = DateTime.Now.ToString("HH:mm:ss") });
        }

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current[flagKey].wasPressedThisFrame || string.IsNullOrEmpty(folder)) return;
            Append(new Entry { flagged = reply, time = DateTime.Now.ToString("HH:mm:ss") });
            Debug.Log($"SpeechReviewRecorder: reply {reply} flagged for review ({folder}).");
        }

        void Append(Entry entry) => File.AppendAllText(Path.Combine(folder, "index.jsonl"), JsonUtility.ToJson(entry) + "\n");

        [Serializable]
        class Entry
        {
            public string file;
            public int reply;
            public int flagged;
            public string voice;
            public string text;
            public string time;
        }

        static byte[] Wav(float[] samples, int sampleRate)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write("RIFF".ToCharArray());
            w.Write(36 + samples.Length * 2);
            w.Write("WAVEfmt ".ToCharArray());
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(sampleRate);
            w.Write(sampleRate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write("data".ToCharArray());
            w.Write(samples.Length * 2);
            foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767));
            return stream.ToArray();
        }
    }
}
