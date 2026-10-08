using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChatbotAI.EditorTools
{
    /// Tools > Voice > Check Recorded Speech: runs TTSServer/speech_check.py on the latest
    /// SpeechReview session (the replies flagged with F8, or all of it) and prints the report -
    /// which words came out unclear or skipped, and a naturalness score per sentence.
    public static class SpeechCheckMenu
    {
        [MenuItem("Tools/Voice/Check Recorded Speech (flagged or all)")]
        static void CheckFlagged() => Run("");

        [MenuItem("Tools/Voice/Check Recorded Speech (every sentence)")]
        static void CheckAll() => Run("--all");

        static void Run(string args)
        {
            string ttsDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TTSServer");
            var info = new ProcessStartInfo
            {
                FileName = ChatbotAI.Platform.VenvPython(Path.Combine(ttsDir, "venv")),
                Arguments = $"speech_check.py {args}",
                WorkingDirectory = ttsDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                CreateNoWindow = true,
            };
            info.EnvironmentVariables["HF_HUB_OFFLINE"] = "1";
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            Debug.Log("Checking recorded speech (loads two models - about half a minute)...");
            var process = new Process { StartInfo = info };
            var output = new System.Text.StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (output) output.AppendLine(e.Data);
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Reported from the editor's own update loop (the process's events come on other threads).
            void Poll()
            {
                if (!process.HasExited) return;
                EditorApplication.update -= Poll;
                process.WaitForExit();  // flushes the remaining output
                string report;
                lock (output) report = output.ToString();
                Debug.Log(report.Length > 0 ? "Speech check:\n" + report : "Speech check produced no report - is there a SpeechReview session yet?");
            }
            EditorApplication.update += Poll;
        }
    }
}
