using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// Moves the character's mouth from what uLipSync hears: for each sound (vowels A I U E O and
    /// consonant groups M/F/S) a mix of face blendshapes, scaled by how loud the voice is. The
    /// numbers live in a Lip Sync Tuning asset (one per face, set by the character) and are edited
    /// in this component's Inspector: overall mouth strength, a separate jaw strength, how fast the
    /// mouth opens and closes, the loudness range, and the shapes per sound, with preview buttons.
    /// With no tuning set, the mouth is mapped from the face's own blendshape names.
    [DefaultExecutionOrder(50)]
    public class AvatarLipSync : MonoBehaviour
    {
        [SerializeField] uLipSync.uLipSync source;
        [SerializeField] SkinnedMeshRenderer face;
        [SerializeField] AvatarLipSyncTuning tuning;

        // On top of the character's tuning, for every character (menu > Advanced > Mouth & lip-sync).
        [Tunable("Mouth & lip-sync", "Lip movement", 0f, 2f, 0.05f, unit = "x", note = "Lips and mouth shapes, for every character.")]
        [SerializeField, Range(0f, 2f)] float mouthScale = 1f;
        [Tunable("Mouth & lip-sync", "Jaw opening", 0f, 2f, 0.05f, unit = "x")]
        [SerializeField, Range(0f, 2f)] float jawScale = 1f;
        [Tunable("Mouth & lip-sync", "Mouth speed", 0.5f, 2f, 0.05f, unit = "x", note = "Higher follows the voice more sharply; lower is smoother.")]
        [SerializeField, Range(0.5f, 2f)] float mouthSpeed = 1f;

        /// Inspector preview: show this sound at full size instead of what's heard (null = live).
        [System.NonSerialized] public string previewSound;

        uLipSync.LipSyncInfo info;
        float level, levelVelocity;
        AvatarLipSyncTuning autoTuning;  // no tuning asset: mapped from the face at runtime
        readonly Dictionary<string, float> weights = new Dictionary<string, float>();
        readonly Dictionary<string, float> velocities = new Dictionary<string, float>();
        readonly Dictionary<string, float> totals = new Dictionary<string, float>();
        AvatarFace.FaceRig rig;

        public SkinnedMeshRenderer Face => face;
        public uLipSync.uLipSync Source => source;
        /// The tuning asset (null if the mouth is auto-mapped).
        public AvatarLipSyncTuning TuningAsset => tuning;

        AvatarLipSyncTuning Tuning
        {
            get
            {
                if (tuning) return tuning;
                if (!autoTuning && face && face.sharedMesh) autoTuning = AvatarLipSyncTuning.CreateFor(face.sharedMesh, out _);
                return autoTuning;
            }
        }

        /// Points the mouth at a (new) character's face; null tuning = map from the face's names.
        public void Configure(uLipSync.uLipSync lipSyncSource, SkinnedMeshRenderer faceMesh, AvatarLipSyncTuning lipSyncTuning)
        {
            bool running = Application.isPlaying && isActiveAndEnabled;
            if (running) OnDisable();
            source = lipSyncSource;
            face = faceMesh;
            tuning = lipSyncTuning;
            autoTuning = null;
            rig = null;
            jawBone = null;
            jawSearched = jawTurned = false;
            weights.Clear();
            velocities.Clear();
            if (running) OnEnable();
        }

        void OnEnable()
        {
            if (source) source.onLipSyncUpdate.AddListener(OnLipSyncUpdate);
        }

        void OnDisable()
        {
            if (source) source.onLipSyncUpdate.RemoveListener(OnLipSyncUpdate);
        }

        void OnLipSyncUpdate(uLipSync.LipSyncInfo lipSyncInfo) => info = lipSyncInfo;

        void LateUpdate()
        {
            var t = Tuning;
            if (!face || !t) return;
            float targetLevel = 0f;
            if (previewSound != null) targetLevel = 1f;
            else if (info.rawVolume > 0f)
                targetLevel = Mathf.Clamp01((Mathf.Log10(info.rawVolume) - t.silenceLevel) / Mathf.Max(t.fullLevel - t.silenceLevel, 1e-3f));
            level = Smooth(t, level, targetLevel, ref levelVelocity);

            // How much of each sound, smoothed, then normalised so they share one mouth.
            float sum = 0f;
            foreach (var s in t.sounds)
            {
                float target;
                if (previewSound != null) target = s.sound == previewSound ? 1f : 0f;
                else if (t.blendSounds && info.phonemeRatios != null) info.phonemeRatios.TryGetValue(s.sound, out target);
                else target = s.sound == info.phoneme ? 1f : 0f;
                weights.TryGetValue(s.sound, out float w);
                velocities.TryGetValue(s.sound, out float v);
                w = Smooth(t, w, target, ref v);
                weights[s.sound] = w;
                velocities[s.sound] = v;
                sum += w;
            }

            // Every shape this component drives starts from zero each frame, then each sound adds its
            // part; set on every mesh that has the shape (teeth, lashes, beard... move with the lips).
            totals.Clear();
            float jaw = 0f;  // how open the jaw shapes are, 0-1 (drives the jaw bone too)
            foreach (var s in t.sounds)
            {
                float w = sum > 1f ? weights[s.sound] / sum : weights[s.sound];
                foreach (var shape in s.shapes)
                {
                    if (string.IsNullOrEmpty(shape.blendShape)) continue;
                    totals.TryGetValue(shape.blendShape, out float total);
                    float strength = shape.isJaw ? t.jawStrength * jawScale : t.mouthStrength * mouthScale;
                    float amount = Mathf.Max(w, 0f) * shape.weight * strength * level;
                    totals[shape.blendShape] = total + amount * 100f;
                    if (shape.isJaw) jaw += amount;
                }
            }
            rig ??= new AvatarFace.FaceRig(AvatarFace.FaceRig.RootOf(face));
            foreach (var kv in totals) rig.Set(kv.Key, kv.Value);
            // Jaw bone: jawBoneDegrees on the most jaw-opening sound (usually "aa") at full loudness, scaled by Jaw Strength.
            if (t.jawBoneDegrees > 0f || jawTurned)
            {
                float widest = MostJaw(t);
                TurnJawBone(widest > 0f ? t.jawBoneDegrees * Mathf.Min(jaw / widest, 1.5f) : 0f);
            }
            // A shape taken out of the tuning (Inspector edit) is let go of rather than left where it was.
            foreach (string name in driven)
                if (!totals.ContainsKey(name)) rig.Set(name, 0f);
            driven.Clear();
            driven.UnionWith(totals.Keys);
        }

        readonly HashSet<string> driven = new HashSet<string>();

        // Jaw bone (CC3+/CC4/CC5 faces open the mouth with it). Turned after the body animation has
        // posed it; if nothing re-poses it between frames, our own last pose is the base, so it
        // doesn't keep turning further.
        Transform jawBone;
        bool jawSearched, jawTurned;
        Quaternion jawBase, jawLastSet;

        /// How far lip-sync turned the jaw bone this frame, in degrees (for tests / tuning).
        public float JawDegrees { get; private set; }
        /// How loud the voice is now on the tuning's silence..full scale, 0-1 (for tests / tuning).
        public float Level => level;

        void TurnJawBone(float degrees)
        {
            JawDegrees = degrees;
            if (!jawSearched)
            {
                jawSearched = true;
                jawBone = FindJawBone(AvatarFace.FaceRig.RootOf(face));
            }
            if (!jawBone) return;
            if (!jawTurned || jawBone.localRotation != jawLastSet) jawBase = jawBone.localRotation;
            jawBone.localRotation = jawBase;
            if (degrees > 0.01f)
                jawBone.rotation = Quaternion.AngleAxis(degrees, jawBone.root.right) * jawBone.rotation;
            jawLastSet = jawBone.localRotation;
            jawTurned = true;
        }

        /// The largest total of jaw-shape weights any one sound has (the "aa").
        public static float MostJaw(AvatarLipSyncTuning t)
        {
            float most = 0f;
            foreach (var s in t.sounds)
            {
                float sum = 0f;
                foreach (var shape in s.shapes) if (shape.isJaw) sum += shape.weight;
                most = Mathf.Max(most, sum);
            }
            return most;
        }

        /// The jaw bone: the Humanoid Jaw, else a bone named like "JawRoot" / "Jaw".
        public static Transform FindJawBone(GameObject character)
        {
            if (!character) return null;
            var animator = character.GetComponentInChildren<Animator>();
            if (animator && animator.isHuman)
            {
                var jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
                if (jaw) return jaw;
            }
            Transform named = null;
            foreach (var t in character.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("JawRoot", StringComparison.OrdinalIgnoreCase)) return t;
                if (!named && t.name.EndsWith("_Jaw", StringComparison.OrdinalIgnoreCase)) named = t;
            }
            return named;
        }

        float Smooth(AvatarLipSyncTuning t, float current, float target, ref float velocity) =>
            Mathf.SmoothDamp(current, target, ref velocity, (target > current ? t.openSeconds : t.closeSeconds) / Mathf.Max(0.1f, mouthSpeed));
    }
}
