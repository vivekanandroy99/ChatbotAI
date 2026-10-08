using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChatbotAI.Dialogue;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChatbotAI.Avatar
{
    /// The stage behind the avatar, replacing the flat background colour. Builds (in code, nothing saved in the scene):
    ///   - Studio: a real photo studio - one seamless sheet of backdrop paper (floor, a curved cove, a wall all the way
    ///     round, so no camera turn or lens ever shows an edge) in a lit URP material with a faint paper grain, the wall
    ///     a few metres behind the character; a background light hidden behind the character lights the wall behind
    ///     the head (as seen from the camera, so it stays behind when the camera turns); the scene's key light casts
    ///     the character's real shadow on the floor, and URP's SSAO darkens the cove and the feet;
    ///   - Gradient: the same room in a flat, unlit gradient (ChatbotAI/Stage Backdrop shader);
    ///   - Picture: a plane that fills the view behind the character, out of focus;
    ///   - a rim light from behind that outlines hair and shoulders (all but Flat colour).
    /// Which look: the active Avatar Profile's Stage, else Default Look; the app's menu (Stage) can override the style,
    /// picture and brightness per avatar (PlayerPrefs "stage/<avatarId>"). Colours follow the app's Light / Dark theme
    /// (CompanionUI sets Dark). Flat colour hides everything and the camera's clear colour shows, as before.
    [ExecuteAlways, DefaultExecutionOrder(1000)]
    public class StageBackdrop : MonoBehaviour
    {
        /// Pictures dropped in StreamingAssets/<this> can be picked in the app (also after building the app).
        public const string PicturesFolder = "Stage Pictures";

        [Tooltip("The stage for avatars whose Avatar Profile names no Stage.")]
        [SerializeField] StageLook defaultLook;
        [Tooltip("ChatbotAI/Stage Backdrop (Gradient and Picture; kept here so builds include it).")]
        [SerializeField] Shader shader;
        [Tooltip("A URP Lit material for the Studio's paper (kept here so builds include the shader).")]
        [SerializeField] Material paperMaterial;
        [Tooltip("The camera that films the avatar (the main camera if empty).")]
        [SerializeField] Camera stageCamera;
        [Tooltip("The Avatar Stage - the stage follows its character (light behind the head, the wall behind its back).")]
        [SerializeField] AvatarStage avatarStage;

        [Header("Premium look")]
        [Tooltip("The post-processing volume (Assets/Stage/Studio Post.asset): colour grading, soft bloom, vignette, " +
                 "background blur.")]
        [SerializeField] UnityEngine.Rendering.Volume postVolume;
        [Tunable("Display & performance", "Premium look (post-processing)",
                 note = "Film-like colour, a soft glow on highlights, darker corners and a blurred background. Off saves a little graphics power.")]
        [SerializeField] bool postProcessing = true;
        [Tunable("Display & performance", "Background blur", 0f, 1f, 0.05f,
                 note = "How out of focus the stage behind the character is, like a portrait camera. 0 = sharp.")]
        [SerializeField, Range(0f, 1f)] float backgroundBlur = 0.45f;
        [Tunable("Display & performance", "Smooth edges (anti-aliasing)", note = "Removes jagged edges on hair and outlines.")]
        [SerializeField] bool smoothEdges = true;
        [Tunable("Display & performance", "Fill light", 0f, 1f, 0.05f,
                 note = "Soft light on the shadow side of the face (a fraction of the main light). 0 = dramatic, 0.5 = even.")]
        [SerializeField, Range(0f, 1f)] float fillLight = 0.35f;
        [Tunable("Display & performance", "3D detail limit (million pixels)", 1f, 9f, 0.1f,
                 note = "On a screen bigger than this (4K = 8.3) the 3D scene is drawn at this size and sharpened up to fit; " +
                        "text and buttons stay fully sharp. 4K drawn in full slowed answers by ~50% in a test. 9 = always full size.")]
        [SerializeField, Range(1f, 9f)] float renderMegapixels = 3.7f;

        [Header("Studio lighting (HDRI)")]
        [Tooltip("A Skybox/Panoramic material; the studio environment (softboxes in a dark room) is drawn into it in Play " +
                 "mode and lights the character: soft light from all round, and softbox reflections in eyes, skin and hair.")]
        [SerializeField] Material environmentSkybox;
        [Tunable("Display & performance", "Studio lighting (reflections & soft light)",
                 note = "Lights the character like a photo studio: soft light from all round and softbox highlights in the eyes.")]
        [SerializeField] bool studioEnvironment = true;
        [Tunable("Display & performance", "Studio light strength", 0f, 2f, 0.05f,
                 note = "How much the studio's softboxes light the character (0.7 = normal; higher is brighter and flatter).")]
        [SerializeField, Range(0f, 2f)] float environmentStrength = 0.7f;

        [Header("Size of the studio")]
        [Tooltip("Radius of the round studio, metres. Its wall is the look's Wall Distance behind the character; the camera " +
                 "must stay inside (full body on a tall screen is 5.5 m in front).")]
        [SerializeField, Range(6f, 20f)] float radius = 10f;
        [Tooltip("Radius of the curve where the floor runs into the wall, metres.")]
        [SerializeField, Range(0.5f, 4f)] float coveRadius = 1.5f;
        [Tooltip("Wall height, metres (high enough that a camera looking up never sees its top).")]
        [SerializeField, Range(4f, 30f)] float wallHeight = 14f;

        [Serializable]
        public class Choice
        {
            public int style = -1;          // -1 = the look's own style
            public string picture = "";     // "" = the look's own picture, else a file in StreamingAssets/Stage Pictures
            public float brightness = -1f;  // <= 0 = the look's own
        }

        /// The lights on the character (menu > Lighting), per bot. Every value is a change on the scene's own lights and on the
        /// Advanced settings (Fill light, Studio light strength), so 1 / 0 = as delivered.
        [Serializable]
        public class Lighting
        {
            public float key = 1f;       // main light brightness, x
            public float turn;           // main light direction round the character, degrees (+ = from the viewer's right)
            public float height;         // main light higher (+) or lower (-), degrees
            public float warmth;         // -1 cool daylight ... +1 warm golden
            public float soft = 1f;      // soft fill on the shadow side, x
            public float back = 1f;      // back (rim) light, x
            public float studio = 1f;    // the studio's soft all-round light and reflections, x
            public float shadows = 1f;   // how dark the shadows are, x

            public Lighting Copy() => (Lighting)MemberwiseClone();

            public bool SameAs(Lighting o) =>
                Mathf.Abs(key - o.key) < 0.01f && Mathf.Abs(turn - o.turn) < 0.5f && Mathf.Abs(height - o.height) < 0.5f &&
                Mathf.Abs(warmth - o.warmth) < 0.01f && Mathf.Abs(soft - o.soft) < 0.01f && Mathf.Abs(back - o.back) < 0.01f &&
                Mathf.Abs(studio - o.studio) < 0.01f && Mathf.Abs(shadows - o.shadows) < 0.01f;
        }

        /// A ready-made lighting (menu > Lighting): tap one, then fine-tune.
        public readonly struct LightingPreset
        {
            public readonly string name, description;
            public readonly Lighting values;
            public LightingPreset(string name, string description, Lighting values)
            {
                this.name = name;
                this.description = description;
                this.values = values;
            }
        }

        public static readonly LightingPreset[] LightingPresets =
        {
            new LightingPreset("Natural", "As set up - a warm main light with soft fill", new Lighting()),
            new LightingPreset("Soft", "Gentle and even, light shadows - flattering for faces",
                new Lighting { key = 0.85f, soft = 1.7f, studio = 1.3f, shadows = 0.5f, back = 0.8f }),
            new LightingPreset("Bright", "Bright and clear, like a shop window",
                new Lighting { key = 1.35f, soft = 1.3f, studio = 1.2f, shadows = 0.75f }),
            new LightingPreset("Dramatic", "A strong light from one side and deep shadows",
                new Lighting { key = 1.3f, turn = 40f, height = -5f, soft = 0.25f, studio = 0.55f, back = 1.4f, shadows = 1f }),
            new LightingPreset("Warm", "Golden and welcoming",
                new Lighting { warmth = 0.7f, key = 1.05f, soft = 1.1f }),
            new LightingPreset("Cool", "Clean daylight, modern",
                new Lighting { warmth = -0.85f, key = 1.05f, soft = 1.1f }),
            new LightingPreset("Glow", "A bright halo around the edges from behind",
                new Lighting { key = 0.9f, back = 2f, soft = 1.2f, studio = 1.1f }),
        };

        public static int MatchingLighting(Lighting l)
        {
            for (int i = 0; i < LightingPresets.Length; i++)
                if (LightingPresets[i].values.SameAs(l)) return i;
            return -1;
        }

        static string LightingKey(AvatarProfile profile) => "light/" + (profile ? profile.avatarId : "default");

        public static Lighting LoadLighting(AvatarProfile profile)
        {
            string json = PlayerPrefs.GetString(LightingKey(profile), "");
            if (json.Length > 0)
                try { return JsonUtility.FromJson<Lighting>(json) ?? new Lighting(); }
                catch (ArgumentException) { }
            return new Lighting();
        }

        public static void SaveLighting(AvatarProfile profile, Lighting l)
        {
            PlayerPrefs.SetString(LightingKey(profile), JsonUtility.ToJson(l));
            PlayerPrefs.Save();
        }

        public static void ForgetLighting(AvatarProfile profile)
        {
            PlayerPrefs.DeleteKey(LightingKey(profile));
            PlayerPrefs.Save();
        }

        public static bool HasLighting(AvatarProfile profile) => PlayerPrefs.HasKey(LightingKey(profile));

        Lighting lighting = new Lighting();

        /// The lighting being shown now (the saved one for the active bot, or one being tried in the menu).
        public Lighting CurrentLighting => lighting;

        /// Shows this lighting at once, without saving (a slider being dragged).
        public void PreviewLighting(Lighting l) => lighting = l.Copy();

        // The scene's main light as delivered - the menu's changes are made from these.
        Light keyBaseOf;
        float keyBaseIntensity, keyBaseShadow;
        Quaternion keyBaseRotation;
        Color keyBaseColour;

        // 1 = as delivered; menu > Lighting's warm/cool tint keeps the light's brightness.
        static Color Tint(Color c, float warmth)
        {
            if (Mathf.Abs(warmth) < 0.01f) return c;
            // Toward a golden or a daylight-blue light (blended, not multiplied: the scene's own light is already warm).
            var toward = warmth > 0f ? new Color(1f, 0.72f, 0.42f) : new Color(0.72f, 0.86f, 1f);
            var tinted = Color.Lerp(c, toward, Mathf.Clamp01(Mathf.Abs(warmth)));
            tinted.a = 1f;
            return tinted;
        }
        static readonly int WallTop = Shader.PropertyToID("_WallTop"), WallBottom = Shader.PropertyToID("_WallBottom"),
            FloorColor = Shader.PropertyToID("_Floor"), Glow = Shader.PropertyToID("_Glow"),
            GlowCenter = Shader.PropertyToID("_GlowCenter"), PoolCenter = Shader.PropertyToID("_PoolCenter"),
            PoolStrength = Shader.PropertyToID("_PoolStrength"), ShadowStrength = Shader.PropertyToID("_ShadowStrength"),
            GradientHeight = Shader.PropertyToID("_GradientHeight"), Brightness = Shader.PropertyToID("_Brightness"),
            MainTex = Shader.PropertyToID("_MainTex"), PictureBlur = Shader.PropertyToID("_PictureBlur"),
            PictureMode = Shader.PropertyToID("_PictureMode"),
            BaseColor = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap"),
            Smoothness = Shader.PropertyToID("_Smoothness");

        static readonly Dictionary<string, Texture2D> loadedPictures = new Dictionary<string, Texture2D>();
        static Texture2D paperGrain;

        bool dark = true, dirty = true;
        GameObject drum, plane;
        Light rim, backgroundLight, keyLight, fill;
        Material gradientMaterial, pictureMaterial, paper;
        Mesh drumMesh, planeMesh;
        MeshRenderer drumRenderer;
        AvatarRegistry registry;
        AvatarProfile applied;
        Choice choice = new Choice();
        StageLook currentLook;
        Texture2D picture;
        GameObject trackedModel;
        Transform head, hips;

        /// Follow the app's Light / Dark appearance.
        public bool Dark
        {
            get => dark;
            set { dark = value; dirty = true; }
        }

        public StageLook DefaultLook => defaultLook;

        /// What's shown now (after menu choices and fallbacks).
        public StageLook.Style CurrentStyle { get; private set; }

        /// Re-read the menu choices (after changing them).
        public void Refresh() => dirty = true;

        public StageLook LookFor(AvatarProfile profile) => profile && profile.stage ? profile.stage : defaultLook;

        static string Key(AvatarProfile profile) => "stage/" + (profile ? profile.avatarId : "default");

        public static Choice LoadChoice(AvatarProfile profile)
        {
            string json = PlayerPrefs.GetString(Key(profile), "");
            if (json.Length > 0)
                try { return JsonUtility.FromJson<Choice>(json) ?? new Choice(); }
                catch (ArgumentException) { }
            return new Choice();
        }

        public static void SaveChoice(AvatarProfile profile, Choice c)
        {
            PlayerPrefs.SetString(Key(profile), JsonUtility.ToJson(c));
            PlayerPrefs.Save();
        }

        public static void ForgetChoice(AvatarProfile profile)
        {
            PlayerPrefs.DeleteKey(Key(profile));
            PlayerPrefs.Save();
        }

        public static bool HasChoice(AvatarProfile profile) => PlayerPrefs.HasKey(Key(profile));

        /// Picture files the app can use (StreamingAssets/Stage Pictures: .png / .jpg).
        public static List<string> PictureFiles()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, PicturesFolder);
            if (!Directory.Exists(folder)) return new List<string>();
            return Directory.GetFiles(folder)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static Texture2D LoadPicture(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            if (loadedPictures.TryGetValue(file, out var cached) && cached) return cached;
            string path = Path.Combine(Application.streamingAssetsPath, PicturesFolder, file);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                name = file, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave,
            };
            if (!tex.LoadImage(File.ReadAllBytes(path), true))
            {
                DestroyImmediate(tex);
                Debug.LogWarning($"Stage: couldn't read the picture {file}.");
                return null;
            }
            loadedPictures[file] = tex;
            return tex;
        }

        // The paper's grain: tileable value noise - large soft mottling (hand-painted backdrops) over a fine grain.
        // White at strength 0; it multiplies the paper colour, so it only ever darkens a little (up to ~20%).
        // Made again only when the strength changes.
        static float paperGrainStrength = -1f;

        static Texture2D PaperGrain(float strength)
        {
            strength = Mathf.Round(Mathf.Clamp01(strength) * 20f) / 20f;
            if (paperGrain && Mathf.Approximately(strength, paperGrainStrength)) return paperGrain;
            if (paperGrain) DestroyImmediate(paperGrain);
            paperGrainStrength = strength;
            const int size = 512;
            var rng = new System.Random(7);
            float[][] lattices = new[] { 4, 9, 23, 64, 170 }.Select(g => Enumerable.Range(0, g * g).Select(_ => (float)rng.NextDouble()).ToArray()).ToArray();
            int[] grids = { 4, 9, 23, 64, 170 };
            float[] weights = { 0.45f, 0.25f, 0.12f, 0.1f, 0.08f };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float v = 0f;
                    for (int o = 0; o < grids.Length; o++)
                    {
                        int g = grids[o];
                        float fx = x / (float)size * g, fy = y / (float)size * g;
                        int x0 = (int)fx, y0 = (int)fy;
                        float tx = fx - x0, ty = fy - y0;
                        tx = tx * tx * (3f - 2f * tx);
                        ty = ty * ty * (3f - 2f * ty);
                        float L(int ix, int iy) => lattices[o][(iy % g) * g + ix % g];
                        v += weights[o] * Mathf.Lerp(Mathf.Lerp(L(x0, y0), L(x0 + 1, y0), tx), Mathf.Lerp(L(x0, y0 + 1), L(x0 + 1, y0 + 1), tx), ty);
                    }
                    float shade = 1f - strength * 0.22f * (1f - Mathf.Clamp01(v * 1.3f - 0.15f));
                    byte b = (byte)Mathf.RoundToInt(shade * 255f);
                    pixels[y * size + x] = new Color32(b, b, b, 255);
                }
            paperGrain = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "Stage paper grain", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave,
            };
            paperGrain.SetPixels32(pixels);
            paperGrain.Apply(true, true);
            return paperGrain;
        }

        // ---------------- Lifecycle ----------------

        void OnEnable()
        {
            if (Application.isPlaying) registry = FindAnyObjectByType<AvatarRegistry>();
            dirty = true;
            Build();
            LateUpdate();
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorApplication.update += EditorTick;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
            RestoreEnvironment();
            RestoreRenderScale();
            if (environmentTexture) { if (Application.isPlaying) Destroy(environmentTexture); else DestroyImmediate(environmentTexture); }
            if (environmentMaterial) { if (Application.isPlaying) Destroy(environmentMaterial); else DestroyImmediate(environmentMaterial); }
            Cleanup();
        }

        void Cleanup()
        {
            foreach (UnityEngine.Object o in new UnityEngine.Object[]
                     {
                         drum, plane, rim ? rim.gameObject : null, backgroundLight ? backgroundLight.gameObject : null, fill ? fill.gameObject : null,
                         gradientMaterial, pictureMaterial, paper, drumMesh, planeMesh,
                     })
                if (o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
            drum = plane = null;
            rim = backgroundLight = fill = null;
        }

        void OnValidate() => dirty = true;

#if UNITY_EDITOR
        // Outside Play mode: keep up with edits to the Stage Look asset (a few times a second is plenty).
        double nextEditorTick;
        void EditorTick()
        {
            if (Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup < nextEditorTick) return;
            nextEditorTick = UnityEditor.EditorApplication.timeSinceStartup + 0.25;
            if (!this || !isActiveAndEnabled) return;
            dirty = true;
            LateUpdate();
        }
#endif

        void LateUpdate()
        {
            if (!drum) Build();
            if (!drum) return;
            var profile = registry ? registry.Active : null;
            if (profile != applied || dirty)
            {
                choice = profile ? LoadChoice(profile) : new Choice();
                lighting = profile ? LoadLighting(profile) : new Lighting();
                applied = profile;
                dirty = false;
            }
            Apply(profile);
            Track();
            if (Application.isPlaying) LimitRenderSize();
        }

        // ---------------- 3D render size on big screens ----------------

        // A 4K screen has 2.25x the pixels of 1440p: drawn in full, the 3D scene (lit skin and hair, shadows, blur) kept the
        // graphics card ~70% busy and slowed the AI sharing it - in a test speech recognition took 0.86 s instead of 0.5 s
        // and the first sound came at 3.1 s instead of 1.95 s. So above renderMegapixels URP draws the 3D at that size
        // (renderScale) and FSR sharpens it up to the screen; UI Toolkit draws on top at the full size. URP's asset is
        // shared, so the original values come back when this stops (in the Editor the asset file stays unchanged).
        UniversalRenderPipelineAsset scaledAsset;
        float originalScale;
        UpscalingFilterSelection originalFilter;

        void LimitRenderSize()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!asset) return;
            float pixels = (float)Screen.width * Screen.height;
            float scale = pixels <= 0 ? 1f : Mathf.Clamp(Mathf.Sqrt(renderMegapixels * 1e6f / pixels), 0.5f, 1f);
            if (scale > 0.99f) scale = 1f;
            if (!scaledAsset)
            {
                if (Mathf.Approximately(scale, 1f)) return;   // nothing to do on screens up to the limit
                scaledAsset = asset;
                originalScale = asset.renderScale;
                originalFilter = asset.upscalingFilter;
                asset.upscalingFilter = UpscalingFilterSelection.FSR;
                Debug.Log($"StageBackdrop: {Screen.width}x{Screen.height} screen - 3D drawn at {scale:0.00}x ({renderMegapixels:0.0} million pixels), sharpened to fit.");
            }
            if (Mathf.Abs(asset.renderScale - scale) > 0.005f) asset.renderScale = scale;
        }

        void RestoreRenderScale()
        {
            if (!scaledAsset) return;
            scaledAsset.renderScale = originalScale;
            scaledAsset.upscalingFilter = originalFilter;
            scaledAsset = null;
        }

        // ---------------- Building ----------------

        void Build()
        {
            if (!shader) shader = Shader.Find("ChatbotAI/Stage Backdrop");
            if (!shader)
            {
                Debug.LogWarning("Stage: the ChatbotAI/Stage Backdrop shader is missing - no stage.");
                return;
            }
            Cleanup();
            const HideFlags flags = HideFlags.HideAndDontSave;
            gradientMaterial = new Material(shader) { name = "Stage (gradient)", hideFlags = flags };
            pictureMaterial = new Material(shader) { name = "Stage (picture)", hideFlags = flags };
            var litShader = paperMaterial ? paperMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            paper = paperMaterial ? new Material(paperMaterial) : new Material(litShader);
            paper.name = "Stage (studio paper)";
            paper.hideFlags = flags;
            // No reflections of Unity's default sky: at grazing angles they tinted the floor blue.
            paper.SetFloat("_EnvironmentReflections", 0f);
            paper.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            drumMesh = MakeDrum();
            planeMesh = MakePlane();
            drum = MakeRenderer("Stage (studio)", drumMesh, paper);
            drumRenderer = drum.GetComponent<MeshRenderer>();
            plane = MakeRenderer("Stage (picture)", planeMesh, pictureMaterial);

            rim = MakeLight("Stage rim light", LightType.Directional);
            backgroundLight = MakeLight("Stage background light", LightType.Spot);
            backgroundLight.innerSpotAngle = 10f;
            fill = MakeLight("Stage fill light", LightType.Directional);
        }

        static Light MakeLight(string name, LightType type)
        {
            var light = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>();
            light.type = type;
            light.shadows = LightShadows.None;
            return light;
        }

        static GameObject MakeRenderer(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.allowOcclusionWhenDynamic = false;
            return go;
        }

        // The studio's cross-section (floor out to the cove, the cove's quarter circle, the wall up), turned round the
        // vertical axis. UVs in metres / 4 (one tile of paper grain = 4 m).
        Mesh MakeDrum()
        {
            var profile = new List<Vector2>();
            float floorEnd = radius - coveRadius;
            for (float r = 0f; r < floorEnd - 0.2f; r += r < 2f ? 0.4f : 1f) profile.Add(new Vector2(r, 0f));
            const int coveSteps = 12;
            for (int i = 0; i <= coveSteps; i++)
            {
                float a = Mathf.Lerp(-90f, 0f, i / (float)coveSteps) * Mathf.Deg2Rad;
                profile.Add(new Vector2(floorEnd + coveRadius * Mathf.Cos(a), coveRadius + coveRadius * Mathf.Sin(a)));
            }
            for (float y = coveRadius + 0.75f; y < wallHeight; y += y < 5f ? 0.75f : 2f) profile.Add(new Vector2(radius, y));
            profile.Add(new Vector2(radius, wallHeight));
            var along = new float[profile.Count];
            for (int p = 1; p < profile.Count; p++) along[p] = along[p - 1] + Vector2.Distance(profile[p - 1], profile[p]);

            const int segments = 192;
            float circumference = 2f * Mathf.PI * radius;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments, angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                for (int p = 0; p < profile.Count; p++)
                {
                    vertices.Add(new Vector3(profile[p].x * cos, profile[p].y, profile[p].x * sin));
                    uvs.Add(new Vector2(t * circumference / 4f, along[p] / 4f));
                }
            }
            var triangles = new List<int>();
            int ring = profile.Count;
            for (int s = 0; s < segments; s++)
                for (int p = 0; p < ring - 1; p++)
                {
                    int a = s * ring + p, b = a + ring;
                    // Facing inwards (the camera is inside): lit correctly without back faces.
                    triangles.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
                }
            var mesh = new Mesh { name = "Stage studio", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.bounds = new Bounds(new Vector3(0f, wallHeight / 2f, 0f), new Vector3(radius * 2f, wallHeight, radius * 2f));
            return mesh;
        }

        static Mesh MakePlane()
        {
            var mesh = new Mesh { name = "Stage picture", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------- Look ----------------

        void Apply(AvatarProfile profile)
        {
            var look = LookFor(profile);
            currentLook = look;
            if (!look)
            {
                CurrentStyle = StageLook.Style.FlatColour;
                drum.SetActive(false);
                plane.SetActive(false);
                rim.enabled = backgroundLight.enabled = fill.enabled = false;
                return;
            }
            var style = choice.style >= 0 && choice.style <= (int)StageLook.Style.FlatColour ? (StageLook.Style)choice.style : look.style;
            float brightness = choice.brightness > 0f ? choice.brightness : look.brightness;
            picture = null;
            if (style == StageLook.Style.Picture)
            {
                picture = choice.picture.Length > 0 ? LoadPicture(choice.picture) : look.picture;
                if (!picture) picture = look.picture ? look.picture : LoadPicture(PictureFiles().FirstOrDefault());
                if (!picture) style = StageLook.Style.Studio;   // nothing to show
            }
            CurrentStyle = style;

            var colours = look.For(dark);
            bool studio = style == StageLook.Style.Studio, gradient = style == StageLook.Style.Gradient;
            drum.SetActive(studio || gradient);
            plane.SetActive(style == StageLook.Style.Picture);
            if (studio)
            {
                drumRenderer.sharedMaterial = paper;
                drumRenderer.receiveShadows = true;   // the character's shadow from the key light
                var paperColour = colours.paper * Mathf.Sqrt(brightness);
                paperColour.a = 1f;
                paper.SetColor(BaseColor, paperColour);
                paper.SetFloat(Smoothness, look.sheen);
                paper.SetTexture(BaseMap, PaperGrain(look.paperTexture));
            }
            else if (gradient)
            {
                drumRenderer.sharedMaterial = gradientMaterial;
                drumRenderer.receiveShadows = false;
                gradientMaterial.SetFloat(PictureMode, 0f);
                gradientMaterial.SetColor(WallTop, colours.wallTop);
                gradientMaterial.SetColor(WallBottom, colours.wallBottom);
                gradientMaterial.SetColor(FloorColor, colours.floor);
                gradientMaterial.SetColor(Glow, Color.black);
                gradientMaterial.SetFloat(PoolStrength, 0f);
                gradientMaterial.SetFloat(ShadowStrength, look.contactShadow);
                gradientMaterial.SetFloat(GradientHeight, 5f);
                gradientMaterial.SetFloat(Brightness, brightness);
            }
            if (picture)
            {
                pictureMaterial.SetFloat(PictureMode, 1f);
                pictureMaterial.SetTexture(MainTex, picture);
                pictureMaterial.SetFloat(PictureBlur, look.pictureBlur * 6f);
                pictureMaterial.SetFloat(Brightness, brightness);
            }

            backgroundLight.enabled = studio && colours.backgroundLightStrength > 0f;
            backgroundLight.color = colours.backgroundLight;
            backgroundLight.intensity = colours.backgroundLightStrength * brightness;
            backgroundLight.spotAngle = look.backgroundLightSize;
            backgroundLight.innerSpotAngle = look.backgroundLightSize * 0.25f;

            // The rim light stays below the key light, so the key light remains URP's main (shadow-casting) light.
            if (!keyLight || !keyLight.isActiveAndEnabled)
                keyLight = FindObjectsByType<Light>()
                    .Where(l => l != rim && l != fill && l.type == LightType.Directional && l.isActiveAndEnabled).OrderByDescending(l => l.intensity).FirstOrDefault();
            // The menu's lighting (Play mode only - in the Editor the scene's light is never touched): the main light's
            // brightness, direction, warmth and shadows, from the values the scene delivered.
            var tune = Application.isPlaying ? lighting : new Lighting();
            if (keyLight && Application.isPlaying)
            {
                if (keyBaseOf != keyLight)
                {
                    keyBaseOf = keyLight;
                    keyBaseIntensity = keyLight.intensity;
                    keyBaseRotation = keyLight.transform.rotation;
                    keyBaseColour = keyLight.color;
                    keyBaseShadow = keyLight.shadowStrength;
                }
                keyLight.intensity = keyBaseIntensity * Mathf.Max(0f, tune.key);
                keyLight.transform.rotation = Quaternion.AngleAxis(-tune.turn, Vector3.up) * keyBaseRotation *
                                              Quaternion.AngleAxis(tune.height, Vector3.right);
                keyLight.color = Tint(keyBaseColour, tune.warmth);
                keyLight.shadowStrength = Mathf.Clamp01(keyBaseShadow * Mathf.Max(0f, tune.shadows));
            }
            float key = keyLight ? keyLight.intensity : 1f;
            rim.enabled = style != StageLook.Style.FlatColour && look.rimLight * tune.back > 0.01f;
            rim.color = colours.rimLight;
            // Never above the key: URP's main (shadow-casting) light must stay the key light.
            rim.intensity = Mathf.Min(look.rimLight * tune.back, key * 0.95f);
            // Fill: soft light on the shadow side of the face, a fraction of the key (a photo studio's reflector).
            float softFill = Mathf.Clamp01(fillLight * tune.soft);
            fill.enabled = softFill > 0.01f;
            fill.color = new Color(0.92f, 0.95f, 1f);
            fill.intensity = key * softFill * 0.9f;
        }

        // ---------------- Studio lighting (HDRI) ----------------

        Texture2D environmentTexture;
        Material environmentMaterial;
        string environmentKey;
        bool environmentApplied;
        (UnityEngine.Rendering.AmbientMode mode, Material skybox, DefaultReflectionMode reflections, float ambient, float reflection) savedLighting;

        // In Play mode: the studio's environment (a dark room with softboxes, drawn below) becomes the scene's skybox for
        // lighting only - the camera still clears to a colour and the stage hides it - and Unity recomputes the ambient
        // light and the reflections from it. Redrawn only when the theme, strength or key light changes.
        void ApplyEnvironment(Vector3 facing)
        {
            if (!Application.isPlaying) return;
            if (!studioEnvironment || !currentLook)
            {
                RestoreEnvironment();
                return;
            }
            var keyFrom = keyLight ? -keyLight.transform.forward : new Vector3(0.4f, 0.6f, 0.7f).normalized;
            float strength = environmentStrength * Mathf.Max(0f, lighting.studio);
            string key = $"{dark}|{strength:0.00}|{Round(keyFrom)}|{Round(facing)}";
            if (key == environmentKey && environmentApplied) return;
            environmentKey = key;

            if (!environmentApplied)
                savedLighting = (RenderSettings.ambientMode, RenderSettings.skybox, RenderSettings.defaultReflectionMode,
                                 RenderSettings.ambientIntensity, RenderSettings.reflectionIntensity);
            DrawEnvironment(keyFrom.normalized, facing);
            if (!environmentMaterial)
            {
                var shader = environmentSkybox ? environmentSkybox.shader : Shader.Find("Skybox/Panoramic");
                environmentMaterial = environmentSkybox ? new Material(environmentSkybox) : new Material(shader);
                environmentMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            environmentMaterial.SetTexture("_MainTex", environmentTexture);
            environmentMaterial.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = environmentMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = strength;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = Mathf.Clamp01(0.6f + 0.4f * strength);
            DynamicGI.UpdateEnvironment();
            environmentApplied = true;
        }

        static string Round(Vector3 v) => $"{v.x:0.0},{v.y:0.0},{v.z:0.0}";

        void RestoreEnvironment()
        {
            if (!environmentApplied) return;
            environmentApplied = false;
            environmentKey = null;
            (RenderSettings.ambientMode, RenderSettings.skybox, RenderSettings.defaultReflectionMode,
             RenderSettings.ambientIntensity, RenderSettings.reflectionIntensity) = savedLighting;
            DynamicGI.UpdateEnvironment();
        }

        // A photo studio as an HDR panorama (latitude-longitude, the Skybox/Panoramic layout): a dark room, a big warm
        // softbox where the key light comes from, a dimmer cool fill on the other side, two tall strip lights behind the
        // character (rim) and a soft overhead. Values are linear light; 1 = a white surface in full light.
        void DrawEnvironment(Vector3 keyFrom, Vector3 facing)
        {
            const int width = 512, height = 256;
            if (!environmentTexture)
                environmentTexture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
                {
                    name = "Studio environment", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
                };
            // Fill: the key's direction mirrored across the line the character faces, a little lower.
            var keyFlat = Vector3.ProjectOnPlane(keyFrom, Vector3.up);
            var fillFlat = Vector3.Reflect(keyFlat, Vector3.Cross(Vector3.up, facing).normalized);
            var fillFrom = (fillFlat.normalized * 0.95f + Vector3.up * 0.2f).normalized;
            var behind = -facing;
            var softboxes = new (Vector3 dir, float halfW, float halfH, Color light)[]
            {
                (keyFrom, 22f, 30f, new Color(1f, 0.95f, 0.88f) * 3.5f),
                (fillFrom, 28f, 26f, new Color(0.88f, 0.94f, 1f) * 1.0f),
                ((Quaternion.AngleAxis(55f, Vector3.up) * behind + Vector3.up * 0.25f).normalized, 6f, 34f, new Color(1f, 1f, 1f) * 2f),
                ((Quaternion.AngleAxis(-55f, Vector3.up) * behind + Vector3.up * 0.25f).normalized, 6f, 34f, new Color(1f, 1f, 1f) * 2f),
                (Vector3.up, 30f, 30f, new Color(1f, 1f, 1f) * 0.6f),
            };
            // The room: dark in the dark theme, a pale grey studio in the light theme.
            float floor = dark ? 0.012f : 0.05f, horizon = dark ? 0.03f : 0.09f, ceiling = dark ? 0.045f : 0.12f;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float latitude = (1f - (y + 0.5f) / height) * Mathf.PI;
                float sinLat = Mathf.Sin(latitude), cosLat = Mathf.Cos(latitude);
                for (int x = 0; x < width; x++)
                {
                    float longitude = (0.5f - (x + 0.5f) / width) * 2f * Mathf.PI;
                    var d = new Vector3(sinLat * Mathf.Cos(longitude), cosLat, sinLat * Mathf.Sin(longitude));
                    float room = d.y < 0f ? Mathf.Lerp(horizon, floor, -d.y) : Mathf.Lerp(horizon, ceiling, d.y);
                    var c = new Color(room, room, room * 1.02f);
                    foreach (var (dir, halfW, halfH, light) in softboxes)
                    {
                        float ahead = Vector3.Dot(d, dir);
                        if (ahead <= 0.2f) continue;
                        var right = Vector3.Cross(Mathf.Abs(dir.y) > 0.95f ? facing : Vector3.up, dir).normalized;
                        var up = Vector3.Cross(dir, right);
                        float ax = Mathf.Abs(Mathf.Atan2(Vector3.Dot(d, right), ahead) * Mathf.Rad2Deg);
                        float ay = Mathf.Abs(Mathf.Atan2(Vector3.Dot(d, up), ahead) * Mathf.Rad2Deg);
                        float w = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfW - 3f, halfW + 1f, ax))) *
                                  (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfH - 3f, halfH + 1f, ay)));
                        if (w <= 0f) continue;
                        float hot = 0.85f + 0.15f * (1f - (ax / halfW) * (ax / halfW));   // a touch brighter in the middle
                        c += light * (w * hot);
                    }
                    pixels[y * width + x] = c;
                }
            }
            environmentTexture.SetPixels(pixels);
            environmentTexture.Apply(false);
        }

        // Premium look, in Play mode (runtime copies - the camera and the profile asset aren't edited): post-processing
        // on the camera, SMAA edges, and a background blur that starts just behind the character, wherever the camera is.
        void ApplyPost(Camera cam, float cameraToHead)
        {
            if (!Application.isPlaying) return;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data)
            {
                data.renderPostProcessing = postProcessing;
                data.antialiasing = smoothEdges ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
                data.antialiasingQuality = AntialiasingQuality.High;
            }
            if (!postVolume) return;
            postVolume.weight = postProcessing ? 1f : 0f;
            if (postVolume.profile.TryGet(out DepthOfField dof))
            {
                dof.active = postProcessing && backgroundBlur > 0.01f && CurrentStyle != StageLook.Style.FlatColour;
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(cameraToHead + 0.6f);
                dof.gaussianEnd.Override(cameraToHead + Mathf.Lerp(14f, 3.5f, backgroundBlur));
                dof.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, 1.5f, backgroundBlur));
                dof.highQualitySampling.Override(true);
            }
        }

        // Every frame: the studio's wall sits Wall Distance behind the character; the background light, hidden behind
        // the character, lights the wall behind the head as seen from the camera; the rim light shines from behind
        // towards the camera; the picture fills the view.
        void Track()
        {
            var cam = stageCamera ? stageCamera : Camera.main;
            if (!cam) return;
            var model = avatarStage ? avatarStage.Model : null;
            if (model != trackedModel)
            {
                trackedModel = model;
                var animator = model ? model.GetComponentInChildren<Animator>() : null;
                head = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                hips = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            }
            float ground = model ? model.transform.position.y : 0f;
            Vector3 root = model ? model.transform.position : Vector3.zero;
            Vector3 headPos = head ? head.position : new Vector3(root.x, ground + 1.55f, root.z);
            Vector3 feet = hips ? new Vector3(hips.position.x, ground, hips.position.z) : new Vector3(headPos.x, ground, headPos.z);
            var facing = model ? model.transform.forward : Vector3.forward;
            facing.y = 0f;
            facing = facing.sqrMagnitude > 1e-4f ? facing.normalized : Vector3.forward;

            // The round studio's centre is in front of the character, so its wall is Wall Distance behind it.
            float wallDistance = currentLook ? Mathf.Min(currentLook.wallDistance, radius - 1f) : 4f;
            var centre = new Vector3(root.x, ground, root.z) + facing * (radius - wallDistance);
            drum.transform.position = centre;

            var toCharacter = headPos - cam.transform.position;
            toCharacter.y = 0f;
            var behind = toCharacter.sqrMagnitude > 1e-4f ? toCharacter.normalized : -facing;

            // Where the line from the camera through the head meets the wall.
            var p = new Vector2(headPos.x - centre.x, headPos.z - centre.z);
            var b = new Vector2(behind.x, behind.z);
            float pb = Vector2.Dot(p, b), toWall = -pb + Mathf.Sqrt(Mathf.Max(0f, pb * pb - p.sqrMagnitude + radius * radius));
            var onWall = headPos + behind * toWall;
            onWall.y = headPos.y + 0.35f;

            gradientMaterial.SetVector(GlowCenter, onWall);
            gradientMaterial.SetVector(PoolCenter, feet);

            // The background light: low behind the character (hidden by the body), aimed at the wall behind the head.
            var lightPos = feet + behind * 0.6f + Vector3.up * 0.6f;
            backgroundLight.transform.SetPositionAndRotation(lightPos, Quaternion.LookRotation(onWall - lightPos));
            backgroundLight.range = Vector3.Distance(lightPos, onWall) * 2.2f;

            rim.transform.rotation = Quaternion.LookRotation((-behind + Vector3.down * 0.55f).normalized);

            // The fill comes from the camera's side of the character, on the side away from the key light.
            var toCamera = -behind;
            var keyForward = keyLight ? keyLight.transform.forward : Vector3.down;
            float keySide = Mathf.Sign(Vector3.Cross(toCamera, -keyForward).y);
            var fillFrom = Quaternion.AngleAxis(-keySide * 40f, Vector3.up) * toCamera;
            fill.transform.rotation = Quaternion.LookRotation((-fillFrom + Vector3.down * 0.3f).normalized);

            ApplyPost(cam, Vector3.Distance(cam.transform.position, headPos));
            ApplyEnvironment(facing);

            if (plane.activeSelf && picture)
            {
                float distance = Mathf.Min(Vector3.Distance(cam.transform.position, headPos) + 4f, cam.farClipPlane * 0.9f);
                float height = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), width = height * cam.aspect;
                float pictureAspect = picture.width / (float)Mathf.Max(1, picture.height);
                // Cover the view (like a CSS "cover"), cropping the picture rather than leaving bars.
                if (pictureAspect > width / height) width = height * pictureAspect;
                else height = width / pictureAspect;
                plane.transform.SetPositionAndRotation(cam.transform.position + cam.transform.forward * distance, cam.transform.rotation);
                plane.transform.localScale = new Vector3(width * 1.02f, height * 1.02f, 1f);
            }
        }
    }
}
