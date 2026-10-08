using UnityEngine;

namespace ChatbotAI.Audio
{
    /// Caps rendering at 60 fps. The graphics card is shared with the AI models (language
    /// model, Whisper, and the voice server's Veena, which has to generate speech faster than
    /// it plays); left uncapped, Unity drew ~900 fps and Veena fell behind real time
    /// (1.1x - audible gaps) where it otherwise runs at ~0.6x.
    static class FrameRateCap
    {
        public const int TargetFps = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
        }
    }
}
