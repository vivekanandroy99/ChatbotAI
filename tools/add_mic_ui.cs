var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject canvasGO = null, speechInputGO = null, harnessGO = null;
foreach (var go in roots)
{
    if (go.name == "Canvas") canvasGO = go;
    if (go.name == "SpeechInput") speechInputGO = go;
    if (go.name == "TestHarness") harnessGO = go;
}

var speechInput = speechInputGO.GetComponent<ChatbotAI.Audio.SpeechInputController>();

// Transcript text (above the question input field)
var transcriptGO = new UnityEngine.GameObject("Text_Transcript");
transcriptGO.transform.SetParent(canvasGO.transform, false);
var transcriptRect = transcriptGO.AddComponent<UnityEngine.RectTransform>();
transcriptRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.96f);
transcriptRect.anchorMax = new UnityEngine.Vector2(0.95f, 1.0f);
transcriptRect.offsetMin = UnityEngine.Vector2.zero;
transcriptRect.offsetMax = UnityEngine.Vector2.zero;
var transcriptText = transcriptGO.AddComponent<UnityEngine.UI.Text>();
transcriptText.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
transcriptText.color = UnityEngine.Color.yellow;
transcriptText.fontSize = 16;
transcriptText.alignment = UnityEngine.TextAnchor.MiddleLeft;
transcriptText.text = "(mic idle)";

// Start Listening button
var startGO = new UnityEngine.GameObject("Button_StartListening");
startGO.transform.SetParent(canvasGO.transform, false);
var startRect = startGO.AddComponent<UnityEngine.RectTransform>();
startRect.anchorMin = new UnityEngine.Vector2(0.05f, 0.02f);
startRect.anchorMax = new UnityEngine.Vector2(0.47f, 0.12f);
startRect.offsetMin = UnityEngine.Vector2.zero;
startRect.offsetMax = UnityEngine.Vector2.zero;
startGO.AddComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.2f, 0.8f, 0.3f);
var startButton = startGO.AddComponent<UnityEngine.UI.Button>();
var startTextGO = new UnityEngine.GameObject("Text");
startTextGO.transform.SetParent(startGO.transform, false);
var startTextRect = startTextGO.AddComponent<UnityEngine.RectTransform>();
startTextRect.anchorMin = UnityEngine.Vector2.zero;
startTextRect.anchorMax = UnityEngine.Vector2.one;
startTextRect.offsetMin = UnityEngine.Vector2.zero;
startTextRect.offsetMax = UnityEngine.Vector2.zero;
var startText = startTextGO.AddComponent<UnityEngine.UI.Text>();
startText.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
startText.text = "Start Listening";
startText.color = UnityEngine.Color.black;
startText.alignment = UnityEngine.TextAnchor.MiddleCenter;

// Stop Listening button
var stopGO = new UnityEngine.GameObject("Button_StopListening");
stopGO.transform.SetParent(canvasGO.transform, false);
var stopRect = stopGO.AddComponent<UnityEngine.RectTransform>();
stopRect.anchorMin = new UnityEngine.Vector2(0.53f, 0.02f);
stopRect.anchorMax = new UnityEngine.Vector2(0.95f, 0.12f);
stopRect.offsetMin = UnityEngine.Vector2.zero;
stopRect.offsetMax = UnityEngine.Vector2.zero;
stopGO.AddComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.8f, 0.3f, 0.2f);
var stopButton = stopGO.AddComponent<UnityEngine.UI.Button>();
var stopTextGO = new UnityEngine.GameObject("Text");
stopTextGO.transform.SetParent(stopGO.transform, false);
var stopTextRect = stopTextGO.AddComponent<UnityEngine.RectTransform>();
stopTextRect.anchorMin = UnityEngine.Vector2.zero;
stopTextRect.anchorMax = UnityEngine.Vector2.one;
stopTextRect.offsetMin = UnityEngine.Vector2.zero;
stopTextRect.offsetMax = UnityEngine.Vector2.zero;
var stopText = stopTextGO.AddComponent<UnityEngine.UI.Text>();
stopText.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
stopText.text = "Stop Listening";
stopText.color = UnityEngine.Color.black;
stopText.alignment = UnityEngine.TextAnchor.MiddleCenter;

// Wire into TestHarness
var harness = harnessGO.GetComponent<ChatbotAI.Dialogue.TestHarness>();
var harnessSO = new UnityEditor.SerializedObject(harness);
harnessSO.FindProperty("speechInput").objectReferenceValue = speechInput;
harnessSO.FindProperty("transcriptText").objectReferenceValue = transcriptText;
harnessSO.FindProperty("startListenButton").objectReferenceValue = startButton;
harnessSO.FindProperty("stopListenButton").objectReferenceValue = stopButton;
harnessSO.ApplyModifiedProperties();

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("Mic UI added and wired.");
