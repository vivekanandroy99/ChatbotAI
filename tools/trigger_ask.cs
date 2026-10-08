var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject dialogueGO = null;
foreach (var go in goRoots)
{
    if (go.name == "DialogueBrain") { dialogueGO = go; break; }
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
controller.OnReply += (reply) => UnityEngine.Debug.Log("TEST_REPLY: " + reply);
controller.Ask("What are your store hours?");
UnityEngine.Debug.Log("Ask() triggered.");
