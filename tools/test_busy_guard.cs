var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject dialogueGO = null;
foreach (var go in goRoots) { if (go.name == "DialogueBrain") dialogueGO = go; }
var dialogueController = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();

// Simulate the user asking a second question while the first TTS reply is still generating.
dialogueController.Ask("Are you open on Sunday?");
UnityEngine.Debug.Log("BUSY_GUARD_TEST_SECOND_ASK_TRIGGERED");
