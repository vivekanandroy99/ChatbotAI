UnityEngine.GameObject dialogueGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    if (go.name == "DialogueBrain") dialogueGO = go;
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();
var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");

string rules =
    "Answer using ONLY the information in the CONTEXT given with each question. Never use outside knowledge, even if you know the answer. " +
    "If the CONTEXT answers the question - including when it says something is not covered, not included or not available - give that answer. " +
    "Keep any conditions or exceptions the CONTEXT attaches to the answer (like 'waived if', 'only for', 'above Rs X'). " +
    "Only if nothing in the CONTEXT answers the question, reply with exactly: NO_ANSWER. " +
    "Keep answers short (one to three sentences) and natural to say out loud - no lists, markdown, or document names. " +
    "Reply in the same language as the question: Hindi (Devanagari script) for a Hindi question, English for an English one.";

string[] questions =
{
    "Is there a fee to check my laptop?",
    "Can I pay in installments?",
    "Is water damage covered?",
    "क्या पानी से हुआ नुकसान वारंटी में शामिल है?",
    "Do you give student discounts?",
    "Does the Nova Phone X support 5G?",
};

async void RunAll()
{
    agent.systemPrompt = profile.personaPrompt + "\n\n" + rules;
    await agent.ClearHistory();
    foreach (var q in questions)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
        System.Action<string> handler = r => tcs.TrySetResult(r);
        controller.OnReply += handler;
        controller.Ask(q);
        string reply = await tcs.Task;
        controller.OnReply -= handler;
        UnityEngine.Debug.Log($"PV_TEST Q: {q}\n   A: {reply}");
    }
    UnityEngine.Debug.Log("PV_TEST_DONE");
}
RunAll();
