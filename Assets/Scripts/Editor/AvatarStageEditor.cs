using System.Linq;
using ChatbotAI.Avatar;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// Avatar Stage inspector: pick any character in the project from a list and show it in the
    /// scene (the model is swapped and lip-sync, body, eyes and blinking re-wired), or re-frame the camera.
    [CustomEditor(typeof(AvatarStage))]
    public class AvatarStageEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var stage = (AvatarStage)target;
            var characters = AssetDatabase.FindAssets("t:" + nameof(AvatarCharacter))
                .Select(g => AssetDatabase.LoadAssetAtPath<AvatarCharacter>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c).ToList();

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                int index = characters.IndexOf(stage.Shown);
                var labels = characters.Select(c => $"{c.displayName}  ({c.name})").ToArray();
                int picked = EditorGUILayout.Popup(new GUIContent("Show character", "Every Avatar Character asset in the project"), index, labels);
                if (picked != index && picked >= 0) ShowInScene(stage, characters[picked]);
                if (GUILayout.Button(new GUIContent("Reload", "Spawn the character again (e.g. after its prefab changed)"), GUILayout.Width(60)) && stage.Character)
                    ShowInScene(stage, stage.Character);
            }
            if (stage.Shown)
                EditorGUILayout.HelpBox($"On stage: {stage.Shown.displayName}. In Play mode the active Avatar Profile's Character is shown " +
                                        "instead, when it names one.", MessageType.None);
            if (characters.Count == 0)
                EditorGUILayout.HelpBox("No characters yet: Assets > Create > ChatbotAI > Avatar Character, then set its prefab.", MessageType.Info);

            EditorGUILayout.Space();
            DrawDefaultInspector();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame camera on face"))
                {
                    stage.FrameCamera();
                    if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
                }
                using (new EditorGUI.DisabledScope(!stage.Shown))
                    if (GUILayout.Button("Select character asset")) Selection.activeObject = stage.Shown;
            }
        }

        /// Edit mode: swaps the model in the scene (undoable) and marks the scene changed.
        public static void ShowInScene(AvatarStage stage, AvatarCharacter character)
        {
            Undo.SetCurrentGroupName("Show character");
            Undo.RecordObjects(stage.GetComponents<Component>().Cast<Object>().ToArray(), "Show character");
            var cam = Camera.main;
            if (cam) Undo.RecordObject(cam.transform, "Show character");
            stage.Show(character);
            foreach (var c in stage.GetComponents<Component>()) EditorUtility.SetDirty(c);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        }
    }
}
