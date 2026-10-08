UnityEngine.GameObject dialogueGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == "DialogueBrain") dialogueGO = go;
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
controller.OnReply += r => UnityEngine.Debug.Log("ASK_ONE reply: " + r);
controller.Ask("How can I contact Altcore?");
