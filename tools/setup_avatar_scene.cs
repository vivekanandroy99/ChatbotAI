// Edit mode: sets up the avatar in the dialogue scene. Safe to re-run: it only ADDS what's missing
// and never overwrites your work.
//
// How the pieces fit (see CLAUDE.md "Avatar"):
//   Avatar Profile (persona, voice...)  --Character-->  Avatar Character asset
//       = prefab (model) + Avatar Animation Set (states/clips) + Lip Sync Tuning (mouth shapes)
//   Scene: "Avatar" = AvatarStage - spawns the character's prefab as a child and wires
//       AvatarAnimator / AvatarLipSync / AvatarBlink / AvatarEyes (on the stage) to it.
//
// This script, for the ActorCore party-f-0001 character:
//  - prepares the motion files (Humanoid, real take only, looping in place) if not done yet;
//  - creates the animation set, lip-sync tuning and character assets if missing;
//  - gives Avatar Profiles without a character this one;
//  - turns an older scene "Avatar" (the character itself with the components on it) into a stage,
//    keeping the components' settings; frames camera and light only for a brand-new avatar.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { UnityEngine.Debug.Log("SETUP: still in Play mode - not changed"); return null; }

const string characterModel = "Assets/ActorCore Model/Female Model/Actor/party-f-0001/party-f-0001.fbx";
const string characterPrefab = "Assets/ActorCore Model/Female Model/Actor/party-f-0001/Prefabs/party-f-0001.prefab";
const string animationSetPath = "Assets/ActorCore Model/Female Model/Avatar Animations.asset";
const string tuningPath = "Assets/ActorCore Model/Female Model/Party F Lip Sync.asset";
const string characterPath = "Assets/ActorCore Model/Female Model/Party F.asset";
const string scenePath = "Assets/Scenes/AltcoreBot_v5.unity";
// The voice's own profile if it's been calibrated (tools/calibrate_lipsync.cs), else uLipSync's sample.
const string calibratedProfile = "Assets/Avatar LipSync/uLipSync-Profile-veena_kavya.asset";
string lipSyncProfile = System.IO.File.Exists(calibratedProfile) ? calibratedProfile
    : "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Female.asset";
var motions = new (string state, string path)[]
{
    ("Idle", "Assets/ActorCore Model/Idle Motion/Motion/idle-378963.fbx"),
    ("Talk", "Assets/ActorCore Model/Stand Talk Motion/Motion/stand-talk-378997.fbx"),
};

UnityEngine.AnimationClip LongestClip(string path)
{
    UnityEngine.AnimationClip best = null;
    foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
        if (o is UnityEngine.AnimationClip c && (!best || c.length > best.length)) best = c;
    return best;
}

// 1. The starting motion files: Humanoid (bone map copied from the character), only the real take
//    (ActorCore files also carry a 0.02 s T-pose take), looping in place - only if not done yet.
var characterImporter = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(characterModel);
foreach (var (state, path) in motions)
{
    var importer = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(path);
    if (importer.animationType == UnityEditor.ModelImporterAnimationType.Human && importer.clipAnimations.Length == 1) continue;
    importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
    importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
    var human = importer.humanDescription;
    human.human = characterImporter.humanDescription.human;
    human.skeleton = new UnityEngine.SkeletonBone[0];
    importer.humanDescription = human;
    importer.SaveAndReimport();
    UnityEditor.TakeInfo take = importer.importedTakeInfos[0];
    foreach (var t in importer.importedTakeInfos)
        if (t.stopTime - t.startTime > take.stopTime - take.startTime) take = t;
    UnityEditor.ModelImporterClipAnimation keep = null;
    foreach (var c in importer.defaultClipAnimations)
        if (c.takeName == take.name) keep = c;
    keep.name = state;
    keep.loopTime = keep.loopPose = true;
    keep.lockRootRotation = keep.keepOriginalOrientation = true;
    keep.lockRootHeightY = keep.keepOriginalPositionY = true;
    keep.lockRootPositionXZ = keep.keepOriginalPositionXZ = true;
    importer.clipAnimations = new[] { keep };
    importer.SaveAndReimport();
    UnityEngine.Debug.Log($"SETUP: prepared {System.IO.Path.GetFileName(path)} ({state})");
}

