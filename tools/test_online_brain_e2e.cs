// Play mode, with tools/mock_online_ai.py running: Iris (open chat) answers through the online brain (the canned "MOCK ..." text),
// then - once the mock is stopped (the script waits for tools/mock_stopped.flag) - through the model on this PC (fallback).
// Result: tools/online_test_e2e.txt. Conversation saving is off meanwhile; the online settings are put back at the end.
string root0 = System.IO.Directory.GetCurrentDirectory();
string outFile = System.IO.Path.Combine(root0, "tools/online_test_e2e.txt");
string flag = System.IO.Path.Combine(root0, "tools/mock_stopped.flag");
var ui = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.UI.CompanionUI>();
var uiRoot = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>().rootVisualElement;
var registry = ChatbotAI.Dialogue.AvatarRegistry.Instance;
var dialogue = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Dialogue.DialogueController>();
var output = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Audio.SpeechOutputController>();
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(outFile, log.ToString(), System.Text.Encoding.UTF8); }
async System.Threading.Tasks.Task Wait(int ms) { await System.Threading.Tasks.Task.Delay(ms); }
async System.Threading.Tasks.Task<string> AskIris(string q)
{
    if (output.IsBusy) output.Stop();
    for (int i = 0; i < 200 && ui.Current != ChatbotAI.UI.CompanionUI.State.Ready; i++) await Wait(50);
    dialogue.Ask(q);
    await Wait(500);
    for (int i = 0; i < 1800 && ui.Current != ChatbotAI.UI.CompanionUI.State.Speaking; i++) await Wait(50);
    await Wait(1500);
    var reply = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(uiRoot, "replyText");
    string text = reply.text;
    output.Stop();
    await Wait(600);
    return text;
}
async void Run()
{
    var B = ChatbotAI.Dialogue.OnlineBrain.Provider.Other;
    bool wasOn = ChatbotAI.Dialogue.OnlineBrain.Enabled, wasLog = ChatbotAI.Dialogue.ConversationLog.Enabled;
    var wasProvider = ChatbotAI.Dialogue.OnlineBrain.Current;
    var startBot = registry.Active;
    try
    {
        for (int i = 0; i < 600 && ui.Current == ChatbotAI.UI.CompanionUI.State.Starting; i++) await Wait(300);
        ChatbotAI.Dialogue.ConversationLog.Enabled = false;
        foreach (var p in registry.Profiles) if (p.avatarId == "chat_female") registry.SetActive(p);
        await Wait(3500);
        ChatbotAI.Dialogue.OnlineBrain.Current = B;
        ChatbotAI.Dialogue.OnlineBrain.SetServer(B, "http://127.0.0.1:8890");
        ChatbotAI.Dialogue.OnlineBrain.SetKey(B, "mock-key");
        ChatbotAI.Dialogue.OnlineBrain.SetModel(B, "mock-model");
        ChatbotAI.Dialogue.OnlineBrain.Enabled = true;
        Log($"Ready={ChatbotAI.Dialogue.OnlineBrain.Ready}");
        Log("online  : " + await AskIris("Hello there, how are you?"));
        Log("status  : " + ChatbotAI.Dialogue.OnlineBrain.Status);
        Log("WAITING for tools/mock_stopped.flag (stop the mock, then create the file)");
        for (int i = 0; i < 1200 && !System.IO.File.Exists(flag); i++) await Wait(500);
        Log("fallback: " + await AskIris("Tell me one fun fact about the moon."));
        Log("status  : " + ChatbotAI.Dialogue.OnlineBrain.Status + $"  Ready={ChatbotAI.Dialogue.OnlineBrain.Ready}");
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        ChatbotAI.Dialogue.OnlineBrain.RemoveKey(B);
        ChatbotAI.Dialogue.OnlineBrain.SetServer(B, "");
        ChatbotAI.Dialogue.OnlineBrain.SetModel(B, "");
        ChatbotAI.Dialogue.OnlineBrain.Enabled = wasOn;
        ChatbotAI.Dialogue.OnlineBrain.Current = wasProvider;
        ChatbotAI.Dialogue.ConversationLog.Enabled = wasLog;
        ChatbotAI.Dialogue.ConversationLog.Reload();
        try { registry.SetActive(startBot); } catch { }
        Log("ALLDONE");
    }
}
Run();
return null;
