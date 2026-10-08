using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChatbotAI.Audio;
using ChatbotAI.Avatar;
using ChatbotAI.Dialogue;
using UnityEditor;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// The lip-sync tuning panel, shown on the Avatar in the scene (Avatar Lip Sync) and on a Lip
    /// Sync Tuning asset: overall strength, jaw strength, speeds and loudness range; per sound the
    /// face shapes (picked from the face's own blendshapes) and weights; Preview holds a sound's mouth
    /// shape on the face (in or out of Play mode); Auto-map fills the sounds from the face's naming;
    /// in Play mode, Say test sentence speaks a line so changes can be seen live. Everything edited
    /// here is saved in the tuning asset (kept when leaving Play mode).
    class LipSyncTuningPanel
    {
        static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            ["A"] = "open - aa (आ)", ["I"] = "wide - ee (ई)", ["U"] = "rounded - oo (ऊ)", ["E"] = "half-wide - e (ए)",
            ["O"] = "round - o (ओ)", ["M"] = "lips pressed - m b p", ["F"] = "lower lip to teeth - f v", ["S"] = "teeth together - s sh ch",
        };

        string previewing;
        AvatarLipSync previewOn;

        public void Draw(AvatarLipSyncTuning tuning, AvatarLipSync lipSync)
        {
            var so = new SerializedObject(tuning);
            so.Update();
            var prop = so.GetIterator();
            for (bool enter = true; prop.NextVisible(enter); enter = false)
                if (prop.name != "sounds" && prop.name != "m_Script") EditorGUILayout.PropertyField(prop, true);
            so.ApplyModifiedProperties();

            var face = lipSync ? lipSync.Face : null;
            if (!face || !face.sharedMesh)
            {
                EditorGUILayout.HelpBox("Select the Avatar in the scene (Avatar Lip Sync) to edit the mouth shapes with the face's blendshapes and preview them.",
                    MessageType.Info);
                return;
            }
            string[] names = Enumerable.Range(0, face.sharedMesh.blendShapeCount).Select(i => face.sharedMesh.GetBlendShapeName(i)).ToArray();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Mouth shapes per sound", EditorStyles.boldLabel);
                if (GUILayout.Button(new GUIContent("Auto-map from face", "Replace the list with the starting mapping for this face's naming (CC/ActorCore, ARKit, Meta visemes, VRM)"), GUILayout.Width(140)))
                    AutoMap(tuning, face);
            }

            for (int si = 0; si < tuning.sounds.Count; si++)
            {
                var sound = tuning.sounds[si];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        string s = EditorGUILayout.TextField(sound.sound, GUILayout.Width(40));
                        if (EditorGUI.EndChangeCheck()) Change(tuning, () => sound.sound = s);
                        Descriptions.TryGetValue(sound.sound ?? "", out string description);
                        EditorGUILayout.LabelField(description ?? "", EditorStyles.miniLabel);
                        bool on = previewing == sound.sound && previewOn == lipSync;
                        if (GUILayout.Button(on ? "Stop" : "Preview", GUILayout.Width(62)))
                        {
                            if (on) StopPreview();
                            else Preview(lipSync, sound.sound);
                        }
                        if (GUILayout.Button("x", GUILayout.Width(20)))
                        {
                            Change(tuning, () => tuning.sounds.RemoveAt(si));
                            break;
                        }
                    }
                    for (int k = 0; k < sound.shapes.Count; k++)
                    {
                        var shape = sound.shapes[k];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            int index = System.Array.IndexOf(names, shape.blendShape);
                            EditorGUI.BeginChangeCheck();
                            int picked = EditorGUILayout.Popup(index, names, GUILayout.MinWidth(120));
                            float weight = EditorGUILayout.Slider(shape.weight, 0f, 1f);
                            bool jaw = GUILayout.Toggle(shape.isJaw, new GUIContent("jaw", "Scaled by Jaw Strength"), GUILayout.Width(40));
                            if (EditorGUI.EndChangeCheck())
                                Change(tuning, () =>
                                {
                                    if (picked >= 0) shape.blendShape = names[picked];
                                    shape.weight = weight;
                                    shape.isJaw = jaw;
                                });
                            if (GUILayout.Button("-", GUILayout.Width(20)))
                            {
                                Change(tuning, () => sound.shapes.RemoveAt(k));
                                break;
                            }
                        }
                    }
                    if (GUILayout.Button("+ shape", EditorStyles.miniButton, GUILayout.Width(70)))
                        Change(tuning, () => sound.shapes.Add(new AvatarLipSyncTuning.ShapeWeight { blendShape = names.FirstOrDefault(), weight = 0.5f }));
                }
            }
            if (GUILayout.Button("+ sound"))
                Change(tuning, () => tuning.sounds.Add(new AvatarLipSyncTuning.Sound { sound = "?" }));

            if (previewing != null && previewOn == lipSync) ApplyPreview(lipSync, tuning);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Say test sentence (Hindi)", "Play mode: speak a line to see the lip-sync live")))
                    Say("नमस्ते! मैं आपकी मदद के लिए यहाँ हूँ। Altscape के बारे में कुछ भी पूछिए, जैसे projects, pricing या booking।", true);
                if (GUILayout.Button("Say test sentence (English)"))
                    Say("Hello! I'm here to help. Ask me anything about Altscape, like projects, pricing, or booking a visit.", false);
            }
            EditorGUILayout.HelpBox($"Saved in '{tuning.name}' - every character using it gets these settings, also when changed in Play mode. " +
                                    "Preview holds a sound's shape at full size so weights can be judged by eye. Jaw Strength scales every shape marked 'jaw'.",
                MessageType.None);
        }

        static void Change(AvatarLipSyncTuning tuning, System.Action change)
        {
            Undo.RecordObject(tuning, "Edit lip-sync");
            change();
            EditorUtility.SetDirty(tuning);
        }

        static void AutoMap(AvatarLipSyncTuning tuning, SkinnedMeshRenderer face)
        {
            var sounds = AvatarFace.DefaultSounds(face.sharedMesh, out string family);
            if (sounds == null)
            {
                EditorUtility.DisplayDialog("Auto-map", "This face has no mouth blendshapes with a naming this app recognises.", "OK");
                return;
            }
            Change(tuning, () => tuning.sounds = sounds);
            Debug.Log($"Lip-sync mapped from the face ({family}): {string.Join(", ", sounds.Select(s => s.sound))}");
        }

        void Preview(AvatarLipSync lipSync, string sound)
        {
            StopPreview();
            previewing = sound;
            previewOn = lipSync;
            lipSync.previewSound = sound;  // Play mode: the component shows it
        }

        // Outside Play mode the component isn't running, so the shape is set here.
        void ApplyPreview(AvatarLipSync lipSync, AvatarLipSyncTuning tuning)
        {
            if (Application.isPlaying) return;
            SetShapes(lipSync.Face, tuning, previewing);
        }

        public void StopPreview()
        {
            if (previewing == null) return;
            previewing = null;
            if (!previewOn) return;
            previewOn.previewSound = null;
            var tuning = previewOn.TuningAsset;
            if (!Application.isPlaying && tuning) SetShapes(previewOn.Face, tuning, null);
            previewOn = null;
        }

        static void SetShapes(SkinnedMeshRenderer face, AvatarLipSyncTuning tuning, string sound)
        {
            if (!face) return;
            // Every mesh with the shape (teeth, lashes... on multi-mesh characters), like the component does.
            var rig = new AvatarFace.FaceRig(AvatarFace.FaceRig.RootOf(face));
            var totals = new Dictionary<string, float>();
            foreach (var s in tuning.sounds)
            foreach (var shape in s.shapes)
                if (!string.IsNullOrEmpty(shape.blendShape)) totals[shape.blendShape] = 0f;
            var active = tuning.sounds.FirstOrDefault(s => s.sound == sound);
            if (active != null)
                foreach (var shape in active.shapes)
                    if (!string.IsNullOrEmpty(shape.blendShape))
                        totals[shape.blendShape] += shape.weight * (shape.isJaw ? tuning.jawStrength : tuning.mouthStrength) * 100f;
            foreach (var kv in totals) rig.Set(kv.Key, kv.Value);

            // The jaw bone (CC3+/CC4/CC5 faces), put back where it was when the preview stops.
            var jaw = AvatarLipSync.FindJawBone(AvatarFace.FaceRig.RootOf(face));
            if (jaw)
            {
                if (!jawRest.ContainsKey(jaw)) jawRest[jaw] = jaw.localRotation;
                jaw.localRotation = jawRest[jaw];
                float widest = AvatarLipSync.MostJaw(tuning);
                float jawAmount = active == null || widest <= 0f ? 0f : active.shapes.Where(s => s.isJaw).Sum(s => s.weight) * tuning.jawStrength / widest;
                if (active != null && tuning.jawBoneDegrees > 0f)
                    jaw.rotation = Quaternion.AngleAxis(tuning.jawBoneDegrees * Mathf.Min(jawAmount, 1.5f), jaw.root.right) * jaw.rotation;
                if (active == null) jawRest.Remove(jaw);
            }
            SceneView.RepaintAll();
        }

        static readonly Dictionary<Transform, Quaternion> jawRest = new Dictionary<Transform, Quaternion>();

        static void Say(string text, bool hindi)
        {
            var output = Object.FindAnyObjectByType<SpeechOutputController>();
            var dialogue = Object.FindAnyObjectByType<DialogueController>();
            var profile = dialogue ? dialogue.ActiveProfile : null;
            if (!output || profile == null) return;
            output.Speak(profile.ForSpeech(text, hindi), hindi ? profile.hindiVoice : profile.englishVoice, profile.speechSpeed);
        }
    }

    /// Avatar Lip Sync on the Avatar: which face and voice it's wired to (set by the Avatar Stage),
    /// then the tuning panel for the character's Lip Sync Tuning asset.
    [CustomEditor(typeof(AvatarLipSync))]
    public class AvatarLipSyncEditor : Editor
    {
        readonly LipSyncTuningPanel panel = new LipSyncTuningPanel();

        void OnDisable() => panel.StopPreview();

        public override void OnInspectorGUI()
        {
            var lipSync = (AvatarLipSync)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Voice (uLipSync)", lipSync.Source, typeof(uLipSync.uLipSync), true);
                EditorGUILayout.ObjectField("Face", lipSync.Face, typeof(SkinnedMeshRenderer), true);
                EditorGUILayout.ObjectField("Tuning", lipSync.TuningAsset, typeof(AvatarLipSyncTuning), false);
            }
            var stage = lipSync.GetComponent<AvatarStage>();
            if (!lipSync.TuningAsset)
            {
                EditorGUILayout.HelpBox("No tuning asset: the mouth is mapped from the face's blendshape names. Create one to fine-tune it.", MessageType.Info);
                using (new EditorGUI.DisabledScope(!lipSync.Face || !stage || !stage.Shown))
                    if (GUILayout.Button("Create tuning for this character")) CreateTuning(lipSync, stage);
                return;
            }
            EditorGUILayout.Space();
            panel.Draw(lipSync.TuningAsset, lipSync);
        }

        static void CreateTuning(AvatarLipSync lipSync, AvatarStage stage)
        {
            var character = stage.Shown;
            var tuning = AvatarLipSyncTuning.CreateFor(lipSync.Face.sharedMesh, out _) ?? ScriptableObject.CreateInstance<AvatarLipSyncTuning>();
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(character)).Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{character.name} Lip Sync.asset");
            AssetDatabase.CreateAsset(tuning, path);
            Undo.RecordObject(character, "Create lip-sync tuning");
            character.lipSync = tuning;
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssets();
            Undo.RecordObject(lipSync, "Create lip-sync tuning");
            lipSync.Configure(lipSync.Source, lipSync.Face, tuning);
            EditorUtility.SetDirty(lipSync);
        }
    }

    /// A Lip Sync Tuning asset: the same panel, using the face of the Avatar in the open scene
    /// that uses this tuning (for the blendshape lists and preview).
    [CustomEditor(typeof(AvatarLipSyncTuning))]
    public class AvatarLipSyncTuningEditor : Editor
    {
        readonly LipSyncTuningPanel panel = new LipSyncTuningPanel();

        void OnDisable() => panel.StopPreview();

        public override void OnInspectorGUI()
        {
            var tuning = (AvatarLipSyncTuning)target;
            var user = Object.FindObjectsByType<AvatarLipSync>().FirstOrDefault(l => l.TuningAsset == tuning);
            if (!user)
            {
                DrawDefaultInspector();
                EditorGUILayout.HelpBox("Show a character that uses this tuning on the Avatar Stage to edit it with its face's blendshapes and preview.",
                    MessageType.Info);
                return;
            }
            panel.Draw(tuning, user);
        }
    }
}
