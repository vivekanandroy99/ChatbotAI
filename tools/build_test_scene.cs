var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
    UnityEditor.SceneManagement.NewSceneMode.Single);

// --- Placeholder AvatarProfile asset ---
System.IO.Directory.CreateDirectory("Assets/Resources/Avatars");
string profilePath = "Assets/Resources/Avatars/DefaultAvatar.asset";
ChatbotAI.Dialogue.AvatarProfile profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>(profilePath);
if (profile == null)
{
    profile = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Dialogue.AvatarProfile>();
    UnityEditor.AssetDatabase.CreateAsset(profile, profilePath);
}

// --- LLM ---
var llmGO = new UnityEngine.GameObject("LLM");
var llmComp = llmGO.AddComponent<LLMUnity.LLM>();
string qwenFull = System.IO.Path.GetFullPath("Assets/StreamingAssets/Models/LLM/Qwen2.5-3B-Instruct-Q4_K_M.gguf");
string qwenFilename = LLMUnity.LLMManager.LoadModel(qwenFull, true, "Qwen2.5-3B-Instruct-Q4_K_M");
llmComp.model = qwenFilename;

// --- Dialogue brain ---
var dialogueGO = new UnityEngine.GameObject("DialogueBrain");
var agent = dialogueGO.AddComponent<LLMUnity.LLMAgent>();
agent.llm = llmComp;
var registry = dialogueGO.AddComponent<ChatbotAI.Dialogue.AvatarRegistry>();
var controller = dialogueGO.AddComponent<ChatbotAI.Dialogue.DialogueController>();

var controllerSO = new UnityEditor.SerializedObject(controller);
controllerSO.FindProperty("agent").objectReferenceValue = agent;
controllerSO.FindProperty("avatarRegistry").objectReferenceValue = registry;
controllerSO.ApplyModifiedProperties();

// --- Whisper (English + Hindi) ---
var whisperEnGO = new UnityEngine.GameObject("WhisperEnglish");
var whisperEn = whisperEnGO.AddComponent<Whisper.WhisperManager>();
whisperEn.language = "en";
whisperEn.IsModelPathInStreamingAssets = true;
whisperEn.ModelPath = "Models/Whisper/ggml-large-v3-turbo-q5_0.bin";

var whisperHiGO = new UnityEngine.GameObject("WhisperHindi");
var whisperHi = whisperHiGO.AddComponent<Whisper.WhisperManager>();
whisperHi.language = "hi";
whisperHi.IsModelPathInStreamingAssets = true;
whisperHi.ModelPath = "Models/Whisper/ggml-hindi-large-v2-f16.bin";

var micGO = new UnityEngine.GameObject("SpeechInput");
var mic = micGO.AddComponent<Whisper.Utils.MicrophoneRecord>();
var speechInput = micGO.AddComponent<ChatbotAI.Audio.SpeechInputController>();
var speechSO = new UnityEditor.SerializedObject(speechInput);
speechSO.FindProperty("microphone").objectReferenceValue = mic;
speechSO.FindProperty("whisperEnglish").objectReferenceValue = whisperEn;
speechSO.FindProperty("whisperHindi").objectReferenceValue = whisperHi;
speechSO.ApplyModifiedProperties();

// --- UI: Canvas + EventSystem + InputField + Output Text + Submit Button ---
var eventSystemGO = new UnityEngine.GameObject("EventSystem");
eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

var canvasGO = new UnityEngine.GameObject("Canvas");
var canvas = canvasGO.AddComponent<UnityEngine.Canvas>();
canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

// Input field
var inputGO = new UnityEngine.GameObject("InputField_Question");
inputGO.transform.SetParent(canvasGO.transform, false);
var inputRect = inputGO.AddComponent<UnityEngine.RectTransform>();
inputRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.85f);
inputRect.anchorMax = new UnityEngine.Vector2(0.75f, 0.95f);
inputRect.offsetMin = UnityEngine.Vector2.zero;
inputRect.offsetMax = UnityEngine.Vector2.zero;
inputGO.AddComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(1, 1, 1, 0.9f);
var inputField = inputGO.AddComponent<UnityEngine.UI.InputField>();

