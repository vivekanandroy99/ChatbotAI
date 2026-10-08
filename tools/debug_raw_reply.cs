var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject dialogueGO = null;
foreach (var go in goRoots)
{
    if (go.name == "DialogueBrain") { dialogueGO = go; break; }
}
var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();
var sw = System.Diagnostics.Stopwatch.StartNew();
string lastText = "";
_ = agent.Chat("What are your store hours?", (partial) => { lastText = partial; }, () => {
    sw.Stop();
    UnityEngine.Debug.Log($"RAW_LLM_REPLY ({sw.ElapsedMilliseconds}ms): [{lastText}]");
});
UnityEngine.Debug.Log("Raw Chat() triggered.");
