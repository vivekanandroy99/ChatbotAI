UnityEngine.GameObject dialogueGO = null, outGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "SpeechOutput") outGO = go;
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var output = outGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
string[] bookish = { "स्थापित", "संपर्क", "प्रदान", "उपलब्ध", "सहायता", "अल्ट", "माध्यम", "सलाह", "उपयोग", "अधिक", "केवल" };

string[] questions =
{
    "नमस्ते! कैसे हो आप?",
    "hello there",
    "आप कौन हो, क्या करते हो, कुछ बताई है आपके बारे में?",
    "Altcore क्या है? कुछ बता सकते हैं आप.",
    "और ये कब शुरू हुई थी?",
    "What is Altscape?",
    "क्या ये mobile पर भी चलता है?",
    "Does Altscape work on mobile?",
    "Altcore क्या solutions देता है?",
    "अल्टस्केप किन रियल एस्टेट क्षेत्रों के लिए बना है?",
    "Altscape sales team की कैसे मदद करता है?",
    "अल्टकोर से कैसे संपर्क करें?",
    "भारत के प्रधानमंत्री कौन हैं?",
    "आज मौसम कैसा है?",
    "Who won the cricket world cup?",
    "What does Altscape cost per month?",
    "बहुत बढ़िया, धन्यवाद!",
    "thanks, bye!",
};
async void RunAll()
{
    foreach (var q in questions)
    {
        var replyTcs = new System.Threading.Tasks.TaskCompletionSource<string>();
        var speechTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var doneTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        System.Action<string> onReply = r => replyTcs.TrySetResult(r);
        long voiceAt = -1;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        System.Action onSpeech = () => { voiceAt = sw.ElapsedMilliseconds; speechTcs.TrySetResult(true); };
        System.Action onDone = () => doneTcs.TrySetResult(true);
        System.Action<string> onFail = e => { speechTcs.TrySetResult(false); doneTcs.TrySetResult(false); };
        controller.OnReply += onReply; output.OnSpeechStarted += onSpeech; output.OnSpeechFinished += onDone; output.OnFailed += onFail;

        controller.Ask(q);
        string reply = await replyTcs.Task;
        long thinkMs = sw.ElapsedMilliseconds;
        bool spoke = await speechTcs.Task;
        await doneTcs.Task;
        controller.OnReply -= onReply; output.OnSpeechStarted -= onSpeech; output.OnSpeechFinished -= onDone; output.OnFailed -= onFail;

        bool askedHindi = ChatbotAI.Dialogue.RefusalBank.IsHindi(q);
        bool repliedHindi = ChatbotAI.Dialogue.RefusalBank.IsHindi(reply);
        string verdict = askedHindi == repliedHindi ? "OK" : "WRONG LANGUAGE";
        foreach (var w in bookish) if (reply.Contains(w)) verdict += " + flagged '" + w + "'";
        UnityEngine.Debug.Log($"HB [{verdict}] (text {thinkMs} ms, voice starts {voiceAt} ms) Q: {q}\n   A: {reply}");
    }
    UnityEngine.Debug.Log("HB_DONE");
}
RunAll();
