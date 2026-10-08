using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChatbotAI.Audio
{
    /// Optional voice engines that are downloaded on request (TTSServer/download_voice.py)
    /// - Veena and IndicF5 for natural Hindi, Kokoro v1.1 for three more English voices. Used by the avatar Inspector's Download
    /// button and usable from in-app UI. The voice server loads a downloaded engine
    /// the first time one of its voices is used; until then it falls back to Kokoro.
    public static class VoiceEngineDownloads
    {
        public class Engine
        {
            public string id;
            public string displayName;
            public string sizeText;
            public string repoFolder;       // Hugging Face cache folder name, or
            public string localFilePattern; // model file(s) in TTSServer/models
            public string localSubfolder;   // ...or in this folder under it
            public string termsUrl;
            /// Licence allows only non-commercial use (shown next to its voices).
            public bool nonCommercial;
            public bool gated;              // terms must be accepted on Hugging Face first
        }

        public static readonly Engine KokoroV11 = new Engine
        {
            id = "kokoro11",
            displayName = "Kokoro v1.1 English voices (Maple, Sol, Vale)",
            sizeText = "0.4 GB",
            localFilePattern = "kokoro-v1.1*.onnx",
            termsUrl = "https://huggingface.co/hexgrad/Kokoro-82M-v1.1-zh",
        };

        public static readonly Engine Veena = new Engine
        {
            id = "veena",
            displayName = "Veena Hindi voices (Maya Research)",
            sizeText = "2.4 GB",
            localFilePattern = "*eena*.gguf",
            termsUrl = "https://huggingface.co/maya-research/Veena",
        };

        public static readonly Engine IndicF5 = new Engine
        {
            id = "indicf5",
            displayName = "IndicF5 Hindi voices (AI4Bharat)",
            sizeText = "1.4 GB",
            repoFolder = "models--ai4bharat--IndicF5",
            termsUrl = "https://huggingface.co/ai4bharat/IndicF5",
            gated = true,
        };

        // Svara, Piper and AI4Bharat Indic-TTS were removed in v5 (2026-10-07; kept in Backups/AltcoreBot_v4-4).

        public enum State { NotDownloaded, Downloading, Downloaded, Failed }

        public static State Status { get; private set; }
        public static string StatusMessage { get; private set; } = "";
        public static float Progress { get; private set; }

        static Process process;

        public static string ProjectRoot => Platform.AppFolder;
        public static string TtsDir => Path.Combine(ProjectRoot, "TTSServer");

        public static string HubCache
        {
            get
            {
                string home = Environment.GetEnvironmentVariable("HF_HOME");
                return string.IsNullOrEmpty(home)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "huggingface", "hub")
                    : Path.Combine(home, "hub");
            }
        }

        public static string ModelsDir => Path.Combine(TtsDir, "models");

        public static bool IsDownloaded(Engine engine)
        {
            if (engine.localFilePattern != null)
            {
                string folder = engine.localSubfolder == null ? ModelsDir : Path.Combine(ModelsDir, engine.localSubfolder);
                return Directory.Exists(folder) && Directory.GetFiles(folder, engine.localFilePattern).Length > 0;
            }
            string snapshots = Path.Combine(HubCache, engine.repoFolder, "snapshots");
            return Directory.Exists(snapshots) &&
                   Directory.GetDirectories(snapshots).Any(d => File.Exists(Path.Combine(d, "model.safetensors")));
        }

        /// Starts the download in the background; watch Status / Progress / StatusMessage.
        public static void Download(Engine engine)
        {
            if (process != null && !process.HasExited) return;
            string python = Platform.VenvPython(Path.Combine(TtsDir, "venv"));
            if (!File.Exists(python))
            {
                Fail("voice engine's Python environment not found");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = python,
                Arguments = $"download_voice.py {engine.id}",
                WorkingDirectory = TtsDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                CreateNoWindow = true,
            };
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            startInfo.EnvironmentVariables.Remove("HF_HUB_OFFLINE");

            Status = State.Downloading;
            Progress = 0;
            StatusMessage = "Starting download...";
            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (s, e) => HandleLine(e.Data);
            process.Exited += (s, e) =>
            {
                if (Status == State.Downloading) Fail("download stopped unexpectedly");
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        static void HandleLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (line.StartsWith("PROGRESS "))
            {
                var parts = line.Split(new[] { ' ' }, 3);
                if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float p))
                    Progress = p;
                if (parts.Length > 2) StatusMessage = parts[2];
            }
            else if (line == "DONE")
            {
                Status = State.Downloaded;
                Progress = 1;
                StatusMessage = "Downloaded - it's used the next time a Hindi reply is spoken.";
            }
            else if (line.StartsWith("FAILED "))
            {
                Fail(line.Substring(7));
            }
        }

        static void Fail(string message)
        {
            Status = State.Failed;
            StatusMessage = message;
            Debug.LogWarning($"Voice download failed: {message}");
        }
    }
}
