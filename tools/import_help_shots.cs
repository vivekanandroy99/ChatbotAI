// Edit mode, after tools/capture_help_shots.cs: import settings for the guide pictures in Assets/Resources/Help -
// high-quality compression (BC7: the menu text stays crisp), no mipmaps, up to 4096 px (the tallest are ~3500).
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return "HELP: in Play mode - not changed";
UnityEditor.AssetDatabase.Refresh();
var log = new System.Text.StringBuilder("HELP:");
foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Help" }))
{
    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    ti.textureType = UnityEditor.TextureImporterType.Default;
    ti.mipmapEnabled = false;
    ti.npotScale = UnityEditor.TextureImporterNPOTScale.None;
    ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    ti.maxTextureSize = 4096;
    ti.textureCompression = UnityEditor.TextureImporterCompression.CompressedHQ;
    ti.SaveAndReimport();
    var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(path);
    log.Append($"\n{System.IO.Path.GetFileName(path)} {tex.width}x{tex.height} {tex.format}");
}
return log.ToString();