// 2. The character's assets - each created once; after that they're yours to edit in the Inspector.
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(characterPrefab);
var set = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.AvatarAnimationSet>(animationSetPath);
if (!set)
{
    set = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Avatar.AvatarAnimationSet>();
    set.characterModel = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(characterModel);
    foreach (var (state, path) in motions) set.Find(state).clips.Add(LongestClip(path));
    UnityEditor.AssetDatabase.CreateAsset(set, animationSetPath);
    UnityEngine.Debug.Log("SETUP: created " + animationSetPath);
}
var tuning = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.AvatarLipSyncTuning>(tuningPath);
if (!tuning)
{
    tuning = ChatbotAI.Avatar.AvatarLipSyncTuning.CreateFor(ChatbotAI.Avatar.AvatarFace.FindFace(prefab).sharedMesh, out string family);
    UnityEditor.AssetDatabase.CreateAsset(tuning, tuningPath);
    UnityEngine.Debug.Log($"SETUP: created {tuningPath} (mapped from the face: {family})");
}
var character = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.AvatarCharacter>(characterPath);
if (!character)
{
    character = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Avatar.AvatarCharacter>();
    character.displayName = "Party F (ActorCore)";
    character.prefab = prefab;
    character.animations = set;
    character.lipSync = tuning;
    UnityEditor.AssetDatabase.CreateAsset(character, characterPath);
    UnityEngine.Debug.Log("SETUP: created " + characterPath);
}
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AvatarProfile"))
{
    var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
    if (profile.character) continue;
    profile.character = character;
    UnityEditor.EditorUtility.SetDirty(profile);
    UnityEngine.Debug.Log($"SETUP: {profile.name} appears as {character.displayName}");
}
UnityEditor.AssetDatabase.SaveAssets();

// 3. Scene.
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
bool wasOpen = scene.isLoaded;
if (!wasOpen) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEngine.GameObject speechGO = null, cameraGO = null, avatar = null, dialogueGO = null, inputGO = null;
foreach (var go in scene.GetRootGameObjects())
{
    if (go.name == "SpeechOutput") speechGO = go;
    if (go.name == "Main Camera") cameraGO = go;
    if (go.name == "Avatar") avatar = go;
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "SpeechInput") inputGO = go;
}
T Ensure<T>(UnityEngine.GameObject go) where T : UnityEngine.Component
{
    var c = go.GetComponent<T>();
    return c ? c : go.AddComponent<T>();
}
void SetRef(UnityEngine.Object component, string field, UnityEngine.Object value)
{
    var so = new UnityEditor.SerializedObject(component);
    var p = so.FindProperty(field);
    if (p != null && !p.objectReferenceValue) p.objectReferenceValue = value;  // only fills empty slots
    so.ApplyModifiedPropertiesWithoutUndo();
}

// Lip-sync hearing side: uLipSync on its own child object, fed ~70 ms ahead of the audible voice by
// LipSyncLookahead (listening to the live output, the mouth trailed the voice by 90-105 ms).
var speechOutput = speechGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
var oldLipSync = speechGO.GetComponent<uLipSync.uLipSync>();
if (oldLipSync) UnityEngine.Object.DestroyImmediate(oldLipSync);
var lipSyncTransform = speechGO.transform.Find("LipSync");
var lipSyncGO = lipSyncTransform ? lipSyncTransform.gameObject : new UnityEngine.GameObject("LipSync");
lipSyncGO.transform.SetParent(speechGO.transform, false);
var lipSync = Ensure<uLipSync.uLipSync>(lipSyncGO);
if (!lipSync.profile) lipSync.profile = UnityEditor.AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(lipSyncProfile);
if (lipSync.onLipSyncUpdate == null) lipSync.onLipSyncUpdate = new uLipSync.LipSyncUpdateEvent();  // AvatarLipSync listens in code
var lookahead = Ensure<ChatbotAI.Audio.LipSyncLookahead>(speechGO);
SetRef(lookahead, "speechOutput", speechOutput);
SetRef(lookahead, "lipSync", lipSync);

