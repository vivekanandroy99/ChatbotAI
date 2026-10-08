using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatbotAI.Audio
{
    /// Gives uLipSync the profile calibrated for the voice that's speaking. A profile learnt from
    /// one voice hears another mostly as silence (Kevin's male voices on Kavya's profile: 69-88% of
    /// voiced frames -> the mouth stayed shut), so each voice gets its own
    /// (tools/make_lipsync_calibration.py + tools/calibrate_lipsync.cs ->
    /// Assets/Avatar LipSync/uLipSync-Profile-<voice>.asset). Voices without one use the fallback.
    /// The Inspector's "Find calibrated profiles" fills the list from that folder.
    public class LipSyncVoiceProfiles : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string voice;
            public uLipSync.Profile profile;
        }

        [SerializeField] SpeechOutputController speechOutput;
        [SerializeField] uLipSync.uLipSync lipSync;
        [Tooltip("For voices not in the list.")]
        [SerializeField] uLipSync.Profile fallback;
        public List<Entry> profiles = new List<Entry>();

        string appliedVoice;

        public void Configure(SpeechOutputController output, uLipSync.uLipSync target, uLipSync.Profile fallbackProfile)
        {
            speechOutput = output;
            lipSync = target;
            fallback = fallbackProfile;
        }

        void Update()
        {
            if (!speechOutput || !lipSync) return;
            string voice = speechOutput.CurrentVoice;
            if (voice == appliedVoice || string.IsNullOrEmpty(voice)) return;
            appliedVoice = voice;
            var entry = profiles.Find(e => e.voice == voice && e.profile);
            var profile = entry != null ? entry.profile : fallback;
            if (profile && lipSync.profile != profile)
            {
                lipSync.profile = profile;
                Debug.Log($"Lip-sync profile for {voice}: {profile.name}");
            }
        }
    }
}
