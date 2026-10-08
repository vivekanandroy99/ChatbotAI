var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject whisperHindiGO = null;
foreach (var go in goRoots)
{
    if (go.name == "WhisperHindi") { whisperHindiGO = go; break; }
}

if (whisperHindiGO == null)
{
    UnityEngine.Debug.LogError("WhisperHindi GameObject not found in active scene!");
}
else
{
    var whisperHi = whisperHindiGO.GetComponent<Whisper.WhisperManager>();
    whisperHi.ModelPath = "Models/Whisper/ggml-hindi-medium-f16.bin";
    UnityEditor.EditorUtility.SetDirty(whisperHindiGO);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    UnityEngine.Debug.Log("Updated WhisperHindi ModelPath to Models/Whisper/ggml-hindi-medium-f16.bin and saved scene.");
}
