UnityEngine.GameObject outGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == "SpeechOutput") outGO = go;
var output = outGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
var src = outGO.GetComponent<UnityEngine.AudioSource>();
UnityEngine.Debug.Log($"RIB: runInBackground={UnityEngine.Application.runInBackground} playerSetting={UnityEditor.PlayerSettings.runInBackground} " +
    $"frame={UnityEngine.Time.frameCount} t={UnityEngine.Time.realtimeSinceStartup:0} busy={output.IsBusy} playing={src.isPlaying} clip={(src.clip ? src.clip.length.ToString("0.0") + "s" : "none")}");
