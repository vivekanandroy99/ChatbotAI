string path = UnityEngine.Application.streamingAssetsPath + "/Models/LLM/Qwen2.5-7B-Instruct-Q4_K_M.gguf";
var header = new byte[4];
using (var fs = System.IO.File.OpenRead(path)) fs.Read(header, 0, 4);
string magic = System.Text.Encoding.ASCII.GetString(header);

LLMUnity.LLMManager.LoadModel(path, true, "Qwen2.5-7B-Instruct");

var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject llmGO = null;
foreach (var go in scene.GetRootGameObjects()) if (go.name == "LLM") llmGO = go;
var selector = llmGO.GetComponent<ChatbotAI.Dialogue.LLMModelSelector>();
var so = new UnityEditor.SerializedObject(selector);
so.FindProperty("model").enumValueIndex = (int)ChatbotAI.Dialogue.LLMModelSelector.ModelChoice.Best;
so.FindProperty("bestModel").stringValue = "Models/LLM/Qwen2.5-7B-Instruct-Q4_K_M.gguf";
so.ApplyModifiedProperties();
var llm = llmGO.GetComponent<LLMUnity.LLM>();
llm.model = selector.SelectedModelPath;
UnityEditor.EditorUtility.SetDirty(llmGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

var sb = new System.Text.StringBuilder($"REG_7B: magic={magic} llm.model={llm.model} contextSize={llm.contextSize}\n");
foreach (var e in LLMUnity.LLMManager.modelEntries) sb.Append($"  {e.filename} includeInBuild={e.includeInBuild}\n");
UnityEngine.Debug.Log(sb.ToString());
