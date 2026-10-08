var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject dialogueGO = null, llmGO = null;
foreach (var go in scene.GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "LLM") llmGO = go;
}
var mainAgent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();

var existing = dialogueGO.transform.Find("Translator");
var translatorGO = existing != null ? existing.gameObject : new UnityEngine.GameObject("Translator");
translatorGO.transform.SetParent(dialogueGO.transform, false);
var translator = translatorGO.GetComponent<LLMUnity.LLMAgent>() ?? translatorGO.AddComponent<LLMUnity.LLMAgent>();
translator.llm = llmGO.GetComponent<LLMUnity.LLM>();
translator.numPredict = 256;
translator.temperature = mainAgent.temperature;

var so = new UnityEditor.SerializedObject(dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>());
so.FindProperty("translator").objectReferenceValue = translator;
so.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(translatorGO);
UnityEditor.EditorUtility.SetDirty(dialogueGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log($"TRANSLATOR_OK: llm={translator.llm?.name} numPredict={translator.numPredict} temp={translator.temperature}");
