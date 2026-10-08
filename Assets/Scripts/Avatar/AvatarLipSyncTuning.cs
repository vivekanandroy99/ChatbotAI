using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// How a character's mouth follows the voice - one asset per face (characters built the same
    /// way, e.g. all CC/ActorCore ones, can share one). For each sound uLipSync reports (vowels
    /// A I U E O, consonant groups M = m/b/p, F = f/v, S = s/sh/ch) a mix of the face's blendshapes;
    /// plus overall mouth and jaw strength, how fast the mouth opens and closes, and the loudness
    /// range. Tuned in AvatarLipSync's Inspector (with preview buttons) on the Avatar in the scene.
    [CreateAssetMenu(menuName = "ChatbotAI/Avatar Lip Sync Tuning", fileName = "Lip Sync Tuning")]
    public class AvatarLipSyncTuning : ScriptableObject
    {
        [Serializable]
        public class ShapeWeight
        {
            public string blendShape;
            [Range(0f, 1f)] public float weight = 0.5f;
            [Tooltip("Scaled by Jaw Strength instead of Mouth Strength.")]
            public bool isJaw;
        }

        [Serializable]
        public class Sound
        {
            [Tooltip("A sound uLipSync reports: A I U E O, M (m/b/p), F (f/v), S (s/sh/ch).")]
            public string sound;
            public List<ShapeWeight> shapes = new List<ShapeWeight>();
        }

        [Header("Overall")]
        [Tooltip("Scales every lip shape.")]
        [Range(0f, 1.5f)] public float mouthStrength = 1f;
        [Tooltip("Scales jaw-opening shapes separately - lower it if the jaw moves too much.")]
        [Range(0f, 1.5f)] public float jawStrength = 0.6f;
        [Tooltip("Seconds to open towards a new shape (smaller = snappier).")]
        [Range(0.005f, 0.2f)] public float openSeconds = 0.03f;
        [Tooltip("Seconds to close / let go of a shape.")]
        [Range(0.005f, 0.3f)] public float closeSeconds = 0.06f;
        [Tooltip("Voice level (log10) at or below which the mouth is closed.")]
        [Range(-3.5f, -1f)] public float silenceLevel = -2.1f;
        [Tooltip("Voice level (log10) at which the mouth reaches full size. Veena's speech runs about -1.8 (quiet) to -1.0 (loud).")]
        [Range(-2f, 0f)] public float fullLevel = -0.95f;
        [Tooltip("Mix neighbouring sounds by how sure uLipSync is (smoother), instead of snapping to the most likely one.")]
        public bool blendSounds = true;
        [Tooltip("For faces whose mouth opens with a jaw BONE (CC3+/CC4/CC5 characters: their exported face shapes barely " +
                 "part the lips): how far the jaw bone turns, in degrees, on a full, loud 'aa' - scaled by Jaw Strength, " +
                 "other sounds in proportion to their jaw shapes. ~12-16 for CC4/CC5. 0 = shapes only (ActorScan, ARKit faces).")]
        [Range(0f, 30f)] public float jawBoneDegrees = 0f;

        [Header("Mouth shapes per sound")]
        public List<Sound> sounds = new List<Sound>();

        /// A tuning with the starting mapping for this face's naming (null if it has no recognised mouth shapes).
        public static AvatarLipSyncTuning CreateFor(Mesh face, out string family)
        {
            var sounds = AvatarFace.DefaultSounds(face, out family);
            if (sounds == null) return null;
            var tuning = CreateInstance<AvatarLipSyncTuning>();
            tuning.sounds = sounds;
            // CC3+/CC4/CC5 naming (Jaw_Open, not ActorScan's A25_Jaw_Open): the mouth opens with the jaw bone.
            if (AvatarFace.IndexOf(face, "Jaw_Open") >= 0 && AvatarFace.IndexOf(face, "A25_Jaw_Open") < 0) tuning.jawBoneDegrees = 14f;
            return tuning;
        }
    }
}
