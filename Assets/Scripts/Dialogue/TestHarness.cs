using UnityEngine;
using UnityEngine.UI;
using ChatbotAI.Audio;

namespace ChatbotAI.Dialogue
{
    /// Minimal text-in/text-out harness for manually verifying the dialogue
    /// pipeline (build-order step 4), plus push-to-talk mic input (step 5)
    /// feeding straight into the same dialogue brain.
    public class TestHarness : MonoBehaviour
    {
        [SerializeField] DialogueController controller;
        [SerializeField] InputField inputField;
        [SerializeField] Text outputText;
        [SerializeField] Button submitButton;

        [Header("Mic (optional)")]
        [SerializeField] SpeechInputController speechInput;
        [SerializeField] Text transcriptText;
        [SerializeField] Button startListenButton;
        [SerializeField] Button stopListenButton;
        [SerializeField] Button langEnglishButton;
        [SerializeField] Button langHindiButton;
        [SerializeField] Text langStatusText;

        [Header("Voice reply (optional)")]
        [SerializeField] Audio.SpeechOutputController speechOutput;
        [SerializeField] Audio.TTSProcessManager ttsProcessManager;
        [SerializeField] Text voiceStatusText;

        void Start()
        {
            submitButton.onClick.AddListener(Submit);
            inputField.onSubmit.AddListener(_ => Submit());
            controller.OnReply += reply => outputText.text = reply;

            if (speechOutput != null && voiceStatusText != null)
            {
                voiceStatusText.text = (ttsProcessManager != null && ttsProcessManager.IsReady)
                    ? "Voice engine ready."
                    : "Voice engine starting up (loading the TTS model)...";
                if (ttsProcessManager != null)
                    ttsProcessManager.OnReady += () => voiceStatusText.text = "Voice engine ready.";

                speechOutput.OnGenerating += () => voiceStatusText.text = "Generating voice reply...";
                speechOutput.OnSpeechStarted += () => voiceStatusText.text = "Speaking...";
                speechOutput.OnSpeechFinished += () => voiceStatusText.text = "";
                speechOutput.OnFailed += error => voiceStatusText.text = "Voice reply failed: " + error;
            }

            if (speechInput == null) return;
            startListenButton.onClick.AddListener(speechInput.StartListening);
            stopListenButton.onClick.AddListener(speechInput.StopListening);
            speechInput.OnListeningStarted += () => transcriptText.text = "Listening...";
            speechInput.OnListeningStopped += () => transcriptText.text = "Transcribing...";
            speechInput.OnLanguageDetected += lang => { if (langStatusText != null) langStatusText.text = "Detected: " + lang; };
            speechInput.OnTranscribed += HandleTranscribed;

            // Language auto-detects by default (SpeechInputController.InputLanguage.Auto).
            // These buttons are just a manual override for testing one model in isolation.
            if (langEnglishButton != null)
            {
                langEnglishButton.onClick.AddListener(() => speechInput.SetLanguage(SpeechInputController.InputLanguage.English));
                langHindiButton.onClick.AddListener(() => speechInput.SetLanguage(SpeechInputController.InputLanguage.Hindi));
            }
            if (langStatusText != null) langStatusText.text = "Mic language: Auto-detect";
        }

        void HandleTranscribed(string text)
        {
            transcriptText.text = "You said: " + text;
            outputText.text = "...";
            controller.Ask(text);
        }

        void Submit()
        {
            string text = inputField.text;
            if (string.IsNullOrWhiteSpace(text)) return;
            outputText.text = "...";
            controller.Ask(text);
            inputField.text = "";
        }
    }
}
