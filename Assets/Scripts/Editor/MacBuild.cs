using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChatbotAI.EditorTools
{
    /// The Mac version's build (run it ON the Mac, after tools/setup_mac.sh - see MAC-SETUP.md). Same idea as
    /// PortableBuild for Windows: next to AltcoreBot.app go
    ///   TTSServer/            the voice & knowledge server (code, Kokoro, its Mac Python environment)
    ///   TTSRuntime/python/    the Python (uv's self-contained build) that environment was made with
    ///   TTSRuntime/hf_cache/  the document search models (bge-m3 + reranker)
    /// plus the Editor menu's settings (default-settings.json inside the app). No Veena on the Mac.
    public class MacBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        static readonly string[] HubModels = { "models--BAAI--bge-m3", "models--BAAI--bge-reranker-v2-m3" };

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneOSX) return;
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                Debug.LogWarning("MacBuild: built on Windows - the voice server wasn't added. Build the Mac version on the Mac.");
                return;
            }
            MakePortable(report.summary.outputPath);
        }

        /// Builds/<working scene>_mac/AltcoreBot.app (e.g. Builds/AltcoreBot_v5_mac/).
        [MenuItem("Tools/ChatbotAI/Build Mac App (Builds folder)")]
        public static void BuildMacApp()
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                EditorUtility.DisplayDialog("Build Mac App", "Build the Mac version on the Mac (it needs the Mac's Python " +
                                            "environment from tools/setup_mac.sh). See MAC-SETUP.md.", "OK");
                return;
            }
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("MacBuild: no scene is ticked in the build list."); return; }
            string version = Path.GetFileNameWithoutExtension(scenes[0]) + "_mac";
            string project = Directory.GetParent(Application.dataPath).FullName;
            string app = Path.Combine(project, "Builds", version, PlayerSettings.productName + ".app");
            PlayerSettings.macOS.microphoneUsageDescription = "The assistant listens to your questions.";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = app, target = BuildTarget.StandaloneOSX, options = BuildOptions.None,
            });
            Debug.Log($"MacBuild: build {report.summary.result} -> {app} ({report.summary.totalSize / 1e9:0.0} GB, {report.summary.totalTime})");
        }

        public static void MakePortable(string appPath)
        {
            string buildDir = Path.GetDirectoryName(appPath);
            string streaming = Path.Combine(appPath, "Contents", "Resources", "Data", "StreamingAssets");
            string project = Directory.GetParent(Application.dataPath).FullName;
            string server = Path.Combine(project, "TTSServer");
            var log = new List<string>();
            try
            {
                // 1. The server (no Windows leftovers if the folder was copied from the PC).
                Progress("Copying the voice & knowledge engine...", 0.05f);
                if (!File.Exists(Path.Combine(server, "venv", "bin", "python3")))
                    throw new Exception("TTSServer/venv isn't the Mac one - run tools/setup_mac.sh first");
                Rsync(server, Path.Combine(buildDir, "TTSServer"), "__pycache__", ".cache", "*.pyc", "*.log", "indictts-venv",
                      "Veena*.gguf", "venv/Scripts", "venv/Lib");
                log.Add("TTSServer copied");

                // 2. The Python the environment was made with (pyvenv.cfg "home" = <python>/bin).
                Progress("Copying Python...", 0.55f);
                string home = PythonHome(Path.Combine(server, "venv", "pyvenv.cfg"));
                string pythonDir = home == null ? null : Directory.GetParent(home)?.FullName;
                if (pythonDir == null || !Directory.Exists(pythonDir)) throw new Exception("couldn't find the Python TTSServer/venv was made with");
                Rsync(pythonDir, Path.Combine(buildDir, "TTSRuntime", "python"), "__pycache__");
                log.Add($"Python copied from {pythonDir}");

                // 3. Document search models.
                Progress("Copying the knowledge models...", 0.7f);
                string hub = Path.Combine(Environment.GetEnvironmentVariable("HF_HOME") ??
                                          Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", ".cache", "huggingface"), "hub");
                foreach (string model in HubModels)
                {
                    string from = Path.Combine(hub, model);
                    if (!Directory.Exists(from)) throw new Exception($"model {model} isn't in {hub} - run tools/setup_mac.sh");
                    Rsync(from, Path.Combine(buildDir, "TTSRuntime", "hf_cache", "hub", model));
                }
                log.Add("models copied");

                // 4. The settings made in the Editor's menu.
                Progress("Saving your settings into the build...", 0.9f);
                log.Add($"{ExportSettings(Path.Combine(streaming, DefaultSettings.FileName))} settings exported");

                // 5. Test-only knowledge.
                string test = Path.Combine(streaming, "Knowledge", "zz_test_brand");
                if (Directory.Exists(test)) { Directory.Delete(test, true); log.Add("test knowledge left out"); }

                File.WriteAllText(Path.Combine(buildDir, "README.txt"), Readme(Path.GetFileName(appPath)));
                Debug.Log($"MacBuild: {buildDir} is ready ({string.Join(", ", log)}).");
            }
            catch (Exception e)
            {
                Debug.LogError($"MacBuild: the build in {buildDir} is NOT complete - {e.Message}. Done so far: {string.Join(", ", log)}.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Progress(string text, float amount) => EditorUtility.DisplayProgressBar("Making the Mac build", text, amount);

        // rsync -a --delete (only changes are copied on a rebuild; HF's relative links inside a model folder are kept).
        static void Rsync(string from, string to, params string[] excludes)
        {
            Directory.CreateDirectory(to);
            string args = "-a --delete " + string.Join(" ", excludes.Select(e => $"--exclude \"{e}\"")) +
                          $" \"{from.TrimEnd('/')}/\" \"{to.TrimEnd('/')}/\"";
            using var p = Process.Start(new ProcessStartInfo("rsync", args)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            p.StandardOutput.ReadToEnd();
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception($"copying {from} failed (rsync {p.ExitCode}: {err.Trim()})");
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

        /// The Editor's PlayerPrefs that DefaultSettings.Travels, as JSON. On a Mac they're in
        /// ~/Library/Preferences/unity.<company>.<product>.plist; plutil turns it into XML for the names and types,
        /// the values come from PlayerPrefs (the Editor's live copy).
        static int ExportSettings(string path)
        {
            var file = new DefaultSettings.SettingsFile { madeOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            string plist = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", "Library", "Preferences",
                                        $"unity.{PlayerSettings.companyName}.{PlayerSettings.productName}.plist");
            if (File.Exists(plist))
            {
                using var p = Process.Start(new ProcessStartInfo("plutil", $"-convert xml1 -o - \"{plist}\"")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
                string xml = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                var doc = new XmlDocument { XmlResolver = null };
                doc.LoadXml(Regex.Replace(xml, @"<!DOCTYPE[^>]*>", ""));
                var dict = doc.SelectSingleNode("/plist/dict");
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                for (var node = dict?.FirstChild; node != null; node = node.NextSibling)
                {
                    if (node.Name != "key") continue;
                    var valueNode = node.NextSibling;
                    string name = node.InnerText;
                    if (valueNode == null || !DefaultSettings.Travels(name) || file.entries.Exists(e => e.key == name)) continue;
                    var entry = new DefaultSettings.Entry { key = name };
                    switch (valueNode.Name)
                    {
                        case "integer": entry.type = "int"; entry.value = PlayerPrefs.GetInt(name).ToString(inv); break;
                        case "real": entry.type = "float"; entry.value = PlayerPrefs.GetFloat(name).ToString("R", inv); break;
                        case "string": case "data": entry.type = "string"; entry.value = PlayerPrefs.GetString(name); break;
                        default: continue;
                    }
                    file.entries.Add(entry);
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(file, true));
            return file.entries.Count;
        }

        static string Readme(string app) =>
            "Altcore assistant (Mac version) - how to install\n" +
            "================================================\n\n" +
            "1. Copy this WHOLE folder to the Mac (for example into your home folder). Keep " + app + "\n" +
            "   next to the TTSServer and TTSRuntime folders - it needs them.\n" +
            "2. Open " + app + ". The first time, macOS may block it (it isn't from the App Store): right-click\n" +
            "   it > Open > Open. Allow the microphone when asked.\n" +
            "3. The first start takes a minute or two (it loads the AI).\n\n" +
            "Needs: a Mac with Apple silicon (M1 or newer), 16 GB memory or more (24 GB recommended), a microphone.\n" +
            "English voices only (no Veena Hindi voices on the Mac). No internet needed.\n";
    }
}
