using System.Collections.Generic;
using ChatbotAI.Dialogue;
using UnityEngine;
using UnityEngine.UI;

namespace ChatbotAI.UI
{
    /// In-app avatar picker (rough): an "Avatar" button on the right opens a list of every Avatar
    /// Profile (Resources/Avatars); picking one makes it the active avatar - its persona, knowledge,
    /// voices and 3D character all switch (the Avatar Stage swaps the model, whatever it was saying
    /// stops). Builds its own UI at runtime - nothing to lay out. Lasts for the session; the app
    /// starts with the registry's first avatar.
    public class AvatarPickerUI : MonoBehaviour
    {
        [SerializeField] AvatarRegistry registry;
        [Tooltip("Draw order against other UI (higher = on top).")]
        [SerializeField] int sortingOrder = 20;
        [Tooltip("Top-right corner offset, in pixels at 1280x720 (below the test screen's Ask button).")]
        [SerializeField] Vector2 offset = new Vector2(-16, -150);

        Font font;
        Text toggleLabel;
        GameObject list;
        readonly List<(AvatarProfile profile, Image background)> rows = new List<(AvatarProfile, Image)>();

        static readonly Color Panel = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        static readonly Color Row = new Color(0.22f, 0.22f, 0.26f, 1f);
        static readonly Color Current = new Color(0.18f, 0.55f, 0.95f, 1f);

        void Start()
        {
            if (!registry) registry = AvatarRegistry.Instance ? AvatarRegistry.Instance : FindAnyObjectByType<AvatarRegistry>();
            if (!registry)
            {
                Debug.LogWarning("AvatarPickerUI: no Avatar Registry in the scene.");
                enabled = false;
                return;
            }
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            registry.OnActiveChanged += _ => Refresh();
            Refresh();
        }

        void Build()
        {
            var canvasGO = new GameObject("Avatar Picker", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var toggle = MakeButton(canvasGO.transform, "Avatar", Current, () => list.SetActive(!list.activeSelf));
            var trt = (RectTransform)toggle.transform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1, 1);
            trt.anchoredPosition = offset;
            trt.sizeDelta = new Vector2(280, 44);
            toggleLabel = toggle.GetComponentInChildren<Text>();

            list = new GameObject("List", typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            list.transform.SetParent(canvasGO.transform, false);
            list.GetComponent<Image>().color = Panel;
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rt = (RectTransform)list.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = offset + new Vector2(0, -52);
            rt.sizeDelta = new Vector2(280, 0);

            var title = MakeText(list.transform, "Choose an avatar", 16, new Color(0.8f, 0.8f, 0.85f));
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            foreach (var p in registry.Profiles)
            {
                var profile = p;
                string what = profile.IsOpenChat ? "open chat" : profile.TopicsPhrase(false);
                var button = MakeButton(list.transform, $"{profile.displayName}  ·  {what}", Row, () => Pick(profile));
                button.name = profile.avatarId;
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
                rows.Add((profile, button.GetComponent<Image>()));
            }
            list.SetActive(false);
        }

        void Pick(AvatarProfile profile)
        {
            list.SetActive(false);
            registry.SetActive(profile);
        }

        void Refresh()
        {
            var active = registry.Active;
            if (toggleLabel) toggleLabel.text = $"Avatar: {(active ? active.displayName + (active.IsOpenChat ? " (open chat)" : "") : "-")}  ▼";
            foreach (var (profile, background) in rows) background.color = profile == active ? Current : Row;
        }

        Button MakeButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            var text = MakeText(go.transform, label, 17, Color.white);
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12, 0);
            trt.offsetMax = new Vector2(-12, 0);
            return button;
        }

        Text MakeText(Transform parent, string content, int size, Color color)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }
    }
}
