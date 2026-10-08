using System;
using System.IO;
using UnityEngine;

namespace ChatbotAI
{
    /// Tells the build's watchdog (Start AltcoreBot.cmd -> AltcoreBot Watchdog.ps1, written by PortableBuild) that the
    /// app is alive: every few seconds it touches heartbeat.txt next to the exe. A crash ends the process (the watchdog
    /// starts it again); a freeze stops the heartbeat (the watchdog closes and restarts it). Closing the app normally
    /// writes "closed", so the watchdog then stops too. Built app only.
    public class AppHeartbeat : MonoBehaviour
    {
        public const string FileName = "heartbeat.txt";
        const float Interval = 5f;

        static string PathOf => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", FileName);

        float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (Application.isEditor) return;
            var go = new GameObject("App Heartbeat") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<AppHeartbeat>();
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Interval;
            Write("alive " + DateTime.Now.ToString("s"));
        }

        void OnApplicationQuit() => Write("closed " + DateTime.Now.ToString("s"));

        static void Write(string text)
        {
            try { File.WriteAllText(PathOf, text); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
        }
    }
}
