// Edit mode: builds CC4/CC5 actor exports with the CC/iC Unity Tools importer (same as its window's Build button in batch
// mode: high-quality materials, Humanoid rig), giving each a prefab under <actor folder>/Prefabs.
// Set `names` to the actors to build (folders under Assets/ActorCore Model/Actors). Safe to re-run (rebuilds).
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return "IMPORT: in Play mode - not changed";
string[] names = { "Pearl", "Peter" };   // earlier runs: Ethan, Iris, Leo, Maya
const string root = "Assets/ActorCore Model/Actors";
var log = new System.Text.StringBuilder("IMPORT:");
foreach (string name in names)
{
    string fbxPath = $"{root}/{name}/{name}.Fbx";
    string guid = UnityEditor.AssetDatabase.AssetPathToGUID(fbxPath);
    if (string.IsNullOrEmpty(guid)) { log.Append($"\n{name}: no {fbxPath}"); continue; }
    var t0 = System.DateTime.Now;
    var info = new Reallusion.Import.CharacterInfo(guid);
    info.CheckGeneration();
    info.BuildQuality = info.CanHaveHighQualityMaterials ? Reallusion.Import.MaterialQuality.High : Reallusion.Import.MaterialQuality.Default;
    info.Refresh();
    var prefab = new Reallusion.Import.Importer(info).Import(true);
    info.Write();
    info.Release();
    log.Append($"\n{name}: {(prefab ? UnityEditor.AssetDatabase.GetAssetPath(prefab) : "no prefab")} ({(System.DateTime.Now - t0).TotalSeconds:0} s)");
}
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
