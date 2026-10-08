using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChatbotAI.EditorTools
{
    /// Plug and play Windows builds: after Unity builds the exe, everything else the app needs is put next to it, so
    /// the build folder can be copied to any PC (NVIDIA GPU + driver) and run - no Python, no downloads, no setup:
    ///   TTSServer/             the voice & knowledge server (code, voices, models, its Python environment)
    ///   TTSRuntime/python/     a private copy of the Python those environments were made with
    ///   TTSRuntime/hf_cache/   the downloaded models the server loads (knowledge search bge-m3 + its reranker, SNAC audio decoder)
    ///   <App>_Data/StreamingAssets/default-settings.json   the settings made in the Editor's menu (DefaultSettings)
    /// and the test-only knowledge folder zz_test_brand is left out. TTSProcessManager points the Python environments
    /// at TTSRuntime/python when the app starts. Copies are incremental (robocopy /MIR): only changes are copied on a
    /// rebuild into the same folder.
    public class PortableBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        /// Models from the Hugging Face cache that the server loads at run time (knowledge.py, veena engine).
        static readonly string[] HubModels = { "models--BAAI--bge-m3", "models--BAAI--bge-reranker-v2-m3", "models--hubertsiuzdak--snac_24khz" };

        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows) return;
            MakePortable(report.summary.outputPath);
        }

        /// Builds always go to Builds/<working scene>/ in the project folder (e.g. Builds/AltcoreBot_v5/AltcoreBot.exe) -
        /// one folder per version; building the same version again updates its folder (only changes are copied).
        [MenuItem("Tools/ChatbotAI/Build Windows App (Builds folder)")]
        public static void BuildWindowsApp()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("PortableBuild: no scene is ticked in the build list."); return; }
            string version = Path.GetFileNameWithoutExtension(scenes[0]);
            string project = Directory.GetParent(Application.dataPath).FullName;
            string exe = Path.Combine(project, "Builds", version, PlayerSettings.productName + ".exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = exe, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None,
            });
            Debug.Log($"PortableBuild: build {report.summary.result} -> {exe} ({report.summary.totalSize / 1e9:0.0} GB, {report.summary.totalTime})");
        }

        [MenuItem("Tools/ChatbotAI/Make a Windows Build Portable...")]
        static void MakeExistingBuildPortable()
        {
            string exe = EditorUtility.OpenFilePanel("The built app (.exe)", "", "exe");
            if (!string.IsNullOrEmpty(exe)) MakePortable(exe);
        }

        public static void MakePortable(string exePath)
        {
            string buildDir = Path.GetDirectoryName(exePath);
            string dataDir = Path.Combine(buildDir, Path.GetFileNameWithoutExtension(exePath) + "_Data");
            string project = Directory.GetParent(Application.dataPath).FullName;
            string server = Path.Combine(project, "TTSServer");
            var log = new List<string>();
            try
            {
                // 1. The server: code, voices, models, Python environments (caches and logs left out).
                Progress("Copying the voice & knowledge engine (the first time takes a few minutes)...", 0.05f);
                Robocopy(server, Path.Combine(buildDir, "TTSServer"), "/MIR", "/XD", "__pycache__", ".cache", "/XF", "*.pyc", "*.log");
                log.Add("TTSServer copied");

                // 2. The Python the environments were made with (pyvenv.cfg "home").
                Progress("Copying Python...", 0.55f);
                string home = PythonHome(Path.Combine(server, "venv", "pyvenv.cfg"));
                if (home == null || !Directory.Exists(home)) throw new Exception("couldn't find the Python install TTSServer/venv was made with");
                Robocopy(home, Path.Combine(buildDir, "TTSRuntime", "python"), "/MIR", "/XD", "__pycache__");
                log.Add($"Python copied from {home}");

                // 3. The downloaded models the server loads.
                Progress("Copying the knowledge and voice models...", 0.7f);
                string hub = Path.Combine(Environment.GetEnvironmentVariable("HF_HOME") ??
                                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "huggingface"), "hub");
                foreach (string model in HubModels)
                {
                    string from = Path.Combine(hub, model);
                    if (!Directory.Exists(from)) throw new Exception($"model {model} isn't in {hub} - start the app once in the Editor so it downloads");
                    Robocopy(from, Path.Combine(buildDir, "TTSRuntime", "hf_cache", "hub", model), "/MIR");
                }
                log.Add("models copied");

                // 4. The settings made in the Editor's menu.
                Progress("Saving your settings into the build...", 0.9f);
                int settings = ExportSettings(Path.Combine(dataDir, "StreamingAssets", DefaultSettings.FileName));
                log.Add($"{settings} settings exported");

                // 5. Test-only knowledge (tools/test_models.cs) - the build's own copy; the project's stays.
                string test = Path.Combine(dataDir, "StreamingAssets", "Knowledge", "zz_test_brand");
                if (Directory.Exists(test))
                {
                    Directory.Delete(test, true);
                    log.Add("test knowledge left out");
                }

                // 6. Keeps the app running on a kiosk: restarts it after a crash or a freeze (see AppHeartbeat).
                string exe = Path.GetFileName(exePath);
                string name = Path.GetFileNameWithoutExtension(exePath);
                File.WriteAllText(Path.Combine(buildDir, $"{name} Watchdog.ps1"), Watchdog(exe));
                File.WriteAllText(Path.Combine(buildDir, $"Start {name}.cmd"),
                    "@echo off\r\nrem Starts the app and keeps it running - it's started again if it ever crashes or freezes.\r\n" +
                    $"start \"\" powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"%~dp0{name} Watchdog.ps1\"\r\n");
                log.Add("watchdog written");

                File.WriteAllText(Path.Combine(buildDir, "README.txt"), Readme(exe));
                Debug.Log($"PortableBuild: {buildDir} is ready to copy to another PC ({string.Join(", ", log)}).");
            }
            catch (Exception e)
            {
                Debug.LogError($"PortableBuild: the build in {buildDir} is NOT complete - {e.Message}. Done so far: {string.Join(", ", log)}.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Progress(string text, float amount) => EditorUtility.DisplayProgressBar("Making the build plug and play", text, amount);

        // robocopy: exit codes below 8 mean success (1 = files copied, 0 = nothing to copy, 2/3 = extra files...).
        static void Robocopy(string from, string to, params string[] options)
        {
            string args = $"\"{from}\" \"{to}\" " + string.Join(" ", options.Select(o => o.Contains(' ') ? $"\"{o}\"" : o)) +
                          " /R:1 /W:1 /NFL /NDL /NJH /NJS /NP";
            using var p = Process.Start(new ProcessStartInfo("robocopy", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode >= 8) throw new Exception($"copying {from} failed (robocopy code {p.ExitCode})");
        }

        static string PythonHome(string pyvenvCfg)
        {
            if (!File.Exists(pyvenvCfg)) return null;
            foreach (string line in File.ReadAllLines(pyvenvCfg))
            {
                var m = Regex.Match(line, @"^\s*home\s*=\s*(.+?)\s*$");
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        /// The Editor's saved settings (PlayerPrefs - on Windows in the registry under the company and product name)
        /// that DefaultSettings.Travels, as JSON. Ints are stored as DWORDs, floats as the bits of a double, strings
        /// as UTF-8 bytes; value names end in "_h<hash>".
        static int ExportSettings(string path)
        {
            var file = new DefaultSettings.SettingsFile { madeOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            string keyPath = $@"Software\Unity\UnityEditor\{PlayerSettings.companyName}\{PlayerSettings.productName}";
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath))
            {
                if (key != null)
                    foreach (string raw in key.GetValueNames())
                    {
                        string name = Regex.Replace(raw, @"_h\d+$", "");
                        if (!DefaultSettings.Travels(name) || file.entries.Exists(e => e.key == name)) continue;
                        // The registry gives the names and types; the values come from PlayerPrefs when it has them
                        // (the Editor keeps them in memory - the registry can lag behind or hold values it hasn't loaded).
                        object v = key.GetValue(raw);
                        bool live = PlayerPrefs.HasKey(name);
                        var inv = System.Globalization.CultureInfo.InvariantCulture;
                        var entry = new DefaultSettings.Entry { key = name };
                        switch (v)
                        {
                            case int i:
                                entry.type = "int";
                                entry.value = (live ? PlayerPrefs.GetInt(name) : i).ToString(inv);
                                break;
                            case long l:
                                entry.type = "float";
                                entry.value = (live ? PlayerPrefs.GetFloat(name) : (float)BitConverter.Int64BitsToDouble(l)).ToString("R", inv);
                                break;
                            case byte[] bytes:
                                entry.type = "string";
                                entry.value = live ? PlayerPrefs.GetString(name) : System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                                break;
                            default:
                                continue;
                        }
                        file.entries.Add(entry);
                    }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(file, true));
            return file.entries.Count;
        }

        // Windows PowerShell 5.1 script (every Windows 10/11 has it). Started hidden by "Start <app>.cmd".
        static string Watchdog(string exe) =>
            "# Keeps " + exe + " running: starts it, and starts it again if it crashes or stops responding.\r\n" +
            "# Closing the app normally (its menu's Close the app, or the window) ends this too.\r\n" +
            "$here = Split-Path -Parent $MyInvocation.MyCommand.Path\r\n" +
            "$exe = Join-Path $here '" + exe + "'\r\n" +
            "$beat = Join-Path $here '" + AppHeartbeat.FileName + "'\r\n" +
            "$log = Join-Path $here 'watchdog.log'\r\n" +
            "$hangSeconds = 120\r\n" +
            "$restarts = @()\r\n" +
            "function Note($text) { Add-Content -Path $log -Value \"$(Get-Date -Format s)  $text\" }\r\n" +
            "while ($true) {\r\n" +
            "    Set-Content -Path $beat -Value 'starting'\r\n" +
            "    $p = Start-Process -FilePath $exe -WorkingDirectory $here -PassThru\r\n" +
            "    $killed = $false\r\n" +
            "    while (-not $p.HasExited) {\r\n" +
            "        Start-Sleep -Seconds 5\r\n" +
            "        $age = ((Get-Date) - (Get-Item $beat).LastWriteTime).TotalSeconds\r\n" +
            "        if (-not $p.HasExited -and $age -gt $hangSeconds) {\r\n" +
            "            Note \"not responding for $([int]$age) s - closing it\"\r\n" +
            "            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue\r\n" +
            "            $killed = $true\r\n" +
            "            Start-Sleep -Seconds 3\r\n" +
            "        }\r\n" +
            "    }\r\n" +
            "    $closed = (Get-Content -Path $beat -ErrorAction SilentlyContinue) -like 'closed*'\r\n" +
            "    if (-not $killed -and ($closed -or $p.ExitCode -eq 0)) { Note 'closed normally'; break }\r\n" +
            "    $now = Get-Date\r\n" +
            "    $restarts = @($restarts | Where-Object { $_ -gt $now.AddMinutes(-10) }) + $now\r\n" +
            "    if ($restarts.Count -gt 5) { Note 'stopped 5 times in 10 minutes - not starting it again'; break }\r\n" +
            "    Note \"stopped (exit code $($p.ExitCode)) - starting it again\"\r\n" +
            "    Start-Sleep -Seconds 5\r\n" +
            "}\r\n";

        static string Readme(string exe) =>
            "Altcore assistant - how to install\r\n" +
            "===================================\r\n\r\n" +
            "1. Copy this WHOLE folder to the PC (for example C:\\Altcore). Not into Program Files - the app writes into\r\n" +
            "   this folder (taught answers, voice engine setup).\r\n" +
            $"2. Double-click \"Start {Path.GetFileNameWithoutExtension(exe)}.cmd\" (or {exe} itself). The first start takes a minute\r\n" +
            "   or two (it loads the AI). Started with the .cmd, the app is started again by itself if it ever crashes or\r\n" +
            "   stops responding (watchdog.log in this folder says when). Closing it from its menu (Close the app) stops it\r\n" +
            "   for good. For a kiosk, put a shortcut to the .cmd in the Startup folder (Win+R, shell:startup).\r\n\r\n" +
            "Needs: Windows 10/11, an NVIDIA graphics card (8 GB+ memory; 16 GB recommended) with a current driver,\r\n" +
            "a microphone. Nothing else to install - no internet needed.\r\n\r\n" +
            "Portrait TV: set the display to Portrait in Windows display settings; the app adapts by itself.\r\n" +
            "Settings are in the app's menu (top right), behind the sign-in set up in Unity.\r\n";
    }
}
