var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, whisperHiGO = null;
foreach (var go in roots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
}

var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var whisperEnSO = new UnityEditor.SerializedObject(whisperEn);
whisperEnSO.FindProperty("useGpu").boolValue = false;
whisperEnSO.ApplyModifiedProperties();

var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();
var whisperHiSO = new UnityEditor.SerializedObject(whisperHi);
whisperHiSO.FindProperty("useGpu").boolValue = false;
whisperHiSO.ApplyModifiedProperties();

UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("Disabled useGpu on both WhisperManagers (ggml-vulkan crashed on concurrent access) - back to CPU backend.");
