using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChatbotAI.UI
{
    /// A line icon drawn in code (no image files), in the element's USS `color`.
    public class Glyph : VisualElement
    {
        public enum Kind { None, Menu, Close, Back, Chevron, Check, Mic, Stop, Speaker, Spinner, Keyboard, Send, Dot, Help }

        Kind kind;
        IVisualElementScheduledItem spin;

        public Kind Shape
        {
            get => kind;
            set
            {
                if (kind == value) return;
                kind = value;
                spin ??= schedule.Execute(MarkDirtyRepaint).Every(16);
                if (kind == Kind.Spinner) spin.Resume(); else spin.Pause();
                MarkDirtyRepaint();
            }
        }

        public Glyph(Kind kind = Kind.None)
        {
            AddToClassList("glyph");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            Shape = kind;
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 1f || h < 1f || kind == Kind.None) return;
            float s = Mathf.Min(w, h);
            var o = new Vector2((w - s) / 2f, (h - s) / 2f);
            Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);

            var p = ctx.painter2D;
            var color = resolvedStyle.color;
            p.strokeColor = color;
            p.fillColor = color;
            p.lineWidth = Mathf.Max(1.2f, s * 0.085f);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            void Line(float x1, float y1, float x2, float y2)
            {
                p.BeginPath();
                p.MoveTo(P(x1, y1));
                p.LineTo(P(x2, y2));
                p.Stroke();
            }

            void Poly(bool fill, params float[] xy)
            {
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
                if (fill) { p.ClosePath(); p.Fill(); }
                else p.Stroke();
            }

            switch (kind)
            {
                case Kind.Menu:
                    Line(0.2f, 0.32f, 0.8f, 0.32f);
                    Line(0.2f, 0.5f, 0.8f, 0.5f);
                    Line(0.2f, 0.68f, 0.8f, 0.68f);
                    break;
                case Kind.Close:
                    Line(0.3f, 0.3f, 0.7f, 0.7f);
                    Line(0.7f, 0.3f, 0.3f, 0.7f);
                    break;
                case Kind.Back:
                    Poly(false, 0.6f, 0.22f, 0.32f, 0.5f, 0.6f, 0.78f);
                    break;
                case Kind.Chevron:
                    p.lineWidth = Mathf.Max(1.2f, s * 0.1f);
                    Poly(false, 0.4f, 0.26f, 0.64f, 0.5f, 0.4f, 0.74f);
                    break;
                case Kind.Check:
                    p.lineWidth = Mathf.Max(1.4f, s * 0.11f);
                    Poly(false, 0.24f, 0.52f, 0.42f, 0.7f, 0.78f, 0.3f);
                    break;
                case Kind.Mic:
                    RoundRect(p, P(0.385f, 0.14f), 0.23f * s, 0.43f * s, 0.115f * s);
                    p.Fill();
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.44f), 0.22f * s, Angle.Degrees(0), Angle.Degrees(180));
                    p.Stroke();
                    Line(0.5f, 0.66f, 0.5f, 0.82f);
                    Line(0.38f, 0.82f, 0.62f, 0.82f);
                    break;
                case Kind.Stop:
                    RoundRect(p, P(0.32f, 0.32f), 0.36f * s, 0.36f * s, 0.07f * s);
                    p.Fill();
                    break;
                case Kind.Speaker:
                    Poly(true, 0.16f, 0.4f, 0.3f, 0.4f, 0.48f, 0.24f, 0.48f, 0.76f, 0.3f, 0.6f, 0.16f, 0.6f);
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.5f), 0.14f * s, Angle.Degrees(-50), Angle.Degrees(50));
                    p.Stroke();
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.5f), 0.29f * s, Angle.Degrees(-50), Angle.Degrees(50));
                    p.Stroke();
                    break;
                case Kind.Spinner:
                {
                    float start = Time.realtimeSinceStartup * 360f % 360f;
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.5f), 0.32f * s, Angle.Degrees(start), Angle.Degrees(start + 270f));
                    p.Stroke();
                    break;
                }
                case Kind.Keyboard:
                    p.lineWidth = Mathf.Max(1.2f, s * 0.07f);
                    RoundRect(p, P(0.12f, 0.26f), 0.76f * s, 0.48f * s, 0.08f * s);
                    p.Stroke();
                    for (int row = 0; row < 2; row++)
                        for (int i = 0; i < 4; i++)
                        {
                            p.BeginPath();
                            p.Arc(P(0.29f + i * 0.14f, 0.4f + row * 0.1f), 0.022f * s, Angle.Degrees(0), Angle.Degrees(360));
                            p.Fill();
                        }
                    Line(0.36f, 0.63f, 0.64f, 0.63f);
                    break;
                case Kind.Send:
                    Line(0.5f, 0.74f, 0.5f, 0.28f);
                    Poly(false, 0.3f, 0.46f, 0.5f, 0.26f, 0.7f, 0.46f);
                    break;
                case Kind.Help:
                    // A question mark: the hook, its stem, the dot.
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.38f), 0.16f * s, Angle.Degrees(200), Angle.Degrees(410));
                    p.LineTo(P(0.5f, 0.6f));
                    p.Stroke();
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.76f), 0.055f * s, Angle.Degrees(0), Angle.Degrees(360));
                    p.Fill();
                    break;
                case Kind.Dot:
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.5f), 0.14f * s, Angle.Degrees(0), Angle.Degrees(360));
                    p.Fill();
                    break;
            }
        }

        internal static void RoundRect(Painter2D p, Vector2 pos, float w, float h, float r)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) / 2f);
            float x = pos.x, y = pos.y;
            p.BeginPath();
            p.MoveTo(new Vector2(x + r, y));
            p.LineTo(new Vector2(x + w - r, y));
            p.ArcTo(new Vector2(x + w, y), new Vector2(x + w, y + r), r);
            p.LineTo(new Vector2(x + w, y + h - r));
            p.ArcTo(new Vector2(x + w, y + h), new Vector2(x + w - r, y + h), r);
            p.LineTo(new Vector2(x + r, y + h));
            p.ArcTo(new Vector2(x, y + h), new Vector2(x, y + h - r), r);
            p.LineTo(new Vector2(x, y + r));
            p.ArcTo(new Vector2(x, y), new Vector2(x + r, y), r);
            p.ClosePath();
        }
    }

    /// A touch keyboard drawn in the app (a TV or kiosk may have no physical keyboard, and Windows' own touch
    /// keyboard doesn't show for Unity apps): letters, a numbers/symbols page, shift, space, backspace and Enter.
    /// Types into whatever Target returns.
    public class OnScreenKeyboard : VisualElement
    {
        static readonly string[][] Letters =
        {
            new[] { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p" },
            new[] { "a", "s", "d", "f", "g", "h", "j", "k", "l" },
            new[] { "⇧", "z", "x", "c", "v", "b", "n", "m", "⌫" },
            new[] { "123", "@", "space", ".", "↵" },
        };

        static readonly string[][] Symbols =
        {
            new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" },
            new[] { "-", "/", ":", ";", "(", ")", "₹", "&", "\"" },
            new[] { "#", "_", ",", "?", "!", "'", "+", "=", "⌫" },
            new[] { "ABC", "@", "space", ".", "↵" },
        };

        public Func<TextField> Target;
        public event Action Enter, Done;

        bool shift, symbols, doneKey;

        /// Adds a "Done" key (hides the keyboard) - Enter types a new line in a multi-line box.
        public bool DoneKey
        {
            get => doneKey;
            set { doneKey = value; Build(); }
        }

        public OnScreenKeyboard()
        {
            AddToClassList("osk");
            Build();
        }

        void Build()
        {
            Clear();
            var rows = symbols ? Symbols : Letters;
            for (int r = 0; r < rows.Length; r++)
            {
                var line = new VisualElement().WithClass("osk__row");
                var keys = doneKey && r == rows.Length - 1 ? rows[r].Concat(new[] { "Done" }) : rows[r];
                foreach (string key in keys)
                {
                    string k = key;
                    string label = k == "space" ? "space" : shift && k.Length == 1 && char.IsLetter(k[0]) ? k.ToUpperInvariant() : k;
                    var b = new Button(() => Press(k)) { text = label, focusable = false }.WithClass("osk__key");
                    if (k == "space") b.AddToClassList("osk__key--space");
                    else if (k == "↵") b.AddToClassList("osk__key--enter");
                    else if (k.Length > 1 || k == "⇧" || k == "⌫") b.AddToClassList("osk__key--wide");
                    if (k == "⇧" && shift) b.AddToClassList("osk__key--on");
                    line.Add(b);
                }
                Add(line);
            }
        }

        void Press(string key)
        {
            var field = Target?.Invoke();
            switch (key)
            {
                case "⇧": shift = !shift; Build(); return;
                case "123": symbols = true; Build(); return;
                case "ABC": symbols = false; Build(); return;
                case "Done": Done?.Invoke(); return;
                case "↵":
                    if (field == null || !field.multiline) { Enter?.Invoke(); return; }
                    break;
            }
            if (field == null) return;
            string value = field.value ?? "";
            if (key == "⌫")
            {
                if (value.Length > 0) field.value = value.Substring(0, value.Length - 1);
            }
            else
            {
                string ch = key == "space" ? " " : key == "↵" ? "\n" : shift ? key.ToUpperInvariant() : key;
                field.value = value + ch;
                if (shift) { shift = false; Build(); }
            }
            field.SelectRange(field.value.Length, field.value.Length);
        }
    }

    /// An on/off switch (iOS style). Looks: .switch, .switch__knob, .switch--on.
    public class Switch : VisualElement
    {
        bool value;
        public event Action<bool> Changed;

        public bool Value
        {
            get => value;
            set { this.value = value; EnableInClassList("switch--on", value); }
        }

        public Switch(bool on)
        {
            AddToClassList("switch");
            Add(new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("switch__knob"));
            Value = on;
            RegisterCallback<ClickEvent>(_ =>
            {
                Value = !Value;
                Changed?.Invoke(Value);
            });
        }
    }

    /// A row of mutually exclusive options. Looks: .segmented, .segmented__item, .segmented__item--selected.
    public class Segmented : VisualElement
    {
        readonly List<Label> items = new List<Label>();
        int index = -1;
        public event Action<int> Changed;

        public int Index
        {
            get => index;
            set
            {
                index = value;
                for (int i = 0; i < items.Count; i++) items[i].EnableInClassList("segmented__item--selected", i == index);
            }
        }

        public Segmented(IEnumerable<string> options, int selected)
        {
            AddToClassList("segmented");
            foreach (string option in options)
            {
                int i = items.Count;
                var item = new Label(option).WithClass("segmented__item");
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    if (Index == i) return;
                    Index = i;
                    Changed?.Invoke(i);
                });
                items.Add(item);
                Add(item);
            }
            Index = selected;
        }
    }

    /// A draggable value slider. Looks: .slider, .slider__track, .slider__fill, .slider__knob.
    /// Changed fires while dragging; Committed once on release.
    public class ValueSlider : VisualElement
    {
        readonly VisualElement track, fill, knob;
        readonly float min, max, step;
        const float KnobSize = 28f;   // .slider__knob width; the track is inset by half of it (.slider__track margin)
        float value;
        bool dragging;
        public event Action<float> Changed, Committed;

        public float Value
        {
            get => value;
            set
            {
                this.value = Mathf.Clamp(step > 0 ? Mathf.Round(value / step) * step : value, min, max);
                float t = Mathf.InverseLerp(min, max, this.value);
                fill.style.width = Length.Percent(t * 100f);
                // The knob's centre sits on the track (inset by half a knob each side): at 100% it used to start at the
                // track's end and hang out of the row.
                knob.style.left = Length.Percent(t * 100f);
                knob.style.translate = new Translate(-t * KnobSize, 0);
            }
        }

        public ValueSlider(float min, float max, float value, float step = 0f)
        {
            this.min = min;
            this.max = max;
            this.step = step;
            AddToClassList("slider");
            track = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("slider__track");
            fill = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("slider__fill");
            knob = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("slider__knob");
            track.Add(fill);
            Add(track);
            Add(knob);
            Value = value;

            RegisterCallback<PointerDownEvent>(e =>
            {
                dragging = true;
                this.CapturePointer(e.pointerId);
                SetFrom(e.localPosition.x);
                e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e => { if (dragging) SetFrom(e.localPosition.x); });
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging) return;
                dragging = false;
                this.ReleasePointer(e.pointerId);
                Committed?.Invoke(Value);
            });
            // The finger was taken away (a touch cancelled, another element took it): don't stay stuck "dragging" -
            // the next touch anywhere on the slider would otherwise move it. Keep what was set.
            RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (!dragging) return;
                dragging = false;
                Committed?.Invoke(Value);
            });
        }

        void SetFrom(float x)
        {
            float before = Value;
            float inset = track.layout.x;
            Value = Mathf.Lerp(min, max, Mathf.InverseLerp(inset, inset + track.layout.width, x));
            if (!Mathf.Approximately(before, Value)) Changed?.Invoke(Value);
        }
    }

    /// A scrollbar to drag with a finger - touch screens have no scroll wheel, and the ScrollView's own scroller is
    /// unstyled without Unity's default theme. Drag the thumb, or tap the track to jump there. Hidden when everything
    /// fits; while shown, the view gets .has-scrollbar (room for it). Sits inside the view, along its right edge.
    /// Looks: .scrollbar, .scrollbar__thumb.
    public class TouchScrollbar : VisualElement
    {
        readonly ScrollView view;
        readonly VisualElement thumb;
        bool dragging;
        float grab;

        public static TouchScrollbar AddTo(ScrollView view) => new TouchScrollbar(view);

        TouchScrollbar(ScrollView view)
        {
            this.view = view;
            AddToClassList("scrollbar");
            view.hierarchy.Add(this);
            thumb = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("scrollbar__thumb");
            Add(thumb);
            view.verticalScroller.valueChanged += _ => Refresh();
            view.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            view.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            RegisterCallback<GeometryChangedEvent>(_ => Refresh());

            RegisterCallback<PointerDownEvent>(e =>
            {
                float top = thumb.layout.y, height = thumb.layout.height;
                bool onThumb = e.localPosition.y >= top && e.localPosition.y <= top + height;
                grab = onThumb ? e.localPosition.y - top : height / 2f;
                dragging = true;
                AddToClassList("scrollbar--dragging");
                this.CapturePointer(e.pointerId);
                ScrollTo(e.localPosition.y);
                e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e => { if (dragging) ScrollTo(e.localPosition.y); });
            RegisterCallback<PointerUpEvent>(e => { if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId); });
            RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                dragging = false;
                RemoveFromClassList("scrollbar--dragging");
            });
        }

        float ViewHeight => view.contentViewport.layout.height;
        float ContentHeight => view.contentContainer.layout.height;

        void Refresh()
        {
            float track = layout.height, viewH = ViewHeight, contentH = ContentHeight;
            bool needed = contentH > viewH + 1f && track > 0f && !float.IsNaN(contentH);
            EnableInClassList("scrollbar--hidden", !needed);
            view.EnableInClassList("has-scrollbar", needed);
            if (!needed) return;
            float height = Mathf.Min(track, Mathf.Max(track * viewH / contentH, 56f));
            float t = Mathf.Clamp01(view.scrollOffset.y / (contentH - viewH));
            thumb.style.height = height;
            thumb.style.top = t * (track - height);
        }

        void ScrollTo(float y)
        {
            float room = layout.height - thumb.layout.height;
            if (room <= 0f) return;
            float t = Mathf.Clamp01((y - grab) / room);
            view.scrollOffset = new Vector2(view.scrollOffset.x, t * (ContentHeight - ViewHeight));
        }
    }

    /// Scroll a list by dragging it with a finger (or the mouse), like a phone. Tested 2026-10-06: Unity's ScrollView
    /// didn't move at all for a 40-unit finger swipe here, so a swipe on the menu or the conversation did nothing.
    /// A press only becomes a drag after `threshold` units, so a slightly wobbly tap still taps; once dragging, the list
    /// keeps the finger and the row under it gets no click. Sliders, the scrollbar and text boxes keep their own drags.
    public static class DragScroll
    {
        public static void AddTo(ScrollView view, float threshold = 14f)
        {
            int pointer = -1;
            bool dragging = false;
            Vector2 start = default;
            float startOffset = 0f;

            bool OwnDrag(IEventHandler target)
            {
                for (var e = target as VisualElement; e != null && e != view; e = e.hierarchy.parent)
                    if (e is ValueSlider || e is TouchScrollbar || e is TextField) return true;
                return false;
            }

            view.RegisterCallback<PointerDownEvent>(e =>
            {
                if (pointer != -1 || e.button > 0 || OwnDrag(e.target)) return;
                pointer = e.pointerId;
                dragging = false;
                start = e.position;
                startOffset = view.scrollOffset.y;
            }, TrickleDown.TrickleDown);

            view.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (e.pointerId != pointer) return;
                float dy = e.position.y - start.y;
                if (!dragging)
                {
                    if (Mathf.Abs(dy) < threshold) return;
                    dragging = true;
                    start = e.position;               // no jump by the threshold
                    startOffset = view.scrollOffset.y;
                    dy = 0f;
                    view.CapturePointer(pointer);
                }
                float max = Mathf.Max(0f, view.contentContainer.layout.height - view.contentViewport.layout.height);
                view.scrollOffset = new Vector2(view.scrollOffset.x, Mathf.Clamp(startOffset - dy, 0f, max));
                e.StopPropagation();
            }, TrickleDown.TrickleDown);

            view.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != pointer) return;
                if (dragging)
                {
                    if (view.HasPointerCapture(pointer)) view.ReleasePointer(pointer);
                    e.StopPropagation();
                }
                pointer = -1;
                dragging = false;
            }, TrickleDown.TrickleDown);

            view.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == pointer) { pointer = -1; dragging = false; } }, TrickleDown.TrickleDown);
            view.RegisterCallback<PointerCaptureOutEvent>(_ => { if (dragging) { pointer = -1; dragging = false; } });
        }
    }

    static class ElementExtensions
    {
        public static T WithClass<T>(this T element, params string[] classes) where T : VisualElement
        {
            foreach (string c in classes) element.AddToClassList(c);
            return element;
        }
    }
}
