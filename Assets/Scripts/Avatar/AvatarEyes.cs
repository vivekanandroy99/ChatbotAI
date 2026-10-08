using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// Eye gaze: looks at the viewer (the camera) with the small, quick eye flicks real eyes
    /// make, and now and then glances aside or down for a moment. Turns the Humanoid eye bones
    /// after the body animation has posed them (the Animator resets them to straight ahead every
    /// frame), and moves the face's eye-look blendshapes (ARKit eyeLook* / CC A06-A13) with the
    /// gaze so the eyelids follow - without them, looking up left white showing under the iris
    /// (a stare), so upward gaze is also kept small.
    [DefaultExecutionOrder(100)]
    public class AvatarEyes : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [Tooltip("What to look at; the main camera if empty.")]
        [SerializeField] Transform lookAt;
        [SerializeField] float maxYawDegrees = 25f;
        [SerializeField] float maxUpDegrees = 6f;
        [SerializeField] float maxDownDegrees = 15f;
        [Tooltip("Size of the small flicks around the target, in degrees.")]
        [Tunable("Eyes", "Small eye movements", 0f, 4f, 0.1f, unit = "°", note = "The tiny flicks real eyes make while looking at you. 0 = a fixed stare.")]
        [SerializeField] float flickDegrees = 1.5f;
        [SerializeField] Vector2 secondsBetweenFlicks = new Vector2(0.4f, 1.8f);
        [SerializeField] Vector2 secondsBetweenGlances = new Vector2(4f, 9f);
        [SerializeField] Vector2 glanceSeconds = new Vector2(0.4f, 0.9f);
        [Tunable("Eyes", "Glances away", 0f, 25f, 0.5f, unit = "°", note = "How far it now and then looks aside. 0 = always looks at you.")]
        [SerializeField] float glanceDegrees = 12f;
        [Tooltip("How strongly the eye-look blendshapes (eyelids) follow the gaze, 0-1.")]
        [Tunable("Eyes", "Eyelids follow the gaze", 0f, 1f, 0.05f, unit = "%")]
        [Range(0f, 1f)] [SerializeField] float lidFollow = 0.7f;

        Transform head, leftEye, rightEye;
        Vector3 headForwardLocal, headUpLocal;              // the face's forward/up in head space
        Vector3 leftForwardInEye, rightForwardInEye;        // where each eye points, in its own space
        Vector2 offset, targetOffset;                       // current / wanted gaze offset (yaw, pitch)
        float nextFlick, nextGlance, glanceEnds;
        float lastYaw, lastPitch;                           // gaze of the left eye this frame
        AvatarFace.FaceRig face;
        string lookUpL, lookUpR, lookDownL, lookDownR, lookInL, lookInR, lookOutL, lookOutR;

        /// Moves a (new) character's eyes. Call while it's still in its rest pose (just spawned).
        public void Configure(Animator characterAnimator)
        {
            animator = characterAnimator;
            if (!Application.isPlaying) return;
            enabled = true;
            Start();
        }

        void Start()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!animator || !animator.isHuman)
            {
                enabled = false;  // eye bones come from the Humanoid mapping
                return;
            }
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (!head || !leftEye || !rightEye)
            {
                enabled = false;  // a character without eye bones
                return;
            }
            // At rest the eyes look where the character faces.
            Vector3 facing = transform.forward;
            headForwardLocal = Quaternion.Inverse(head.rotation) * facing;
            headUpLocal = Quaternion.Inverse(head.rotation) * Vector3.up;
            leftForwardInEye = Quaternion.Inverse(leftEye.rotation) * facing;
            rightForwardInEye = Quaternion.Inverse(rightEye.rotation) * facing;
            // Names: CC ActorScan (A06-A13) | ARKit | CC3+/CC4/CC5 (Left/Right = the character's own sides).
            face = new AvatarFace.FaceRig(animator.gameObject);
            lookUpL = face.First("A06_Eye_Look_Up_Left|eyeLookUpLeft|Eye_Look_Up_L");
            lookUpR = face.First("A07_Eye_Look_Up_Right|eyeLookUpRight|Eye_Look_Up_R");
            lookDownL = face.First("A08_Eye_Look_Down_Left|eyeLookDownLeft|Eye_Look_Down_L");
            lookDownR = face.First("A09_Eye_Look_Down_Right|eyeLookDownRight|Eye_Look_Down_R");
            lookOutL = face.First("A10_Eye_Look_Out_Left|eyeLookOutLeft|Eye_Look_Left_L");
            lookInL = face.First("A11_Eye_Look_In_Left|eyeLookInLeft|Eye_Look_Right_L");
            lookInR = face.First("A12_Eye_Look_In_Right|eyeLookInRight|Eye_Look_Left_R");
            lookOutR = face.First("A13_Eye_Look_Out_Right|eyeLookOutRight|Eye_Look_Right_R");
            nextFlick = Time.time + 0.5f;
            nextGlance = Time.time + Random.Range(secondsBetweenGlances.x, secondsBetweenGlances.y);
        }

        void LateUpdate()
        {
            Transform target = lookAt ? lookAt : Camera.main ? Camera.main.transform : null;
            if (!target) return;

            // Flicks and glances: a new small offset every so often; a glance holds a bigger one briefly.
            if (Time.time >= nextGlance)
            {
                // Aside or a little down - never up (see the class notes).
                float side = Random.value < 0.5f ? -1f : 1f;
                targetOffset = new Vector2(side * Random.Range(0.6f, 1f) * glanceDegrees, Random.Range(0f, 0.5f) * glanceDegrees);
                glanceEnds = Time.time + Random.Range(glanceSeconds.x, glanceSeconds.y);
                nextGlance = Time.time + Random.Range(secondsBetweenGlances.x, secondsBetweenGlances.y);
                nextFlick = glanceEnds;
            }
            else if (Time.time >= nextFlick)
            {
                targetOffset = Random.insideUnitCircle * flickDegrees;
                nextFlick = Time.time + Random.Range(secondsBetweenFlicks.x, secondsBetweenFlicks.y);
            }
            // Eyes jump quickly between points (saccades), they don't drift.
            offset = Vector2.Lerp(offset, targetOffset, 1f - Mathf.Exp(-Time.deltaTime * 30f));

            Aim(leftEye, leftForwardInEye, target.position);
            Aim(rightEye, rightForwardInEye, target.position);
            FollowWithLids();
        }

        // Eye-look blendshapes in proportion to the gaze. Yaw > 0 turns the eyes to the
        // character's right: inward for the left eye, outward for the right.
        void FollowWithLids()
        {
            if (face == null) return;
            float right = Mathf.Clamp01(lastYaw / maxYawDegrees) * lidFollow * 100f;
            float left = Mathf.Clamp01(-lastYaw / maxYawDegrees) * lidFollow * 100f;
            float up = Mathf.Clamp01(-lastPitch / 20f) * lidFollow * 100f;
            float down = Mathf.Clamp01(lastPitch / 20f) * lidFollow * 100f;
            Set(lookInL, right); Set(lookOutR, right);
            Set(lookOutL, left); Set(lookInR, left);
            Set(lookUpL, up); Set(lookUpR, up);
            Set(lookDownL, down); Set(lookDownR, down);
        }

        void Set(string shape, float weight)
        {
            if (shape != null) face.Set(shape, weight);
        }

        void Aim(Transform eye, Vector3 forwardInEye, Vector3 targetPos)
        {
            // Target direction in the head's frame, as yaw/pitch, limited to what eyes can do.
            Quaternion headFrame = head.rotation * Quaternion.LookRotation(headForwardLocal, headUpLocal);
            Vector3 local = Quaternion.Inverse(headFrame) * (targetPos - eye.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg + offset.x;
            float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg + offset.y;
            yaw = Mathf.Clamp(yaw, -maxYawDegrees, maxYawDegrees);
            pitch = Mathf.Clamp(pitch, -maxUpDegrees, maxDownDegrees);
            if (eye == leftEye)
            {
                lastYaw = yaw;
                lastPitch = pitch;
            }
            Vector3 wanted = headFrame * (Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward);

            Vector3 current = eye.rotation * forwardInEye;
            eye.rotation = Quaternion.FromToRotation(current, wanted) * eye.rotation;
        }
    }
}
