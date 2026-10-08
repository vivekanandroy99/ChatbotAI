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
