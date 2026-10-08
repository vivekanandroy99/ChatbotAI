var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject llmGO = null, whisperEnGO = null, whisperHiGO = null, canvasGO = null, harnessGO = null, dialogueGO = null;
foreach (var go in roots)
{
    if (go.name == "LLM") llmGO = go;
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
    if (go.name == "Canvas") canvasGO = go;
    if (go.name == "TestHarness") harnessGO = go;
    if (go.name == "DialogueBrain") dialogueGO = go;
}

// --- GPU offload for the LLM (RTX 5060 Ti available) ---
var llmComp = llmGO.GetComponent<LLMUnity.LLM>();
llmComp.numGPULayers = 999;

// --- GPU (Vulkan) for both Whisper models ---
var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var whisperEnSO = new UnityEditor.SerializedObject(whisperEn);
whisperEnSO.FindProperty("useGpu").boolValue = true;
whisperEnSO.ApplyModifiedProperties();

var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();
var whisperHiSO = new UnityEditor.SerializedObject(whisperHi);
whisperHiSO.FindProperty("useGpu").boolValue = true;
whisperHiSO.ApplyModifiedProperties();

// --- Cap reply length for snappier responses ---
var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();
agent.numPredict = 200;

// --- Recreate the placeholder AvatarProfile from the updated script defaults
//     (bilingual keywords + language-matching persona instruction) ---
string profilePath = "Assets/Resources/Avatars/DefaultAvatar.asset";
UnityEditor.AssetDatabase.DeleteAsset(profilePath);
var profile = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Dialogue.AvatarProfile>();
UnityEditor.AssetDatabase.CreateAsset(profile, profilePath);
UnityEditor.AssetDatabase.SaveAssets();

// --- Language toggle buttons (English / Hindi) on the Canvas ---
UnityEngine.UI.Button MakeButton(string name, string label, UnityEngine.Vector2 aMin, UnityEngine.Vector2 aMax, UnityEngine.Color color)
{
    var go = new UnityEngine.GameObject(name);
    go.transform.SetParent(canvasGO.transform, false);
    var rect = go.AddComponent<UnityEngine.RectTransform>();
    rect.anchorMin = aMin;
    rect.anchorMax = aMax;
    rect.offsetMin = UnityEngine.Vector2.zero;
    rect.offsetMax = UnityEngine.Vector2.zero;
    go.AddComponent<UnityEngine.UI.Image>().color = color;
    var button = go.AddComponent<UnityEngine.UI.Button>();
    var textGO = new UnityEngine.GameObject("Text");
    textGO.transform.SetParent(go.transform, false);
    var textRect = textGO.AddComponent<UnityEngine.RectTransform>();
    textRect.anchorMin = UnityEngine.Vector2.zero;
    textRect.anchorMax = UnityEngine.Vector2.one;
    textRect.offsetMin = UnityEngine.Vector2.zero;
    textRect.offsetMax = UnityEngine.Vector2.zero;
    var text = textGO.AddComponent<UnityEngine.UI.Text>();
    text.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
    text.text = label;
    text.color = UnityEngine.Color.black;
    text.alignment = UnityEngine.TextAnchor.MiddleCenter;
    return button;
}

var langEnButton = MakeButton("Button_LangEnglish", "English", new UnityEngine.Vector2(0.05f, 0.14f), new UnityEngine.Vector2(0.24f, 0.20f), new UnityEngine.Color(0.7f, 0.7f, 0.9f));
var langHiButton = MakeButton("Button_LangHindi", "हिंदी", new UnityEngine.Vector2(0.26f, 0.14f), new UnityEngine.Vector2(0.45f, 0.20f), new UnityEngine.Color(0.9f, 0.8f, 0.5f));

var langStatusGO = new UnityEngine.GameObject("Text_LangStatus");
langStatusGO.transform.SetParent(canvasGO.transform, false);
var langStatusRect = langStatusGO.AddComponent<UnityEngine.RectTransform>();
langStatusRect.anchorMin = new UnityEngine.Vector2(0.47f, 0.14f);
langStatusRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.20f);
langStatusRect.offsetMin = UnityEngine.Vector2.zero;
langStatusRect.offsetMax = UnityEngine.Vector2.zero;
var langStatusText = langStatusGO.AddComponent<UnityEngine.UI.Text>();
langStatusText.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
langStatusText.color = UnityEngine.Color.cyan;
langStatusText.alignment = UnityEngine.TextAnchor.MiddleLeft;
langStatusText.text = "Mic language: English";

var harness = harnessGO.GetComponent<ChatbotAI.Dialogue.TestHarness>();
var harnessSO = new UnityEditor.SerializedObject(harness);
harnessSO.FindProperty("langEnglishButton").objectReferenceValue = langEnButton;
harnessSO.FindProperty("langHindiButton").objectReferenceValue = langHiButton;
harnessSO.FindProperty("langStatusText").objectReferenceValue = langStatusText;
harnessSO.ApplyModifiedProperties();

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("Applied fixes: GPU offload (LLM+Whisper), numPredict cap, bilingual AvatarProfile, language toggle UI.");
