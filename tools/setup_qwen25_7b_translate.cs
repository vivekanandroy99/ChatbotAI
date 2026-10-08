string rel = "Models/LLM/Qwen2.5-7B-Instruct-Q4_K_M.gguf";
string path = UnityEngine.Application.streamingAssetsPath + "/" + rel;
LLMUnity.LLMManager.LoadModel(path, true, "setup_qwen25_7b_translate");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject llmGO = null, brain = null;
foreach (var go in scene.GetRootGameObjects()) { if (go.name == "LLM") llmGO = go; if (go.name == "DialogueBrain") brain = go; }
var llm = llmGO.GetComponent<LLMUnity.LLM>();
llm.model = rel;
UnityEditor.EditorUtility.SetDirty(llmGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("SETUP: model=" + llm.model + " hindiReplies=0");