using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatbotAI.UI
{
    /// Which monitor the app fills, for a PC with several screens (menu > Display > Screen). The choice is kept on this
    /// PC (companion/display - it never travels with a build: other PCs have other monitors) and the app moves there
    /// by itself at the next start. Uses Unity's own Screen.MoveMainWindowTo - built app only, not the Editor.
    public static class DisplayChoice
    {
        public const string PrefKey = "companion/display";

        public static List<DisplayInfo> List()
        {
            var list = new List<DisplayInfo>();
            Screen.GetDisplayLayout(list);
            return list;
        }

        public static DisplayInfo Current => Screen.mainWindowDisplayInfo;

        public static bool IsCurrent(DisplayInfo d) => d.Equals(Current);

        public static string Describe(DisplayInfo d) =>
            $"{d.width} × {d.height}  ·  {(d.height > d.width ? "portrait" : "landscape")}" +
            (d.refreshRate.value > 1 ? $"  ·  {Mathf.RoundToInt((float)d.refreshRate.value)} Hz" : "");

        public static string NameOf(DisplayInfo d, int index) => string.IsNullOrWhiteSpace(d.name) ? $"Display {index + 1}" : d.name;

        public static bool CanMove => !Application.isEditor;

        /// Screens Windows has switched on (counted from its own display list, which - unlike Unity's - also counts a screen that
        /// is only MIRRORING another one). More here than in List() = Windows is showing several screens as one (Win+P: Duplicate),
        /// and then there is nothing to pick between. -1 = couldn't tell (not Windows, or the call failed).
        public static int ConnectedScreens()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                int count = 0;
                var adapter = NewDevice();
                for (uint i = 0; EnumDisplayDevices(null, i, ref adapter, 0); i++, adapter = NewDevice())
                {
                    if ((adapter.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0 || (adapter.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0) continue;
                    var monitor = NewDevice();
                    int monitors = 0;
                    for (uint j = 0; EnumDisplayDevices(adapter.DeviceName, j, ref monitor, 0); j++, monitor = NewDevice())
                        if ((monitor.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0) monitors++;
                    count += Math.Max(1, monitors);   // an output with a screen on it but no monitor name still counts once
                }
                return count;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"DisplayChoice: couldn't ask Windows about the screens ({e.Message}).");
            }
#endif
            return -1;
        }

        /// One line for the log: what Unity and Windows each say about the screens.
        public static string Summary()
        {
            var list = List();
            var names = new List<string>();
            for (int i = 0; i < list.Count; i++) names.Add($"{NameOf(list[i], i)} {list[i].width}x{list[i].height}");
            return $"Unity lists {list.Count} screen(s) [{string.Join("; ", names)}], Windows reports {ConnectedScreens()} switched on.";
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1, DISPLAY_DEVICE_MIRRORING_DRIVER = 0x8, DISPLAY_DEVICE_ACTIVE = 0x1;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        struct DisplayDevice
        {
            public int cb;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        static DisplayDevice NewDevice() => new DisplayDevice { cb = System.Runtime.InteropServices.Marshal.SizeOf<DisplayDevice>() };

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);
#endif

        /// Moves the app onto that monitor (filling it when full screen) and remembers it. done runs once it's there.
        public static void Use(DisplayInfo d, int index, Action done = null)
        {
            PlayerPrefs.SetString(PrefKey, $"{d.name}|{d.width}|{d.height}|{index}");
            PlayerPrefs.Save();
            MoveTo(d, done);
        }

        static void MoveTo(DisplayInfo d, Action done)
        {
            if (!CanMove || IsCurrent(d))
            {
                done?.Invoke();
                return;
            }
            var mode = Screen.fullScreenMode;
            var position = mode == FullScreenMode.Windowed
                ? new Vector2Int(Mathf.Max(0, (d.workArea.width - Screen.width) / 2), Mathf.Max(0, (d.workArea.height - Screen.height) / 2))
                : Vector2Int.zero;
            var op = Screen.MoveMainWindowTo(d, position);
            op.completed += _ =>
            {
                // Full screen: take the new monitor's own resolution (it may differ from the old one's).
                if (mode != FullScreenMode.Windowed) Screen.SetResolution(d.width, d.height, mode);
                done?.Invoke();
            };
        }

        // At start: back onto the chosen monitor - matched by name and size, then by name, then by its place in the
        // list (two identical monitors). A monitor that's no longer connected is skipped.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplySaved()
        {
            // In Player.log: why the menu's Screen page lists what it lists (a TV that is off or mirrored isn't a screen to pick).
            if (!Application.isEditor) Debug.Log("DisplayChoice: " + Summary());
            string saved = PlayerPrefs.GetString(PrefKey, "");
            if (!CanMove || saved.Length == 0) return;
            string[] p = saved.Split('|');
            if (p.Length < 4) return;
            var list = List();
            int.TryParse(p[1], out int w);
            int.TryParse(p[2], out int h);
            int.TryParse(p[3], out int index);
            bool Same(DisplayInfo d) => d.name == p[0] && d.width == w && d.height == h;
            int found = index < list.Count && Same(list[index]) ? index : list.FindIndex(Same);
            if (found < 0) found = list.FindIndex(d => d.name == p[0]);
            if (found < 0 && index < list.Count && list.Count > 1) found = index;
            if (found < 0)
            {
                Debug.Log("DisplayChoice: the chosen screen isn't connected - staying where Windows put the app.");
                return;
            }
            MoveTo(list[found], null);
        }
    }
}
