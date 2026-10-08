var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject llmGO = null;
foreach (var go in roots)
{
    if (go.name == "LLM") llmGO = go;
}

// numGPULayers > 0 never actually reaches the GPU on this system (Blackwell
// not supported by the bundled LlamaLib), but it DOES change which CPU
// backend gets picked - it lands on the slower "tinyblas" instead of the
// properly-optimized "avx512" that gets picked when GPU mode is off. So
// leaving it on made things slower for no benefit.
var llmComp = llmGO.GetComponent<LLMUnity.LLM>();
llmComp.numGPULayers = 0;

// Recreate the placeholder AvatarProfile from the updated script defaults
// (gateConfidenceThreshold removed, gate is now hit-count based).
string profilePath = "Assets/Resources/Avatars/DefaultAvatar.asset";
UnityEditor.AssetDatabase.DeleteAsset(profilePath);
var profile = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Dialogue.AvatarProfile>();
UnityEditor.AssetDatabase.CreateAsset(profile, profilePath);
UnityEditor.AssetDatabase.SaveAssets();

UnityEditor.EditorUtility.SetDirty(llmGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("numGPULayers reset to 0 (avoids tinyblas regression); AvatarProfile regenerated with fixed gate.");
