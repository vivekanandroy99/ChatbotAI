var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject whisperHiGO = null;
foreach (var go in roots)
{
    if (go.name == "WhisperHindi") whisperHiGO = go;
}

var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();
var so = new UnityEditor.SerializedObject(whisperHi);
so.FindProperty("useGpu").boolValue = true;
so.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(whisperHiGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("Enabled useGpu on WhisperHindi too.");
