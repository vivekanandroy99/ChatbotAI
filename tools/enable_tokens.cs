var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, whisperHiGO = null;
foreach (var go in roots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
}

var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
whisperEn.enableTokens = true;

var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();
whisperHi.enableTokens = true;

UnityEditor.EditorUtility.SetDirty(whisperEnGO);
UnityEditor.EditorUtility.SetDirty(whisperHiGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("enableTokens set on both WhisperManagers for confidence-based language auto-detect.");
