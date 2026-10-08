using System.IO;
using System.Linq;
using ChatbotAI.Audio;
using ChatbotAI.Dialogue;
using UnityEditor;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// Avatar Profile inspector: the default fields, plus a drop box right under
    /// the document list that accepts files straight from Windows Explorer, and
    /// voice pickers (the speech model's own voices) with a preview button.
    [CustomEditor(typeof(AvatarProfile))]
    public class AvatarProfileEditor : Editor
    {
        const string ImportRoot = "Assets/KnowledgeDocuments";

        public override void OnInspectorGUI()
        {
            var profile = (AvatarProfile)target;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            var prop = serializedObject.GetIterator();
            for (bool enter = true; prop.NextVisible(enter); enter = false)
            {
                if (prop.propertyPath == nameof(AvatarProfile.englishVoice))
                {
                    DrawVoice(profile, prop, VoiceCatalog.Language.English);
                    continue;
                }
                if (prop.propertyPath == nameof(AvatarProfile.hindiVoice))
                {
                    DrawVoice(profile, prop, VoiceCatalog.Language.Hindi);
                    DrawGenderCheck(profile);
                    continue;
                }
                if (prop.propertyPath == nameof(AvatarProfile.character))
                {
                    var before = prop.objectReferenceValue;
                    EditorGUILayout.PropertyField(prop);
                    if (prop.objectReferenceValue != before) characterChanged = true;
                    EditorGUILayout.HelpBox("The character's model, animations and lip-sync are set up in its own asset (select it). " +
                                            "The Avatar Stage in the scene shows it.", MessageType.None);
                    continue;
                }
                using (new EditorGUI.DisabledScope(prop.propertyPath == "m_Script"))
                    EditorGUILayout.PropertyField(prop, true);

                if (prop.propertyPath == nameof(AvatarProfile.knowledgeDocuments))
                    DrawKnowledgeTools(profile, serializedObject.FindProperty(nameof(AvatarProfile.knowledgeDocuments)));
            }

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed) KnowledgeSync.Sync(profile);
            if (characterChanged && profile.character && !Application.isPlaying)
            {
                // Show the newly picked character in the open scene right away (Play mode switches by itself).
                var stage = Object.FindAnyObjectByType<ChatbotAI.Avatar.AvatarStage>();
                if (stage && stage.Shown != profile.character) AvatarStageEditor.ShowInScene(stage, profile.character);
            }
            characterChanged = false;
        }

        bool characterChanged;

        void DrawVoice(AvatarProfile profile, SerializedProperty prop, VoiceCatalog.Language language)
        {
            var voices = VoiceCatalog.For(language);
            int index = System.Array.FindIndex(voices, v => v.id == prop.stringValue);
            var labels = voices.Select(v => v.Label).ToList();
            if (index < 0)
            {
                // A voice id typed by hand that isn't in the list - keep it selectable.
                labels.Insert(0, $"{prop.stringValue} (not a known voice)");
                index = 0;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int picked = EditorGUILayout.Popup(new GUIContent(prop.displayName, prop.tooltip), index, labels.ToArray());
                if (picked != index)
                    prop.stringValue = voices[labels.Count > voices.Length ? picked - 1 : picked].id;

                bool canPreview = Application.isPlaying && Object.FindAnyObjectByType<SpeechOutputController>() != null;
                using (new EditorGUI.DisabledScope(!canPreview))
                {
                    var button = new GUIContent("Preview", canPreview ? "Say a sample line in this voice" : "Enter Play mode to preview voices");
                    if (GUILayout.Button(button, GUILayout.Width(64)))
                    {
                        string line = profile.ForSpeech(VoiceCatalog.PreviewLines[language], language == VoiceCatalog.Language.Hindi);
                        Object.FindAnyObjectByType<SpeechOutputController>().Speak(line, prop.stringValue, profile.speechSpeed);
                    }
                }
            }
            if (VoiceCatalog.TryGet(prop.stringValue, out var selected) && selected.engine != null)
            {
                var server = Object.FindAnyObjectByType<TTSProcessManager>(FindObjectsInactive.Include);
                var engine = VoiceModelLibrary.EngineOf(selected);
                if (server != null && !server.IsSwitchedOn(engine))
                    EditorGUILayout.HelpBox($"{VoiceModelLibrary.EngineName(engine)} is turned off (TTS Process > Voice models) - " +
                                            "this voice speaks with Kokoro until it's turned back on.", MessageType.Warning);
                else
                    DrawEngineDownload(selected);
            }
        }

        /// Download status for a voice that needs an optional engine (IndicF5).
        void DrawEngineDownload(VoiceCatalog.Voice voice)
        {
            var engine = voice.engine;
            if (VoiceEngineDownloads.IsDownloaded(engine))
            {
                EditorGUILayout.HelpBox($"{engine.displayName}: downloaded. The first Hindi reply after starting takes a few " +
                                        "seconds longer while it loads.", MessageType.None);
                return;
            }

            string fallback = voice.female ? "hf_alpha" : "hm_omega";
            switch (VoiceEngineDownloads.Status)
            {
                case VoiceEngineDownloads.State.Downloading:
                    Rect bar = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(bar, VoiceEngineDownloads.Progress, VoiceEngineDownloads.StatusMessage);
                    Repaint();
                    return;
                case VoiceEngineDownloads.State.Failed:
                    EditorGUILayout.HelpBox($"Download failed: {VoiceEngineDownloads.StatusMessage}", MessageType.Error);
                    break;
                default:
                    EditorGUILayout.HelpBox($"{engine.displayName} isn't downloaded yet ({engine.sizeText}). Until it is, Hindi " +
                                            $"replies use Kokoro's {fallback}." +
                                            (engine.gated ? " The model is gated: accept its terms on Hugging Face (logged in) before downloading." : ""),
                                            MessageType.Warning);
                    break;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"Download ({engine.sizeText})")) VoiceEngineDownloads.Download(engine);
                if (GUILayout.Button(engine.gated ? "Open terms page" : "Model page", GUILayout.Width(120))) Application.OpenURL(engine.termsUrl);
            }
        }

        static void DrawGenderCheck(AvatarProfile profile)
        {
            if (VoiceCatalog.TryGet(profile.englishVoice, out var en) && VoiceCatalog.TryGet(profile.hindiVoice, out var hi) &&
                en.female != hi.female)
                EditorGUILayout.HelpBox("The English and Hindi voices are different genders - the avatar will sound like two people.",
                    MessageType.Warning);
        }

        static void DrawKnowledgeTools(AvatarProfile profile, SerializedProperty list)
        {
            Rect box = GUILayoutUtility.GetRect(0, 44, GUILayout.ExpandWidth(true));
            GUI.Box(box, "Drop documents here from Windows Explorer\n(.pdf  .docx  .pptx  .txt  .md - any number)", EditorStyles.helpBox);

            var evt = Event.current;
            if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && box.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (string path in DragAndDrop.paths)
                    {
                        if (Directory.Exists(path))
                        {
                            foreach (string file in Directory.GetFiles(path)) AddDocument(profile, list, file);
                        }
                        else
                        {
                            AddDocument(profile, list, path);
                        }
                    }
                    GUI.changed = true;
                }
                evt.Use();
            }

            int usable = 0;
            foreach (var doc in profile.knowledgeDocuments)
            {
                if (doc == null) continue;
                string path = AssetDatabase.GetAssetPath(doc);
                if (AvatarProfile.IsSupportedDocument(path)) { usable++; continue; }
                string ext = Path.GetExtension(path).ToLowerInvariant();
                string fix = ext == ".doc" || ext == ".ppt" ? " Re-save it as .docx/.pptx." : "";
                EditorGUILayout.HelpBox($"{Path.GetFileName(path)} isn't a supported document type and will be ignored.{fix}", MessageType.Warning);
            }
            if (profile.IsOpenChat)
                EditorGUILayout.HelpBox("Open Chat mode: documents, thresholds and refusal lines aren't used - the avatar talks about anything.",
                    MessageType.Info);
            else if (usable == 0)
                EditorGUILayout.HelpBox("No documents yet - this avatar will refuse every question until you add some.", MessageType.Info);
        }

        static void AddDocument(AvatarProfile profile, SerializedProperty list, string path)
        {
            if (!AvatarProfile.IsSupportedDocument(path))
            {
                Debug.LogWarning($"Skipped '{Path.GetFileName(path)}' - supported types are .pdf, .docx, .pptx, .txt, .md.");
                return;
            }

            path = path.Replace('\\', '/');
            Object asset = path.StartsWith("Assets/")
                ? AssetDatabase.LoadMainAssetAtPath(path)
                : ImportExternalFile(profile, path);
            if (asset == null) return;

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == asset) return;

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = asset;
        }

        static Object ImportExternalFile(AvatarProfile profile, string sourcePath)
        {
            string folder = EnsureFolder(ImportRoot + "/" + profile.KnowledgeFolderName);
            string dest = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileName(sourcePath));
            File.Copy(sourcePath, dest);
            AssetDatabase.ImportAsset(dest);
            return AssetDatabase.LoadMainAssetAtPath(dest);
        }

        static string EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return folder;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            return folder;
        }
    }
}
