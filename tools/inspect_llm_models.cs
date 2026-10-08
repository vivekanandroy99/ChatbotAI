UnityEngine.GameObject llmGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == "LLM") llmGO = go;
var llm = llmGO.GetComponent<LLMUnity.LLM>();
var sb = new System.Text.StringBuilder();
sb.Append($"LLM_MODELS: current model='{llm.model}' contextSize={llm.contextSize}\n");
foreach (var entry in LLMUnity.LLMManager.modelEntries)
    sb.Append($"  entry filename='{entry.filename}' path='{entry.path}' lora={entry.lora} includeInBuild={entry.includeInBuild}\n");
UnityEngine.Debug.Log(sb.ToString());
