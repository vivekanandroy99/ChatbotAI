var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject testHarnessGO = null, speechOutGO = null, ttsProcGO = null;
foreach (var go in roots)
{
    if (go.name == "TestHarness") testHarnessGO = go;
    if (go.name == "SpeechOutput") speechOutGO = go;
    if (go.name == "TTSProcess") ttsProcGO = go;
}

var speechOutput = speechOutGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
var ttsProcess = ttsProcGO.GetComponent<ChatbotAI.Audio.TTSProcessManager>();
var testHarness = testHarnessGO.GetComponent<ChatbotAI.Dialogue.TestHarness>();

var soSpeech = new UnityEditor.SerializedObject(speechOutput);
soSpeech.FindProperty("processManager").objectReferenceValue = ttsProcess;
soSpeech.ApplyModifiedProperties();

var soHarness = new UnityEditor.SerializedObject(testHarness);
soHarness.FindProperty("ttsProcessManager").objectReferenceValue = ttsProcess;
soHarness.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(speechOutGO);
UnityEditor.EditorUtility.SetDirty(testHarnessGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

UnityEngine.Debug.Log("WIRE_TTS_FIX_OK: processManager wired into SpeechOutputController and TestHarness.");
