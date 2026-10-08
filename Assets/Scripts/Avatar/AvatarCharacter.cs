using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// Everything one 3D character needs, in one asset - picked per avatar in the Avatar Profile
    /// ("Character"), or on the Avatar Stage in the scene:
    ///   Prefab       - the character as built by its importer (CC/iC Unity Tools, a VRM/FBX
    ///                  prefab...): mesh, materials, a Humanoid Animator.
    ///   Animations   - its body states and clips (Idle, Talk, Listen, Think...), or its own Animator
    ///                  Controller. Characters on the same kind of rig can share one set; left empty,
    ///                  the stage's fallback set is used.
    ///   Lip Sync     - which face blendshapes make each mouth sound and how strongly. Faces built
    ///                  the same way can share one; left empty, it's mapped from the face's own names.
    /// Eyes and blinking need nothing here: they're found from the Humanoid eye bones and the
    /// face's eyelid shapes. The Inspector checks what the prefab has and creates what's missing.
    [CreateAssetMenu(menuName = "ChatbotAI/Avatar Character", fileName = "New Character")]
    public class AvatarCharacter : ScriptableObject
    {
        public string displayName = "New character";

        [Tooltip("The character prefab (with a Humanoid Animator and face blendshapes).")]
        public GameObject prefab;

        [Tooltip("Body animation states and clips. Empty = the Avatar Stage's fallback set.")]
        public AvatarAnimationSet animations;

        [Tooltip("Mouth shapes per sound and lip-sync strengths. Empty = mapped from the face's blendshape names.")]
        public AvatarLipSyncTuning lipSync;

        [Header("Face")]
        [Tooltip("How far the eyelids rest down, 0-1, so the character doesn't stare. Uses the face's relaxed-eye shape " +
                 "(Eye_Relax_L/R on CC4/CC5), else part-closes the blink shape. Some exports rest with the lids wide open - " +
                 "Kevin (CC5) looks natural at 0.4, sleepy from 0.7; ActorScan faces like Party F need 0.")]
        [Range(0f, 1f)] public float restingEyelids = 0f;

        [Header("Framing - wide screens (16:9)")]
        [Tooltip("Camera distance from the face when the stage frames this character (2 m = head to waist, so body " +
                 "language shows; 1.3 m = head and shoulders).")]
        [Range(0.5f, 4f)] public float cameraDistance = 2f;
        [Tooltip("How far below the eyes the camera aims. Higher puts the face higher on screen, leaving room for the " +
                 "text panel at the bottom.")]
        [Range(-0.5f, 0.8f)] public float aimBelowEyes = 0.25f;

        [Header("Framing - tall screens (9:16, phone-style)")]
        [Tooltip("Camera distance on a tall screen: a narrow frame needs more distance to keep the shoulders and hands " +
                 "in (talking gestures swing the hands ~1 m apart - at 2.4 m they left the frame).")]
        [Range(0.5f, 5f)] public float portraitCameraDistance = 3.4f;
        [Tooltip("How far below the eyes the camera aims on a tall screen - keeps the face in the upper part, above the text panel.")]
        [Range(-0.5f, 0.8f)] public float portraitAimBelowEyes = 0.65f;

        [Header("Framing - angle (both screen shapes)")]
        [Tooltip("Degrees the camera goes round the character (positive = from the character's left, the viewer's right). " +
                 "A little turn looks more natural than straight on. The app's menu (Camera) has ready-made shots too.")]
        [Range(-45f, 45f)] public float cameraAngle = 10f;
        [Tooltip("Degrees the camera looks down on the character (negative = from below).")]
        [Range(-20f, 30f)] public float cameraTilt = 4f;
    }
}
