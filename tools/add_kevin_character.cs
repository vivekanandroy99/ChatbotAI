// Edit mode: adds Kevin (CC5, ActorCore) as an Avatar Character (model + animation set + lip-sync
// tuning). Safe to re-run (only creates what's missing; completes a character already made for his
// prefab). Run tools/fix_cc5_materials.cs first for his brows / hair cap / stray camera. Copy this
// for the next character: change the paths and clips.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { UnityEngine.Debug.Log("KEVIN: in Play mode - not changed"); return null; }

const string folder = "Assets/ActorCore Model/CharKevinCC5";
const string model = folder + "/kevinChar.Fbx";
const string prefabPath = folder + "/Prefabs/kevinChar.prefab";
const string setPath = folder + "/Kevin Animations.asset";
const string tuningPath = folder + "/Kevin Lip Sync.asset";
const string characterPath = folder + "/Kevin.asset";
var clips = new (string state, string path)[]
{
    ("Idle", "Assets/ActorCore Model/IdleMale/Motion/male-idle_279398.fbx"),
    ("Talk", "Assets/ActorCore Model/Stand Talk Motion/Motion/stand-talk-378997.fbx"),
};
var log = new System.Text.StringBuilder("KEVIN:");

UnityEngine.AnimationClip Longest(string path)
{
    UnityEngine.AnimationClip best = null;
    foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
        if (o is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview__") && (!best || c.length > best.length)) best = c;
    return best;
}

var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
var set = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.AvatarAnimationSet>(setPath);
if (!set)
{
    set = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Avatar.AvatarAnimationSet>();
    set.characterModel = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(model);
    foreach (var (state, path) in clips) set.Find(state).clips.Add(Longest(path));
    UnityEditor.AssetDatabase.CreateAsset(set, setPath);
    // Humanoid with Kevin's bone map, real take only, looping in place (files already prepared are left alone).
    ChatbotAI.EditorTools.AvatarAnimationSetEditor.Prepare(set);
    log.Append($" created {setPath} (Idle {set.Find("Idle").clips[0]?.name}, Talk {set.Find("Talk").clips[0]?.name});");
}
var tuning = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.AvatarLipSyncTuning>(tuningPath);
if (!tuning)
{
    var face = ChatbotAI.Avatar.AvatarFace.FindFace(prefab);
    tuning = ChatbotAI.Avatar.AvatarLipSyncTuning.CreateFor(face.sharedMesh, out string family);
    UnityEditor.AssetDatabase.CreateAsset(tuning, tuningPath);
    log.Append($" created {tuningPath} from {face.name} ({family}, {tuning.sounds.Count} sounds);");
}
// A character already made for this prefab (e.g. by hand in the Inspector) is completed, not duplicated.
ChatbotAI.Avatar.AvatarCharacter character = null;
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AvatarCharacter"))
{
    var c = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.AvatarCharacter>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
    if (c && c.prefab == prefab) character = c;
}
if (character)
{
    if (!character.animations) { character.animations = set; log.Append($" gave {character.name} the animation set;"); }
    if (!character.lipSync) { character.lipSync = tuning; log.Append($" gave {character.name} the lip-sync tuning;"); }
    UnityEditor.EditorUtility.SetDirty(character);
}
else
{
    character = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Avatar.AvatarCharacter>();
    character.displayName = "Kevin (CC5)";
    character.prefab = prefab;
    character.animations = set;
    character.lipSync = tuning;
    UnityEditor.AssetDatabase.CreateAsset(character, characterPath);
    log.Append($" created {characterPath};");
}
UnityEditor.AssetDatabase.SaveAssets();

// The avatar using this character and the in-app picker: tools/setup_avatars.cs.
UnityEngine.Debug.Log(log.ToString());
return log.ToString();
