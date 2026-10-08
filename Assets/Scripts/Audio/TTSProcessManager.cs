using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace ChatbotAI.Audio
{
    /// Launches the local sidecar (TTSServer/server.py - voice generation plus
    /// the document knowledge base) as a child process on startup and kills it
    /// on exit, so the whole app is still "click the exe and it just works".
    /// Dev-time runs the Python script directly; the distribution build swaps
    /// in a PyInstaller-frozen exe at this same path per CLAUDE.md.
    public class TTSProcessManager : MonoBehaviour
    {
        const string ProgressPrefix = "PROGRESS|";

        [SerializeField] string healthUrl = "http://127.0.0.1:8765/health";
        [SerializeField] float healthPollIntervalSeconds = 1f;
        [Tooltip("Cold starts (models not yet in the OS file cache) can take a couple of minutes.")]
        [SerializeField] int maxHealthPolls = 300;

        // Which model file each voice engine runs (TTSServer/models) - picked from the
        // voice model list in the Inspector (VoiceModelLibrary). Read when the server starts.
        [SerializeField] string kokoroModel = "kokoro-v1.0.onnx";
        [SerializeField] string veenaModel = "Veena-q4_k_m.gguf";

        public string SelectedModel(VoiceModelLibrary.Engine engine) =>
            engine == VoiceModelLibrary.Engine.Kokoro ? kokoroModel
            : engine == VoiceModelLibrary.Engine.Veena ? veenaModel
            : engine == VoiceModelLibrary.Engine.KokoroV11 ? "kokoro-v1.1-zh.onnx"
            : VoiceEngineDownloads.IndicF5.repoFolder;

        /// Takes effect the next time the voice server starts.
        public void SelectModel(VoiceModelLibrary.Engine engine, string fileName)
        {
            if (engine == VoiceModelLibrary.Engine.Kokoro) kokoroModel = fileName;
            else if (engine == VoiceModelLibrary.Engine.Veena) veenaModel = fileName;
        }

        // Optional engines kept on disk but not used: their voices speak with Kokoro instead,
        // and they're never loaded (no memory, no startup time).
        [SerializeField] List<VoiceModelLibrary.Engine> switchedOff = new List<VoiceModelLibrary.Engine>();

        public bool IsSwitchedOn(VoiceModelLibrary.Engine engine) =>
            engine == VoiceModelLibrary.Engine.Kokoro || !switchedOff.Contains(engine);

        /// Takes effect the next time the voice server starts.
        public void SetSwitchedOn(VoiceModelLibrary.Engine engine, bool on)
        {
            if (engine == VoiceModelLibrary.Engine.Kokoro) return;  // always needed
            switchedOff.Remove(engine);
            if (!on) switchedOff.Add(engine);
        }

        // Document search: the reranker reorders the closest passages (knowledge.py). Menu > AI models; read at start.
        [SerializeField] bool useReranker = true;
        public bool UsesReranker => useReranker;

        Process serverProcess;
        bool shuttingDown;

        public bool IsReady { get; private set; }
        public bool HasFailed { get; private set; }
        public string FailureMessage { get; private set; }

        /// 0-1 load progress reported by the sidecar itself (written from the
        /// process-output thread, read on the main thread).
        public float Progress { get; private set; }
        public string Stage { get; private set; } = "Starting voice & knowledge engine";

        public event Action OnReady;

        async void Awake()
        {
            ApplyMenuChoices();
            if (await IsAlreadyRunning())
            {
                Debug.Log("TTSProcessManager: sidecar already running, reusing it.");
                MarkReady();
                return;
            }

            if (!StartServerProcess()) return;
            await WaitUntilHealthy();
        }

        // Voice models picked in the app's menu (ModelChoices) - read when the voice server starts.
        void ApplyMenuChoices()
        {
            string kokoro = ModelChoices.Get(ModelChoices.Kokoro);
            if (kokoro.Length > 0 && File.Exists(Path.Combine(VoiceModelLibrary.ModelsFolder, kokoro))) kokoroModel = kokoro;
            string veena = ModelChoices.Get(ModelChoices.Veena);
            if (veena.Length > 0 && File.Exists(Path.Combine(VoiceModelLibrary.ModelsFolder, veena))) veenaModel = veena;
            string rerankerOff = ModelChoices.Get(ModelChoices.RerankerOff);
            if (rerankerOff.Length > 0) useReranker = rerankerOff != "1";
            foreach (var engine in VoiceModelLibrary.Optional)
            {
                string off = ModelChoices.Get(ModelChoices.OffKey(engine));
                if (off.Length > 0) SetSwitchedOn(engine, off != "1");
            }
            // Mac version: no Veena (its fast build needs an NVIDIA card) - its voices speak with Kokoro.
            if (Platform.IsMac) SetSwitchedOn(VoiceModelLibrary.Engine.Veena, false);
        }

        async Task<bool> IsAlreadyRunning()
        {
            using var req = UnityWebRequest.Get(healthUrl);
            req.timeout = 2;
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            return req.result == UnityWebRequest.Result.Success;
        }

        bool StartServerProcess()
        {
            string projectRoot = Platform.AppFolder;
            string ttsDir = Path.Combine(projectRoot, "TTSServer");
            string venv = Path.Combine(ttsDir, "venv");
            string pythonExe = Platform.VenvPython(venv);
            string serverScript = Path.Combine(ttsDir, "server.py");

            // A portable build (PortableBuild.cs / MacBuild.cs) carries its own Python and model cache in TTSRuntime/
            // next to TTSServer/, so the app runs on a computer without Python installed.
            string runtimeDir = Path.Combine(projectRoot, "TTSRuntime");
            string bundledPython = Path.Combine(runtimeDir, "python");
            string sitePackages = null;
            if (Directory.Exists(bundledPython))
            {
                if (Platform.IsMac)
                {
                    // A Mac venv's python is a link to the Python it was made with (an absolute path on the Mac that
                    // built it): run the bundled Python itself and hand it the environment's packages instead.
                    pythonExe = Platform.InstalledPython(bundledPython);
                    string lib = Path.Combine(venv, "lib");
                    if (Directory.Exists(lib))
                        foreach (string dir in Directory.GetDirectories(lib, "python3*"))
                            if (Directory.Exists(Path.Combine(dir, "site-packages"))) sitePackages = Path.Combine(dir, "site-packages");
                }
                // Windows: the environments are pointed at that Python (their pyvenv.cfg holds an absolute path, and
                // the folder may have been copied anywhere).
                else if (!PointEnvironmentAt(venv, bundledPython)) return false;
            }
            string bundledModels = Path.Combine(runtimeDir, "hf_cache");

            if (!File.Exists(pythonExe) || !File.Exists(serverScript))
            {
                Fail(Platform.IsMac && !Directory.Exists(venv)
                    ? "The voice & knowledge engine isn't set up on this Mac yet - run tools/setup_mac.sh (see MAC-SETUP.md)."
                    : $"Voice & knowledge engine not found at {ttsDir}");
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = "server.py",
                WorkingDirectory = ttsDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                CreateNoWindow = true
            };
            startInfo.EnvironmentVariables["KNOWLEDGE_ROOT"] = Path.Combine(Application.streamingAssetsPath, "Knowledge");
            // Models are already in the local cache; without this, Hugging Face
            // libraries still phone home on every startup (slow, and breaks the
            // fully-offline requirement).
            startInfo.EnvironmentVariables["HF_HUB_OFFLINE"] = "1";
            startInfo.EnvironmentVariables["TRANSFORMERS_OFFLINE"] = "1";
            if (Directory.Exists(bundledModels)) startInfo.EnvironmentVariables["HF_HOME"] = bundledModels;
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            if (sitePackages != null) startInfo.EnvironmentVariables["PYTHONPATH"] = sitePackages;
            startInfo.EnvironmentVariables["KOKORO_MODEL"] = kokoroModel;
            startInfo.EnvironmentVariables["VEENA_MODEL"] = veenaModel;
            startInfo.EnvironmentVariables["RERANKER_OFF"] = useReranker ? "0" : "1";
            startInfo.EnvironmentVariables["VOICE_ENGINES_OFF"] = string.Join(",", switchedOff.ConvertAll(VoiceModelLibrary.EngineId));

            serverProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            serverProcess.OutputDataReceived += (s, e) => HandleOutput(e.Data);
            serverProcess.ErrorDataReceived += (s, e) => HandleOutput(e.Data);
            serverProcess.Exited += (s, e) =>
            {
                if (!IsReady && !shuttingDown) Fail("Voice & knowledge engine stopped while loading - see the [TTS] lines in the Console.");
            };
            serverProcess.Start();
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();

            Debug.Log($"TTSProcessManager: started sidecar (PID {serverProcess.Id}).");
            return true;
        }

        /// Points a Python environment (venv) at the given Python install - rewrites "home" / "executable" in its
        /// pyvenv.cfg when they point elsewhere. False (and the loading screen says why) if the file can't be written.
        bool PointEnvironmentAt(string venvDir, string pythonDir)
        {
            string cfg = Path.Combine(venvDir, "pyvenv.cfg");
            if (!File.Exists(cfg)) return true;   // that environment isn't in this build
            string[] lines = File.ReadAllLines(cfg);
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string want = lines[i].StartsWith("home ") || lines[i].StartsWith("home=") ? $"home = {pythonDir}"
                            : lines[i].StartsWith("executable ") || lines[i].StartsWith("executable=") ? $"executable = {Path.Combine(pythonDir, "python.exe")}"
                            : lines[i];
                if (want != lines[i]) { lines[i] = want; changed = true; }
            }
            if (!changed) return true;
            try
            {
                File.WriteAllLines(cfg, lines, new System.Text.UTF8Encoding(false));
                Debug.Log($"TTSProcessManager: {Path.GetFileName(venvDir)} now uses the bundled Python ({pythonDir}).");
                return true;
            }
            catch (System.Exception e)
            {
                Fail($"Can't set up the voice engine in {venvDir} ({e.Message}). Put the app in a folder you can write to " +
                     "(not Program Files).");
                return false;
            }
        }

        void HandleOutput(string line)
        {
            if (line == null) return;
            if (line.StartsWith(ProgressPrefix))
            {
                // Format: PROGRESS|<0-1>|<stage label>
                var parts = line.Split('|');
                if (parts.Length >= 3 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float p))
                {
                    Progress = Mathf.Clamp01(p);
                    Stage = parts[2];
                }
                return;
            }
            Debug.Log($"[TTS] {line}");
        }

        async Task WaitUntilHealthy()
        {
            for (int i = 0; i < maxHealthPolls && !HasFailed; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(healthPollIntervalSeconds));
                if (this == null) return;
                if (await IsAlreadyRunning())
                {
                    Debug.Log("TTSProcessManager: sidecar is ready.");
                    MarkReady();
                    return;
                }
            }
            if (!HasFailed) Fail("Voice & knowledge engine did not start in time.");
        }

        void MarkReady()
        {
            Progress = 1f;
            Stage = "Ready";
            IsReady = true;
            Restarting = false;
            OnReady?.Invoke();
            if (!watching) Watch();
        }

        // ---------------- Keeping the voice server running ----------------

        [Tooltip("If the voice & knowledge server stops or stops answering, start it again (at most 3 times in 10 minutes).")]
        [SerializeField] bool restartIfItStops = true;

        /// The server stopped and is being started again (voices and document search are back in ~30-60 s).
        public bool Restarting { get; private set; }

        bool watching;
        readonly List<float> restartTimes = new List<float>();

        // Every 5 s: is the process still there, and does it answer? 4 missed answers in a row (~30 s - it answers
        // even while generating speech) count as stopped. Then it's started again, as at startup.
        async void Watch()
        {
            watching = true;
            int missed = 0;
            while (this && !shuttingDown && restartIfItStops)
            {
                await Task.Delay(5000);
                if (this == null || shuttingDown) return;
                if (Restarting) continue;
                bool exited = serverProcess != null && serverProcess.HasExited;
                missed = !exited && await IsAlreadyRunning() ? 0 : missed + 1;
                if (!exited && missed < 4) continue;
                missed = 0;

                float now = Time.realtimeSinceStartup;
                restartTimes.RemoveAll(t => now - t > 600f);
                if (restartTimes.Count >= 3)
                {
                    Debug.LogError("TTSProcessManager: the voice server stopped 3 times in 10 minutes - not starting it again.");
                    HasFailed = true;
                    FailureMessage = "The voice engine keeps stopping - restart the app.";
                    return;
                }
                restartTimes.Add(now);
                Debug.LogWarning($"TTSProcessManager: the voice server {(exited ? $"stopped (exit code {SafeExitCode()})" : "stopped answering")} - starting it again.");
                Restarting = true;
                if (!exited && serverProcess != null)
                    try { serverProcess.Kill(); } catch (Exception) { }
                if (serverProcess == null)
                    Debug.LogWarning("TTSProcessManager: it was started outside the app (reused) - starting a new one; close the old one if it hangs.");
                if (!StartServerProcess()) { Restarting = false; return; }
                await WaitUntilHealthy();
            }
            watching = false;
        }

        string SafeExitCode()
        {
            try { return serverProcess.ExitCode.ToString(CultureInfo.InvariantCulture); }
            catch (Exception) { return "?"; }
        }

        void Fail(string message)
        {
            if (HasFailed) return;
            HasFailed = true;
            FailureMessage = message;
            Debug.LogError($"TTSProcessManager: {message}");
        }

        void OnApplicationQuit()
        {
            KillServer();
        }

        void OnDestroy()
        {
            KillServer();
        }

        void KillServer()
        {
            shuttingDown = true;
            if (serverProcess == null || serverProcess.HasExited) return;
            try
            {
                serverProcess.Kill();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"TTSProcessManager: failed to kill sidecar - {e.Message}");
            }
        }
    }
}
