using UnityEngine;

namespace ChatbotAI.Audio
{
    /// Feeds uLipSync the speech a little AHEAD of what is audible. uLipSync reacts only after it
    /// has heard a window of audio, and the mouth then trails the voice (measured ~90-105 ms; viewers
    /// notice past ~45 ms). The voice's audio is already here before it plays (PcmStream), so this
    /// hands uLipSync the samples `lookaheadSeconds` in the future, lined up with the audio clock.
    /// Sits on the voice's AudioSource object (OnAudioFilterRead runs in step with playback); the
    /// uLipSync itself must be on another object so it doesn't also analyse the live output.
    [RequireComponent(typeof(AudioSource))]
    public class LipSyncLookahead : MonoBehaviour
    {
        [SerializeField] SpeechOutputController speechOutput;
        [SerializeField] uLipSync.uLipSync lipSync;
        [Tooltip("How far ahead of the audible voice the mouth is analysed.")]
        [Tunable("Mouth & lip-sync", "Mouth ahead of the voice", 0f, 0.2f, 0.005f, unit = "s",
                 note = "Mouths are read ahead of the audio so they move with it; ~0.07 s looks right.")]
        [SerializeField] float lookaheadSeconds = 0.07f;

        int outputRate;
        float[] feed = new float[0], source = new float[0];

        void Awake() => outputRate = AudioSettings.outputSampleRate;

        void OnAudioFilterRead(float[] data, int channels)
        {
            int frames = data.Length / channels;
            if (feed.Length != frames) feed = new float[frames];
            System.Array.Clear(feed, 0, frames);

            var stream = speechOutput ? speechOutput.PlayingStream : null;
            int rate = stream != null ? stream.SampleRate : 0;
            if (rate > 0)
            {
                // This block starts `now` on the audio clock; read the clip from `now + lookahead`.
                double seconds = AudioSettings.dspTime - speechOutput.PlayingStartDsp + lookaheadSeconds;
                double step = (double)rate / outputRate;
                double first = seconds * rate;
                int from = (int)System.Math.Floor(first);
                int count = (int)System.Math.Ceiling(frames * step) + 2;
                if (source.Length < count) source = new float[count];
                stream.CopyAt(from, source, count);
                for (int i = 0; i < frames; i++)
                {
                    double p = first - from + i * step;
                    int k = (int)p;
                    float f = (float)(p - k);
                    feed[i] = k + 1 < count ? source[k] * (1 - f) + source[k + 1] * f : 0f;
                }
            }
            if (lipSync) lipSync.OnDataReceived(feed, 1);
        }
    }
}
