var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject llmGO = null;
foreach (var go in roots)
{
    if (go.name == "LLM") llmGO = go;
}

var llmComp = llmGO.GetComponent<LLMUnity.LLM>();
string fullPath = System.IO.Path.GetFullPath("Assets/StreamingAssets/Models/LLM/Qwen2.5-1.5B-Instruct-Q4_K_M.gguf");
string filename = LLMUnity.LLMManager.LoadModel(fullPath, true, "Qwen2.5-1.5B-Instruct-Q4_K_M");
llmComp.model = filename;

UnityEditor.EditorUtility.SetDirty(llmGO);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("Swapped LLM to Qwen2.5-1.5B-Instruct: " + filename);
