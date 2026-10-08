using ChatbotAI.Audio;
using ChatbotAI.Dialogue;
using UnityEngine;
using Whisper;

namespace ChatbotAI.UI
{
    /// How far the startup chain has got (language model -> speech recognition -> voice & knowledge
    /// engine), for loading screens. Call Update() once a frame.
    public class StartupProgress
    {
        // Rough share of total startup time each stage takes.
        const float LlmWeight = 0.15f;
        const float SpeechWeight = 0.15f;
        const float EngineWeight = 0.70f;

        readonly DialogueController dialogue;
        readonly WhisperManager[] speechModels;
        readonly TTSProcessManager engine;
        readonly float startTime = Time.realtimeSinceStartup;

        /// 0-1, eased so it never jumps back or leaps ahead.
        public float Shown { get; private set; }
        public bool IsReady { get; private set; }
        public bool HasFailed => engine && engine.HasFailed;
        public string FailureMessage => engine ? engine.FailureMessage : "";
        /// What's loading now ("Loading language model"...).
        public string Stage { get; private set; } = "Starting";
        public float Elapsed => Time.realtimeSinceStartup - startTime;

        /// The three parts the loading screen lists: the language model, speech recognition, voice & knowledge.
        public bool BrainReady { get; private set; }
        public bool EarsReady { get; private set; }
        public bool VoiceReady { get; private set; }

        public StartupProgress(DialogueController dialogue, WhisperManager[] speechModels, TTSProcessManager engine)
        {
            this.dialogue = dialogue;
            this.speechModels = speechModels;
            this.engine = engine;
        }

        public void Update()
        {
            if (IsReady || HasFailed) return;
            bool llmReady = dialogue && dialogue.IsWarmedUp;

            int loadedSpeech = 0, usedSpeech = 0;
            foreach (var m in speechModels)
            {
                if (m == null || !WhisperLoading.LoadsOnStart(m)) continue;  // unused models never load
                usedSpeech++;
                if (m.IsLoaded) loadedSpeech++;
            }
            float speech = usedSpeech == 0 ? 1f : (float)loadedSpeech / usedSpeech;
            bool engineReady = !engine || engine.IsReady;
            float engineProgress = engineReady ? 1f : engine.Progress;
            BrainReady = llmReady;
            EarsReady = speech >= 1f;
            VoiceReady = engineReady;

            if (llmReady && speech >= 1f && engineReady)
            {
                IsReady = true;
                Shown = 1f;
                Stage = "Ready";
                return;
            }

            float target = (llmReady ? LlmWeight : 0f) + speech * SpeechWeight + engineProgress * EngineWeight;
            Shown = Mathf.MoveTowards(Shown, target, Time.unscaledDeltaTime * 0.5f);
            Stage = !llmReady ? "Loading language model"
                : speech < 1f ? "Loading speech recognition"
                : engine.Stage;
        }
    }
}
