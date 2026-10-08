UnityEngine.GameObject dialogueGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    if (go.name == "DialogueBrain") dialogueGO = go;
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
string[] questions = { "Do you charge a diagnosis fee?", "How much does it cost to diagnose a broken laptop?" };
async void RunAll()
{
    foreach (var q in questions)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
        System.Action<string> handler = r => tcs.TrySetResult(r);
        controller.OnReply += handler;
        controller.Ask(q);
        string reply = await tcs.Task;
        controller.OnReply -= handler;
        UnityEngine.Debug.Log($"ONE_TEST Q: {q}\n   A: {reply}");
    }
    UnityEngine.Debug.Log("ONE_TEST_DONE");
}
RunAll();
