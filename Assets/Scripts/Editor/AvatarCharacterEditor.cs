using System.IO;
using System.Linq;
using ChatbotAI.Avatar;
using UnityEditor;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// Avatar Character inspector: the fields, then a check of what the prefab gives the app
    /// (Humanoid rig for body animation and eye gaze, eye bones, mouth and eyelid blendshapes),
    /// buttons to create the animation set / lip-sync tuning it's missing, and Show on stage.
    [CustomEditor(typeof(AvatarCharacter))]
    public class AvatarCharacterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var character = (AvatarCharacter)target;
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Check", EditorStyles.boldLabel);

            if (!character.prefab)
            {
                EditorGUILayout.HelpBox("Set Prefab: the character as its importer built it (e.g. CC/iC Unity Tools' Prefabs folder).", MessageType.Warning);
                return;
            }

            var animator = character.prefab.GetComponentInChildren<Animator>(true);
            var avatar = animator ? animator.avatar : null;
            bool human = avatar && avatar.isHuman;
            bool eyes = human && avatar.humanDescription.human.Any(h => h.humanName == "LeftEye") &&
                        avatar.humanDescription.human.Any(h => h.humanName == "RightEye");
            Line(human, "Humanoid rig", human ? "body animations can play" :
                "no Humanoid Animator - set the model's Rig to Humanoid, or body clips and eye gaze won't work");
            Line(eyes, "Eye bones", eyes ? "eyes follow the viewer" : "no eye bones in the Humanoid mapping - the eyes stay still");

            var face = AvatarFace.FindFace(character.prefab);
            var mesh = face ? face.sharedMesh : null;
            string family = null;
            if (mesh) AvatarFace.DefaultSounds(mesh, out family);
            Line(family != null, "Mouth shapes", family != null ? $"{family} ({mesh.blendShapeCount} blendshapes on '{face.name}')"
                : "no mouth blendshapes this app recognises - lip-sync needs a Lip Sync Tuning mapped by hand");
            bool blink = mesh && AvatarFace.BlinkShapes(mesh).Length > 0;
            Line(blink, "Eyelids", blink ? "blinks" : "no eyelid blendshapes found - no blinking");

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Animations", character.animations ? $"{character.animations.name}: {Describe(character.animations)}"
                    : "none - the Avatar Stage's fallback set is used", EditorStyles.wordWrappedLabel);
                if (!character.animations && GUILayout.Button("Create set", GUILayout.Width(90)))
                    CreateAnimationSet(character);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Lip Sync", character.lipSync ? $"{character.lipSync.name}: {character.lipSync.sounds.Count} sounds"
                    : "none - mapped from the face's names at runtime", EditorStyles.wordWrappedLabel);
                using (new EditorGUI.DisabledScope(family == null))
                    if (!character.lipSync && GUILayout.Button(new GUIContent("Create tuning", "Mapped from the face, to fine-tune"), GUILayout.Width(90)))
                        CreateLipSyncTuning(character, mesh);
            }

            EditorGUILayout.Space();
            var stage = Object.FindAnyObjectByType<AvatarStage>();
            using (new EditorGUI.DisabledScope(!stage || Application.isPlaying))
                if (GUILayout.Button(stage ? (stage.Shown == character ? "Reload on stage" : "Show on stage") : "Show on stage (no Avatar Stage in the open scene)"))
                    AvatarStageEditor.ShowInScene(stage, character);
        }

        static string Describe(AvatarAnimationSet set)
        {
            if (set.customController) return $"own controller ({set.customController.name})";
            return string.Join(", ", set.states.Select(s => $"{s.name} {s.clips.Count(c => c)}"));
        }

        static void Line(bool ok, string label, string detail)
        {
            var icon = EditorGUIUtility.IconContent(ok ? "TestPassed" : "console.warnicon.sml");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(icon, GUILayout.Width(18), GUILayout.Height(18));
                EditorGUILayout.LabelField(label, detail, EditorStyles.wordWrappedLabel);
            }
        }

        static string FolderOf(Object asset) => Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset)).Replace('\\', '/');

        static void CreateAnimationSet(AvatarCharacter character)
        {
            var set = CreateInstance<AvatarAnimationSet>();
            // The model file the prefab was built from: 'Prepare clips' copies its bone mapping to new motions.
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(character.prefab);
            set.characterModel = source ? source : character.prefab;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{FolderOf(character)}/{character.name} Animations.asset");
            AssetDatabase.CreateAsset(set, path);
            Undo.RecordObject(character, "Create animation set");
            character.animations = set;
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssets();
            Selection.activeObject = set;
            Debug.Log($"Created {path} - add clips to its states, then 'Prepare clips'.");
        }

        static void CreateLipSyncTuning(AvatarCharacter character, Mesh face)
        {
            var tuning = AvatarLipSyncTuning.CreateFor(face, out _);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{FolderOf(character)}/{character.name} Lip Sync.asset");
            AssetDatabase.CreateAsset(tuning, path);
            Undo.RecordObject(character, "Create lip-sync tuning");
            character.lipSync = tuning;
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {path} - fine-tune it on the Avatar in the scene (Avatar Lip Sync).");
        }
    }
}
