using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChatbotAI
{
    /// Where API keys are kept: the operating system's own secret store, never in the scene, a build, an exported bot or
    /// default-settings.json. Windows: Credential Manager (per signed-in user); Mac: the Keychain (through the `security` tool);
    /// anywhere else (or if that fails) a scrambled copy in PlayerPrefs - not secure, only keeps a key from being read at a glance.
    public static class SecretStore
    {
        const string Prefix = "AltcoreBot/";

        public static bool Has(string name) => !string.IsNullOrEmpty(Get(name));

        public static string Get(string name)
        {
            try
            {
                if (Platform.IsWindows) { string v = WindowsRead(Prefix + name); if (v != null) return v; }
                else if (Platform.IsMac) { string v = MacRead(Prefix + name); if (v != null) return v; }
            }
            catch (Exception e) { Debug.LogWarning($"SecretStore: couldn't read {name} ({e.Message}) - using the fallback."); }
            return Unscramble(PlayerPrefs.GetString("secret/" + name, ""));
        }

        public static void Set(string name, string value)
        {
            if (string.IsNullOrEmpty(value)) { Delete(name); return; }
            bool stored = false;
            try
            {
                if (Platform.IsWindows) stored = WindowsWrite(Prefix + name, value);
                else if (Platform.IsMac) stored = MacWrite(Prefix + name, value);
            }
            catch (Exception e) { Debug.LogWarning($"SecretStore: couldn't save {name} in the system store ({e.Message})."); }
            if (stored) PlayerPrefs.DeleteKey("secret/" + name);
            else PlayerPrefs.SetString("secret/" + name, Scramble(value));
            PlayerPrefs.Save();
        }

        public static void Delete(string name)
        {
            try
            {
                if (Platform.IsWindows) WindowsDelete(Prefix + name);
                else if (Platform.IsMac) MacDelete(Prefix + name);
            }
            catch (Exception e) { Debug.LogWarning($"SecretStore: couldn't remove {name} ({e.Message})."); }
            PlayerPrefs.DeleteKey("secret/" + name);
            PlayerPrefs.Save();
        }

        // ---- fallback: XOR with a per-PC pad, base64 ----

        static byte[] Pad => Encoding.UTF8.GetBytes(SystemInfo.deviceUniqueIdentifier + "|altcore");

        static string Scramble(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text), pad = Pad;
            for (int i = 0; i < data.Length; i++) data[i] ^= pad[i % pad.Length];
            return Convert.ToBase64String(data);
        }

        static string Unscramble(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            try
            {
                byte[] data = Convert.FromBase64String(text), pad = Pad;
                for (int i = 0; i < data.Length; i++) data[i] ^= pad[i % pad.Length];
                return Encoding.UTF8.GetString(data);
            }
            catch (FormatException) { return ""; }
        }

        // ---- macOS Keychain ----

        static string Security(string args, out int exit)
        {
            var info = new ProcessStartInfo("security", args)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            using var p = Process.Start(info);
            string output = p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            exit = p.ExitCode;
            return output.TrimEnd('\n', '\r');
        }

        static string MacRead(string service)
        {
            string v = Security($"find-generic-password -s \"{service}\" -w", out int exit);
            return exit == 0 ? v : null;
        }

        static bool MacWrite(string service, string value)
        {
            Security($"add-generic-password -U -a \"{Environment.UserName}\" -s \"{service}\" -w \"{value.Replace("\"", "")}\"", out int exit);
            return exit == 0;
        }

        static void MacDelete(string service) => Security($"delete-generic-password -s \"{service}\"", out _);

        // ---- Windows Credential Manager ----

        const int CRED_TYPE_GENERIC = 1, CRED_PERSIST_LOCAL_MACHINE = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct Credential
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public long LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CredWriteW")]
        static extern bool CredWrite(ref Credential credential, int flags);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CredReadW")]
        static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CredDeleteW")]
        static extern bool CredDelete(string target, int type, int flags);

        [DllImport("advapi32.dll")]
        static extern void CredFree(IntPtr buffer);

        static bool WindowsWrite(string target, string value)
        {
            byte[] blob = Encoding.UTF8.GetBytes(value);
            IntPtr mem = Marshal.AllocHGlobal(blob.Length);
            try
            {
                Marshal.Copy(blob, 0, mem, blob.Length);
                var cred = new Credential
                {
                    Type = CRED_TYPE_GENERIC, TargetName = target, CredentialBlobSize = blob.Length, CredentialBlob = mem,
                    Persist = CRED_PERSIST_LOCAL_MACHINE, UserName = Environment.UserName,
                };
                return CredWrite(ref cred, 0);
            }
            finally { Marshal.FreeHGlobal(mem); }
        }

        static string WindowsRead(string target)
        {
            if (!CredRead(target, CRED_TYPE_GENERIC, 0, out IntPtr ptr)) return null;
            try
            {
                var cred = Marshal.PtrToStructure<Credential>(ptr);
                var blob = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
                return Encoding.UTF8.GetString(blob);
            }
            finally { CredFree(ptr); }
        }

        static void WindowsDelete(string target) => CredDelete(target, CRED_TYPE_GENERIC, 0);
    }
}