// The stage. An older "Avatar" was the character itself with the avatar components on it: its
// component settings move to a new stage, and the stage spawns the character fresh.
bool created = !avatar;
if (avatar && !avatar.GetComponent<ChatbotAI.Avatar.AvatarStage>())
{
    var stageGO = new UnityEngine.GameObject("Avatar");
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stageGO, scene);
    stageGO.transform.SetPositionAndRotation(avatar.transform.position, avatar.transform.rotation);
    void Move<T>() where T : UnityEngine.Component
    {
        var old = avatar.GetComponent<T>();
        if (old) UnityEditor.EditorJsonUtility.FromJsonOverwrite(UnityEditor.EditorJsonUtility.ToJson(old), Ensure<T>(stageGO));
    }
    Move<ChatbotAI.Avatar.AvatarAnimator>();
    Move<ChatbotAI.Avatar.AvatarBlink>();
    Move<ChatbotAI.Avatar.AvatarEyes>();
    UnityEngine.Object.DestroyImmediate(avatar);
    avatar = stageGO;
    UnityEngine.Debug.Log("SETUP: the scene's Avatar is now an Avatar Stage (component settings kept)");
}
else if (created)
{
    avatar = new UnityEngine.GameObject("Avatar");
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(avatar, scene);
}
var stage = Ensure<ChatbotAI.Avatar.AvatarStage>(avatar);
SetRef(stage, "character", character);
SetRef(stage, "dialogue", dialogueGO ? dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>() : null);
SetRef(stage, "fallbackAnimations", set);
SetRef(stage, "lipSyncSource", lipSync);
SetRef(stage, "cameraToFrame", cameraGO ? cameraGO.GetComponent<UnityEngine.Camera>() : null);
var body = Ensure<ChatbotAI.Avatar.AvatarAnimator>(avatar);
SetRef(body, "speechOutput", speechOutput);
SetRef(body, "speechInput", inputGO ? inputGO.GetComponent<ChatbotAI.Audio.SpeechInputController>() : null);
SetRef(body, "dialogue", dialogueGO ? dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>() : null);
if (!stage.Model) stage.Show(stage.Character);  // spawns the model and wires everything (frames the camera)

// Testing aid: every spoken sentence saved for review, F8 flags a reply that sounded wrong.
SetRef(Ensure<ChatbotAI.Audio.SpeechReviewRecorder>(speechGO), "speechOutput", speechOutput);

if (created)
{
    // Key light from the front, above and a little to the side of the face.
    var forward = -cameraGO.transform.forward;
    forward.y = 0;
    forward.Normalize();
    foreach (var go in scene.GetRootGameObjects())
    {
        var light = go.GetComponent<UnityEngine.Light>();
        if (light && light.type == UnityEngine.LightType.Directional)
        {
            var side = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, forward);
            go.transform.rotation = UnityEngine.Quaternion.LookRotation((-forward + UnityEngine.Vector3.down * 0.6f + side * 0.35f).normalized);
        }
    }
    var cam = cameraGO.GetComponent<UnityEngine.Camera>();
    cam.fieldOfView = 30f;
    cam.nearClipPlane = 0.05f;
    UnityEngine.Debug.Log("SETUP: avatar added, camera and light framed");
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
if (!wasOpen) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
UnityEngine.Debug.Log("SETUP: scene saved");
return null;
