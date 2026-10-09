// Unity CLI (edit or play mode): checks OnlineVoice against tools/mock_online_ai.py on 127.0.0.1:8890 - the five kinds of
// service (request shape, headers, raw / base64 audio reading), the language pick and Sarvam's language limit, a wrong key.
// Result in tools/online_test_voice.txt (ALLDONE at the end). It puts the app's online settings back afterwards.
string outFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "tools/online_test_voice.txt");
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(outFile, log.ToString()); }
async void Run()
{
    var V = typeof(ChatbotAI.Audio.OnlineVoice);
    bool wasOn = ChatbotAI.Audio.OnlineVoice.Enabled;
    var wasProvider = ChatbotAI.Audio.OnlineVoice.Current;
    try
    {
        ChatbotAI.Audio.OnlineVoice.Enabled = true;
        foreach (var p in (ChatbotAI.Audio.OnlineVoice.Provider[])System.Enum.GetValues(typeof(ChatbotAI.Audio.OnlineVoice.Provider)))
        {
            bool hadKey = ChatbotAI.Audio.OnlineVoice.HasKey(p);
            ChatbotAI.Audio.OnlineVoice.Current = p;
            ChatbotAI.Audio.OnlineVoice.SetServer(p, "http://127.0.0.1:8890");
            if (!hadKey) ChatbotAI.Audio.OnlineVoice.SetKey(p, "mock-key-" + p);
            var (ok, message) = await ChatbotAI.Audio.OnlineVoice.Test();
            Log($"{p}: test ok={ok}: {message}");

            // A Hindi sentence, a female bot's voice -> real request through Request(), read like the app does.
            var req = ChatbotAI.Audio.OnlineVoice.Request("नमस्ते! मैं आपकी कैसे मदद कर सकती हूँ?", "af_heart", 1f);
            Log($"{p}: Request(Hindi) -> {(req == null ? "null" : "sent")}");
            if (req != null)
            {
                while (!req.isDone) await System.Threading.Tasks.Task.Yield();
                var s = (ChatbotAI.Audio.PcmStream)req.downloadHandler;
                Log($"{p}: http {req.responseCode}, rate {s.SampleRate}, {s.ReceivedSeconds:0.00} s, complete {s.Complete}, online {s.Online}");
                req.Dispose();
            }
            // French: Sarvam can't speak it (null = the voice on this PC), the others can.
            var fr = ChatbotAI.Audio.OnlineVoice.CanSpeak(p, "fr");
            Log($"{p}: CanSpeak(fr) = {fr}; text 'こんにちは' is '{ChatbotAI.Audio.Languages.OfText("こんにちは")}', 'வணக்கம்' is '{ChatbotAI.Audio.Languages.OfText("வணக்கம்")}', 'hello' is '{ChatbotAI.Audio.Languages.OfText("hello")}'");
            if (!hadKey) ChatbotAI.Audio.OnlineVoice.RemoveKey(p);
            PlayerPrefs_DeleteServer(p);
        }
        // Wrong key.
        var bad = ChatbotAI.Audio.OnlineVoice.Provider.OpenAI;
        ChatbotAI.Audio.OnlineVoice.Current = bad;
        ChatbotAI.Audio.OnlineVoice.SetServer(bad, "http://127.0.0.1:8891");   // nothing listens here
        bool had = ChatbotAI.Audio.OnlineVoice.HasKey(bad);
        if (!had) ChatbotAI.Audio.OnlineVoice.SetKey(bad, "mock");
        var (ok2, msg2) = await ChatbotAI.Audio.OnlineVoice.Test();
        Log($"unreachable: ok={ok2}: {msg2}");
        if (!had) ChatbotAI.Audio.OnlineVoice.RemoveKey(bad);
        PlayerPrefs_DeleteServer(bad);
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        ChatbotAI.Audio.OnlineVoice.Enabled = wasOn;
        ChatbotAI.Audio.OnlineVoice.Current = wasProvider;
        Log("ALLDONE");
    }
}
void PlayerPrefs_DeleteServer(ChatbotAI.Audio.OnlineVoice.Provider p) => UnityEngine.PlayerPrefs.DeleteKey("online/voice/server/" + p);
Run();
return null;
