var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject dialogueGO = null;
foreach (var go in goRoots)
{
    if (go.name == "DialogueBrain") { dialogueGO = go; break; }
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var sw = System.Diagnostics.Stopwatch.StartNew();
controller.OnReply += (reply) => {
    sw.Stop();
    UnityEngine.Debug.Log($"GPU_TEST_REPLY2 ({sw.ElapsedMilliseconds}ms): {reply}");
};
controller.Ask("What is your warranty policy?");
UnityEngine.Debug.Log("GPU Ask2() triggered.");
