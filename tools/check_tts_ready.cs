var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject ttsProcGO = null;
foreach (var go in goRoots) { if (go.name == "TTSProcess") ttsProcGO = go; }
var ttsProc = ttsProcGO.GetComponent<ChatbotAI.Audio.TTSProcessManager>();
UnityEngine.Debug.Log($"TTS_READY_CHECK: IsReady={ttsProc.IsReady} activeInHierarchy={ttsProcGO.activeInHierarchy}");
