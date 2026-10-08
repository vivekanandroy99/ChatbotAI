// Edit mode: DefaultAvatar speaks Hindi and English with Veena (Kavya), and the voice server runs Veena Q4.
// (Run only once Play mode has fully stopped - changes made during Play are reverted on exit.)
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { UnityEngine.Debug.Log("SETUP: still in Play mode - not changed"); return null; }
var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");
profile.hindiVoice = "veena_kavya";
profile.englishVoice = "veena_kavya";  // one voice in both languages
UnityEditor.EditorUtility.SetDirty(profile);
UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
var manager = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Audio.TTSProcessManager>(UnityEngine.FindObjectsInactive.Include);
var so = new UnityEditor.SerializedObject(manager);
so.FindProperty("veenaModel").stringValue = "Veena-q4_k_m.gguf";
so.FindProperty("kokoroModel").stringValue = "kokoro-v1.0.onnx";
so.ApplyModifiedProperties();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(manager.gameObject.scene);
UnityEngine.Debug.Log("SETUP: englishVoice=" + profile.englishVoice + " hindiVoice=" + profile.hindiVoice + " veenaModel=" + manager.SelectedModel(ChatbotAI.Audio.VoiceModelLibrary.Engine.Veena));
