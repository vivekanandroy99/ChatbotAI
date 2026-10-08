var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject dialogueGO = null, llmGO = null;
foreach (var go in scene.GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "LLM") llmGO = go;
}
var llm = llmGO.GetComponent<LLMUnity.LLM>();
var existing = dialogueGO.transform.Find("Router");
var routerGO = existing != null ? existing.gameObject : new UnityEngine.GameObject("Router");
routerGO.transform.SetParent(dialogueGO.transform, false);
var router = routerGO.GetComponent<LLMUnity.LLMAgent>() ?? routerGO.AddComponent<LLMUnity.LLMAgent>();
router.llm = llm;
router.numPredict = 80;
router.temperature = 0.2f;

var so = new UnityEditor.SerializedObject(dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>());
so.FindProperty("router").objectReferenceValue = router;
so.ApplyModifiedProperties();
UnityEditor.EditorUtility.SetDirty(routerGO);
UnityEditor.EditorUtility.SetDirty(dialogueGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

// Avatar: topic names for redirects, the new refusal lines, and a more polished persona.
var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");
var defaults = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Dialogue.AvatarProfile>();
profile.topicNames = new System.Collections.Generic.List<string> { "Altscape", "Altcore" };
profile.refusalLines = new System.Collections.Generic.List<string>(defaults.refusalLines);
profile.hindiRefusalLines = new System.Collections.Generic.List<string>(defaults.hindiRefusalLines);
profile.personaPrompt =
    "You are AltBot, the friendly front-desk assistant for Altscape and Altcore. You are warm, confident and " +
    "knowledgeable, with a light touch of wit, and you talk like a helpful expert rather than a brochure. " +
    "Keep answers short and conversational, as if spoken out loud.";
UnityEngine.Object.DestroyImmediate(defaults);
UnityEditor.EditorUtility.SetDirty(profile);
UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
UnityEngine.Debug.Log($"ROUTER_OK: router wired, topics={string.Join(",", profile.topicNames)}, refusals={profile.refusalLines.Count}/{profile.hindiRefusalLines.Count}");
