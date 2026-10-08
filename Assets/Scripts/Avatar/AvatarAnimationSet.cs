using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// A character's body animations, grouped into states - edited in the Inspector, one asset
    /// per character (different avatars can have different states and clips). AvatarAnimator plays
    /// the state matching what the app is doing:
    ///   Talk    - the voice is speaking
    ///   Listen  - the mic is recording
    ///   Think   - working out what was said / an answer
    ///   Idle    - anything else (and the fallback for any state with no clips yet)
    /// More states (e.g. Greet, Refuse) can be added now and wired to events later. Each state can
    /// hold any number of clips: one is picked at random each time (and again each time a looping
    /// clip comes round), so several talk clips never look like one repeating loop.
    [CreateAssetMenu(menuName = "ChatbotAI/Avatar Animation Set", fileName = "Avatar Animations")]
    public class AvatarAnimationSet : ScriptableObject
    {
        public const string Idle = "Idle", Talk = "Talk", Listen = "Listen", Think = "Think";

        [Serializable]
        public class State
        {
            [Tooltip("Idle, Talk, Listen and Think are played automatically; other names are for later use.")]
            public string name = Idle;
            [Tooltip("Humanoid clips - one is chosen at random each time.")]
            public List<AnimationClip> clips = new List<AnimationClip>();
            [Tooltip("Blend time into this state, in seconds.")]
            [Range(0f, 2f)] public float blendSeconds = 0.35f;
            [Range(0.25f, 2f)] public float speed = 1f;
            [Tooltip("Looping states pick a new random clip each time the current one comes round.")]
            public bool loop = true;
            [Tooltip("Keep the feet where the clip plants them (the IK goals imported with motion files). Switch off for " +
                     "clips made by hand from muscle values - they have no foot goals, and the legs get pulled out of place.")]
            public bool footIK = true;
        }

        [Tooltip("The character these clips were made for (its model file) - used by the Inspector's " +
                 "'Prepare clips' button to set new motion files up as Humanoid with the same bone mapping.")]
        public GameObject characterModel;

        public List<State> states = new List<State>
        {
            new State { name = Idle, blendSeconds = 0.5f },
            new State { name = Talk, blendSeconds = 0.3f },
            new State { name = Listen, blendSeconds = 0.4f },
            new State { name = Think, blendSeconds = 0.4f },
        };

        [Tooltip("Optional: drive your own Animator Controller instead. It gets a bool per state, named like " +
                 "the state (Talk, Listen, Think), set while that state is active; the clips above are then unused.")]
        public RuntimeAnimatorController customController;

        public State Find(string stateName)
        {
            foreach (var s in states)
                if (string.Equals(s.name, stateName, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        /// The state to play for `wanted`, falling back to Idle when it has no clips.
        public State Resolve(string wanted)
        {
            var s = Find(wanted);
            if (s != null && s.clips.Exists(c => c)) return s;
            return Find(Idle);
        }
    }
}
