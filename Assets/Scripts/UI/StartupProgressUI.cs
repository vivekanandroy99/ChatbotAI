using ChatbotAI.Audio;
using ChatbotAI.Dialogue;
using UnityEngine;
using UnityEngine.UI;
using Whisper;

namespace ChatbotAI.UI
{
    /// Full-screen loading overlay for the whole startup chain (language model
    /// -> speech recognition -> voice & knowledge engine), with an elapsed-time
    /// counter. Sits on the overlay panel itself and blocks input until
    /// everything is ready, since no question can be answered before then.
    public class StartupProgressUI : MonoBehaviour
    {
        [SerializeField] DialogueController dialogueController;
        [SerializeField] WhisperManager[] speechModels;
        [SerializeField] TTSProcessManager engine;

        [Header("UI")]
        [SerializeField] RectTransform barFill;
        [SerializeField] Text statusText;
        [SerializeField] float hideDelaySeconds = 1.5f;

        StartupProgress progress;
        float readyAt = -1f;

        void Awake() => progress = new StartupProgress(dialogueController, speechModels, engine);

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (readyAt >= 0f)
            {
                if (now - readyAt > hideDelaySeconds) gameObject.SetActive(false);
                return;
            }

            progress.Update();
            if (progress.HasFailed)
            {
                statusText.color = new Color(1f, 0.45f, 0.45f);
                statusText.text = progress.FailureMessage;
                return;
            }

            barFill.anchorMax = new Vector2(progress.Shown, 1f);
            if (progress.IsReady)
            {
                readyAt = now;
                statusText.text = $"Ready - started in {progress.Elapsed:0.0}s";
                Debug.Log($"StartupProgressUI: everything loaded in {progress.Elapsed:0.0}s");
                return;
            }
            statusText.text = $"{progress.Stage}...   {Mathf.RoundToInt(progress.Shown * 100)}%   ({progress.Elapsed:0}s)";
        }
    }
}
