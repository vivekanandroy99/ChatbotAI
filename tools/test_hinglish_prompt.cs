UnityEngine.GameObject dialogueGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == "DialogueBrain") dialogueGO = go;
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();

string[] questions =
{
    "अल्टस्केप किन रियल एस्टेट क्षेत्रों के लिए बना है?",
    "Altscape sales team की कैसे मदद करता है?",
    "Altcore क्या करती है?",
};
string suffix =
    "\n\nReply in Hinglish: Hindi grammar and Hindi words in Devanagari, but every English word - names, products, " +
    "technical and business terms - stays in English letters exactly as written in the CONTEXT. " +
    "Example of the style: \"Altscape residential, commercial और mixed-use projects के लिए बना है, और sales team को buyers से बेहतर बात करने में मदद करता है।\"";

async void RunAll()
{
    foreach (var q in questions)
    {
        var match = await ChatbotAI.Dialogue.KnowledgeClient.Retrieve(controller.SidecarUrl, "default", q, 4);
        var sb = new System.Text.StringBuilder("CONTEXT:\n");
        for (int i = 0; i < match.chunks.Length; i++) sb.Append('[').Append(i + 1).Append("] ").Append(match.chunks[i].text.Trim()).Append("\n\n");
        sb.Append("QUESTION: ").Append(q);
        string basePrompt = sb.ToString();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        string a = await agent.Chat(basePrompt, null, null, addToHistory: false);
        long ta = sw.ElapsedMilliseconds; sw.Restart();
        string b = await agent.Chat(basePrompt + suffix, null, null, addToHistory: false);
        long tb = sw.ElapsedMilliseconds;
        UnityEngine.Debug.Log($"HG Q: {q}\n   current ({ta} ms): {a}\n   with example ({tb} ms): {b}");
    }
    UnityEngine.Debug.Log("HG_DONE");
}
RunAll();
