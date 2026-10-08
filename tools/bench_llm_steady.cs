var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject dialogueGO = null;
foreach (var go in goRoots) { if (go.name == "DialogueBrain") dialogueGO = go; }
var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();

async void RunBench()
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    string reply = await agent.Chat("Are you open on weekends?", null, null, addToHistory: false);
    UnityEngine.Debug.Log($"BENCH_STEADY_LLM ({sw.ElapsedMilliseconds}ms): [{reply}]");
}
RunBench();
