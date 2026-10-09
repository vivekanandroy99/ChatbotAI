// Unity CLI (edit or play mode): checks OnlineBrain against tools/mock_online_ai.py on 127.0.0.1:8890 - the three kinds of
// service (request shape, headers, answer reading), the model lists, wrong key, the "temperature refused" retry.
// Result in tools/online_test.txt (ALLDONE at the end). It puts the app's online settings back afterwards.
string outFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "tools/online_test.txt");
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(outFile, log.ToString()); }
async void Run()
{
    var P = ChatbotAI.Dialogue.OnlineBrain.Provider.Other;
    bool wasOn = ChatbotAI.Dialogue.OnlineBrain.Enabled;
    var wasProvider = ChatbotAI.Dialogue.OnlineBrain.Current;
    try
    {
        foreach (var p in new[] { ChatbotAI.Dialogue.OnlineBrain.Provider.OpenAI, ChatbotAI.Dialogue.OnlineBrain.Provider.Claude,
                                  ChatbotAI.Dialogue.OnlineBrain.Provider.Gemini, ChatbotAI.Dialogue.OnlineBrain.Provider.Other })
        {
            ChatbotAI.Dialogue.OnlineBrain.Current = p;
            ChatbotAI.Dialogue.OnlineBrain.SetServer(p, "http://127.0.0.1:8890");
            ChatbotAI.Dialogue.OnlineBrain.SetKey(p, "mock-key-" + p);
            ChatbotAI.Dialogue.OnlineBrain.SetModel(p, "mock-model");
            var (ok, message) = await ChatbotAI.Dialogue.OnlineBrain.Test();
            Log($"{p}: test ok={ok}: {message}");
            var (models, error) = await ChatbotAI.Dialogue.OnlineBrain.ListModels();
            Log($"{p}: models [{string.Join(", ", models)}] {error}");
            var history = new System.Collections.Generic.List<LLMUnity.ChatMessage>
            {
                new LLMUnity.ChatMessage("user", "example question"), new LLMUnity.ChatMessage("assistant", "example answer"),
            };
            string reply = await ChatbotAI.Dialogue.OnlineBrain.Ask("You are a test.", history, "What is Altscape?", 0.2f, 100);
            Log($"{p}: Ask -> {reply}  [{ChatbotAI.Dialogue.OnlineBrain.Status}]");
            ChatbotAI.Dialogue.OnlineBrain.RemoveKey(p);
            ChatbotAI.Dialogue.OnlineBrain.SetServer(p, "");
        }
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        ChatbotAI.Dialogue.OnlineBrain.Enabled = wasOn;
        ChatbotAI.Dialogue.OnlineBrain.Current = wasProvider;
        Log("ALLDONE");
    }
}
Run();
return null;
