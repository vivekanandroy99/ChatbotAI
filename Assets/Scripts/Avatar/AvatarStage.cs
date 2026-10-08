using System;
using ChatbotAI.Dialogue;
using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// The place in the scene where the avatar stands. It shows one AvatarCharacter - the active
    /// Avatar Profile's Character, else the one set here - by spawning its prefab as a child and
    /// pointing the avatar behaviours (all on this object, so their settings survive a character
    /// change) at it: AvatarAnimator gets its Animator and animation set, AvatarLipSync its face
    /// and lip-sync tuning, AvatarBlink its eyelids, AvatarEyes its eye bones. Switching character
    /// (Inspector, Avatar Profile, or Show() at runtime - e.g. from an in-app menu later) swaps the
    /// model and re-wires everything; nothing else in the scene refers to the model itself.
    [DefaultExecutionOrder(-100)]
    public class AvatarStage : MonoBehaviour
    {
        [Tooltip("The character shown in the scene, and in Play mode when the active Avatar Profile names none.")]
        [SerializeField] AvatarCharacter character;
        [Tooltip("In Play mode, show the active Avatar Profile's Character when it names one.")]
        [SerializeField] bool followProfile = true;
        [SerializeField] DialogueController dialogue;
        [Tooltip("Body animations for characters without their own set - any Humanoid character can use them.")]
        [SerializeField] AvatarAnimationSet fallbackAnimations;
        [Tooltip("The uLipSync that hears the voice (SpeechOutput/LipSync).")]
        [SerializeField] uLipSync.uLipSync lipSyncSource;
        [Tooltip("Frame the camera on the new character's face when the character changes.")]
        [SerializeField] bool frameCameraOnChange = true;
        [SerializeField] Camera cameraToFrame;

        [SerializeField, HideInInspector] GameObject model;
        [SerializeField, HideInInspector] AvatarCharacter shown;

        /// The character on stage now, and its spawned model.
        public AvatarCharacter Shown => shown;
        public GameObject Model => model;
        public AvatarCharacter Character => character;
        public event Action<AvatarCharacter> OnCharacterChanged;

        /// The character that should be on stage.
        public AvatarCharacter Wanted
        {
            get
            {
                var profile = followProfile && dialogue && Application.isPlaying ? dialogue.ActiveProfile : null;
                return profile && profile.character ? profile.character : character;
            }
        }

        void Awake()
        {
            if (!dialogue) dialogue = FindAnyObjectByType<DialogueController>();
            if (!lipSyncSource) lipSyncSource = FindAnyObjectByType<uLipSync.uLipSync>();
        }

        AvatarCharacter lastWanted;

        void Start()
        {
            var wanted = lastWanted = Wanted;
            if (wanted && (wanted != shown || !model)) Show(wanted);
            // Already on stage from the saved scene: wire it again, so settings changed in its Character asset since
            // the scene was saved (resting eyelids, lip-sync tuning, motions) apply.
            else if (model && shown) Wire();
        }

        // Follows the active profile when it changes at runtime (another avatar picked); a character
        // put on stage with Show() stays until then.
        void Update()
        {
            // A wide and a tall (phone-style) screen frame the character differently: re-frame when it turns.
            if (frameCameraOnChange && model && framedTall != IsTall) FrameCamera();

            var wanted = Wanted;
            if (wanted == lastWanted) return;
            lastWanted = wanted;
            if (wanted && wanted != shown) Show(wanted);
        }

        bool? framedTall;
        Camera FramingCamera => cameraToFrame ? cameraToFrame : Camera.main;
        bool IsTall => FramingCamera && FramingCamera.aspect < 1f;

        /// Puts a character on stage: spawns its model and wires the avatar behaviours to it.
        public void Show(AvatarCharacter next)
        {
            if (!next || !next.prefab)
            {
                Debug.LogWarning($"AvatarStage: {(next ? next.displayName + " has no prefab" : "no character")} - nothing to show.");
                return;
            }
            var old = model;
            model = Spawn(next.prefab);
            model.transform.SetParent(transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            bool changed = shown != next;
            shown = next;
            if (!Application.isPlaying) character = next;
            Wire();
            if (old && Application.isPlaying)
            {
                // Another bot picked: the new character is posed out of sight while the old one stays on screen, then
                // one cut and one framing. Shown at once, it lay folded on the floor (head at 0.6 m) for a few frames,
                // shot up to standing, and the camera jumped three times following it (2026-10-08).
                if (reveal != null) StopCoroutine(reveal);
                reveal = StartCoroutine(Reveal(model, old, changed));
                OnCharacterChanged?.Invoke(next);
                return;
            }
            if (old) Remove(old);
            if (changed && frameCameraOnChange)
            {
                FrameCamera();
                // Framed again once the idle motion is playing: at spawn the character is still in its bind pose, and
                // an idle that stands a little to one side left the character off-centre (seen 2026-09-30).
                // The new model can stay in its unposed shape for ~0.7 s (head at 0.6 m instead of 1.5 m, seen
                // 2026-10-06 when a bot's look changed): for 3 s, framed again whenever the head has moved since.
                if (Application.isPlaying)
                {
                    reframeAt = Time.time + 0.75f;
                    settleUntil = Time.time + 3f;
                }
            }
            OnCharacterChanged?.Invoke(next);
        }

        Coroutine reveal;

        System.Collections.IEnumerator Reveal(GameObject next, GameObject old, bool frame)
        {
            var hidden = new System.Collections.Generic.List<Renderer>();
            foreach (var r in next.GetComponentsInChildren<Renderer>(true))
                if (r.enabled) { r.enabled = false; hidden.Add(r); }
            Ensure<AvatarAnimator>().PoseNow();
            // Shown once the head holds still (the idle's pose has taken), at most 1 s.
            Vector3? last = null;
            int still = 0;
            for (float until = Time.time + 1f; Time.time < until && still < 3 && next;)
            {
                yield return null;
                var head = HeadPosition();
                still = head.HasValue && last.HasValue && (head.Value - last.Value).sqrMagnitude < 0.02f * 0.02f ? still + 1 : 0;
                last = head;
            }
            reveal = null;
            if (old) Remove(old);
            if (!next) yield break;
            foreach (var r in hidden) if (r) r.enabled = true;
            reframeAt = settleUntil = -1f;
            if (frame && frameCameraOnChange) FrameCamera();
        }

        void Wire()
        {
            var animator = model.GetComponentInChildren<Animator>();
            var face = AvatarFace.FindFace(model);
            if (!animator || !animator.isHuman)
                Debug.LogWarning($"AvatarStage: {shown.displayName} has no Humanoid Animator - body animations and eye gaze need one " +
                                 "(set the model's Rig to Humanoid).");
            if (!face) Debug.LogWarning($"AvatarStage: {shown.displayName} has no face blendshapes - no lip-sync or blinking.");
            Ensure<AvatarAnimator>().Configure(animator, shown.animations ? shown.animations : fallbackAnimations);
            Ensure<AvatarLipSync>().Configure(lipSyncSource, face, shown.lipSync);
            Ensure<AvatarBlink>().Configure(face, shown.restingEyelids);
            Ensure<AvatarEyes>().Configure(animator);
        }

        T Ensure<T>() where T : Component
        {
            var c = GetComponent<T>();
            return c ? c : gameObject.AddComponent<T>();
        }

        /// The screen is taller than wide (phone-style / portrait TV) - framed with the character's tall-screen values.
        public bool IsTallScreen => IsTall;

        // The menu's Camera page moves the camera live while a slider is dragged; saved when it's let go.
        CameraFraming? preview;
        AvatarCharacter previewFor;
        bool previewTall;

        /// How the camera frames the character on stage for the current screen shape: a change being previewed,
        /// else the one saved from the menu, else the character's own.
        public CameraFraming Framing => FramingFor(IsTall);

        CameraFraming FramingFor(bool tall)
        {
            if (!shown) return default;
            if (preview.HasValue && previewFor == shown && previewTall == tall) return preview.Value;
            if (!Application.isPlaying) return CameraFraming.Defaults(shown, tall);
            if (!loaded.HasValue || loadedFor != shown || loadedTall != tall)
            {
                loaded = CameraFraming.Load(shown, tall);
                loadedFor = shown;
                loadedTall = tall;
            }
            return loaded.Value;
        }

        // The saved framing, read once per character and screen shape (Follow asks every frame).
        CameraFraming? loaded;
        AvatarCharacter loadedFor;
        bool loadedTall;

        /// The menu changed the framing (for the character on stage and this screen shape) - shown straight away.
        public void PreviewFraming(CameraFraming framing)
        {
            preview = framing;
            previewFor = shown;
            previewTall = IsTall;
            FrameCamera();
        }

        /// Keeps the previewed framing (per character and screen shape, PlayerPrefs).
        public void SaveFraming()
        {
            if (preview.HasValue && previewFor) CameraFraming.Save(previewFor, previewTall, preview.Value);
            loaded = null;
        }

        /// Back to the character's own framing for this screen shape.
        public void ResetFraming()
        {
            if (!shown) return;
            CameraFraming.Forget(shown, IsTall);
            preview = null;
            loaded = null;
            FrameCamera();
        }

        public bool FramingChanged => shown && CameraFraming.IsSaved(shown, IsTall);

        /// Frames the character from the front of the face (the eyes are in front of the head bone), with the
        /// character's wide-screen or tall-screen framing depending on the camera's shape (as adjusted in the menu).
        public void FrameCamera() => FrameCamera(0f);

        float cameraOwnFov = -1f;
        float reframeAt = -1f, settleUntil = -1f;
        Vector3 framedHead;

        // Follow (menu > Camera): the camera eases towards the framing each frame as the character sways.
        void LateUpdate()
        {
            if (!Application.isPlaying || !model || !shown) return;
            if (reframeAt > 0f && Time.time >= reframeAt)
            {
                reframeAt = -1f;
                if (!preview.HasValue) FrameCamera();
            }
            else if (Time.time < settleUntil && !preview.HasValue && HeadPosition() is Vector3 head &&
                     (head - framedHead).sqrMagnitude > 0.15f * 0.15f)
                FrameCamera();
            if (FramingFor(IsTall).follow) FrameCamera(Time.deltaTime);
        }

        Vector3? HeadPosition()
        {
            var animator = model ? model.GetComponentInChildren<Animator>() : null;
            var head = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            return head ? head.position : (Vector3?)null;
        }

        /// deltaTime > 0: move part of the way there (following), else jump.
        void FrameCamera(float deltaTime)
        {
            var cam = FramingCamera;
            var animator = model ? model.GetComponentInChildren<Animator>() : null;
            if (!cam || !animator || !animator.isHuman) return;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (!head) return;
            framedHead = head.position;
            var faceCenter = leftEye && rightEye ? (leftEye.position + rightEye.position) / 2 : head.position;
            // In Play mode the head turns with the animation - face the way the character stands instead.
            var forward = Application.isPlaying ? animator.transform.forward
                        : leftEye && rightEye ? faceCenter - head.position : transform.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < 1e-6f) forward = transform.forward;
            forward.Normalize();
            bool tall = cam.aspect < 1f;
            var f = FramingFor(tall);
            // Round the character (positive = the camera moves to the viewer's right), then up by the tilt.
            var toCamera = Quaternion.AngleAxis(-f.turn, Vector3.up) * forward;
            var screenRight = Vector3.Cross(Vector3.up, -toCamera).normalized;
            var target = faceCenter + Vector3.down * f.aimBelowEyes - screenRight * f.side;
            float tilt = f.tilt * Mathf.Deg2Rad;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RecordObject(cam.transform, "Frame camera");
#endif
            var position = target + (toCamera * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt)) * f.distance;
            var rotation = Quaternion.LookRotation(target - position);
            if (deltaTime > 0f)
            {
                // Eases in over about a second, so the sway of an idle motion is followed without jitter.
                float k = 1f - Mathf.Exp(-deltaTime * 2.5f);
                position = Vector3.Lerp(cam.transform.position, position, k);
                rotation = Quaternion.Slerp(cam.transform.rotation, rotation, k);
            }
            cam.transform.SetPositionAndRotation(position, rotation);
            if (Application.isPlaying)
            {
                if (cameraOwnFov < 0f) cameraOwnFov = cam.fieldOfView;
                cam.fieldOfView = f.fieldOfView > 1f ? f.fieldOfView : cameraOwnFov;
            }
            framedTall = tall;
        }

        GameObject Spawn(GameObject prefab)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Kept as a prefab instance in the scene, so importer updates to the character still apply.
                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, gameObject.scene);
                UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Show character");
                return instance;
            }
#endif
            return Instantiate(prefab);
        }

        static void Remove(GameObject old)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.DestroyObjectImmediate(old);
                return;
            }
#endif
            Destroy(old);
        }
    }
}
