using ChatbotAI.Audio;
using ChatbotAI.Dialogue;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ChatbotAI.Avatar
{
    /// Plays the character's AvatarAnimationSet: picks the state from what the app is doing
    /// (Talk while the voice is audible, Listen while the mic records, Think while working on an
    /// answer, else Idle), a random clip from that state, and cross-fades between them - no Animator
    /// Controller asset needed, so states and clips are edited in the set's Inspector. If the set
    /// names its own Animator Controller, that is used instead and gets a bool per state.
    [DefaultExecutionOrder(-50)]
    public class AvatarAnimator : MonoBehaviour
    {
        [SerializeField] AvatarAnimationSet animationSet;
        [SerializeField] Animator animator;
        [SerializeField] SpeechOutputController speechOutput;
        [SerializeField] SpeechInputController speechInput;
        [SerializeField] DialogueController dialogue;
        [Tooltip("Keep talking through pauses this long between sentences of a reply (they run up to ~0.7 s).")]
        [Tunable("Body", "Keep the talking pose between sentences", 0f, 2f, 0.05f, unit = "s",
                 note = "Longer = stays in the talking motion through pauses; shorter = drops back to idle sooner.")]
        [SerializeField] float holdTalkSeconds = 0.8f;
        [Tunable("Body", "Body motion speed", 0.5f, 1.5f, 0.05f, unit = "x", note = "All idle and talking motions.")]
        [SerializeField, Range(0.5f, 1.5f)] float motionSpeed = 1f;
        [Tunable("Body", "Smoothness between motions", 0.25f, 3f, 0.05f, unit = "x", note = "How slowly one motion blends into the next.")]
        [SerializeField, Range(0.25f, 3f)] float blendScale = 1f;
        [Tooltip("Testing: play this state regardless of what the app is doing (empty = automatic).")]
        [SerializeField] string forceState = "";

        public string CurrentState { get; private set; }
        public AvatarAnimationSet AnimationSet => animationSet;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable active, fading;
        AnimationClip activeClip;
        AvatarAnimationSet.State activeState;
        float blend = 1f, blendSeconds;
        float lastAudible = -100f;
        bool custom;

        /// Plays a (new) character's body: its Animator and animation set.
        public void Configure(Animator characterAnimator, AvatarAnimationSet set)
        {
            bool running = Application.isPlaying && isActiveAndEnabled;
            if (running) OnDisable();
            animator = characterAnimator;
            animationSet = set;
            if (!Application.isPlaying) return;
            if (running) OnEnable();
            else if (set && characterAnimator) enabled = true;  // switched off earlier for lack of a set
        }

        void OnEnable()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!animationSet || !animator)
            {
                Debug.LogWarning($"AvatarAnimator on {name}: needs an Animation Set and an Animator.");
                enabled = false;
                return;
            }
            animator.applyRootMotion = false;
            custom = animationSet.customController;
            if (custom)
            {
                animator.runtimeAnimatorController = animationSet.customController;
                return;
            }
            animator.runtimeAnimatorController = null;  // the graph below drives the body
            graph = PlayableGraph.Create($"{name} body");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            AnimationPlayableOutput.Create(graph, "Body", animator).SetSourcePlayable(mixer);
            graph.Play();
            activeState = null;
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
            activeState = null;
            activeClip = null;
            CurrentState = null;
        }

        /// A new character on stage: starts the motion for what the app is doing now at full weight and poses the body
        /// straight away, so it isn't seen in its unposed shape (AvatarStage keeps it hidden until it holds still).
        public void PoseNow()
        {
            if (!isActiveAndEnabled || custom || !graph.IsValid() || !animationSet) return;
            var state = animationSet.Resolve(Wanted());
            if (state == null) return;
            if (state != activeState) Play(state);
            blend = 1f;
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            graph.Evaluate(0f);
        }

        /// What the app is doing, as a state name.
        string Wanted()
        {
            if (!string.IsNullOrEmpty(forceState)) return forceState;
            bool speaking = speechOutput && speechOutput.IsSpeaking;
            if (speaking) lastAudible = Time.time;
            if (speaking || (speechOutput && speechOutput.IsBusy && Time.time - lastAudible < holdTalkSeconds))
                return AvatarAnimationSet.Talk;
            if (speechInput && speechInput.IsListening) return AvatarAnimationSet.Listen;
            if ((speechInput && speechInput.IsTranscribing) || (dialogue && dialogue.IsThinking) || (speechOutput && speechOutput.IsBusy))
                return AvatarAnimationSet.Think;
            return AvatarAnimationSet.Idle;
        }

        void Update()
        {
            string wanted = Wanted();
            if (custom)
            {
                foreach (var s in animationSet.states) SetBool(s.name, s.name == wanted);
                SetBool("Talking", wanted == AvatarAnimationSet.Talk);  // older controllers
                CurrentState = wanted;
                return;
            }

            var state = animationSet.Resolve(wanted);
            if (state == null) return;
            if (state != activeState) Play(state);
            if (active.IsValid() && activeState != null) active.SetSpeed(activeState.speed * motionSpeed);
            else if (state.loop && state.clips.Count > 1 && activeClip && active.GetTime() >= activeClip.length)
                Play(state, true);  // came round (clip time runs at the state's speed): another clip from the same state

            if (blend < 1f)
            {
                blend = Mathf.Min(1f, blend + Time.deltaTime / Mathf.Max(blendSeconds, 0.01f));
                mixer.SetInputWeight(0, blend);
                mixer.SetInputWeight(1, 1f - blend);
                if (blend >= 1f && fading.IsValid())
                {
                    graph.Disconnect(mixer, 1);
                    fading.Destroy();
                }
            }
        }

        const float RandomStartMinSeconds = 8f;
        bool startedFromTop;

        void Play(AvatarAnimationSet.State state) => Play(state, false);

        // fromTop: a clip that just came round continues with the next one from its start (it's already mid-state).
        void Play(AvatarAnimationSet.State state, bool fromTop)
        {
            startedFromTop = fromTop;
            AnimationClip clip = Pick(state);
            if (!clip) return;
            if (fading.IsValid())
            {
                graph.Disconnect(mixer, 1);
                fading.Destroy();
            }
            if (active.IsValid())
            {
                graph.Disconnect(mixer, 0);
                fading = active;
                graph.Connect(fading, 0, mixer, 1);
            }
            active = AnimationClipPlayable.Create(graph, clip);
            // A long looping clip starts somewhere random, so every reply (or idle spell) doesn't open with the same
            // gesture - with one talk clip, each answer began with the same arms-crossed moment.
            if (state.loop && clip.length > RandomStartMinSeconds && !startedFromTop)
                active.SetTime(Random.Range(0f, clip.length * 0.8f));
            active.SetSpeed(state.speed * motionSpeed);
            active.SetApplyFootIK(state.footIK);
            graph.Connect(active, 0, mixer, 0);
            blend = fading.IsValid() ? 0f : 1f;
            blendSeconds = state.blendSeconds * blendScale;
            mixer.SetInputWeight(0, blend);
            mixer.SetInputWeight(1, 1f - blend);
            activeClip = clip;
            activeState = state;
            CurrentState = state.name;
        }

        // A random clip of the state, not the one just played when there's a choice.
        AnimationClip Pick(AvatarAnimationSet.State state)
        {
            var usable = state.clips.FindAll(c => c);
            if (usable.Count == 0) return null;
            if (usable.Count > 1) usable.Remove(activeClip);
            return usable[Random.Range(0, usable.Count)];
        }

        void SetBool(string parameter, bool value)
        {
            foreach (var p in animator.parameters)
                if (p.name == parameter && p.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(parameter, value);
                    return;
                }
        }
    }
}