var inputTextGO = new UnityEngine.GameObject("Text");
inputTextGO.transform.SetParent(inputGO.transform, false);
var inputTextRect = inputTextGO.AddComponent<UnityEngine.RectTransform>();
inputTextRect.anchorMin = UnityEngine.Vector2.zero;
inputTextRect.anchorMax = UnityEngine.Vector2.one;
inputTextRect.offsetMin = new UnityEngine.Vector2(10, 5);
inputTextRect.offsetMax = new UnityEngine.Vector2(-10, -5);
var inputText = inputTextGO.AddComponent<UnityEngine.UI.Text>();
inputText.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
inputText.color = UnityEngine.Color.black;
inputText.alignment = UnityEngine.TextAnchor.MiddleLeft;
inputField.textComponent = inputText;

// Submit button
var buttonGO = new UnityEngine.GameObject("Button_Submit");
buttonGO.transform.SetParent(canvasGO.transform, false);
var buttonRect = buttonGO.AddComponent<UnityEngine.RectTransform>();
buttonRect.anchorMin = new UnityEngine.Vector2(0.78f, 0.85f);
buttonRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.95f);
buttonRect.offsetMin = UnityEngine.Vector2.zero;
buttonRect.offsetMax = UnityEngine.Vector2.zero;
buttonGO.AddComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.2f, 0.6f, 1f, 1f);
var submitButton = buttonGO.AddComponent<UnityEngine.UI.Button>();

var buttonTextGO = new UnityEngine.GameObject("Text");
buttonTextGO.transform.SetParent(buttonGO.transform, false);
var buttonTextRect = buttonTextGO.AddComponent<UnityEngine.RectTransform>();
buttonTextRect.anchorMin = UnityEngine.Vector2.zero;
buttonTextRect.anchorMax = UnityEngine.Vector2.one;
buttonTextRect.offsetMin = UnityEngine.Vector2.zero;
buttonTextRect.offsetMax = UnityEngine.Vector2.zero;
var buttonText = buttonTextGO.AddComponent<UnityEngine.UI.Text>();
buttonText.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
buttonText.text = "Ask";
buttonText.color = UnityEngine.Color.white;
buttonText.alignment = UnityEngine.TextAnchor.MiddleCenter;

// Output text
var outputGO = new UnityEngine.GameObject("Text_Output");
outputGO.transform.SetParent(canvasGO.transform, false);
var outputRect = outputGO.AddComponent<UnityEngine.RectTransform>();
outputRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.2f);
outputRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.83f);
outputRect.offsetMin = UnityEngine.Vector2.zero;
outputRect.offsetMax = UnityEngine.Vector2.zero;
var outputText = outputGO.AddComponent<UnityEngine.UI.Text>();
outputText.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
outputText.color = UnityEngine.Color.white;
outputText.fontSize = 20;
outputText.alignment = UnityEngine.TextAnchor.UpperLeft;
outputText.text = "(reply will show here)";

// --- Harness wiring ---
var harnessGO = new UnityEngine.GameObject("TestHarness");
var harness = harnessGO.AddComponent<ChatbotAI.Dialogue.TestHarness>();
var harnessSO = new UnityEditor.SerializedObject(harness);
harnessSO.FindProperty("controller").objectReferenceValue = controller;
harnessSO.FindProperty("inputField").objectReferenceValue = inputField;
harnessSO.FindProperty("outputText").objectReferenceValue = outputText;
harnessSO.FindProperty("submitButton").objectReferenceValue = submitButton;
harnessSO.ApplyModifiedProperties();

UnityEditor.AssetDatabase.SaveAssets();
System.IO.Directory.CreateDirectory("Assets/Scenes");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Scenes/AltcoreBot_v5.unity");

UnityEngine.Debug.Log("Scene built and saved at Assets/Scenes/AltcoreBot_v5.unity");
