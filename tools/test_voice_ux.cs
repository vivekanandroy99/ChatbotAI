// Play mode: asks a few questions end to end and times the voice - question -> first sound,
// and how long it speaks. "the voice fell behind" warnings in the console mean audible gaps.
UnityEngine.GameObject dialogueGO = null, outGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "SpeechOutput") outGO = go;
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var output = outGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
string[] questions =
{
    "Altcore क्या है? कुछ बता सकते हैं आप.",
    "Altscape sales team की कैसे मदद करता है?",
    "नमस्ते! कैसे हो आप?",
    "What is Altscape?",
    "अल्टकोर से कैसे संपर्क करें?",
};

async void Run()
{
    foreach (string q in questions)
    {
        float asked = UnityEngine.Time.realtimeSinceStartup, started = -1, finished = -1;
        System.Action onStart = () => started = UnityEngine.Time.realtimeSinceStartup;
        System.Action onFinish = () => finished = UnityEngine.Time.realtimeSinceStartup;
        output.OnSpeechStarted += onStart;
        output.OnSpeechFinished += onFinish;
        controller.Ask(q);
        while (finished < 0 && UnityEngine.Time.realtimeSinceStartup - asked < 90) await System.Threading.Tasks.Task.Delay(50);
        output.OnSpeechStarted -= onStart;
        output.OnSpeechFinished -= onFinish;
        UnityEngine.Debug.Log($"UX {q} -> first sound {(started < 0 ? -1 : started - asked):0.00}s, spoke {(finished - started):0.0}s");
        await System.Threading.Tasks.Task.Delay(500);
    }
    UnityEngine.Debug.Log("UX_DONE");
}
Run();
