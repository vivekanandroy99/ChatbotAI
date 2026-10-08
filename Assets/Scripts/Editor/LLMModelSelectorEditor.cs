using ChatbotAI.Dialogue;
using LLMUnity;
using UnityEditor;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// Model picker: every model file on disk with its size - "Use" to switch,
    /// "Delete" to free the disk space (and the build size) of unused ones.
    [CustomEditor(typeof(LLMModelSelector))]
    public class LLMModelSelectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var selector = (LLMModelSelector)target;
            var models = LLMModelLibrary.List();

            EditorGUILayout.LabelField("Language model", EditorStyles.boldLabel);
            if (models.Count == 0)
            {
                EditorGUILayout.HelpBox($"No .gguf model files in StreamingAssets/{LLMModelLibrary.RelativeFolder}.", MessageType.Error);
                return;
            }
            if (!models.Exists(m => m.fileName == selector.ModelFile))
                EditorGUILayout.HelpBox($"The selected model ({selector.ModelFile}) isn't on disk - pick one below.", MessageType.Error);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                foreach (var model in models)
                {
                    bool inUse = model.fileName == selector.ModelFile;
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(new GUIContent((inUse ? "● " : "   ") + model.Label, model.fileName),
                            inUse ? EditorStyles.boldLabel : EditorStyles.label);
                        GUILayout.Label(model.SizeText, GUILayout.Width(55));
                        if (inUse)
                        {
                            GUILayout.Label("in use", GUILayout.Width(110));
                            continue;
                        }
                        if (GUILayout.Button("Use", GUILayout.Width(50))) selector.Select(model.fileName);
                        if (GUILayout.Button("Delete", GUILayout.Width(56))) Delete(selector, model);
                    }
                }
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Add model file...")) Add();
            }
            EditorGUILayout.HelpBox(Application.isPlaying
                ? "Stop Play mode to switch or delete models."
                : "Every model file here is copied into a build - delete the ones you don't use.", MessageType.None);
        }

        static void Add()
        {
            string path = EditorUtility.OpenFilePanel("Add language model", "", "gguf");
            if (string.IsNullOrEmpty(path)) return;
            if (LLMModelLibrary.Add(path, out string fileName, out string error))
            {
                AssetDatabase.Refresh();
                Debug.Log($"Added model {fileName}. Press \"Use\" to run it.");
            }
            else
            {
                EditorUtility.DisplayDialog("Couldn't add model", error, "OK");
            }
        }

        static void Delete(LLMModelSelector selector, LLMModelLibrary.ModelFile model)
        {
            if (!EditorUtility.DisplayDialog("Delete model",
                    $"Permanently delete {model.fileName} ({model.SizeText})?\n\nYou'd have to download it again to use it.",
                    "Delete", "Cancel"))
                return;
            if (!LLMModelLibrary.Delete(model.fileName, selector.ModelFile, out string error))
            {
                EditorUtility.DisplayDialog("Couldn't delete model", error, "OK");
                return;
            }
            // LLMUnity keeps its entries by relative path (Models/LLM/x.gguf).
            var entry = LLMManager.modelEntries.Find(e => System.IO.Path.GetFileName(e.filename) == model.fileName);
            if (entry != null) LLMManager.Remove(entry);
            AssetDatabase.Refresh();
            Debug.Log($"Deleted model {model.fileName} ({model.SizeText}).");
        }
    }
}
