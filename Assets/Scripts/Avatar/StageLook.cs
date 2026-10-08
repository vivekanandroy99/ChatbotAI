using System;
using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// What the stage behind an avatar looks like (drawn by StageBackdrop). One asset can be shared by several avatars;
    /// an Avatar Profile's Stage picks one, else the scene's Stage Backdrop default is used. The app's menu (Stage)
    /// can switch the style, picture and brightness per avatar without editing the asset.
    [CreateAssetMenu(menuName = "Chatbot AI/Stage Look", fileName = "Stage Look")]
    public class StageLook : ScriptableObject
    {
        public enum Style
        {
            [Tooltip("A curved photo-studio wall that runs into the floor, a soft glow behind the character, light and a shadow on the floor.")]
            Studio,
            [Tooltip("A plain top-to-bottom colour gradient.")]
            Gradient,
            [Tooltip("A picture (office, brand artwork...), blurred like a photo's background.")]
            Picture,
            [Tooltip("The flat theme colour (no stage).")]
            FlatColour,
        }

        [Serializable]
        public class Colours
        {
            [Tooltip("Studio: the colour of the backdrop paper (wall and floor are one seamless sheet, lit by real lights).")]
            public Color paper;
            [Tooltip("Studio: the background light - a spot hidden behind the character, aimed at the wall behind the head.")]
            public Color backgroundLight;
            [Tooltip("Studio: strength of the background light (0 = off).")]
            [Range(0f, 30f)] public float backgroundLightStrength;
            [Tooltip("Gradient: the wall high up.")] public Color wallTop;
            [Tooltip("Gradient: the wall at the character's height.")] public Color wallBottom;
            [Tooltip("Gradient: the floor.")] public Color floor;
            [Tooltip("The light from behind that outlines hair and shoulders against the stage.")]
            public Color rimLight;

            public Colours() { }

            public Colours(Color paper, Color light, float strength, Color top, Color bottom, Color floor, Color rim)
            {
                this.paper = paper;
                backgroundLight = light;
                backgroundLightStrength = strength;
                wallTop = top;
                wallBottom = bottom;
                this.floor = floor;
                rimLight = rim;
            }
        }

        [Tooltip("Studio (default), Gradient, Picture or Flat colour.")]
        public Style style = Style.Studio;

        [Header("Colours (follow the app's Light / Dark appearance)")]
        public Colours dark = new Colours(new Color32(0x1B, 0x1C, 0x20, 0xFF), new Color32(0xC8, 0xD4, 0xFF, 0xFF), 26f,
                                          new Color32(0x10, 0x10, 0x13, 0xFF), new Color32(0x24, 0x25, 0x2A, 0xFF),
                                          new Color32(0x1B, 0x1B, 0x1F, 0xFF), new Color32(0xB8, 0xC8, 0xFF, 0xFF));
        public Colours light = new Colours(new Color32(0xC4, 0xC6, 0xCB, 0xFF), new Color32(0xFF, 0xF3, 0xE2, 0xFF), 8f,
                                           new Color32(0xB4, 0xB7, 0xBE, 0xFF), new Color32(0xD2, 0xD4, 0xD9, 0xFF),
                                           new Color32(0xC3, 0xC5, 0xCB, 0xFF), new Color32(0xFF, 0xF4, 0xE6, 0xFF));

        [Header("Studio")]
        [Tooltip("How far behind the character the backdrop wall is, metres (real portrait studios: 2-5 m).")]
        [Range(2f, 8f)] public float wallDistance = 4f;
        [Tooltip("Size of the background light's pool on the wall (spot angle, degrees).")]
        [Range(20f, 120f)] public float backgroundLightSize = 70f;
        [Tooltip("Paper texture: the faint grain and mottling of a painted / paper backdrop (0 = perfectly smooth).")]
        [Range(0f, 1f)] public float paperTexture = 0.5f;
        [Tooltip("How shiny the paper is (a little sheen on the floor from the key light).")]
        [Range(0f, 0.6f)] public float sheen = 0.15f;

        [Header("Gradient")]
        [Tooltip("How dark the soft shadow under the feet is (the Studio gets real shadows instead).")]
        [Range(0f, 1f)] public float contactShadow = 0.5f;

        [Header("Picture")]
        [Tooltip("The picture for the Picture style. Pictures in StreamingAssets/Stage Pictures can also be picked in the app.")]
        public Texture2D picture;
        [Tooltip("How out of focus the picture is (0 = sharp).")]
        [Range(0f, 1f)] public float pictureBlur = 0.45f;

        [Header("All styles")]
        [Tooltip("Overall brightness of the stage.")]
        [Range(0.3f, 2f)] public float brightness = 1f;
        [Tooltip("Strength of the light from behind the character (0 = off).")]
        [Range(0f, 2f)] public float rimLight = 0.7f;

        public Colours For(bool darkTheme) => darkTheme ? dark : light;
    }
}
