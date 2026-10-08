var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject llmGO = null;
foreach (var go in scene.GetRootGameObjects()) { if (go.name == "LLM") llmGO = go; }
var llm = llmGO.GetComponent<LLMUnity.LLM>();
// Room for persona + rules + up to ~10 document passages + question + reply.
llm.contextSize = 4096;
UnityEditor.EditorUtility.SetDirty(llmGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");
UnityEditor.EditorUtility.SetDirty(profile);
UnityEditor.AssetDatabase.SaveAssets();

UnityEngine.Debug.Log($"KNOWLEDGE_SCENE_OK: contextSize={llm.contextSize}, avatar folder={profile.KnowledgeFolderPath}, persona starts '{profile.personaPrompt.Substring(0, 30)}'");
