using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// Finds a character's mouth and eyelid blendshapes by the naming conventions of the
    /// common character sources, so a new character needs no hand-mapping:
    /// - Reallusion CC3/CC4/ActorCore: visemes V_Open, V_Wide, V_Tight-O, V_Explosive, V_Dental_Lip...
    /// - Meta/Oculus visemes: viseme_aa, viseme_PP, viseme_FF, viseme_SS...
    /// - ARKit 52 (MetaPerson/Avatar SDK and many others): jawOpen, mouthFunnel, mouthClose...
    /// - VRM: Fcl_MTH_A ... (vowels only)
    /// The first family whose shapes the mesh has is used. uLipSync recognises the vowels A I U E O
    /// and the consonant groups M (m/b/p: lips pressed), F (f/v: lip to teeth) and S (s/sh/ch: teeth
    /// together); each maps to a mix of shapes. These are starting weights - AvatarLipSync's
    /// Inspector is where they're tuned. Jaw shapes are marked so a single slider scales them.
    public static class AvatarFace
    {
        public readonly struct Shape
        {
            public readonly string[] names;  // alternatives, first found wins
            public readonly float weight;    // 0-1

            public Shape(string names, float weight)
            {
                this.names = names.Split('|');
                this.weight = weight;
            }
        }

        public class Family
        {
            public string name;
            public Dictionary<string, Shape[]> sounds;  // A I U E O M F S
            public string[] blink;                      // alternatives: one shape for both eyes, or "L+R"
        }

        const string Jaw = "A25_Jaw_Open|Jaw_Open|jawOpen";

        public static readonly Family[] Families =
        {
            new Family
            {
                name = "Reallusion CC / ActorCore",
                sounds = new Dictionary<string, Shape[]>
                {
                    ["A"] = new[] { new Shape("V_Open", 0.6f), new Shape(Jaw, 0.3f) },
                    ["I"] = new[] { new Shape("V_Wide", 0.5f), new Shape(Jaw, 0.08f) },
                    ["U"] = new[] { new Shape("V_Tight-O|V_Tight_O", 0.45f), new Shape(Jaw, 0.05f) },
                    ["E"] = new[] { new Shape("V_Wide", 0.4f), new Shape(Jaw, 0.15f) },
                    ["O"] = new[] { new Shape("V_Tight-O|V_Tight_O", 0.4f), new Shape(Jaw, 0.18f) },
                    ["M"] = new[] { new Shape("V_Explosive", 0.8f) },
                    ["F"] = new[] { new Shape("V_Dental_Lip", 0.7f) },
                    ["S"] = new[] { new Shape("V_Affricate", 0.45f), new Shape("V_Tight", 0.25f) },
                },
                blink = new[] { "Eye_Blink_L+Eye_Blink_R", "Eyes_Blink", "A14_Eye_Blink_Left+A15_Eye_Blink_Right" },
            },
            new Family
            {
                name = "Meta/Oculus visemes",
                sounds = new Dictionary<string, Shape[]>
                {
                    ["A"] = new[] { new Shape("viseme_aa", 0.8f) },
                    ["I"] = new[] { new Shape("viseme_I", 0.8f) },
                    ["U"] = new[] { new Shape("viseme_U", 0.8f) },
                    ["E"] = new[] { new Shape("viseme_E", 0.8f) },
                    ["O"] = new[] { new Shape("viseme_O", 0.8f) },
                    ["M"] = new[] { new Shape("viseme_PP", 0.9f) },
                    ["F"] = new[] { new Shape("viseme_FF", 0.8f) },
                    ["S"] = new[] { new Shape("viseme_SS", 0.7f) },
                },
                blink = new[] { "eyeBlinkLeft+eyeBlinkRight", "eyesClosed" },
            },
            new Family
            {
                name = "ARKit 52",
                sounds = new Dictionary<string, Shape[]>
                {
                    ["A"] = new[] { new Shape("jawOpen", 0.35f), new Shape("mouthLowerDownLeft", 0.25f), new Shape("mouthLowerDownRight", 0.25f) },
                    ["I"] = new[] { new Shape("mouthStretchLeft", 0.4f), new Shape("mouthStretchRight", 0.4f), new Shape("jawOpen", 0.1f) },
                    ["U"] = new[] { new Shape("mouthPucker", 0.7f), new Shape("jawOpen", 0.05f) },
                    ["E"] = new[] { new Shape("mouthSmileLeft", 0.25f), new Shape("mouthSmileRight", 0.25f), new Shape("jawOpen", 0.2f) },
                    ["O"] = new[] { new Shape("mouthFunnel", 0.6f), new Shape("jawOpen", 0.2f) },
                    ["M"] = new[] { new Shape("mouthClose", 0.6f), new Shape("mouthPressLeft", 0.4f), new Shape("mouthPressRight", 0.4f) },
                    ["F"] = new[] { new Shape("mouthRollLower", 0.6f), new Shape("mouthUpperUpLeft", 0.2f), new Shape("mouthUpperUpRight", 0.2f) },
                    ["S"] = new[] { new Shape("mouthStretchLeft", 0.3f), new Shape("mouthStretchRight", 0.3f) },
                },
                blink = new[] { "eyeBlinkLeft+eyeBlinkRight" },
            },
            new Family
            {
                name = "VRM",
                sounds = new Dictionary<string, Shape[]>
                {
                    ["A"] = new[] { new Shape("Fcl_MTH_A", 0.8f) },
                    ["I"] = new[] { new Shape("Fcl_MTH_I", 0.8f) },
                    ["U"] = new[] { new Shape("Fcl_MTH_U", 0.8f) },
                    ["E"] = new[] { new Shape("Fcl_MTH_E", 0.8f) },
                    ["O"] = new[] { new Shape("Fcl_MTH_O", 0.8f) },
                },
                blink = new[] { "Fcl_EYE_Close" },
            },
        };

        static readonly string[] Vowels = { "A", "I", "U", "E", "O" };

        /// Blendshape index by name, ignoring case and a "prefix." some exporters add; -1 if absent.
        public static int IndexOf(Mesh mesh, string name)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string n = mesh.GetBlendShapeName(i);
                int dot = n.LastIndexOf('.');
                if (string.Equals(n, name, System.StringComparison.OrdinalIgnoreCase) ||
                    (dot >= 0 && string.Equals(n.Substring(dot + 1), name, System.StringComparison.OrdinalIgnoreCase)))
                    return i;
            }
            return -1;
        }

        static string FindName(Mesh mesh, Shape shape)
        {
            foreach (string name in shape.names)
            {
                int i = IndexOf(mesh, name);
                if (i >= 0) return mesh.GetBlendShapeName(i);
            }
            return null;
        }

        /// The main face mesh of a character (for its blendshape names): a visible skinned mesh named
        /// like the body/head/face if one has mouth shapes, else the one with the most blendshapes.
        /// CC3+/CC4/CC5 characters copy the face shapes onto their brows, lashes, hair, teeth... too -
        /// FaceRig moves all of them.
        public static SkinnedMeshRenderer FindFace(GameObject character)
        {
            var meshes = character.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r.sharedMesh && r.sharedMesh.blendShapeCount > 0)
                .OrderByDescending(r => r.gameObject.activeSelf)
                .ThenByDescending(r => r.sharedMesh.blendShapeCount)
                .ToList();
            bool Named(SkinnedMeshRenderer r) => r.name.IndexOf("Body", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                 r.name.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                 r.name.IndexOf("Face", System.StringComparison.OrdinalIgnoreCase) >= 0;
            return meshes.FirstOrDefault(r => r.gameObject.activeSelf && Named(r) && DefaultSounds(r.sharedMesh, out _) != null)
                   ?? meshes.FirstOrDefault();
        }

        /// Every mesh of a character that has face blendshapes, driven together by blendshape name
        /// (the body's "Jaw_Open" also opens the teeth mesh's "Jaw_Open", blinking closes the lashes...).
        public class FaceRig
        {
            readonly SkinnedMeshRenderer[] meshes;
            readonly Dictionary<string, (SkinnedMeshRenderer mesh, int index)[]> targets = new Dictionary<string, (SkinnedMeshRenderer, int)[]>();

            public FaceRig(GameObject character)
            {
                meshes = character ? character.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(r => r.sharedMesh && r.sharedMesh.blendShapeCount > 0).ToArray() : new SkinnedMeshRenderer[0];
            }

            /// The character a face mesh belongs to (its Animator's object).
            public static GameObject RootOf(SkinnedMeshRenderer face)
            {
                if (!face) return null;
                var animator = face.GetComponentInParent<Animator>();
                return animator ? animator.gameObject : face.transform.root.gameObject;
            }

            (SkinnedMeshRenderer mesh, int index)[] Targets(string name)
            {
                if (string.IsNullOrEmpty(name)) return System.Array.Empty<(SkinnedMeshRenderer, int)>();
                if (!targets.TryGetValue(name, out var found))
                {
                    var list = new List<(SkinnedMeshRenderer, int)>();
                    foreach (var m in meshes)
                    {
                        int i = m.sharedMesh.GetBlendShapeIndex(name);
                        if (i < 0) i = IndexOf(m.sharedMesh, name);
                        if (i >= 0) list.Add((m, i));
                    }
                    targets[name] = found = list.ToArray();
                }
                return found;
            }

            public bool Has(string name) => Targets(name).Length > 0;

            /// The first of these alternatives ("A|B|C") the character has, or null.
            public string First(string alternatives)
            {
                foreach (string n in alternatives.Split('|'))
                    if (Has(n)) return n;
                return null;
            }

            /// Sets a blendshape (0-100) on every mesh that has it.
            public void Set(string name, float weight)
            {
                foreach (var (mesh, index) in Targets(name))
                    if (mesh) mesh.SetBlendShapeWeight(index, weight);
            }
        }

        /// Starting mouth mapping from the first family whose vowels the mesh has (consonants
        /// included where it has those shapes too); null if none - e.g. Mixamo characters have no face shapes.
        public static List<AvatarLipSyncTuning.Sound> DefaultSounds(Mesh mesh, out string family)
        {
            foreach (var f in Families)
            {
                if (!Vowels.All(v => f.sounds[v].Any(s => FindName(mesh, s) != null))) continue;
                var result = new List<AvatarLipSyncTuning.Sound>();
                foreach (var sound in f.sounds)
                {
                    var shapes = sound.Value
                        .Select(s => (name: FindName(mesh, s), s.weight))
                        .Where(s => s.name != null)
                        .Select(s => new AvatarLipSyncTuning.ShapeWeight
                        {
                            blendShape = s.name,
                            weight = s.weight,
                            isJaw = s.name.IndexOf("jaw", System.StringComparison.OrdinalIgnoreCase) >= 0,
                        })
                        .ToList();
                    if (shapes.Count > 0) result.Add(new AvatarLipSyncTuning.Sound { sound = sound.Key, shapes = shapes });
                }
                family = f.name;
                return result;
            }
            family = null;
            return null;
        }

        /// Eyelid shape names (one for both eyes, or left + right); empty if none found.
        public static string[] BlinkShapeNames(Mesh mesh) =>
            BlinkShapes(mesh).Select(i => mesh.GetBlendShapeName(i)).ToArray();

        /// Eyelid shapes (one for both eyes, or left + right); empty if none found.
        public static int[] BlinkShapes(Mesh mesh)
        {
            foreach (var f in Families)
            foreach (string option in f.blink)
            {
                int[] indices = option.Split('+').Select(n => IndexOf(mesh, n)).ToArray();
                if (indices.All(i => i >= 0)) return indices;
            }
            return new int[0];
        }
    }
}
