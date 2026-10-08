using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Whisper;

namespace ChatbotAI.Audio
{
    /// Decides English vs Hindi from Whisper's per-language probabilities.
    /// whisper.unity only reports the single most likely of ~100 languages,
    /// which let spoken Hindi come out as Urdu script (or once, Korean). Here
    /// every other language is ignored and Urdu counts as Hindi - it's the same
    /// spoken language in a different script. whisper.unity doesn't bind these
    /// two functions, so they're called directly on the context it already
    /// loaded, under its own lock.
    public static class WhisperLanguageDetector
    {
        public struct Result
        {
            public bool ok;
            public float english;
            /// Hindi and the languages Whisper confuses spoken Hindi with (see HindiLike).
            public float hindi;
            /// Hindi and Urdu only (how it was counted before).
            public float hindiOnly;
            public bool IsEnglish => english >= hindi;
        }

        // Spoken Hindi - especially Hinglish, half English words - is often scored as a neighbouring language
        // (Marathi, Nepali, Punjabi...) as well; counting only Hindi + Urdu, English sometimes "won" and the Hindi
        // question came back transcribed as an English translation, answered in English.
        static readonly string[] HindiLike = { "hi", "ur", "mr", "ne", "pa", "gu", "bn", "sa" };

        [DllImport("libwhisper")]
        static extern int whisper_pcm_to_mel(IntPtr ctx, float[] samples, int nSamples, int nThreads);

        [DllImport("libwhisper")]
        static extern int whisper_lang_auto_detect(IntPtr ctx, int offsetMs, int nThreads, float[] langProbs);

        [DllImport("libwhisper")]
        static extern int whisper_lang_max_id();

        [DllImport("libwhisper")]
        static extern int whisper_lang_id(string lang);

        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        /// Blocking (runs the audio encoder) - call off the main thread.
        /// samples must be 16 kHz mono, which is what MicrophoneRecord records.
        public static Result Detect(WhisperManager manager, float[] samples, int threads)
        {
            var wrapper = typeof(WhisperManager).GetField("_whisper", Private)?.GetValue(manager) as WhisperWrapper;
            if (wrapper == null) return default;
            var ctx = (IntPtr)(typeof(WhisperWrapper).GetField("_whisperCtx", Private)?.GetValue(wrapper) ?? IntPtr.Zero);
            object sync = typeof(WhisperWrapper).GetField("_lock", Private)?.GetValue(wrapper);
            if (ctx == IntPtr.Zero || sync == null) return default;

            lock (sync)
            {
                if (whisper_pcm_to_mel(ctx, samples, samples.Length, threads) != 0) return default;
                var probs = new float[whisper_lang_max_id() + 1];
                if (whisper_lang_auto_detect(ctx, 0, threads, probs) < 0) return default;

                float hindiLike = 0f;
                foreach (string lang in HindiLike)
                {
                    int id = whisper_lang_id(lang);
                    if (id >= 0 && id < probs.Length) hindiLike += probs[id];
                }
                return new Result
                {
                    ok = true,
                    english = probs[whisper_lang_id("en")],
                    hindi = hindiLike,
                    hindiOnly = probs[whisper_lang_id("hi")] + probs[whisper_lang_id("ur")],
                };
            }
        }
    }
}
