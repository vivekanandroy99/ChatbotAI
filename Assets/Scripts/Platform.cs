using System.IO;
using UnityEngine;

namespace ChatbotAI
{
    /// Windows vs Mac differences in one place (runtime checks, so every branch compiles on every machine).
    /// Windows = the kiosk version (NVIDIA/CUDA, Veena); Mac = English, lighter (Metal for the brain and Whisper,
    /// Apple's GPU for document search, Kokoro on the CPU, no Veena) - see MAC-SETUP.md.
    public static class Platform
    {
        public static bool IsMac =>
            Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;

        public static bool IsWindows =>
            Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;

        /// The folder that holds TTSServer/ and TTSRuntime/: the project folder in the Editor, the build folder in a
        /// build (Windows: next to AltcoreBot.exe; Mac: next to AltcoreBot.app - dataPath is AltcoreBot.app/Contents).
        public static string AppFolder
        {
            get
            {
                if (Application.isEditor) return Directory.GetParent(Application.dataPath).FullName;
                if (Application.platform == RuntimePlatform.OSXPlayer)
                    return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
                return Directory.GetParent(Application.dataPath).FullName;
            }
        }

        /// The Python of a Python environment (venv) made on this kind of computer.
        public static string VenvPython(string venvDir) =>
            IsMac ? Path.Combine(venvDir, "bin", "python3") : Path.Combine(venvDir, "Scripts", "python.exe");

        /// The python program inside a Python install folder (a build's TTSRuntime/python).
        public static string InstalledPython(string pythonDir) =>
            IsMac ? Path.Combine(pythonDir, "bin", "python3") : Path.Combine(pythonDir, "python.exe");
    }
}
