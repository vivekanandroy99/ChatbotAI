var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject canvasGO = null, testHarnessGO = null, speechOutGO = null;
foreach (var go in roots)
{
    if (go.name == "Canvas") canvasGO = go;
    if (go.name == "TestHarness") testHarnessGO = go;
    if (go.name == "SpeechOutput") speechOutGO = go;
}

// Find an existing Text element to clone styling/anchoring from (Text_LangStatus).
UnityEngine.Transform langStatusT = canvasGO.transform.Find("Text_LangStatus");
UnityEngine.GameObject voiceStatusGO = canvasGO.transform.Find("Text_VoiceStatus")?.gameObject;
if (voiceStatusGO == null)
{
    voiceStatusGO = new UnityEngine.GameObject("Text_VoiceStatus", typeof(UnityEngine.RectTransform));
    voiceStatusGO.transform.SetParent(canvasGO.transform, false);
    var text = voiceStatusGO.AddComponent<UnityEngine.UI.Text>();
    text.font = UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
    text.fontSize = 18;
    text.color = UnityEngine.Color.white;
    text.alignment = UnityEngine.TextAnchor.MiddleLeft;

    var rect = voiceStatusGO.GetComponent<UnityEngine.RectTransform>();
    // Position it just below Text_LangStatus if we found it, else a sensible default.
    if (langStatusT != null)
    {
        var langRect = langStatusT.GetComponent<UnityEngine.RectTransform>();
        rect.anchorMin = langRect.anchorMin;
        rect.anchorMax = langRect.anchorMax;
        rect.pivot = langRect.pivot;
        rect.sizeDelta = langRect.sizeDelta;
        rect.anchoredPosition = langRect.anchoredPosition + new UnityEngine.Vector2(0, -30);
    }
    else
    {
        rect.anchorMin = new UnityEngine.Vector2(0, 1);
        rect.anchorMax = new UnityEngine.Vector2(0, 1);
        rect.pivot = new UnityEngine.Vector2(0, 1);
        rect.anchoredPosition = new UnityEngine.Vector2(20, -300);
        rect.sizeDelta = new UnityEngine.Vector2(400, 30);
    }
}

var testHarness = testHarnessGO.GetComponent<ChatbotAI.Dialogue.TestHarness>();
var speechOutput = speechOutGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
var voiceStatusText = voiceStatusGO.GetComponent<UnityEngine.UI.Text>();

var so = new UnityEditor.SerializedObject(testHarness);
so.FindProperty("speechOutput").objectReferenceValue = speechOutput;
so.FindProperty("voiceStatusText").objectReferenceValue = voiceStatusText;
so.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(testHarnessGO);
UnityEditor.EditorUtility.SetDirty(voiceStatusGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

UnityEngine.Debug.Log("WIRE_VOICE_STATUS_UI_OK");
