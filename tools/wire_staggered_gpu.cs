var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, whisperHiGO = null, dialogueGO = null;
foreach (var go in roots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
    if (go.name == "DialogueBrain") dialogueGO = go;
}

// Re-enable GPU (Vulkan) on both Whisper models - safe now that init is staggered.
var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var soEn = new UnityEditor.SerializedObject(whisperEn);
soEn.FindProperty("useGpu").boolValue = true;
soEn.ApplyModifiedProperties();

var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();
var soHi = new UnityEditor.SerializedObject(whisperHi);
soHi.FindProperty("useGpu").boolValue = true;
soHi.ApplyModifiedProperties();

// Whisper GameObjects start disabled - StaggeredGpuBootstrap activates them
// only after the LLM finishes loading, so Vulkan never inits at the same
// instant as CUDA.
whisperEnGO.SetActive(false);
whisperHiGO.SetActive(false);

var dialogueController = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();

var bootstrapGO = UnityEngine.GameObject.Find("GpuBootstrap");
if (bootstrapGO == null)
{
    bootstrapGO = new UnityEngine.GameObject("GpuBootstrap");
}
var bootstrap = bootstrapGO.GetComponent<ChatbotAI.Audio.StaggeredGpuBootstrap>();
if (bootstrap == null)
{
    bootstrap = bootstrapGO.AddComponent<ChatbotAI.Audio.StaggeredGpuBootstrap>();
}

var soBootstrap = new UnityEditor.SerializedObject(bootstrap);
soBootstrap.FindProperty("dialogueController").objectReferenceValue = dialogueController;
var deferredProp = soBootstrap.FindProperty("deferredGameObjects");
deferredProp.arraySize = 2;
deferredProp.GetArrayElementAtIndex(0).objectReferenceValue = whisperEnGO;
deferredProp.GetArrayElementAtIndex(1).objectReferenceValue = whisperHiGO;
soBootstrap.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(bootstrapGO);
UnityEditor.EditorUtility.SetDirty(whisperEnGO);
UnityEditor.EditorUtility.SetDirty(whisperHiGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

UnityEngine.Debug.Log("WIRE_STAGGERED_GPU_OK: Whisper GPU re-enabled, staggered activation wired via GpuBootstrap.");
