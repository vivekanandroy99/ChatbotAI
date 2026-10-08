var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject speechGO = null, enGO = null, hiGO = null, dialogueGO = null;
foreach (var go in scene.GetRootGameObjects())
{
    if (go.name == "SpeechInput") speechGO = go;
    if (go.name == "WhisperEnglish") enGO = go;
    if (go.name == "WhisperHindi") hiGO = go;
    if (go.name == "DialogueBrain") dialogueGO = go;
}

var mic = speechGO.GetComponent<Whisper.Utils.MicrophoneRecord>();
mic.echo = false;          // was replaying the user's own voice after every recording
mic.dropVadPart = false;   // was cutting the last vadStopTime seconds, i.e. real words after a pause
UnityEditor.EditorUtility.SetDirty(mic);

foreach (var go in new[] { enGO, hiGO })
{
    var so = new UnityEditor.SerializedObject(go.GetComponent<Whisper.WhisperManager>());
    so.FindProperty("strategy").enumValueIndex = (int)Whisper.Native.WhisperSamplingStrategy.WHISPER_SAMPLING_BEAM_SEARCH;
    so.ApplyModifiedProperties();
}

var input = new UnityEditor.SerializedObject(speechGO.GetComponent<ChatbotAI.Audio.SpeechInputController>());
input.FindProperty("dialogueController").objectReferenceValue = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
input.ApplyModifiedProperties();

UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");
UnityEditor.EditorUtility.SetDirty(profile);
UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);

UnityEngine.Debug.Log($"STT_FIXES_OK: echo={mic.echo} dropVadPart={mic.dropVadPart} vadStop={mic.vadStop}/{mic.vadStopTime}s freq={mic.frequency} voices={profile.englishVoice}/{profile.hindiVoice}");
