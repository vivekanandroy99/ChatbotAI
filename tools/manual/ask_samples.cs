// Play mode: asks the example questions the manual suggests and writes the answers to tools/manual/shots/_ask.txt (to check
// they work before they're printed). Saving conversations is switched off meanwhile.
string outFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "tools/manual/shots/_ask.txt");
var ui = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.UI.CompanionUI>();
var root = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>().rootVisualElement;
var registry = ChatbotAI.Dialogue.AvatarRegistry.Instance;
var dialogue = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Dialogue.DialogueController>();
var output = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Audio.SpeechOutputController>();
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(outFile, log.ToString(), System.Text.Encoding.UTF8); }
async System.Threading.Tasks.Task Wait(int ms) { await System.Threading.Tasks.Task.Delay(ms); }
async void Run()
{
    bool was = ChatbotAI.Dialogue.ConversationLog.Enabled;
    var start = registry.Active;
    try
    {
        for (int i = 0; i < 600 && ui.Current == ChatbotAI.UI.CompanionUI.State.Starting; i++) await Wait(300);
        ChatbotAI.Dialogue.ConversationLog.Enabled = false;
        var tests = new[]
        {
            ("altcore_female", "What does the Altscape ecosystem include?"), ("altcore_female", "Who are you?"), ("altcore_female", "आप कौन हैं?"),
            ("altcore_male", "How can I contact Altcore?"),
            ("pni_female", "What kind of projects have you delivered?"), ("pni_female", "What does P&I do?"),
            ("chat_male", "Tell me something interesting about space."), ("chat_male", "What shall I cook tonight?"),
        };
        string bot = null;
        foreach (var (id, q) in tests)
        {
            if (id != bot)
            {
                foreach (var p in registry.Profiles) if (p.avatarId == id) registry.SetActive(p);
                bot = id;
                await Wait(3500);
            }
            if (output.IsBusy) output.Stop();
            for (int i = 0; i < 200 && ui.Current != ChatbotAI.UI.CompanionUI.State.Ready; i++) await Wait(50);
            dialogue.Ask(q);
            await Wait(500);
            for (int i = 0; i < 1200 && ui.Current != ChatbotAI.UI.CompanionUI.State.Speaking; i++) await Wait(50);
            await Wait(2500);
            var reply = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root, "replyText");
            Log($"[{id}] {q}\n   -> {reply.text}\n");
            output.Stop();
            await Wait(800);
        }
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        try { registry.SetActive(start); } catch { }
        ChatbotAI.Dialogue.ConversationLog.Enabled = was;
        ChatbotAI.Dialogue.ConversationLog.Reload();
        Log("ALLDONE");
    }
}
Run();
return null;
