// Play mode, with tools/mock_online_ai.py running: Iris (open chat) is asked in Tamil (script), and in English with the reply
// language set to French, with the online brain and online voice pointed at the mock. Checks the path: question translated to English,
// reply translated sentence by sentence, spoken through the online voice. Result: tools/online_test_languages_e2e.txt.
string root0 = System.IO.Directory.GetCurrentDirectory();
string outFile = System.IO.Path.Combine(root0, "tools/online_test_languages_e2e.txt");
var ui = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.UI.CompanionUI>();
var uiRoot = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>().rootVisualElement;
var registry = ChatbotAI.Dialogue.AvatarRegistry.Instance;
var dialogue = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Dialogue.DialogueController>();
var output = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Audio.SpeechOutputController>();
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(outFile, log.ToString(), System.Text.Encoding.UTF8); }
var seen = new System.Collections.Generic.List<string>();
UnityEngine.Application.LogCallback grab = (msg, st, type) => { if (msg.StartsWith("Heard in") || msg.StartsWith("Reply language") || msg.StartsWith("OnlineVoice") || msg.StartsWith("OnlineBrain")) seen.Add(msg); };
UnityEngine.Application.logMessageReceived += grab;
async System.Threading.Tasks.Task Wait(int ms) { await System.Threading.Tasks.Task.Delay(ms); }
async System.Threading.Tasks.Task<string> Ask(string q)
{
    if (output.IsBusy) output.Stop();
    for (int i = 0; i < 200 && ui.Current != ChatbotAI.UI.CompanionUI.State.Ready; i++) await Wait(50);
    dialogue.Ask(q);
    await Wait(500);
    for (int i = 0; i < 1800 && ui.Current != ChatbotAI.UI.CompanionUI.State.Speaking; i++) await Wait(50);
    await Wait(2500);
    string text = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(uiRoot, "replyText").text;
    output.Stop();
    await Wait(600);
    return text;
}
async void Run()
{
    var B = ChatbotAI.Dialogue.OnlineBrain.Provider.Other;
    var V = ChatbotAI.Audio.OnlineVoice.Provider.Other;
    bool brainWas = ChatbotAI.Dialogue.OnlineBrain.Enabled, voiceWas = ChatbotAI.Audio.OnlineVoice.Enabled, logWas = ChatbotAI.Dialogue.ConversationLog.Enabled;
    var brainProvider = ChatbotAI.Dialogue.OnlineBrain.Current;
    var voiceProvider = ChatbotAI.Audio.OnlineVoice.Current;
    var startBot = registry.Active;
    string replyWas = dialogue.ReplyCode;
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
        ChatbotAI.Audio.OnlineVoice.Current = V;
        ChatbotAI.Audio.OnlineVoice.SetServer(V, "http://127.0.0.1:8890");
        ChatbotAI.Audio.OnlineVoice.SetKey(V, "mock-key");
        ChatbotAI.Audio.OnlineVoice.Enabled = true;
        Log($"brain ready={ChatbotAI.Dialogue.OnlineBrain.Ready}, voice ready={ChatbotAI.Audio.OnlineVoice.Ready}, speaks ta={ChatbotAI.Audio.OnlineVoice.Speaks("ta")}");

        dialogue.ReplyCode = "";
        Log("Tamil question, same-as-asked : " + await Ask("வணக்கம், நீங்கள் எப்படி இருக்கிறீர்கள்?"));
        Log("  voice: " + ChatbotAI.Audio.OnlineVoice.Status);
        dialogue.ReplyCode = "fr";
        Log("English question, replies in French: " + await Ask("Hello there, how are you today?"));
        Log("  voice: " + ChatbotAI.Audio.OnlineVoice.Status);
        dialogue.ReplyCode = "";
        Log("English question, normal           : " + await Ask("Tell me one fun fact about tea."));
        Log("log lines: " + string.Join(" || ", seen));
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        UnityEngine.Application.logMessageReceived -= grab;
        ChatbotAI.Dialogue.OnlineBrain.RemoveKey(B);
        UnityEngine.PlayerPrefs.DeleteKey("online/server/" + B);
        UnityEngine.PlayerPrefs.DeleteKey("online/model/" + B);
        ChatbotAI.Dialogue.OnlineBrain.Enabled = brainWas;
        ChatbotAI.Dialogue.OnlineBrain.Current = brainProvider;
        ChatbotAI.Audio.OnlineVoice.RemoveKey(V);
        UnityEngine.PlayerPrefs.DeleteKey("online/voice/server/" + V);
        ChatbotAI.Audio.OnlineVoice.Enabled = voiceWas;
        ChatbotAI.Audio.OnlineVoice.Current = voiceProvider;
        dialogue.ReplyCode = replyWas;
        ChatbotAI.Dialogue.ConversationLog.Enabled = logWas;
        ChatbotAI.Dialogue.ConversationLog.Reload();
        try { registry.SetActive(startBot); } catch { }
        Log("ALLDONE");
    }
}
Run();
return null;
