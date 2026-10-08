var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, whisperHiGO = null;
foreach (var go in roots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
}

var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();

var soEn = new UnityEditor.SerializedObject(whisperEn);
soEn.FindProperty("useGpu").boolValue = false;
soEn.ApplyModifiedProperties();

var soHi = new UnityEditor.SerializedObject(whisperHi);
soHi.FindProperty("useGpu").boolValue = false;
soHi.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(whisperEnGO);
UnityEditor.EditorUtility.SetDirty(whisperHiGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

UnityEngine.Debug.Log("CRASH_FIX: Whisper GPU (Vulkan) disabled on both models. LLM stays on CUDA/cublas only - no more concurrent CUDA+Vulkan contexts on the GPU.");
