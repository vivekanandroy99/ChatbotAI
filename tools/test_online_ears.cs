// Unity CLI (edit or play mode): checks OnlineEars against tools/mock_online_ai.py on 127.0.0.1:8890 - the six kinds of service
// (multipart / JSON request shape, headers, the text and the language they answer with), a wrong server. Plus the language list
// helpers (script -> language, "hin"/"Tamil"/"hi-IN" -> code). Result in tools/online_test_ears.txt (ALLDONE at the end).
string outFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "tools/online_test_ears.txt");
var log = new System.Text.StringBuilder();
void Log(string s) { log.AppendLine(s); System.IO.File.WriteAllText(outFile, log.ToString(), System.Text.Encoding.UTF8); }
async void Run()
{
    var E = typeof(ChatbotAI.Audio.OnlineEars);
    bool wasOn = ChatbotAI.Audio.OnlineEars.Enabled;
    var wasProvider = ChatbotAI.Audio.OnlineEars.Current;
    try
    {
        Log("codes: " + string.Join(", ", new[] { "hin", "Tamil", "hi-IN", "od-IN", "eng", "Hebrew", "xx" }.Select(c => c + "=" + (ChatbotAI.Audio.Languages.FromAnyCode(c) ?? "null"))));
        Log("scripts: " + string.Join(", ", new[] { "नमस्ते", "வணக்கம்", "hello", "مرحبا", "привет", "こんにちは", "你好", "안녕하세요", "Hello नमस्ते दोस्त" }.Select(t => t + "=" + (ChatbotAI.Audio.Languages.ScriptLanguage(t) ?? "latin"))));
        ChatbotAI.Audio.OnlineEars.Enabled = true;
        var samples = new float[16000 * 2];
        for (int i = 0; i < samples.Length; i++) samples[i] = 0.2f * UnityEngine.Mathf.Sin(i * 0.05f);
        foreach (var p in (ChatbotAI.Audio.OnlineEars.Provider[])System.Enum.GetValues(typeof(ChatbotAI.Audio.OnlineEars.Provider)))
        {
            bool hadKey = ChatbotAI.Audio.OnlineEars.HasKey(p);
            ChatbotAI.Audio.OnlineEars.Current = p;
            ChatbotAI.Audio.OnlineEars.SetServer(p, "http://127.0.0.1:8890");
            if (!hadKey) ChatbotAI.Audio.OnlineEars.SetKey(p, "mock-key-" + p);
            var (ok, message) = await ChatbotAI.Audio.OnlineEars.Test();
            Log($"{p}: test ok={ok}: {message}");
            foreach (var hint in new[] { "", "ta" })
            {
                var (text, lang) = await ChatbotAI.Audio.OnlineEars.Transcribe(samples, 16000, hint, "Altscape, Altcore.");
                Log($"{p}: hint '{hint}' -> text '{text}' language '{lang}'   [{ChatbotAI.Audio.OnlineEars.Status}]");
            }
            if (!hadKey) ChatbotAI.Audio.OnlineEars.RemoveKey(p);
            UnityEngine.PlayerPrefs.DeleteKey("online/ears/server/" + p);
        }
        var bad = ChatbotAI.Audio.OnlineEars.Provider.Groq;
        ChatbotAI.Audio.OnlineEars.Current = bad;
        ChatbotAI.Audio.OnlineEars.SetServer(bad, "http://127.0.0.1:8891");
        bool had = ChatbotAI.Audio.OnlineEars.HasKey(bad);
        if (!had) ChatbotAI.Audio.OnlineEars.SetKey(bad, "mock");
        var (ok2, msg2) = await ChatbotAI.Audio.OnlineEars.Test();
        Log($"unreachable: ok={ok2}: {msg2}");
        if (!had) ChatbotAI.Audio.OnlineEars.RemoveKey(bad);
        UnityEngine.PlayerPrefs.DeleteKey("online/ears/server/" + bad);
    }
    catch (System.Exception e) { Log("ERROR " + e); }
    finally
    {
        ChatbotAI.Audio.OnlineEars.Enabled = wasOn;
        ChatbotAI.Audio.OnlineEars.Current = wasProvider;
        Log("ALLDONE");
    }
}
Run();
return null;
