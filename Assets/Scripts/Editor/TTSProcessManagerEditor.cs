using System.Collections.Generic;
using System.Linq;
using ChatbotAI.Audio;
using ChatbotAI.Dialogue;
using UnityEditor;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// Voice server settings plus its model list: every voice model on disk by
    /// engine, with its size - "Use" picks which file an engine runs, "Turn off"
    /// keeps an optional engine on disk without using it, "Delete" frees the space,
    /// "Add voice model file..." copies one in.
    [CustomEditor(typeof(TTSProcessManager))]
    public class TTSProcessManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "kokoroModel", "veenaModel", "switchedOff");
            serializedObject.ApplyModifiedProperties();

            var manager = (TTSProcessManager)target;
            var models = VoiceModelLibrary.List();
            var users = VoiceUsers();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Voice models", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                foreach (VoiceModelLibrary.Engine engine in System.Enum.GetValues(typeof(VoiceModelLibrary.Engine)))
                {
                    var files = models.Where(m => m.engine == engine).ToList();
                    users.TryGetValue(engine, out var usedBy);
                    DrawEngineHeader(manager, engine, files.Count > 0, usedBy);
                    if (files.Count == 0)
                    {
                        EditorGUILayout.HelpBox("Not downloaded. Pick one of its voices on an avatar to download it.", MessageType.None);
                        continue;
                    }
                    foreach (var model in files) DrawModel(manager, model, files.Count, usedBy);
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Add voice model file..."))
                    Add();
            }
            EditorGUILayout.HelpBox(Application.isPlaying
                ? "Stop Play mode to switch, add or delete voice models."
                : "Turn off keeps a model on disk but never loads it - its voices speak with Kokoro instead. Changes apply " +
                  "when Play starts. Voices themselves are picked on each avatar.",
                MessageType.None);
        }

        static void DrawEngineHeader(TTSProcessManager manager, VoiceModelLibrary.Engine engine, bool downloaded, List<string> usedBy)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string usage = usedBy == null ? "no avatar uses it" : "used by " + string.Join(", ", usedBy);
                EditorGUILayout.LabelField(new GUIContent(VoiceModelLibrary.EngineName(engine), usage), EditorStyles.miniBoldLabel);
                if (engine == VoiceModelLibrary.Engine.Kokoro || !downloaded) return;
                bool on = manager.IsSwitchedOn(engine);
                GUILayout.Label(on ? "on" : "off - Kokoro speaks instead", on ? EditorStyles.miniLabel : EditorStyles.miniBoldLabel);
                if (GUILayout.Button(on ? "Turn off" : "Turn on", GUILayout.Width(70)))
                {
                    Undo.RecordObject(manager, on ? "Turn voice engine off" : "Turn voice engine on");
                    manager.SetSwitchedOn(engine, !on);
                    EditorUtility.SetDirty(manager);
                }
            }
        }

        void DrawModel(TTSProcessManager manager, VoiceModelLibrary.ModelFile model, int engineFiles, List<string> usedBy)
        {
            // Kokoro and Veena can have several files to choose from; the others are one model each.
            bool choosable = (model.engine == VoiceModelLibrary.Engine.Kokoro || model.engine == VoiceModelLibrary.Engine.Veena) && engineFiles > 1;
            bool selected = !choosable || manager.SelectedModel(model.engine) == model.fileName;
            bool active = selected && manager.IsSwitchedOn(model.engine);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(new GUIContent((active ? "● " : "   ") + model.Label, model.fileName),
                    active ? EditorStyles.boldLabel : EditorStyles.label);
                GUILayout.Label(model.SizeText, GUILayout.Width(55));
                if (choosable)
                {
                    if (selected) GUILayout.Label("selected", GUILayout.Width(52));
                    else if (GUILayout.Button(new GUIContent("Use", "Run this file instead of the selected one"), GUILayout.Width(52)))
                    {
                        Undo.RecordObject(manager, "Select voice model");
                        manager.SelectModel(model.engine, model.fileName);
                        EditorUtility.SetDirty(manager);
                    }
                }
                if (GUILayout.Button("Delete", GUILayout.Width(56)))
                    Delete(manager, model, selected, usedBy);
            }
        }
        static void Delete(TTSProcessManager manager, VoiceModelLibrary.ModelFile model, bool selected, List<string> usedBy)
        {
            string reason = null;
            if (selected && model.engine == VoiceModelLibrary.Engine.Kokoro)
                reason = "Kokoro is always needed - it speaks English and stands in when another voice can't. Select another Kokoro file first.";
            else if (selected && usedBy != null && manager.IsSwitchedOn(model.engine))
                reason = $"In use by {string.Join(", ", usedBy)} - switch those avatars to another voice, or turn this engine off, first.";
            if (reason != null)
            {
                EditorUtility.DisplayDialog("Can't delete voice model", reason, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Delete voice model",
                    $"Permanently delete {model.fileName} ({model.SizeText})?\n\nYou'd have to download it again to use it.",
                    "Delete", "Cancel"))
                return;
            if (!VoiceModelLibrary.Delete(model, null, out string error))
            {
                EditorUtility.DisplayDialog("Couldn't delete voice model", error, "OK");
                return;
            }
            // Deleted the selected file of an engine that has others: select one of those.
            var rest = VoiceModelLibrary.List().FirstOrDefault(m => m.engine == model.engine && !m.isFolder);
            if (selected && rest.fileName != null)
            {
                Undo.RecordObject(manager, "Select voice model");
                manager.SelectModel(model.engine, rest.fileName);
                EditorUtility.SetDirty(manager);
            }
            Debug.Log($"Deleted voice model {model.fileName} ({model.SizeText}).");
        }

        static void Add()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Add voice model", "", new[] { "Voice models", "onnx,gguf" });
            if (string.IsNullOrEmpty(path)) return;
            if (VoiceModelLibrary.Add(path, out string fileName, out string error))
                Debug.Log($"Added voice model {fileName}. Press \"Use\" to run it.");
            else
                EditorUtility.DisplayDialog("Couldn't add voice model", error, "OK");
        }

        /// Engine -> the avatars (and which of their voices) that use it.
        static Dictionary<VoiceModelLibrary.Engine, List<string>> VoiceUsers()
        {
            var users = new Dictionary<VoiceModelLibrary.Engine, List<string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AvatarProfile)))
            {
                var profile = AssetDatabase.LoadAssetAtPath<AvatarProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null) continue;
                foreach (var (id, language) in new[] { (profile.englishVoice, "English"), (profile.hindiVoice, "Hindi") })
                {
                    if (!VoiceCatalog.TryGet(id, out var voice)) continue;
                    var engine = VoiceModelLibrary.EngineOf(voice);
                    if (!users.TryGetValue(engine, out var list)) users[engine] = list = new List<string>();
                    list.Add($"{profile.name} ({language})");
                }
            }
            return users;
        }
    }
}
