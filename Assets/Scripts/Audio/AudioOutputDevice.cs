using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChatbotAI.Audio
{
    /// Which speakers the app plays through (menu > Language & listening > Speaker). Unity has no output-device API, so
    /// this uses Windows' own per-app sound routing - the same as Settings > Sound > "App volume and device
    /// preferences": only this app moves, the PC's default speaker stays as it is. Windows remembers it for the app;
    /// the choice is also kept in PlayerPrefs (companion/speaker - per PC, it never travels with a build) and applied
    /// again at startup. Windows 10 (2004+) / 11 only; elsewhere the list is empty and the system default is used.
    public static class AudioOutputDevice
    {
        public const string PrefKey = "companion/speaker";

        public struct Device
        {
            public string id, name;
        }

        /// The chosen device's id, "" = the system default.
        public static string Chosen => PlayerPrefs.GetString(PrefKey, "");

        /// Active playback devices (speakers, headphones, HDMI TVs...), by their Windows names.
        public static List<Device> List()
        {
            var list = new List<Device>();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                if (enumerator.EnumAudioEndpoints(0 /* render */, 1 /* active */, out var collection) != 0) return list;
                collection.GetCount(out int count);
                for (int i = 0; i < count; i++)
                {
                    if (collection.Item(i, out var device) != 0) continue;
                    device.GetId(out string id);
                    list.Add(new Device { id = id, name = FriendlyName(device) ?? id });
                    Marshal.ReleaseComObject(device);
                }
                Marshal.ReleaseComObject(collection);
                Marshal.ReleaseComObject(enumerator);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AudioOutputDevice: couldn't list the speakers ({e.Message}).");
            }
#endif
            return list;
        }

        /// Plays the app through this device ("" = the system default). False if Windows refused.
        public static bool Use(string id)
        {
            bool ok = Route(id);
            if (ok)
            {
                PlayerPrefs.SetString(PrefKey, id ?? "");
                PlayerPrefs.Save();
                // Unity's audio output opens its device once: reopen it so the new route takes effect now.
                AudioSettings.Reset(AudioSettings.GetConfiguration());
            }
            return ok;
        }

        // Before anything plays: the saved choice again (a device that's gone is left alone - the system default plays).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplySaved()
        {
            string id = Chosen;
            if (string.IsNullOrEmpty(id)) return;
            if (!List().Exists(d => d.id == id))
            {
                Debug.Log("AudioOutputDevice: the chosen speaker isn't connected - using the system default.");
                return;
            }
            if (Route(id)) AudioSettings.Reset(AudioSettings.GetConfiguration());
        }

        /// The device this app is routed to right now according to Windows ("" = none, the system default).
        public static string RoutedNow()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var factory = Factory();
            if (factory == IntPtr.Zero) return "";
            try
            {
                var get = Method<GetPersistedFn>(factory, 26);
                if (get(factory, (uint)System.Diagnostics.Process.GetCurrentProcess().Id, 0, 1, out IntPtr hstring) != 0 || hstring == IntPtr.Zero) return "";
                string full = Marshal.PtrToStringUni(WindowsGetStringRawBuffer(hstring, out _));
                WindowsDeleteString(hstring);
                return ShortId(full);
            }
            finally { Marshal.Release(factory); }
#else
            return "";
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // ---- Windows per-app routing (Windows.Media.Internal.AudioPolicyConfig - undocumented; as used by EarTrumpet) ----

        static bool Route(string id)
        {
            var factory = Factory();
            if (factory == IntPtr.Zero) return false;
            IntPtr hstring = IntPtr.Zero;
            try
            {
                if (!string.IsNullOrEmpty(id))
                {
                    // IMMDevice ids look like {0.0.0.00000000}.{guid}; the router wants the device interface path.
                    string full = $@"\\?\SWD#MMDEVAPI#{id}#{{e6327cad-dcec-4949-ae8a-991e976a79d2}}";
                    WindowsCreateString(full, (uint)full.Length, out hstring);
                }
                var set = Method<SetPersistedFn>(factory, 25);
                uint pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                int a = set(factory, pid, 0 /* render */, 1 /* multimedia */, hstring);
                int b = set(factory, pid, 0 /* render */, 0 /* console */, hstring);
                if (a != 0 || b != 0) Debug.LogWarning($"AudioOutputDevice: Windows refused the speaker (0x{a:X8} / 0x{b:X8}).");
                return a == 0 && b == 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AudioOutputDevice: couldn't route the sound ({e.Message}).");
                return false;
            }
            finally
            {
                if (hstring != IntPtr.Zero) WindowsDeleteString(hstring);
                Marshal.Release(factory);
            }
        }

        static string ShortId(string full)
        {
            if (string.IsNullOrEmpty(full)) return "";
            int start = full.IndexOf("{0.", StringComparison.Ordinal);
            int end = full.LastIndexOf("#{", StringComparison.Ordinal);
            return start >= 0 && end > start ? full.Substring(start, end - start) : full;
        }

        static IntPtr Factory()
        {
            const string cls = "Windows.Media.Internal.AudioPolicyConfig";
            WindowsCreateString(cls, (uint)cls.Length, out IntPtr name);
            try
            {
                // Windows 10 21H2+ / 11, then older Windows 10.
                foreach (var iid in new[] { new Guid("ab3d4648-e242-459f-b02f-541c70306324"), new Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258") })
                {
                    var g = iid;
                    if (RoGetActivationFactory(name, ref g, out IntPtr factory) == 0 && factory != IntPtr.Zero) return factory;
                }
                return IntPtr.Zero;
            }
            finally { WindowsDeleteString(name); }
        }

        static T Method<T>(IntPtr comObject, int slot) where T : Delegate
        {
            IntPtr vtable = Marshal.ReadIntPtr(comObject);
            return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(vtable, slot * IntPtr.Size));
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int SetPersistedFn(IntPtr self, uint processId, int flow, int role, IntPtr deviceId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetPersistedFn(IntPtr self, uint processId, int flow, int role, out IntPtr deviceId);

        [DllImport("combase.dll", PreserveSig = true)]
        static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

        [DllImport("combase.dll", PreserveSig = true, CharSet = CharSet.Unicode)]
        static extern int WindowsCreateString(string source, uint length, out IntPtr hstring);

        [DllImport("combase.dll", PreserveSig = true)]
        static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll", PreserveSig = true)]
        static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

        // ---- Device list (MMDevice API) ----

        static string FriendlyName(IMMDevice device)
        {
            if (device.OpenPropertyStore(0 /* read */, out var store) != 0) return null;
            try
            {
                var key = new PropertyKey { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
                if (store.GetValue(ref key, out var value) != 0) return null;
                string name = value.vt == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(value.pointer) : null;
                PropVariantClear(ref value);
                return name;
            }
            finally { Marshal.ReleaseComObject(store); }
        }

        [DllImport("ole32.dll")]
        static extern int PropVariantClear(ref PropVariant value);

        [StructLayout(LayoutKind.Sequential)]
        struct PropertyKey
        {
            public Guid fmtid;
            public int pid;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        struct PropVariant
        {
            [FieldOffset(0)] public short vt;
            [FieldOffset(8)] public IntPtr pointer;
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int Item(int index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        }

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetAt(int index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        }
#else
        static bool Route(string id) => false;
#endif
    }
}
