using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ChatbotAI.UI
{
    /// Windows' own file dialogs and Recycle Bin, for the running app (Unity has no runtime file picker).
    /// On a Mac: the Finder's Open dialog and the Trash instead.
    /// The Open dialog works with touch and reaches USB drives; it is modal, so the app pauses while it's open.
    public static class WindowsFiles
    {
        /// Where removed files go, for the menu's wording.
        public static string BinName => Platform.IsMac ? "Trash" : "Recycle Bin";

        /// The files picked in Windows' Open dialog (several allowed); empty if cancelled or not on Windows.
        /// filter: pairs like ("Documents", "*.pdf;*.docx").
        public static List<string> PickFiles(string title, params (string name, string pattern)[] filter)
        {
            var picked = new List<string>();
            if (Platform.IsMac) return MacPickFiles(title, filter);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            const int bufferChars = 32768;
            IntPtr buffer = Marshal.AllocHGlobal(bufferChars * 2);
            try
            {
                Marshal.Copy(new byte[bufferChars * 2], 0, buffer, bufferChars * 2);
                string filterText = "";
                foreach (var (name, pattern) in filter) filterText += $"{name} ({pattern.Replace(";", ", ")})\0{pattern}\0";
                var ofn = new OpenFileName
                {
                    structSize = Marshal.SizeOf<OpenFileName>(),
                    owner = GetActiveWindow(),
                    filter = filterText + "\0",
                    file = buffer,
                    maxFile = bufferChars,
                    title = title,
                    flags = OFN_EXPLORER | OFN_ALLOWMULTISELECT | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
                };
                if (!GetOpenFileNameW(ref ofn)) return picked;

                // One file: its full path. Several: the folder, then each name; each ends in \0, the list in \0\0.
                var parts = new List<string>();
                int start = 0;
                for (int i = 0; i < bufferChars; i++)
                {
                    if (Marshal.ReadInt16(buffer, i * 2) != 0) continue;
                    if (i == start) break;
                    parts.Add(Marshal.PtrToStringUni(buffer + start * 2, i - start));
                    start = i + 1;
                }
                if (parts.Count == 1) picked.Add(parts[0]);
                else for (int i = 1; i < parts.Count; i++) picked.Add(Path.Combine(parts[0], parts[i]));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
#endif
            return picked;
        }

        /// Moves a file or folder to the Recycle Bin (so it can be restored). False if Windows refused.
        public static bool Recycle(string path)
        {
            if (Platform.IsMac) return MacTrash(path);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var op = new FileOperation
            {
                func = FO_DELETE,
                from = Path.GetFullPath(path) + "\0\0",
                flags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
            };
            return SHFileOperationW(ref op) == 0 && !op.anyAborted && !File.Exists(path) && !Directory.Exists(path);
#else
            return false;
#endif
        }

        // ---- Mac: the Finder's Open dialog (through AppleScript) and the Trash ----

        static List<string> MacPickFiles(string title, (string name, string pattern)[] filter)
        {
            var picked = new List<string>();
            var types = new List<string>();
            foreach (var (_, pattern) in filter)
                foreach (string p in pattern.Split(';'))
                {
                    string ext = p.Trim().TrimStart('*').TrimStart('.');
                    if (ext.Length > 0 && ext != "*") types.Add($"\"{ext}\"");
                }
            string ofType = types.Count > 0 ? $" of type {{{string.Join(",", types)}}}" : "";
            string script =
                $"set picked to choose file with prompt \"{(title ?? "").Replace("\\", "").Replace("\"", "'")}\"{ofType} with multiple selections allowed\n" +
                "set out to \"\"\n" +
                "repeat with f in picked\n" +
                "set out to out & POSIX path of f & linefeed\n" +
                "end repeat\n" +
                "return out";
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("osascript")
                {
                    UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true,
                };
                using var p = System.Diagnostics.Process.Start(info);
                p.StandardInput.Write(script);
                p.StandardInput.Close();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) return picked;   // cancelled
                foreach (string line in output.Split('\n'))
                    if (line.Trim().Length > 0 && File.Exists(line.Trim())) picked.Add(line.Trim());
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Couldn't open the Mac file dialog: {e.Message}");
            }
            return picked;
        }

        // Moved into ~/.Trash (dragged back out of the Trash to restore it).
        static bool MacTrash(string path)
        {
            try
            {
                string trash = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "~", ".Trash");
                string name = Path.GetFileName(Path.GetFullPath(path).TrimEnd('/'));
                string target = Path.Combine(trash, name);
                for (int i = 2; File.Exists(target) || Directory.Exists(target); i++)
                    target = Path.Combine(trash, $"{Path.GetFileNameWithoutExtension(name)} {i}{Path.GetExtension(name)}");
                if (Directory.Exists(path)) Directory.Move(path, target);
                else File.Move(path, target);
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Couldn't move {path} to the Trash: {e.Message}");
                return false;
            }
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const int OFN_ALLOWMULTISELECT = 0x200, OFN_PATHMUSTEXIST = 0x800, OFN_FILEMUSTEXIST = 0x1000,
                  OFN_EXPLORER = 0x80000, OFN_NOCHANGEDIR = 0x8;
        const uint FO_DELETE = 3;
        const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            public int structSize;
            public IntPtr owner;
            public IntPtr instance;
            public string filter;
            public IntPtr customFilter;
            public int maxCustomFilter;
            public int filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData;
            public IntPtr hook;
            public string templateName;
            public IntPtr reserved;
            public int reservedInt;
            public int flagsEx;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct FileOperation
        {
            public IntPtr hwnd;
            public uint func;
            public string from;
            public string to;
            public ushort flags;
            [MarshalAs(UnmanagedType.Bool)] public bool anyAborted;
            public IntPtr nameMappings;
            public string progressTitle;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetOpenFileNameW(ref OpenFileName ofn);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHFileOperationW(ref FileOperation op);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
#endif
    }
}
