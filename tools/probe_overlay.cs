UnityEngine.GameObject canvasGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == "Canvas") canvasGO = go;
var ui = canvasGO.transform.Find("StartupOverlay").GetComponent<ChatbotAI.UI.StartupProgressUI>();
var t = ui.GetType();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var engine = (ChatbotAI.Audio.TTSProcessManager)t.GetField("engine", flags).GetValue(ui);
var models = (Whisper.WhisperManager[])t.GetField("speechModels", flags).GetValue(ui);
string ms = "";
foreach (var m in models) ms += $"{m.name}:{m.IsLoaded} ";
UnityEngine.Debug.Log($"PROBE: frame={UnityEngine.Time.frameCount} enabled={ui.enabled} activeInHierarchy={ui.gameObject.activeInHierarchy} " +
    $"llmReady={t.GetField("llmReady", flags).GetValue(ui)} shown={t.GetField("shown", flags).GetValue(ui)} readyAt={t.GetField("readyAt", flags).GetValue(ui)} " +
    $"engine.IsReady={engine.IsReady} engine.Progress={engine.Progress} engine.Stage='{engine.Stage}' engine.HasFailed={engine.HasFailed} speech=[{ms}]");
