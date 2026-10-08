using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ChatbotAI
{
    /// Marks an Inspector setting (float, int, bool or enum field of a scene component) as adjustable in the app:
    /// menu > Advanced lists every marked field of the components in the scene, grouped by page. Changes apply at
    /// once and are kept on this computer (PlayerPrefs); "Reset" goes back to the Inspector value. To offer another
    /// setting, add [Tunable(...)] to its field - nothing else. Only mark fields the component reads while running
    /// (not just in Awake), or implement ITunableListener to react to a change.
    [AttributeUsage(AttributeTargets.Field)]
    public class TunableAttribute : Attribute
    {
        public readonly string page, label;
        public readonly float min, max, step;
        /// Shown under the setting.
        public string note;
        /// How the value is shown: "s" seconds, "x" multiplier, "%" 0-1 as percent, "°" degrees, "" plain.
        public string unit = "";

        public TunableAttribute(string page, string label, float min = 0f, float max = 1f, float step = 0.01f)
        {
            this.page = page;
            this.label = label;
            this.min = min;
            this.max = max;
            this.step = step;
        }
    }

    /// A component that wants to know when one of its [Tunable] fields was changed from the menu.
    public interface ITunableListener
    {
        void TunableChanged(string field);
    }

    public static class Tunables
    {
        public class Item
        {
            public MonoBehaviour owner;
            public FieldInfo field;
            public TunableAttribute info;
            public string Key => $"tune/{owner.GetType().Name}/{field.Name}";
            public object Value => field.GetValue(owner);
            public Type Type => field.FieldType;
        }

        // Inspector values, per component and field, captured before any saved change is applied.
        static readonly Dictionary<(MonoBehaviour, string), object> defaults = new Dictionary<(MonoBehaviour, string), object>();

        /// Every [Tunable] field of the components in the open scene(s), in page order.
        public static List<Item> All()
        {
            var items = new List<Item>();
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (!mb) continue;
                for (var t = mb.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        var info = f.GetCustomAttribute<TunableAttribute>();
                        if (info == null || !Supported(f.FieldType)) continue;
                        var item = new Item { owner = mb, field = f, info = info };
                        var key = (mb, f.Name);
                        if (!defaults.ContainsKey(key)) defaults[key] = f.GetValue(mb);
                        items.Add(item);
                    }
            }
            return items;
        }

        // Menu order; pages not listed here come after, alphabetically.
        static readonly string[] PageOrder = { "Voice & sound", "Listening", "Mouth & lip-sync", "Eyes", "Body", "Brain", "Display & performance" };

        public static IEnumerable<string> Pages(List<Item> items) => items.Select(i => i.info.page).Distinct()
            .OrderBy(p => Array.IndexOf(PageOrder, p) is int i && i >= 0 ? i : PageOrder.Length)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase);

        static bool Supported(Type t) => t == typeof(float) || t == typeof(int) || t == typeof(bool) || t.IsEnum;

        /// Puts the saved values onto the scene's components (at startup, before Start).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void ApplySaved()
        {
            foreach (var item in All())
                if (PlayerPrefs.HasKey(item.Key)) Set(item, Load(item), save: false);
        }

        static object Load(Item item)
        {
            if (item.Type == typeof(float)) return PlayerPrefs.GetFloat(item.Key);
            if (item.Type == typeof(int)) return PlayerPrefs.GetInt(item.Key);
            if (item.Type == typeof(bool)) return PlayerPrefs.GetInt(item.Key) != 0;
            return Enum.ToObject(item.Type, PlayerPrefs.GetInt(item.Key));
        }

        public static void Set(Item item, object value, bool save = true)
        {
            item.field.SetValue(item.owner, value);
            if (save)
            {
                if (value is float f) PlayerPrefs.SetFloat(item.Key, f);
                else if (value is int i) PlayerPrefs.SetInt(item.Key, i);
                else if (value is bool b) PlayerPrefs.SetInt(item.Key, b ? 1 : 0);
                else PlayerPrefs.SetInt(item.Key, Convert.ToInt32(value));
                PlayerPrefs.Save();
            }
            (item.owner as ITunableListener)?.TunableChanged(item.field.Name);
        }

        public static bool IsChanged(Item item) => PlayerPrefs.HasKey(item.Key);

        /// Back to the Inspector value.
        public static void Reset(Item item)
        {
            PlayerPrefs.DeleteKey(item.Key);
            PlayerPrefs.Save();
            if (defaults.TryGetValue((item.owner, item.field.Name), out var value)) Set(item, value, save: false);
        }
    }
}
