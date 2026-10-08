// Edit mode: two avatars to switch between in the app, and the in-app avatar picker.
//   AltBot (DefaultAvatar) - Party F character, female voices (as set on it).
//   Kevin  (Kevin)         - Kevin character, male voices; a copy of AltBot otherwise (same documents,
//                            topics, refusal lines), persona renamed. Edit it in its Inspector.
// Safe to re-run: existing avatars are only given a character if they have none... except that AltBot
// is moved back to Party F when it shows Kevin (Kevin has his own avatar now).
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { UnityEngine.Debug.Log("AVATARS: in Play mode - not changed"); return null; }

const string defaultPath = "Assets/Resources/Avatars/DefaultAvatar.asset";
const string kevinPath = "Assets/Resources/Avatars/Kevin.asset";
const string partyFPath = "Assets/ActorCore Model/Female Model/Party F.asset";
const string scenePath = "Assets/Scenes/AltcoreBot_v5.unity";
var log = new System.Text.StringBuilder("AVATARS:");

var partyF = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.AvatarCharacter>(partyFPath);
var kevinPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/ActorCore Model/CharKevinCC5/Prefabs/kevinChar.prefab");
ChatbotAI.Avatar.AvatarCharacter kevinCharacter = null;
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AvatarCharacter"))
{
    var c = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.AvatarCharacter>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
    if (c && c.prefab == kevinPrefab) kevinCharacter = c;
}

var altBot = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>(defaultPath);
if (!altBot.character || altBot.character == kevinCharacter)
{
    altBot.character = partyF;
    UnityEditor.EditorUtility.SetDirty(altBot);
    log.Append($" {altBot.displayName} appears as {partyF.displayName};");
}

var kevin = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>(kevinPath);
if (!kevin)
{
    UnityEditor.AssetDatabase.CopyAsset(defaultPath, kevinPath);
    kevin = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>(kevinPath);
    kevin.avatarId = "kevin";
    kevin.displayName = "Kevin";
    kevin.personaPrompt = kevin.personaPrompt.Replace(altBot.displayName, "Kevin");
    kevin.englishVoice = "am_michael";   // Kokoro, American male, good
    kevin.hindiVoice = "veena_vinaya";   // Veena male (has a pitch-checked anchor); hm_omega if it misbehaves
    kevin.character = kevinCharacter;
    UnityEditor.EditorUtility.SetDirty(kevin);
    log.Append($" created {kevinPath} (voices {kevin.englishVoice} / {kevin.hindiVoice}, character {(kevinCharacter ? kevinCharacter.displayName : "none")});");
}
UnityEditor.AssetDatabase.SaveAssets();
// Its own copy of the documents for the knowledge service (StreamingAssets/Knowledge/kevin).
ChatbotAI.EditorTools.KnowledgeSync.Sync(kevin);

// Scene: the avatar picker (replaces the earlier model-only picker); the registry lists both.
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
bool wasOpen = scene.isLoaded;
if (!wasOpen) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
ChatbotAI.Dialogue.AvatarRegistry registry = null;
UnityEngine.GameObject pickerGO = null;
foreach (var go in scene.GetRootGameObjects())
{
    if (!registry) registry = go.GetComponentInChildren<ChatbotAI.Dialogue.AvatarRegistry>(true);
    if (go.name == "Character Picker UI") { UnityEngine.Object.DestroyImmediate(go); log.Append(" removed the model-only picker;"); continue; }
    if (go.name == "Avatar Picker UI") pickerGO = go;
}
var rso = new UnityEditor.SerializedObject(registry);
var list = rso.FindProperty("profiles");
if (list.arraySize > 0)
{
    bool has = false;
    for (int i = 0; i < list.arraySize; i++) has |= list.GetArrayElementAtIndex(i).objectReferenceValue == kevin;
    if (!has)
    {
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = kevin;
        rso.ApplyModifiedPropertiesWithoutUndo();
        log.Append(" registry lists Kevin;");
    }
}
else log.Append(" registry loads every avatar in Resources/Avatars;");
if (!pickerGO)
{
    pickerGO = new UnityEngine.GameObject("Avatar Picker UI");
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pickerGO, scene);
    var picker = pickerGO.AddComponent<ChatbotAI.UI.AvatarPickerUI>();
    var so = new UnityEditor.SerializedObject(picker);
    so.FindProperty("registry").objectReferenceValue = registry;
    so.ApplyModifiedPropertiesWithoutUndo();
    log.Append(" added the avatar picker;");
}
// The scene shows the first avatar's character in edit mode.
var stage = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Avatar.AvatarStage>();
if (stage && stage.Shown != altBot.character) { stage.Show(altBot.character); log.Append($" stage shows {altBot.character.displayName};"); }
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
if (!wasOpen) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
UnityEngine.Debug.Log(log.ToString());
return log.ToString();
