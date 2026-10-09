// Play mode, with tools/mock_online_ai.py running: the speaker says a sentence through the online voice (a mock tone), then -
// once the mock is stopped (the script waits for tools/mock_stopped.flag) - the same kind of sentence through the voice on this PC
// (fallback). Result: tools/online_test_voice_e2e.txt. The online settings are put back at the end.
string root0 = System.IO.Directory.GetCurrentDirectory();
string outFile = System.IO.Path.Combine(root0, "tools/online_test_voice_e2e.txt");
string flag = System.IO.Path.Combine(root0, "tools/mock_stopped.flag");
var ui = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.UI.CompanionUI>();
var output = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Audio.SpeechOutputController>();
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(outFile, log.ToString(), System.Text.Encoding.UTF8); }
async System.Threading.Tasks.Task Wait(int ms) { await System.Threading.Tasks.Task.Delay(ms); }
async System.Threading.Tasks.Task<(float seconds, bool finished)> Say(string text)
{
    bool started = false, finished = false;
    float t0 = UnityEngine.Time.realtimeSinceStartup, first = -1;
    System.Action s = () => { started = true; first = UnityEngine.Time.realtimeSinceStartup - t0; };
    System.Action f = () => finished = true;
    output.OnSpeechStarted += s; output.OnSpeechFinished += f;
    output.Speak(text, "af_heart", 1f);
    for (int i = 0; i < 400 && !finished; i++) await Wait(50);
    output.OnSpeechStarted -= s; output.OnSpeechFinished -= f;
    return (first, finished);
}
async void Run()
{
    var P = ChatbotAI.Audio.OnlineVoice.Provider.Other;
    bool wasOn = ChatbotAI.Audio.OnlineVoice.Enabled;
    var wasProvider = ChatbotAI.Audio.OnlineVoice.Current;
    try
    {
        for (int i = 0; i < 600 && ui.Current == ChatbotAI.UI.CompanionUI.State.Starting; i++) await Wait(300);
        await Wait(2000);
        ChatbotAI.Audio.OnlineVoice.Current = P;
        ChatbotAI.Audio.OnlineVoice.SetServer(P, "http://127.0.0.1:8890");
        ChatbotAI.Audio.OnlineVoice.SetKey(P, "mock-key");
        ChatbotAI.Audio.OnlineVoice.Enabled = true;
        Log($"Ready={ChatbotAI.Audio.OnlineVoice.Ready}");
        var (a, doneA) = await Say("This sentence is spoken by the online voice. And a second sentence follows it.");
        Log($"online  : first sound after {a:0.00} s, finished {doneA}; status: {ChatbotAI.Audio.OnlineVoice.Status}");
        Log("WAITING for tools/mock_stopped.flag (stop the mock, then create the file)");
        for (int i = 0; i < 1200 && !System.IO.File.Exists(flag); i++) await Wait(500);
        var (b, doneB) = await Say("This sentence should come from the voice on this PC instead.");
        Log($"fallback: first sound after {b:0.00} s, finished {doneB}; status: {ChatbotAI.Audio.OnlineVoice.Status}");
        var (c, doneC) = await Say("A third sentence, now while the online voice is paused for a minute.");
        Log($"paused  : first sound after {c:0.00} s, finished {doneC}; Ready={ChatbotAI.Audio.OnlineVoice.Ready}");
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        ChatbotAI.Audio.OnlineVoice.RemoveKey(P);
        UnityEngine.PlayerPrefs.DeleteKey("online/voice/server/" + P);
        ChatbotAI.Audio.OnlineVoice.Enabled = wasOn;
        ChatbotAI.Audio.OnlineVoice.Current = wasProvider;
        Log("ALLDONE");
    }
}
Run();
return null;
