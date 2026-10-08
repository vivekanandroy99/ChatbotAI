// Edit mode: per-voice lip-sync profiles in the scene (LipSyncVoiceProfiles on SpeechOutput/LipSync,
// listing every calibrated uLipSync-Profile-<voice>.asset, Kavya's as the fallback), and the jaw bone
// for lip-sync tunings of CC3+/CC4/CC5 faces made before that setting existed. Safe to re-run.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { UnityEngine.Debug.Log("LIPVOICES: in Play mode - not changed"); return null; }
const string scenePath = "Assets/Scenes/AltcoreBot_v5.unity";
const string fallbackPath = "Assets/Avatar LipSync/uLipSync-Profile-veena_kavya.asset";
var log = new System.Text.StringBuilder("LIPVOICES:");

// Tunings for CC3+ faces (Jaw_Open, not ActorScan's A25_Jaw_Open) that still have the jaw bone off.
foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AvatarCharacter"))
{
    var c = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.AvatarCharacter>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
    if (!c || !c.prefab || !c.lipSync || c.lipSync.jawBoneDegrees > 0f) continue;
    var face = ChatbotAI.Avatar.AvatarFace.FindFace(c.prefab);
    if (!face) continue;
    var m = face.sharedMesh;
    if (ChatbotAI.Avatar.AvatarFace.IndexOf(m, "Jaw_Open") >= 0 && ChatbotAI.Avatar.AvatarFace.IndexOf(m, "A25_Jaw_Open") < 0
        && ChatbotAI.Avatar.AvatarLipSync.FindJawBone(c.prefab))
    {
        c.lipSync.jawBoneDegrees = 14f;
        UnityEditor.EditorUtility.SetDirty(c.lipSync);
        log.Append($" {c.lipSync.name}: jaw bone 14 deg;");
    }
}
UnityEditor.AssetDatabase.SaveAssets();

var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
bool wasOpen = scene.isLoaded;
if (!wasOpen) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEngine.GameObject speechGO = null;
foreach (var go in scene.GetRootGameObjects()) if (go.name == "SpeechOutput") speechGO = go;
var lipSync = speechGO.GetComponentInChildren<uLipSync.uLipSync>(true);
var voices = lipSync.GetComponent<ChatbotAI.Audio.LipSyncVoiceProfiles>();
if (!voices) voices = lipSync.gameObject.AddComponent<ChatbotAI.Audio.LipSyncVoiceProfiles>();
voices.Configure(speechGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>(), lipSync,
    UnityEditor.AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(fallbackPath));
int n = ChatbotAI.EditorTools.LipSyncVoiceProfilesEditor.Fill(voices);
log.Append($" {n} voice profiles: {string.Join(", ", voices.profiles.ConvertAll(e => e.voice))};");
UnityEditor.EditorUtility.SetDirty(voices);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
if (!wasOpen) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
UnityEngine.Debug.Log(log.ToString());
return log.ToString();
