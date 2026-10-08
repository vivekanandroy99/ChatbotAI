var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null;
foreach (var go in roots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
}

var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var so = new UnityEditor.SerializedObject(whisperEn);
so.FindProperty("useGpu").boolValue = true;
so.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(whisperEnGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("Enabled useGpu on WhisperEnglish only, for isolated single-context testing.");
