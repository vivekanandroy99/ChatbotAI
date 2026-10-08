var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject dialogueGO = null, speechOutGO = null;
foreach (var go in goRoots)
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "SpeechOutput") speechOutGO = go;
}
var dialogueController = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var audioSource = speechOutGO.GetComponent<UnityEngine.AudioSource>();

dialogueController.OnReply += (reply) => {
    UnityEngine.Debug.Log($"E2E_REPLY_RECEIVED: [{reply}]");
};

dialogueController.Ask("What are your store hours?");
UnityEngine.Debug.Log("E2E_TEST_TRIGGERED");
