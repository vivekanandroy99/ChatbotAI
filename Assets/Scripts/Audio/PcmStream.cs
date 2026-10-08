using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatbotAI.Audio
{
    /// One sentence of speech arriving from the voice server's /speak_stream: a
    /// 4-byte sample rate, then 16-bit mono PCM while it's being generated. It is
    /// played through a streamed AudioClip that pulls whatever has arrived; if
    /// generation falls behind, the clip plays silence until more comes.
    public class PcmStream : DownloadHandlerScript
    {
        // Longest sentence a clip can hold; streamed clips don't allocate this.
        const int MaxClipSeconds = 120;

        readonly object gate = new object();
        readonly List<float> samples = new List<float>();
        readonly byte[] header = new byte[4];
        int headerBytes;
        int readPosition;
        int lastByte = -1;  // odd byte left over from the previous piece
        int delivered;      // samples handed to the clip so far, silence included
        int endSample = -1; // clip position where the speech ends, once known
        int starved;        // silence played mid-sentence because generation fell behind
        int firstStarved = -1;

        public PcmStream() : base(new byte[16 * 1024]) { }

        /// 0 until the header has arrived.
        public int SampleRate { get; private set; }
        public bool Complete { get; private set; }

        public float BufferedSeconds
        {
            get
            {
                lock (gate) return SampleRate == 0 ? 0 : (samples.Count - readPosition) / (float)SampleRate;
            }
        }

        /// Clip sample at which the speech ends (-1 while it's still arriving or playing out).
        public int EndSample
        {
            get
            {
                lock (gate) return endSample;
            }
        }

        /// Seconds of silence played mid-sentence while waiting for generation.
        public float StarvedSeconds
        {
            get
            {
                lock (gate) return SampleRate == 0 ? 0 : starved / (float)SampleRate;
            }
        }

        /// Where the first gap was (seconds into the sentence) and how much had arrived by then - for diagnosing gaps.
        public string StarvedAt
        {
            get
            {
                lock (gate) return SampleRate == 0 ? "" : $"at {firstStarved / (float)SampleRate:0.00} s, {samples.Count / (float)SampleRate:0.00} s received, block {lastBlock}, complete {Complete};{starveLog}";
            }
        }

        /// Seconds of speech received so far.
        public float ReceivedSeconds
        {
            get
            {
                lock (gate) return SampleRate == 0 ? 0 : samples.Count / (float)SampleRate;
            }
        }

        /// How fast speech is arriving: seconds of audio per second of waiting, measured from the first piece on
        /// (below 1 = generated slower than it plays). Null until it's been watched long enough to tell.
        public float? ArrivalRate
        {
            get
            {
                lock (gate)
                {
                    double watched = arrivalClock.Elapsed.TotalSeconds;
                    if (SampleRate == 0 || !arrivalClock.IsRunning || watched < MinRateWatchSeconds) return null;
                    return (float)((samples.Count - firstPieceSamples) / (double)SampleRate / watched);
                }
            }
        }

        const double MinRateWatchSeconds = 0.6;
        readonly System.Diagnostics.Stopwatch arrivalClock = new System.Diagnostics.Stopwatch();
        int firstPieceSamples;

        int lastBlock;
        readonly System.Diagnostics.Stopwatch fillClock = new System.Diagnostics.Stopwatch();
        readonly System.Text.StringBuilder starveLog = new System.Text.StringBuilder();

        /// Copies samples from `position` on (zeros where nothing has arrived yet). Safe on the audio thread.
        public void CopyAt(int position, float[] dest, int count)
        {
            lock (gate)
            {
                for (int i = 0; i < count; i++)
                {
                    int p = position + i;
                    dest[i] = p >= 0 && p < samples.Count ? samples[p] : 0f;
                }
            }
        }

        /// Everything received so far (a copy).
        public float[] Samples()
        {
            lock (gate) return samples.ToArray();
        }

        public AudioClip CreateClip() =>
            AudioClip.Create("speech", SampleRate * MaxClipSeconds, 1, SampleRate, true, Fill);

        protected override bool ReceiveData(byte[] data, int length)
        {
            int i = 0;
            while (headerBytes < 4 && i < length) header[headerBytes++] = data[i++];
            if (headerBytes == 4 && SampleRate == 0) SampleRate = BitConverter.ToInt32(header, 0);

            lock (gate)
            {
                if (lastByte >= 0 && i < length)
                {
                    samples.Add((short)(lastByte | (data[i++] << 8)) / 32768f);
                    lastByte = -1;
                }
                for (; i + 1 < length; i += 2)
                    samples.Add((short)(data[i] | (data[i + 1] << 8)) / 32768f);
                if (i < length) lastByte = data[i];
                if (!arrivalClock.IsRunning && samples.Count > 0)
                {
                    arrivalClock.Start();
                    firstPieceSamples = samples.Count;
                }
            }
            return true;
        }

        protected override void CompleteContent()
        {
            lock (gate) Complete = true;
        }

        // Called by the clip (possibly off the main thread) for the next block of audio.
        void Fill(float[] data)
        {
            lock (gate)
            {
                lastBlock = data.Length;
                int count = Math.Min(data.Length, samples.Count - readPosition);
                samples.CopyTo(readPosition, data, 0, count);
                readPosition += count;
                Array.Clear(data, count, data.Length - count);
                if (!fillClock.IsRunning) fillClock.Start();
                if (Complete && endSample < 0 && readPosition == samples.Count) endSample = delivered + count;
                else if (!Complete && readPosition > 0 && count < data.Length)
                {
                    if (firstStarved < 0) firstStarved = delivered + count;
                    starved += data.Length - count;
                    if (starveLog.Length < 400)
                        starveLog.Append($" [{fillClock.ElapsedMilliseconds}ms: clip at {(delivered + count) / (float)SampleRate:0.00}s, had {samples.Count / (float)SampleRate:0.00}s]");
                }
                delivered += data.Length;
            }
        }
    }
}
