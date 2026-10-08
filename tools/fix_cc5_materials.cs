// Edit mode: fixes what CC/iC Unity Tools 2.2.6 gets wrong on CC4/CC5 characters. Some layered hair/brow materials
// come in as plain OPAQUE URP Lit:
//   - Scalp / brow-base layers whose texture carries a transparency mask (CC4: Scalp_Transparency,
//     *_Brow_Base_Transparency; 60-90% of the texture see-through) -> made alpha-blended, drawn just under the
//     hair strands. Opaque, they were grey/black bands across the forehead and solid patches behind the brows.
//   - CC5 hair cap (Hair_Clap): a grey cap, and even transparent a lighter band -> its mesh is switched off
//     (the hair strands are a separate mesh and stay).
//   - CC5 brow shading layers (Brows_Base / Brows_Color) with an all-white opacity map (no mask) -> switched off.
//     The brow HAIRS are a separate mesh with Reallusion's hair shader and stay.
//   - Props exported with the character from the CC scene (Sphere01, obj_default) -> switched off,
//     and so are cameras / lights / audio listeners (CC's "render_focus_" camera).
//   - `hideMeshes`: garments to switch off per character (e.g. a dress worn under other clothes, flickering through).
// Safe to re-run. Add characters to `prefabs`.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { UnityEngine.Debug.Log("FIXCC: in Play mode - not changed"); return null; }

const string actors = "Assets/ActorCore Model/Actors";
string[] prefabs =
{
    actors + "/Ethan/Prefabs/Ethan.prefab",
    actors + "/Iris/Prefabs/Iris.prefab",
    actors + "/Leo/Prefabs/Leo.prefab",
    actors + "/Maya/Prefabs/Maya.prefab",
    actors + "/Pearl/Prefabs/Pearl.prefab",
    actors + "/Peter/Prefabs/Peter.prefab",
};
var hideMeshes = new System.Collections.Generic.Dictionary<string, string[]>
{
    // Maya's export has a dress AND a skirt + turtleneck on top of it: the dress flickered through them.
    // Maya's export has a dress under her turtleneck + skirt: it showed through them in patches (checked by rendering both ways).
    [actors + "/Maya/Prefabs/Maya.prefab"] = new[] { "Dress" },
};
string[] strayObjects = { "Sphere01", "obj_default", "default" };
const int UnderHairQueue = 2990;   // just before the hair shaders' transparent passes
var log = new System.Text.StringBuilder("FIXCC:");

bool IsCap(UnityEngine.Material m) => m && (m.name.StartsWith("Hair_Clap") || m.name.StartsWith("Hair_Cap"));
bool IsBrowLayer(UnityEngine.Material m) => m && (m.name.StartsWith("Brows_Base") || m.name.StartsWith("Brows_Color"));
bool IsPlainLit(UnityEngine.Material m) => m && m.shader && m.shader.name == "Universal Render Pipeline/Lit";
bool IsMaskedLayer(UnityEngine.Material m) => IsPlainLit(m) && m.name.EndsWith("_Transparency") && !m.name.StartsWith("Std_");

UnityEngine.Texture2D Read(UnityEngine.Texture t)
{
    string path = t ? UnityEditor.AssetDatabase.GetAssetPath(t) : null;
    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
    var probe = new UnityEngine.Texture2D(2, 2);
    probe.LoadImage(System.IO.File.ReadAllBytes(path));
    return probe;
}

// An opacity map with no mask in it (every pixel near white).
bool Blank(UnityEngine.Texture t)
{
    var probe = Read(t);
    if (!probe) return true;
    var px = probe.GetPixels32();
    UnityEngine.Object.DestroyImmediate(probe);
    for (int i = 0; i < px.Length; i += 97)
        if (px[i].r < 230) return false;
    return true;
}

// A texture whose alpha channel is a real mask (a good share of it see-through).
bool HasAlphaMask(UnityEngine.Texture t)
{
    var probe = Read(t);
    if (!probe) return false;
    var px = probe.GetPixels32();
    UnityEngine.Object.DestroyImmediate(probe);
    int clear = 0, n = 0;
    for (int i = 0; i < px.Length; i += 97, n++)
        if (px[i].a < 128) clear++;
    return n > 0 && clear > n / 10;
}

