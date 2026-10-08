using System.Collections;
using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// Natural blinking: every few seconds (sometimes twice in a row), the eyelid
    /// blendshapes close and reopen. The shapes are found by name (AvatarFace) unless set.
    /// Between blinks the lids can rest partly down (the character's Resting Eyelids), so a face
    /// exported with wide-open lids doesn't stare.
    public class AvatarBlink : MonoBehaviour, ITunableListener
    {
        [Tunable("Eyes", "Eyelids", -0.5f, 0.5f, 0.01f,
                 note = "Added to every character's resting eyelids: higher = more relaxed (lids lower), lower = wider open.")]
        [SerializeField, Range(-0.5f, 0.5f)] float eyelidAdjust;
        [Tunable("Eyes", "Blinking", 0f, 2f, 0.05f, unit = "x", note = "How often it blinks. 0 = never.")]
        [SerializeField, Range(0f, 2f)] float blinkRate = 1f;

        // With no relaxed-eye shape, the blink shape rests at this share of Resting Eyelids
        // (on Kevin, blink 15% looked like Eye_Relax 40%).
        const float BlinkShareOfRest = 0.37f;

        [SerializeField] SkinnedMeshRenderer face;
        [Tooltip("Eyelid blendshape names; left empty they're found by name (Eye_Blink_L/R, eyeBlinkLeft...). Set on every " +
                 "mesh of the character that has them, so separate eyelash/eye meshes close too.")]
        [SerializeField] string[] blinkShapes = new string[0];
        [Tooltip("How far the lids rest down between blinks, 0-1 (set from the character's Resting Eyelids).")]
        [Range(0f, 1f)] [SerializeField] float restingEyelids;
        [SerializeField] Vector2 secondsBetweenBlinks = new Vector2(2f, 6f);
        [SerializeField] float closeSeconds = 0.06f;
        [SerializeField] float closedSeconds = 0.04f;
        [SerializeField] float openSeconds = 0.12f;
        [Tunable("Eyes", "Double blinks", 0f, 1f, 0.05f, unit = "%")]
        [Range(0f, 1f)] [SerializeField] float doubleBlinkChance = 0.15f;

        public void TunableChanged(string field)
        {
            if (field == nameof(eyelidAdjust) && rig != null) ApplyRest();
        }

        /// Blinks a (new) character's face; eyelid shapes found by name.
        public void Configure(SkinnedMeshRenderer faceMesh, float resting = 0f)
        {
            bool running = Application.isPlaying && isActiveAndEnabled;
            if (running) StopAllCoroutines();
            face = faceMesh;
            blinkShapes = new string[0];
            restingEyelids = resting;
            if (running) OnEnable();
        }

        AvatarFace.FaceRig rig;
        string[] relaxShapes = new string[0];
        float blinkRest;    // resting weight of the blink shape, 0-100

        void OnEnable()
        {
            if (!face) face = AvatarFace.FindFace(gameObject);
            if (!face) return;
            if (blinkShapes == null || blinkShapes.Length == 0) blinkShapes = AvatarFace.BlinkShapeNames(face.sharedMesh);
            rig = new AvatarFace.FaceRig(AvatarFace.FaceRig.RootOf(face));
            ApplyRest();
            if (blinkShapes.Length > 0) StartCoroutine(Blinking());
        }

        // The relaxed-eye shape if the face has one (it also lifts the lower lid a touch), else the blink shape part-closed.
        void ApplyRest()
        {
            relaxShapes = System.Array.FindAll(new[] { "Eye_Relax_L", "Eye_Relax_R" }, rig.Has);
            float rest = Mathf.Clamp01(restingEyelids + eyelidAdjust);
            foreach (string shape in relaxShapes) rig.Set(shape, rest * 100f);
            blinkRest = relaxShapes.Length > 0 ? 0f : rest * BlinkShareOfRest * 100f;
            Set(0f);
        }

        IEnumerator Blinking()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(secondsBetweenBlinks.x, secondsBetweenBlinks.y) / Mathf.Max(blinkRate, 0.05f));
                if (blinkRate <= 0f) continue;
                yield return Blink();
                if (Random.value < doubleBlinkChance)
                {
                    yield return new WaitForSeconds(0.15f);
                    yield return Blink();
                }
            }
        }

        IEnumerator Blink()
        {
            for (float t = 0; t < closeSeconds; t += Time.deltaTime)
            {
                Set(t / closeSeconds);
                yield return null;
            }
            Set(1f);
            yield return new WaitForSeconds(closedSeconds);
            for (float t = 0; t < openSeconds; t += Time.deltaTime)
            {
                Set(1f - t / openSeconds);
                yield return null;
            }
            Set(0f);
        }

        void Set(float closed)
        {
            foreach (string shape in blinkShapes) rig.Set(shape, Mathf.Lerp(blinkRest, 100f, closed));
        }
    }
}
