UnityEngine.GameObject dialogueGO = null, speechInGO = null, speechOutGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "SpeechInput") speechInGO = go;
    if (go.name == "SpeechOutput") speechOutGO = go;
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var input = speechInGO.GetComponent<ChatbotAI.Audio.SpeechInputController>();
var output = speechOutGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();

string dir = @"C:\Users\Admin\AppData\Local\Temp\claude\C--Users-Admin-ChatbotAI\a6d0f108-adb8-4123-bea6-c336772cbbe8\scratchpad\stt_clips";
string[] clips = { "en_what", "en_founded", "hi_verticals", "hi_founded" };

async void RunAll()
{
    // Same call StartListening makes, so the vocabulary hint is in place.
    input.GetType().GetMethod("RefreshVocabularyHint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(input, null);
    await System.Threading.Tasks.Task.Delay(500);
    UnityEngine.Debug.Log("VP_HINT: " + input.GetType().GetField("vocabularyPrompt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(input));

    foreach (var name in clips)
    {
        var clip = ChatbotAI.Audio.WavUtility.ToAudioClip(System.IO.File.ReadAllBytes(System.IO.Path.Combine(dir, name + ".wav")));
        var samples = new float[clip.samples];
        clip.GetData(samples, 0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (text, lang) = await input.TranscribeAsync(samples, clip.frequency, clip.channels);
        long sttMs = sw.ElapsedMilliseconds;

        var replyTcs = new System.Threading.Tasks.TaskCompletionSource<string>();
        var speechTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var doneTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        System.Action<string> onReply = r => replyTcs.TrySetResult(r);
        System.Action onSpeech = () => speechTcs.TrySetResult(true);
        System.Action onDone = () => doneTcs.TrySetResult(true);
        System.Action<string> onFail = e => { speechTcs.TrySetResult(false); doneTcs.TrySetResult(false); };
        controller.OnReply += onReply; output.OnSpeechStarted += onSpeech; output.OnSpeechFinished += onDone; output.OnFailed += onFail;

        sw.Restart();
        controller.Ask(text);
        string reply = await replyTcs.Task;
        long thinkMs = sw.ElapsedMilliseconds;
        sw.Restart();
        bool spoke = await speechTcs.Task;
        long voiceMs = sw.ElapsedMilliseconds;
        await doneTcs.Task;

        controller.OnReply -= onReply; output.OnSpeechStarted -= onSpeech; output.OnSpeechFinished -= onDone; output.OnFailed -= onFail;
        UnityEngine.Debug.Log($"VP [{name}] heard ({lang}, {sttMs} ms): {text}\n   reply ({thinkMs} ms): {reply}\n   voice started after {voiceMs} ms (ok={spoke})");
    }
    UnityEngine.Debug.Log("VP_DONE");
}
RunAll();

