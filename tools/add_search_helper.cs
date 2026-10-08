var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject dialogueGO = null, llmGO = null;
foreach (var go in scene.GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "LLM") llmGO = go;
}
var llm = llmGO.GetComponent<LLMUnity.LLM>();
var existing = dialogueGO.transform.Find("SearchHelper");
var helperGO = existing != null ? existing.gameObject : new UnityEngine.GameObject("SearchHelper");
helperGO.transform.SetParent(dialogueGO.transform, false);
var helper = helperGO.GetComponent<LLMUnity.LLMAgent>() ?? helperGO.AddComponent<LLMUnity.LLMAgent>();
helper.llm = llm;
helper.numPredict = 80;
helper.temperature = 0.2f;

var translator = dialogueGO.transform.Find("Translator").GetComponent<LLMUnity.LLMAgent>();
translator.numPredict = 400;  // Devanagari costs several tokens per word; 256 cut long answers off

var so = new UnityEditor.SerializedObject(dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>());
so.FindProperty("searchHelper").objectReferenceValue = helper;
so.ApplyModifiedProperties();
UnityEditor.EditorUtility.SetDirty(helperGO);
UnityEditor.EditorUtility.SetDirty(translator);
UnityEditor.EditorUtility.SetDirty(dialogueGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log($"HELPER_OK: helper numPredict={helper.numPredict}, translator numPredict={translator.numPredict}");
