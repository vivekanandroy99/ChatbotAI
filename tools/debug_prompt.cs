UnityEngine.GameObject dialogueGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == "DialogueBrain") dialogueGO = go;
var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();

var f = agent.GetType().GetField("debugPrompt", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
UnityEngine.Debug.Log($"DBG: debugPrompt field={(f == null ? "missing" : f.FieldType.Name)} historyCount={agent.chat?.Count}");
if (f != null) f.SetValue(agent, true);
foreach (var m in agent.chat) UnityEngine.Debug.Log($"DBG history [{m.role}]: {m.content.Substring(0, System.Math.Min(80, m.content.Length))}");
controller.Ask("When was altcore established?");
