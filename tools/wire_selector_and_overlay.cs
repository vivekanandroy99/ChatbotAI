var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject llmGO = null, canvasGO = null, dialogueGO = null, whisperEnGO = null, whisperHiGO = null, ttsGO = null;
foreach (var go in scene.GetRootGameObjects())
{
    if (go.name == "LLM") llmGO = go;
    if (go.name == "Canvas") canvasGO = go;
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
    if (go.name == "TTSProcess") ttsGO = go;
}

// --- Model selector (defaults to the accurate 3B model) ---
var selector = llmGO.GetComponent<ChatbotAI.Dialogue.LLMModelSelector>();
if (selector == null) selector = llmGO.AddComponent<ChatbotAI.Dialogue.LLMModelSelector>();
var llm = llmGO.GetComponent<LLMUnity.LLM>();
llm.model = selector.SelectedModelPath;
UnityEditor.EditorUtility.SetDirty(llmGO);

// --- Startup overlay ---
var existing = canvasGO.transform.Find("StartupOverlay");
if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

var font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");

var overlay = new UnityEngine.GameObject("StartupOverlay", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image));
overlay.transform.SetParent(canvasGO.transform, false);
overlay.transform.SetAsLastSibling();
var oRect = overlay.GetComponent<UnityEngine.RectTransform>();
oRect.anchorMin = UnityEngine.Vector2.zero; oRect.anchorMax = UnityEngine.Vector2.one;
oRect.offsetMin = UnityEngine.Vector2.zero; oRect.offsetMax = UnityEngine.Vector2.zero;
overlay.GetComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.05f, 0.06f, 0.09f, 0.92f);

var title = new UnityEngine.GameObject("Title", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Text));
title.transform.SetParent(overlay.transform, false);
var tRect = title.GetComponent<UnityEngine.RectTransform>();
tRect.anchorMin = tRect.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
tRect.sizeDelta = new UnityEngine.Vector2(800, 50); tRect.anchoredPosition = new UnityEngine.Vector2(0, 60);
var tText = title.GetComponent<UnityEngine.UI.Text>();
tText.font = font; tText.fontSize = 30; tText.alignment = UnityEngine.TextAnchor.MiddleCenter; tText.color = UnityEngine.Color.white;
tText.text = "Starting up";

var bar = new UnityEngine.GameObject("Bar", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image));
bar.transform.SetParent(overlay.transform, false);
var bRect = bar.GetComponent<UnityEngine.RectTransform>();
bRect.anchorMin = bRect.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
bRect.sizeDelta = new UnityEngine.Vector2(600, 22); bRect.anchoredPosition = UnityEngine.Vector2.zero;
bar.GetComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.2f, 0.22f, 0.27f, 1f);

var fill = new UnityEngine.GameObject("Fill", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Image));
fill.transform.SetParent(bar.transform, false);
var fRect = fill.GetComponent<UnityEngine.RectTransform>();
fRect.anchorMin = UnityEngine.Vector2.zero; fRect.anchorMax = new UnityEngine.Vector2(0f, 1f);
fRect.offsetMin = UnityEngine.Vector2.zero; fRect.offsetMax = UnityEngine.Vector2.zero;
fill.GetComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.25f, 0.7f, 1f, 1f);

var status = new UnityEngine.GameObject("Status", typeof(UnityEngine.RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(UnityEngine.UI.Text));
status.transform.SetParent(overlay.transform, false);
var sRect = status.GetComponent<UnityEngine.RectTransform>();
sRect.anchorMin = sRect.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
sRect.sizeDelta = new UnityEngine.Vector2(900, 40); sRect.anchoredPosition = new UnityEngine.Vector2(0, -40);
var sText = status.GetComponent<UnityEngine.UI.Text>();
sText.font = font; sText.fontSize = 20; sText.alignment = UnityEngine.TextAnchor.MiddleCenter; sText.color = new UnityEngine.Color(0.8f, 0.85f, 0.9f);
sText.text = "Loading language model...";

var ui = overlay.AddComponent<ChatbotAI.UI.StartupProgressUI>();
var so = new UnityEditor.SerializedObject(ui);
so.FindProperty("dialogueController").objectReferenceValue = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var models = so.FindProperty("speechModels");
models.arraySize = 2;
models.GetArrayElementAtIndex(0).objectReferenceValue = whisperEnGO.GetComponent<Whisper.WhisperManager>();
models.GetArrayElementAtIndex(1).objectReferenceValue = whisperHiGO.GetComponent<Whisper.WhisperManager>();
so.FindProperty("engine").objectReferenceValue = ttsGO.GetComponent<ChatbotAI.Audio.TTSProcessManager>();
so.FindProperty("barFill").objectReferenceValue = fRect;
so.FindProperty("statusText").objectReferenceValue = sText;
so.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(canvasGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log($"WIRE_OK: llm.model={llm.model}");
