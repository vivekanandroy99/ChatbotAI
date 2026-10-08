using System.Collections.Generic;
using System.Linq;
using ChatbotAI.Avatar;
using UnityEditor;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// Avatar Animation Set inspector: the states and their clips, a warning for clips that can't
    /// play on the character yet, and "Prepare clips", which sets their motion files up the way this
    /// character needs: Humanoid with the character's bone mapping, only the real take (ActorCore
    /// files also carry a 0.02 s T-pose take), looping and in place. ActorCore and Mixamo motions
    /// both arrive as separate files that need this before they can play.
    [CustomEditor(typeof(AvatarAnimationSet))]
    public class AvatarAnimationSetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var set = (AvatarAnimationSet)target;

            var notReady = set.states.SelectMany(s => s.clips).Where(c => c && !c.humanMotion).Distinct().ToList();
            EditorGUILayout.Space();
            if (notReady.Count > 0)
                EditorGUILayout.HelpBox($"{notReady.Count} clip(s) aren't Humanoid yet and won't play on the character: " +
                                        string.Join(", ", notReady.Select(c => c.name)) + ". Press Prepare clips.", MessageType.Warning);
            if (!set.characterModel)
                EditorGUILayout.HelpBox("Set Character Model (the character's .fbx) so new clips get its bone mapping.", MessageType.Info);

            using (new EditorGUI.DisabledScope(!set.characterModel || Application.isPlaying))
                if (GUILayout.Button(new GUIContent("Prepare clips", "Set every clip's motion file up as Humanoid, looping, in place")))
                    Prepare(set);
        }

        public static void Prepare(AvatarAnimationSet set)
        {
            var character = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(set.characterModel)) as ModelImporter;
            if (!character || character.animationType != ModelImporterAnimationType.Human)
            {
                EditorUtility.DisplayDialog("Prepare clips", "Character Model must be a Humanoid model file.", "OK");
                return;
            }

            // Which file each entry came from, noted first: reimporting can replace the clip objects.
            var entries = new List<(AvatarAnimationSet.State state, int index, string path)>();
            foreach (var s in set.states)
                for (int i = 0; i < s.clips.Count; i++)
                    if (s.clips[i] && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s.clips[i])) is ModelImporter)
                        entries.Add((s, i, AssetDatabase.GetAssetPath(s.clips[i])));
            var files = new HashSet<string>(entries.Select(e => e.path));

            Undo.RecordObject(set, "Prepare clips");
            int done = 0;
            foreach (string path in files)
            {
                if (path == AssetDatabase.GetAssetPath(set.characterModel)) continue;
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                // Already prepared (maybe for another character on the same kind of rig - the file is shared) - unless
                // the file was replaced by a new export whose take has another name.
                if (importer.animationType == ModelImporterAnimationType.Human && importer.clipAnimations.Length == 1 &&
                    importer.importedTakeInfos.Any(t => t.name == importer.clipAnimations[0].takeName)) continue;
                importer.clipAnimations = new ModelImporterClipAnimation[0];
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                var human = importer.humanDescription;
                human.human = character.humanDescription.human;
                human.skeleton = new SkeletonBone[0];
                importer.humanDescription = human;
                importer.SaveAndReimport();

                // Only the longest take (the real motion), looping and in place.
                var take = importer.importedTakeInfos.OrderByDescending(t => t.stopTime - t.startTime).FirstOrDefault();
                var keep = importer.defaultClipAnimations.FirstOrDefault(c => c.takeName == take.name);
                if (keep == null) continue;
                keep.loopTime = true;
                keep.loopPose = true;
                keep.lockRootRotation = true;
                keep.keepOriginalOrientation = true;
                keep.lockRootHeightY = true;
                keep.keepOriginalPositionY = true;
                keep.lockRootPositionXZ = true;
                keep.keepOriginalPositionXZ = true;
                importer.clipAnimations = new[] { keep };
                importer.SaveAndReimport();
                done++;
            }
            foreach (var (state, index, path) in entries)
                state.clips[index] = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<AnimationClip>()
                    .OrderByDescending(c => c.length).FirstOrDefault() ?? state.clips[index];
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"Prepared {done} motion file(s) for {set.name}.");
        }
    }
}
