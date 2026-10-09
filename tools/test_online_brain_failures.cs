// Unity CLI: OnlineBrain against tools/mock_online_ai.py started with MODE=reject_temperature, bad_key or slow (see that file).
// The OpenAI-style call is made twice: Test(), then Ask(). Result in tools/online_test_failures.txt.
string outFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "tools/online_test_failures.txt");
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(outFile, log.ToString()); }
async void Run()
{
    var p = ChatbotAI.Dialogue.OnlineBrain.Provider.OpenAI;
    var wasProvider = ChatbotAI.Dialogue.OnlineBrain.Current;
    try
    {
        ChatbotAI.Dialogue.OnlineBrain.Current = p;
        ChatbotAI.Dialogue.OnlineBrain.SetServer(p, "http://127.0.0.1:8890");
        ChatbotAI.Dialogue.OnlineBrain.SetKey(p, "mock-key");
        ChatbotAI.Dialogue.OnlineBrain.SetModel(p, "mock-model");
        float t0 = UnityEngine.Time.realtimeSinceStartup;
        var (ok, message) = await ChatbotAI.Dialogue.OnlineBrain.Test();
        Log($"Test ok={ok} ({UnityEngine.Time.realtimeSinceStartup - t0:0.0} s): {message}");
        var (models, error) = await ChatbotAI.Dialogue.OnlineBrain.ListModels();
        Log($"models [{string.Join(", ", models)}] error='{error}'");
        t0 = UnityEngine.Time.realtimeSinceStartup;
        string reply = await ChatbotAI.Dialogue.OnlineBrain.Ask("sys", null, "Hello?", 0.2f, 100);
        Log($"Ask -> {(reply ?? "null")} ({UnityEngine.Time.realtimeSinceStartup - t0:0.0} s) [{ChatbotAI.Dialogue.OnlineBrain.Status}]");
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        ChatbotAI.Dialogue.OnlineBrain.RemoveKey(p);
        ChatbotAI.Dialogue.OnlineBrain.SetServer(p, "");
        ChatbotAI.Dialogue.OnlineBrain.SetModel(p, "");
        ChatbotAI.Dialogue.OnlineBrain.Current = wasProvider;
        Log("ALLDONE");
    }
}
Run();
return null;
