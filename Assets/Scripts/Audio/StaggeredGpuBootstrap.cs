using System.Collections;
using UnityEngine;
using ChatbotAI.Dialogue;
using Whisper;

namespace ChatbotAI.Audio
{
    /// Three different GPU backends are in play here - LLM (CUDA/cublas),
    /// Whisper (Vulkan), and TTS (PyTorch/CUDA, in a separate process). They
    /// must never initialize their GPU contexts at the same instant - doing
    /// so once already crashed the whole PC (a driver-level fault, confirmed
    /// via Windows event log, not a Unity exception). So loading is staged
    /// strictly one backend at a time: LLM loads+warms up first, then
    /// deferredGameObjects (the Whisper managers) activate, then once those
    /// report IsLoaded, secondStageGameObjects (the TTS sidecar launcher)
    /// activates. Actual runtime inference is already sequential
    /// (mic -> STT -> LLM -> TTS), so this only affects the one-time
    /// startup race.
    public class StaggeredGpuBootstrap : MonoBehaviour
    {
        [SerializeField] DialogueController dialogueController;
        [SerializeField] GameObject[] deferredGameObjects;
        [SerializeField] WhisperManager[] waitForLoaded;
        [SerializeField] GameObject[] secondStageGameObjects;

        void OnEnable()
        {
            dialogueController.OnWarmupComplete += ActivateDeferred;
        }

        void OnDisable()
        {
            dialogueController.OnWarmupComplete -= ActivateDeferred;
        }

        void ActivateDeferred()
        {
            Debug.Log("StaggeredGpuBootstrap: ActivateDeferred - activating Whisper GameObjects.");
            foreach (var go in deferredGameObjects) go.SetActive(true);
            if (secondStageGameObjects != null && secondStageGameObjects.Length > 0)
            {
                Debug.Log($"StaggeredGpuBootstrap: starting second-stage wait for {waitForLoaded.Length} manager(s).");
                StartCoroutine(WaitThenActivateSecondStage());
            }
            else
            {
                Debug.Log("StaggeredGpuBootstrap: no second-stage GameObjects configured - skipping.");
            }
        }

        IEnumerator WaitThenActivateSecondStage()
        {
            foreach (var manager in waitForLoaded)
            {
                Debug.Log($"StaggeredGpuBootstrap: waiting on {manager.name}.IsLoaded (currently {manager.IsLoaded}).");
                // (A model that isn't used is never loaded - see SpeechInputController.)
                yield return new WaitUntil(() => manager.IsLoaded || !WhisperLoading.LoadsOnStart(manager));
                Debug.Log($"StaggeredGpuBootstrap: {manager.name} is loaded.");
            }

            Debug.Log("StaggeredGpuBootstrap: activating second-stage GameObjects.");
            foreach (var go in secondStageGameObjects) go.SetActive(true);
        }
    }
}
