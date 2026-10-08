var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject llmGO = null;
foreach (var go in goRoots) { if (go.name == "LLM") llmGO = go; }
var llm = llmGO.GetComponent<LLMUnity.LLM>();

llm.flashAttention = true;
// No conversation history is kept (addToHistory:false), so we only ever need
// room for one system prompt + one question + one reply - 8192 was leftover
// default headroom we don't use, and it costs KV-cache alloc/init time.
llm.contextSize = 1024;

UnityEditor.EditorUtility.SetDirty(llmGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEngine.Debug.Log("LLM_TUNE: flashAttention=true, contextSize=1024");
