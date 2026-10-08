using ChatbotAI.Audio;
using UnityEditor;
using UnityEngine;
using Whisper;

namespace ChatbotAI.EditorTools
{
    /// Speech input settings plus the speech-recognition model list: every
    /// Whisper model on disk with its size and which slot uses it - "General"
    /// (English, the language check, and Hindi by default) or "Hindi" (only used
    /// when Hindi Recognizer is the fine-tuned model). Delete frees the space;
    /// "Add Whisper model file..." copies a whisper.cpp model in.
    [CustomEditor(typeof(SpeechInputController))]
    public class SpeechInputControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var general = serializedObject.FindProperty("whisperEnglish").objectReferenceValue as WhisperManager;
            var hindi = serializedObject.FindProperty("whisperHindi").objectReferenceValue as WhisperManager;
            bool fineTuned = serializedObject.FindProperty("hindiRecognizer").enumValueIndex ==
                             (int)SpeechInputController.HindiRecognizer.FineTuned;
            string generalPath = ModelPath(general), hindiPath = ModelPath(hindi);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Speech recognition models", EditorStyles.boldLabel);
            var models = WhisperModelLibrary.List();
            if (models.Count == 0)
                EditorGUILayout.HelpBox($"No Whisper models in StreamingAssets/{WhisperModelLibrary.RelativeFolder}.", MessageType.Error);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                foreach (var model in models)
                {
                    bool isGeneral = model.RelativePath == generalPath;
                    bool isHindi = model.RelativePath == hindiPath;
                    string use = isGeneral && isHindi ? "general + Hindi" : isGeneral ? "general" : isHindi ? (fineTuned ? "Hindi" : "Hindi (off)") : null;
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        bool active = isGeneral || (isHindi && fineTuned);
                        EditorGUILayout.LabelField(new GUIContent((active ? "● " : "   ") + model.Label, model.fileName),
                            active ? EditorStyles.boldLabel : EditorStyles.label);
                        GUILayout.Label(model.SizeText, GUILayout.Width(55));
                        using (new EditorGUI.DisabledScope(isGeneral))
                            if (GUILayout.Button(new GUIContent("General", "Use for English, the language check, and Hindi (unless the fine-tuned Hindi model is on)"), GUILayout.Width(60)))
                                SetModel(general, model);
                        using (new EditorGUI.DisabledScope(isHindi))
                            if (GUILayout.Button(new GUIContent("Hindi", "Use as the fine-tuned Hindi model (only used when Hindi Recognizer is set to it)"), GUILayout.Width(50)))
                                SetModel(hindi, model);
                        using (new EditorGUI.DisabledScope(use != null))
                            if (GUILayout.Button(new GUIContent("Delete", use != null ? $"In use ({use}) - pick another model for that slot first" : "Delete this model file"), GUILayout.Width(56)))
                                Delete(model);
                    }
                }
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Add Whisper model file...")) Add();
            }
            EditorGUILayout.HelpBox(Application.isPlaying
                ? "Stop Play mode to switch, add or delete models."
                : "The \"Hindi\" model is only loaded when Hindi Recognizer is set to the fine-tuned model. Every model file here " +
                  "is copied into a build - delete the ones you don't use.", MessageType.None);
        }

        static string ModelPath(WhisperManager manager) =>
            manager == null ? null : new SerializedObject(manager).FindProperty("modelPath").stringValue.Replace('\\', '/');

        static void SetModel(WhisperManager manager, WhisperModelLibrary.ModelFile model)
        {
            if (manager == null) return;
            var so = new SerializedObject(manager);
            so.FindProperty("modelPath").stringValue = model.RelativePath;
            so.FindProperty("isModelPathInStreamingAssets").boolValue = true;
            so.ApplyModifiedProperties();
            Debug.Log($"{manager.name} now uses {model.fileName}.");
        }

        static void Delete(WhisperModelLibrary.ModelFile model)
        {
            if (!EditorUtility.DisplayDialog("Delete speech recognition model",
                    $"Permanently delete {model.fileName} ({model.SizeText})?\n\nYou'd have to download it again to use it.",
                    "Delete", "Cancel"))
                return;
            if (!WhisperModelLibrary.Delete(model.fileName, null, out string error))
            {
                EditorUtility.DisplayDialog("Couldn't delete model", error, "OK");
                return;
            }
            AssetDatabase.Refresh();
            Debug.Log($"Deleted speech recognition model {model.fileName} ({model.SizeText}).");
        }

        static void Add()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Add Whisper model", "", new[] { "whisper.cpp models", "bin", "All files", "*" });
            if (string.IsNullOrEmpty(path)) return;
            if (WhisperModelLibrary.Add(path, out string fileName, out string error))
            {
                AssetDatabase.Refresh();
                Debug.Log($"Added speech recognition model {fileName}. Press \"General\" or \"Hindi\" to use it.");
            }
            else
            {
                EditorUtility.DisplayDialog("Couldn't add model", error, "OK");
            }
        }
    }
}
