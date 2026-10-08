using System;
using UnityEngine;

namespace ChatbotAI.Audio
{
    /// Minimal 16-bit PCM WAV decoder - just enough to turn the bytes the
    /// local TTS sidecar returns into a playable AudioClip, without pulling
    /// in a full audio library for one format.
    public static class WavUtility
    {
        public static AudioClip ToAudioClip(byte[] wavBytes)
        {
            const int headerSizeMin = 44;
            if (wavBytes == null || wavBytes.Length < headerSizeMin) return null;

            int channels = BitConverter.ToInt16(wavBytes, 22);
            int sampleRate = BitConverter.ToInt32(wavBytes, 24);
            int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

            int pos = 12;
            int dataStart = -1, dataSize = -1;
            while (pos + 8 <= wavBytes.Length)
            {
                string chunkId = System.Text.Encoding.ASCII.GetString(wavBytes, pos, 4);
                int chunkSize = BitConverter.ToInt32(wavBytes, pos + 4);
                if (chunkId == "data")
                {
                    dataStart = pos + 8;
                    dataSize = chunkSize;
                    break;
                }
                pos += 8 + chunkSize;
            }
            if (dataStart < 0) return null;

            int bytesPerSample = bitsPerSample / 8;
            int sampleCount = dataSize / bytesPerSample;
            float[] samples = new float[sampleCount];

            if (bitsPerSample == 16)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    short raw = BitConverter.ToInt16(wavBytes, dataStart + i * 2);
                    samples[i] = raw / 32768f;
                }
            }
            else
            {
                Debug.LogError($"WavUtility: unsupported bits-per-sample {bitsPerSample}");
                return null;
            }

            AudioClip clip = AudioClip.Create("TTSClip", sampleCount / channels, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
