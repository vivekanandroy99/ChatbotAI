var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject dialogueGO = null;
foreach (var go in roots) { if (go.name == "DialogueBrain") dialogueGO = go; }
var dialogueController = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();

// TTS process manager - launches/reuses the local Python sidecar.
var ttsProcGO = UnityEngine.GameObject.Find("TTSProcess");
if (ttsProcGO == null) ttsProcGO = new UnityEngine.GameObject("TTSProcess");
var ttsProc = ttsProcGO.GetComponent<ChatbotAI.Audio.TTSProcessManager>();
if (ttsProc == null) ttsProc = ttsProcGO.AddComponent<ChatbotAI.Audio.TTSProcessManager>();

// Speech output - AudioSource + controller that calls the sidecar and plays the reply.
var speechOutGO = UnityEngine.GameObject.Find("SpeechOutput");
if (speechOutGO == null) speechOutGO = new UnityEngine.GameObject("SpeechOutput");
var audioSource = speechOutGO.GetComponent<UnityEngine.AudioSource>();
if (audioSource == null) audioSource = speechOutGO.AddComponent<UnityEngine.AudioSource>();
var speechOutput = speechOutGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
if (speechOutput == null) speechOutput = speechOutGO.AddComponent<ChatbotAI.Audio.SpeechOutputController>();

var soSpeechOut = new UnityEditor.SerializedObject(speechOutput);
soSpeechOut.FindProperty("audioSource").objectReferenceValue = audioSource;
soSpeechOut.ApplyModifiedProperties();

// Bridge - routes DialogueController.OnReply text to SpeechOutputController.Speak.
var bridgeGO = UnityEngine.GameObject.Find("DialogueAudioBridge");
if (bridgeGO == null) bridgeGO = new UnityEngine.GameObject("DialogueAudioBridge");
var bridge = bridgeGO.GetComponent<ChatbotAI.Dialogue.DialogueAudioBridge>();
if (bridge == null) bridge = bridgeGO.AddComponent<ChatbotAI.Dialogue.DialogueAudioBridge>();

var soBridge = new UnityEditor.SerializedObject(bridge);
soBridge.FindProperty("dialogueController").objectReferenceValue = dialogueController;
soBridge.FindProperty("speechOutput").objectReferenceValue = speechOutput;
soBridge.ApplyModifiedProperties();

UnityEditor.EditorUtility.SetDirty(ttsProcGO);
UnityEditor.EditorUtility.SetDirty(speechOutGO);
UnityEditor.EditorUtility.SetDirty(bridgeGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

UnityEngine.Debug.Log("WIRE_TTS_OK: TTSProcess, SpeechOutput, DialogueAudioBridge wired into the scene.");
