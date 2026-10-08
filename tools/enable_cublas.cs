LLMUnity.LLMUnitySetup.SetCUBLAS(true);
LLMUnity.LLMUnitySetup.DebugMode = LLMUnity.LLMUnitySetup.DebugModeType.Debug;

var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject llmGO = null;
foreach (var go in roots)
{
    if (go.name == "LLM") llmGO = go;
}
var llmComp = llmGO.GetComponent<LLMUnity.LLM>();
llmComp.numGPULayers = 999;

UnityEditor.EditorUtility.SetDirty(llmGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("CUBLAS enabled (was excluded by default), debug logging maxed, numGPULayers=999. Will take effect on next domain reload / Play.");