void MakeTransparent(UnityEngine.Material m, int queue)
{
    m.SetFloat("_Surface", 1f);
    m.SetFloat("_Blend", 0f);
    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
    m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    m.SetFloat("_ZWrite", 0f);
    m.SetFloat("_AlphaClip", 0f);
    m.SetFloat("_Cull", 0f);
    m.SetOverrideTag("RenderType", "Transparent");
    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    m.DisableKeyword("_ALPHATEST_ON");
    m.renderQueue = queue;
    m.SetShaderPassEnabled("DepthOnly", false);
    m.SetShaderPassEnabled("ShadowCaster", false);
    UnityEditor.EditorUtility.SetDirty(m);
}

// The texture has to keep its alpha when compressed.
void KeepAlpha(UnityEngine.Texture t)
{
    var importer = UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(t)) as UnityEditor.TextureImporter;
    if (importer == null || importer.alphaSource == UnityEditor.TextureImporterAlphaSource.FromInput) return;
    importer.alphaSource = UnityEditor.TextureImporterAlphaSource.FromInput;
    importer.alphaIsTransparency = true;
    importer.SaveAndReimport();
}

foreach (string prefabPath in prefabs)
{
    if (!UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath)) { log.Append($"\n{prefabPath}: missing"); continue; }
    log.Append($"\n{System.IO.Path.GetFileNameWithoutExtension(prefabPath)}:");
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
    // Scene cameras / lights exported with the character would render or light over the app's own.
    foreach (var c in root.GetComponentsInChildren<UnityEngine.Behaviour>(true))
        if ((c is UnityEngine.Camera || c is UnityEngine.Light || c is UnityEngine.AudioListener) && c.gameObject.activeSelf)
        {
            c.gameObject.SetActive(false);
            log.Append($" hid {c.GetType().Name} '{c.gameObject.name}';");
        }
    hideMeshes.TryGetValue(prefabPath, out var toHide);
    foreach (var r in root.GetComponentsInChildren<UnityEngine.Renderer>(true))
    {
        var mats = r.sharedMaterials;
        if (System.Array.IndexOf(strayObjects, r.name) >= 0 || (toHide != null && System.Array.IndexOf(toHide, r.name) >= 0))
        {
            if (r.gameObject.activeSelf) { r.gameObject.SetActive(false); log.Append($" hid {r.name};"); }
            continue;
        }
        // CC5 hair cap: even transparent, its texture leaves a lighter band; the hair strands cover the scalp without it.
        if (mats.Length > 0 && System.Array.TrueForAll(mats, IsCap))
        {
            foreach (var m in mats) if (m.GetFloat("_Surface") != 1f) MakeTransparent(m, (int)UnityEngine.Rendering.RenderQueue.Transparent);
            if (r.gameObject.activeSelf) { r.gameObject.SetActive(false); log.Append($" hid hair cap ({r.name});"); }
            continue;
        }
        bool allBrowLayers = mats.Length > 0 && System.Array.TrueForAll(mats, IsBrowLayer);
        if (allBrowLayers && System.Array.TrueForAll(mats, m => Blank(m.GetTexture("_BaseMap"))) && r.gameObject.activeSelf)
        {
            r.gameObject.SetActive(false);
            log.Append($" hid unmasked brow layers ({r.name});");
            continue;
        }
        // CC4 scalp / brow-base layers: their texture's alpha is the mask.
        foreach (var m in mats)
        {
            if (!IsMaskedLayer(m) || m.GetFloat("_Surface") == 1f) continue;
            var tex = m.GetTexture("_BaseMap");
            if (!HasAlphaMask(tex)) continue;
            KeepAlpha(tex);
            MakeTransparent(m, UnderHairQueue);
            log.Append($" {m.name} see-through;");
        }
    }
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.Debug.Log(log.ToString());
return log.ToString();
