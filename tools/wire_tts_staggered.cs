var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, whisperHiGO = null, ttsProcGO = null, bootstrapGO = null;
foreach (var go in roots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
    if (go.name == "TTSProcess") ttsProcGO = go;
    if (go.name == "GpuBootstrap") bootstrapGO = go;
}

var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();
var bootstrap = bootstrapGO.GetComponent<ChatbotAI.Audio.StaggeredGpuBootstrap>();

// TTSProcess starts disabled - only activates once both Whisper models
// report IsLoaded, so PyTorch/CUDA never inits at the same instant as
// llama.cpp's CUDA or Whisper's Vulkan.
ttsProcGO.SetActive(false);

var so = new UnityEditor.SerializedObject(bootstrap);
var waitForLoadedProp = so.FindProperty("waitForLoaded");
waitForLoadedProp.arraySize = 2;
waitForLoadedProp.GetArrayElementAtIndex(0).objectReferenceValue = whisperEn;
waitForLoadedProp.GetArrayElementAtIndex(1).objectReferenceValue = whisperHi;

var secondStageProp = so.FindProperty("secondStageGameObjects");
secondStageProp.arraySize = 1;
secondStageProp.GetArrayElementAtIndex(0).objectReferenceValue = ttsProcGO;
so.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(bootstrapGO);
UnityEditor.EditorUtility.SetDirty(ttsProcGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

UnityEngine.Debug.Log("WIRE_TTS_STAGGERED_OK: TTSProcess now waits for both Whisper models to finish loading before launching.");
