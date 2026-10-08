UnityEngine.GameObject dialogueGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    if (go.name == "DialogueBrain") dialogueGO = go;
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();

string[] questions =
{
    "What are your store hours?",
    "Is water damage covered?",
    "Is there a fee to check my laptop?",
    "क्या पानी से हुआ नुकसान वारंटी में शामिल है?",
    "लैपटॉप की वारंटी कितने साल की है?",
    "Does the Nova Phone X support 5G?",
    "Who won the cricket world cup?",
};

async void RunAll()
{
    foreach (var q in questions)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
        System.Action<string> handler = r => tcs.TrySetResult(r);
        controller.OnReply += handler;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        controller.Ask(q);
        string reply = await tcs.Task;
        controller.OnReply -= handler;
        UnityEngine.Debug.Log($"KB_TEST ({sw.ElapsedMilliseconds}ms) Q: {q}\n   A: {reply}");
    }
    UnityEngine.Debug.Log("KB_TEST_DONE");
}
RunAll();
