// Unity CLI: OnlineBrain when the service can't be reached (nothing listening on 127.0.0.1:8890 - stop tools/mock_online_ai.py first):
// the error text, and that after two failures online answers pause for a minute (Ready = false). Result: tools/online_test_down.txt.
string outFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "tools/online_test_down.txt");
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(outFile, log.ToString()); }
async void Run()
{
    var p = ChatbotAI.Dialogue.OnlineBrain.Provider.OpenAI;
    var wasProvider = ChatbotAI.Dialogue.OnlineBrain.Current;
    bool wasOn = ChatbotAI.Dialogue.OnlineBrain.Enabled;
    try
    {
        ChatbotAI.Dialogue.OnlineBrain.Current = p;
        ChatbotAI.Dialogue.OnlineBrain.Enabled = true;
        ChatbotAI.Dialogue.OnlineBrain.SetServer(p, "http://127.0.0.1:8890");
        ChatbotAI.Dialogue.OnlineBrain.SetKey(p, "mock-key");
        ChatbotAI.Dialogue.OnlineBrain.SetModel(p, "mock-model");
        Log($"before: Ready={ChatbotAI.Dialogue.OnlineBrain.Ready}");
        for (int i = 1; i <= 2; i++)
        {
            string reply = await ChatbotAI.Dialogue.OnlineBrain.Ask("sys", null, "Hello?", 0.2f, 100);
            Log($"Ask {i} -> {(reply ?? "null")} [{ChatbotAI.Dialogue.OnlineBrain.Status}]  Ready={ChatbotAI.Dialogue.OnlineBrain.Ready}");
        }
        var (ok, message) = await ChatbotAI.Dialogue.OnlineBrain.Test();
        Log($"Test ok={ok}: {message}  Ready={ChatbotAI.Dialogue.OnlineBrain.Ready}");
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        ChatbotAI.Dialogue.OnlineBrain.RemoveKey(p);
        ChatbotAI.Dialogue.OnlineBrain.SetServer(p, "");
        ChatbotAI.Dialogue.OnlineBrain.SetModel(p, "");
        ChatbotAI.Dialogue.OnlineBrain.Enabled = wasOn;
        ChatbotAI.Dialogue.OnlineBrain.Current = wasProvider;
        Log("ALLDONE");
    }
}
Run();
return null;
