UnityEngine.GameObject dialogueGO = null, outGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "SpeechOutput") outGO = go;
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var output = outGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
string[] fileWords = { "Deck", "Handbook", ".pdf", ".docx", "document", "file", "Riverside", "library" };

string[] questions =
{
    "When was altcore established?",
    "Which real estate verticals does Altscape support?",
    "अल्टस्केप किन रियल एस्टेट क्षेत्रों के लिए बना है?",
    "Altcore की स्थापना कब हुई थी?",
    "Altscape sales team की कैसे मदद करता है?",
    "अल्टस्केप क्या है?",
    "Altcore क्या करती है?",
    "Who won the cricket world cup?",
    "भारत के प्रधानमंत्री कौन हैं?",
};

async void RunAll()
{
    UnityEngine.Debug.Log("FT_MODEL: " + UnityEngine.Object.FindFirstObjectByType<LLMUnity.LLM>().model);
    foreach (var q in questions)
    {
        var replyTcs = new System.Threading.Tasks.TaskCompletionSource<string>();
        var speechTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var doneTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        System.Action<string> onReply = r => replyTcs.TrySetResult(r);
        System.Action onSpeech = () => speechTcs.TrySetResult(true);
        System.Action onDone = () => doneTcs.TrySetResult(true);
        System.Action<string> onFail = e => { speechTcs.TrySetResult(false); doneTcs.TrySetResult(false); };
        controller.OnReply += onReply; output.OnSpeechStarted += onSpeech; output.OnSpeechFinished += onDone; output.OnFailed += onFail;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        controller.Ask(q);
        string reply = await replyTcs.Task;
        long thinkMs = sw.ElapsedMilliseconds; sw.Restart();
        bool spoke = await speechTcs.Task;
        long voiceMs = sw.ElapsedMilliseconds;
        await doneTcs.Task;
        controller.OnReply -= onReply; output.OnSpeechStarted -= onSpeech; output.OnSpeechFinished -= onDone; output.OnFailed -= onFail;

        string leak = "";
        foreach (var w in fileWords) if (reply.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) leak += " [mentions '" + w + "']";
        UnityEngine.Debug.Log($"FT Q: {q}\n   A ({thinkMs} ms, voice +{voiceMs} ms, ok={spoke}): {reply}{leak}");
    }
    UnityEngine.Debug.Log("FT_DONE");
}
RunAll();
