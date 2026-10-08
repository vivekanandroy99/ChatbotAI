// Runs two suites: the real Altscape/Altcore avatar and a made-up coffee brand
// (StreamingAssets/Knowledge/zz_test_brand) whose avatar lists no topic names,
// so brand detection, redirects and answers are checked on a second brand too.
UnityEngine.GameObject dialogueGO = null, outGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "SpeechOutput") outGO = go;
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var output = outGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
var registry = dialogueGO.GetComponent<ChatbotAI.Dialogue.AvatarRegistry>();
string[] bookish = { "स्थापित", "संपर्क", "प्रदान", "उपलब्ध", "सहायता", "माध्यम", "उपयोग", "अधिक", "केवल" };

var brand = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Dialogue.AvatarProfile>();
brand.avatarId = "zz_test_brand";
brand.displayName = "Bree";
brand.personaPrompt = "You are Bree, the friendly assistant at the front counter. You are warm and helpful, and keep answers short and conversational, as if spoken out loud.";
brand.hindiVoice = "hf_beta";
brand.pronunciations.Add(new ChatbotAI.Dialogue.Pronunciation { word = "Koregaon", sayAs = "Koray-gown", sayAsInHindi = "कोरेगांव" });
var field = typeof(ChatbotAI.Dialogue.AvatarRegistry).GetField("profiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var profiles = (System.Collections.Generic.List<ChatbotAI.Dialogue.AvatarProfile>)field.GetValue(registry);
if (!profiles.Exists(p => p.avatarId == brand.avatarId)) profiles.Add(brand);

var suites = new (string avatar, string[] questions)[]
{
    ("default", new[]
    {
        "नमस्ते! कैसे हो आप?",
        "आप कौन हो, क्या करते हो?",
        "Altcore क्या है? कुछ बता सकते हैं आप.",
        "और ये कब शुरू हुई थी?",
        "What is Altscape?",
        "क्या ये mobile पर भी चलता है?",
        "Altcore क्या solutions देता है?",
        "अल्टस्केप किन रियल एस्टेट क्षेत्रों के लिए बना है?",
        "Altscape sales team की कैसे मदद करता है?",
        "अल्टकोर से कैसे संपर्क करें?",
        "भारत के प्रधानमंत्री कौन हैं?",
        "What does Altscape cost per month?",
        "बहुत बढ़िया, धन्यवाद!",
    }),
    ("zz_test_brand", new[]
    {
        "hello!",
        "Brewline क्या है?",
        "सब्सक्रिप्शन के कितने प्लान हैं और उनकी कीमत क्या है?",
        "क्या मैं इसे कभी भी cancel कर सकता हूँ?",
        "Do your cafés serve beer?",
        "Is delivery available in Delhi?",
        "बरिस्ता कोर्स कितने दिन का है और कहाँ होता है?",
        "Can I bring my dog?",
        "Do you offer franchises?",
        "कल का मैच कौन जीता?",
        "customer care का नंबर क्या है?",
        "thanks, bye!",
    }),
};

string[] TranslationSet =
{
    "Altcore is an experiential technology firm that builds interactive solutions to make complex spaces and ideas easy to understand.",
    "Altscape helps sales teams turn buyer interest into confident decisions by answering questions instantly through interactive experiences.",
    "You can reach Altcore at +91 90961 91973 or email contact@altcore.co.",
    "Altscape is built for residential, commercial, plotting and township, mixed use, architecture and interior design projects.",
    "Yes, pets are welcome in the outdoor seating areas of all Brewline cafés.",
    "Subscriptions can be paused or cancelled at any time from the Brewline app, with no cancellation fee.",
    "The weekend Barista Academy course runs for two days at the Koregaon Park café in Pune and costs Rs 3,500.",
    "I don't have details on franchises, but Brewline currently runs six cafés in Pune and Mumbai.",
    "Altscape works as a web-based experience, so buyers can explore projects from any device, anytime.",
    "Delivery is currently available only in Pune and Mumbai, so it isn't available in Delhi yet.",
};

async void RunAll()
{
    long hindiTotal = 0, englishTotal = 0; int hindiCount = 0, englishCount = 0;
    foreach (var (avatar, questions) in suites)
    {
        registry.SetActive(avatar);
        foreach (var q in questions)
        {
            var replyTcs = new System.Threading.Tasks.TaskCompletionSource<string>();
            var speechTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var doneTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            long voiceAt = -1;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            System.Action<string> onReply = r => replyTcs.TrySetResult(r);
            System.Action onSpeech = () => { voiceAt = sw.ElapsedMilliseconds; speechTcs.TrySetResult(true); };
            System.Action onDone = () => doneTcs.TrySetResult(true);
            System.Action<string> onFail = e => { speechTcs.TrySetResult(false); doneTcs.TrySetResult(false); };
            controller.OnReply += onReply; output.OnSpeechStarted += onSpeech; output.OnSpeechFinished += onDone; output.OnFailed += onFail;

            controller.Ask(q);
            string reply = await replyTcs.Task;
            long thinkMs = sw.ElapsedMilliseconds;
            await speechTcs.Task;
            await doneTcs.Task;
            controller.OnReply -= onReply; output.OnSpeechStarted -= onSpeech; output.OnSpeechFinished -= onDone; output.OnFailed -= onFail;

            bool askedHindi = ChatbotAI.Dialogue.RefusalBank.IsHindi(q);
            bool repliedHindi = ChatbotAI.Dialogue.RefusalBank.IsHindi(reply);
            if (askedHindi) { hindiTotal += voiceAt; hindiCount++; } else { englishTotal += voiceAt; englishCount++; }
            string verdict = askedHindi == repliedHindi ? "OK" : "WRONG LANGUAGE";
            foreach (var w in bookish) if (reply.Contains(w)) verdict += " + flagged '" + w + "'";
            UnityEngine.Debug.Log($"HB [{verdict}] [{avatar}] (text {thinkMs} ms, voice {voiceAt} ms) Q: {q}\n   A: {reply}");
        }
    }
    registry.SetActive("default");

    // Same English sentences through every model's translator, for a side-by-side Hindi comparison.
    var toHindi = typeof(ChatbotAI.Dialogue.DialogueController).GetMethod("ToHindi", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    long translateTotal = 0;
    foreach (var en in TranslationSet)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string hi = await (System.Threading.Tasks.Task<string>)toHindi.Invoke(controller, new object[] { en, registry.Active });
        translateTotal += sw.ElapsedMilliseconds;
        UnityEngine.Debug.Log($"HB TR ({sw.ElapsedMilliseconds} ms) {en}\n   HI: {hi}");
    }
    UnityEngine.Debug.Log($"HB translate avg {translateTotal / TranslationSet.Length} ms");
    UnityEngine.Debug.Log($"HB_DONE avg voice start: Hindi {hindiTotal / System.Math.Max(1, hindiCount)} ms, English {englishTotal / System.Math.Max(1, englishCount)} ms");
}
RunAll();
