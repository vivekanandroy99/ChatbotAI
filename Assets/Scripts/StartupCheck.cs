using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChatbotAI
{
    /// What this PC is missing for the app, in plain words: shown on the loading screen and at the top of the menu, so
    /// staff see it at a glance (an unplugged microphone used to look like "the bot doesn't listen").
    public static class StartupCheck
    {
        public const int RecommendedGpuMb = 12 * 1024;
        public const int RecommendedRamMb = 16 * 1024;

        /// Problems found now (empty = all good). Cheap enough to call when the menu opens.
        public static List<string> Problems()
        {
            var list = new List<string>();

            var mics = Microphone.devices;
            string chosenMic = PlayerPrefs.GetString("companion/microphone", "");
            if (mics.Length == 0)
                list.Add("No microphone found - plug one in. Visitors can't talk to the bot without it.");
            else if (chosenMic.Length > 0 && !mics.Contains(chosenMic))
                list.Add($"The chosen microphone \"{chosenMic}\" isn't connected - the default microphone is used.");

            var speakers = Audio.AudioOutputDevice.List();
            string chosenSpeaker = Audio.AudioOutputDevice.Chosen;
            if (speakers.Count == 0)
                list.Add("No speaker or headphones found - the bot's replies can't be heard.");
            else if (chosenSpeaker.Length > 0 && !speakers.Exists(s => s.id == chosenSpeaker))
                list.Add("The chosen speaker isn't connected - the default speaker is used.");

            string gpu = SystemInfo.graphicsDeviceName ?? "";
            if (!SystemInfo.graphicsDeviceVendor.ToUpperInvariant().Contains("NVIDIA") && !gpu.ToUpperInvariant().Contains("NVIDIA"))
                list.Add($"The graphics card ({gpu}) isn't NVIDIA - the AI needs one and will be very slow or won't start.");
            else if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < RecommendedGpuMb)
                list.Add($"The graphics card has {SystemInfo.graphicsMemorySize / 1024f:0} GB of memory - 12 GB or more is recommended " +
                         "(with less, answers are slow and Indian voices may break up; turn off unused voice engines in Advanced > AI models).");

            if (SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < RecommendedRamMb)
                list.Add($"This PC has {SystemInfo.systemMemorySize / 1024f:0} GB of memory (RAM) - 16 GB or more is recommended.");

            return list;
        }

        static List<string> atStart;

        /// The check made when the app started (logged once).
        public static List<string> AtStart
        {
            get
            {
                if (atStart != null) return atStart;
                atStart = Problems();
                foreach (string p in atStart) Debug.LogWarning("Startup check: " + p);
                if (atStart.Count == 0)
                    Debug.Log($"Startup check: all good ({SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsMemorySize / 1024f:0} GB; " +
                              $"{Microphone.devices.Length} microphone(s)).");
                return atStart;
            }
        }
    }
}
