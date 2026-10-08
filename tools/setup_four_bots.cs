// Edit mode: the four bots from the CC4 actors in Assets/ActorCore Model (built by tools/import_actors.cs, cleaned by
// tools/fix_cc5_materials.cs):
//   Altcore Female - Maya   (knowledge only, Altscape/Altcore documents)   af_heart / veena_kavya
//   Altcore Male   - Ethan  (knowledge only, same documents)               am_michael / veena_vinaya
//   Chat Female    - Iris   (open chat, her own personality)              af_bella / hf_alpha (Kokoro - Veena Maitri was the least clear Hindi voice)
//   Chat Male      - Leo    (open chat, his own personality)              am_fenrir / veena_vinaya
// Characters, motion sets and lip-sync tunings go to Assets/Characters (outside the model folder, so models can be
// swapped without losing them). The three older profiles (AltBot, Kevin, Kevin Open Chat) become Maya, Ethan and Leo;
// Iris is new. Safe to re-run.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { UnityEngine.Debug.Log("BOTS: in Play mode - not changed"); return null; }
const string actors = "Assets/ActorCore Model/Actors";
const string motion = "Assets/ActorCore Model/Motion";
const string outDir = "Assets/Characters";
const string avatars = "Assets/Resources/Avatars";
const string scenePath = "Assets/Scenes/AltcoreBot_v5.unity";
var log = new System.Text.StringBuilder("BOTS:");
if (!UnityEditor.AssetDatabase.IsValidFolder(outDir)) UnityEditor.AssetDatabase.CreateFolder("Assets", "Characters");

T Load<T>(string path) where T : UnityEngine.Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);

UnityEngine.AnimationClip Longest(string path)
{
    UnityEngine.AnimationClip best = null;
    foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
        if (o is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview__") && (!best || c.length > best.length)) best = c;
    return best;
}

// ---- Motion sets: one per body type, shared by both characters of that type ----
// Rebuilt from the motion files on every run, so re-exported files are picked up. A file holding only CC's
// "Calibration" take (a T-pose + range-of-motion test, what CC exports when no motion is applied) isn't a real
// motion: the state gets the stand-in from tools/make_placeholder_motions.cs until a real one is exported.
var placeholderIdle = Load<UnityEngine.AnimationClip>($"{outDir}/Placeholder Idle.anim");
var placeholderTalk = Load<UnityEngine.AnimationClip>($"{outDir}/Placeholder Talk.anim");
ChatbotAI.Avatar.AvatarAnimationSet MotionSet(string name, string modelPath,
    System.Collections.Generic.List<string> idles, System.Collections.Generic.List<string> talks)
{
    string path = $"{outDir}/{name}.asset";
    var set = Load<ChatbotAI.Avatar.AvatarAnimationSet>(path);
    if (!set)
    {
        set = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Avatar.AvatarAnimationSet>();
        UnityEditor.AssetDatabase.CreateAsset(set, path);
    }
    set.characterModel = Load<UnityEngine.GameObject>(modelPath);
    var idleState = set.Find(ChatbotAI.Avatar.AvatarAnimationSet.Idle);
    var talkState = set.Find(ChatbotAI.Avatar.AvatarAnimationSet.Talk);
    idleState.clips.Clear();
    talkState.clips.Clear();
    foreach (string idle in idles) if (Longest(idle)) idleState.clips.Add(Longest(idle));
    foreach (string talk in talks) if (Longest(talk)) talkState.clips.Add(Longest(talk));
    // Humanoid with the model's bone map, real take only, looping in place.
    ChatbotAI.EditorTools.AvatarAnimationSetEditor.Prepare(set);
    int calibration = idleState.clips.RemoveAll(c => !c || c.name == "Calibration") + talkState.clips.RemoveAll(c => !c || c.name == "Calibration");
    if (idleState.clips.Count == 0 && placeholderIdle) idleState.clips.Add(placeholderIdle);
    if (talkState.clips.Count == 0 && placeholderTalk) talkState.clips.Add(placeholderTalk);
    // The stand-ins have no foot IK goals (made from muscle values); real motion files do.
    idleState.footIK = !idleState.clips.Contains(placeholderIdle);
    talkState.footIK = !talkState.clips.Contains(placeholderTalk);
    UnityEditor.EditorUtility.SetDirty(set);
    var idleNames = idleState.clips.ConvertAll(c => c ? $"{c.name} {c.length:0.0}s" : "none");
    var talkNames = talkState.clips.ConvertAll(c => c ? $"{c.name} {c.length:0.0}s" : "none");
    log.Append($"\n{name}: idle {string.Join(", ", idleNames)}; talk {string.Join(", ", talkNames)}" +
               (calibration > 0 ? $" ({calibration} file(s) held only CC's Calibration motion - skipped)" : ""));
    return set;
}
// Motion files are found by their folder's name under Motion/: "Idle..." or "Talk...", "...Male" or "...Female"
// (e.g. Idle2_Female/, Talk1_Male/) - drop a new export into such a folder and re-run.
var found = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
foreach (string key in new[] { "male idle", "male talk", "female idle", "female talk" }) found[key] = new System.Collections.Generic.List<string>();
foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { motion }))
{
    string file = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(file)).ToLowerInvariant();
    string body = folder.Contains("female") ? "female" : folder.Contains("male") ? "male" : null;
    string kind = folder.Contains("talk") ? "talk" : folder.Contains("idle") ? "idle" : null;
    if (body == null || kind == null) { log.Append($"\n(skipped {file}: folder name says neither male/female or idle/talk)"); continue; }
    found[$"{body} {kind}"].Add(file);
}
foreach (var list in found.Values) list.Sort();
var male = MotionSet("Male Motions", $"{actors}/Ethan/Ethan.Fbx", found["male idle"], found["male talk"]);
var female = MotionSet("Female Motions", $"{actors}/Iris/Iris.Fbx", found["female idle"], found["female talk"]);

// ---- Characters: model + motion set + lip-sync tuning ----
ChatbotAI.Avatar.AvatarCharacter Character(string name, ChatbotAI.Avatar.AvatarAnimationSet set)
{
    string path = $"{outDir}/{name}.asset";
    var prefab = Load<UnityEngine.GameObject>($"{actors}/{name}/Prefabs/{name}.prefab");
    string tuningPath = $"{outDir}/{name} Lip Sync.asset";
    var tuning = Load<ChatbotAI.Avatar.AvatarLipSyncTuning>(tuningPath);
    if (!tuning)
    {
        var face = ChatbotAI.Avatar.AvatarFace.FindFace(prefab);
        tuning = ChatbotAI.Avatar.AvatarLipSyncTuning.CreateFor(face.sharedMesh, out string family);
        tuning.jawStrength = 0.8f;   // as tuned on Kevin (CC5): jaw bone 14 deg x 0.8
        UnityEditor.AssetDatabase.CreateAsset(tuning, tuningPath);
        log.Append($"\n{name}: lip-sync from {face.name} ({family}, {tuning.sounds.Count} sounds, jaw bone {tuning.jawBoneDegrees} deg)");
    }
    var c = Load<ChatbotAI.Avatar.AvatarCharacter>(path);
    if (!c)
    {
        c = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Avatar.AvatarCharacter>();
        UnityEditor.AssetDatabase.CreateAsset(c, path);
        log.Append($"\n{name}: character created");
    }
    c.displayName = name;
    // These CC4 faces rest with the lids wide open (a stare, like Kevin's). They have no Eye_Relax shape, so the blink
    // shape rests part-closed: 0.65 -> ~24% blink looked natural (15% still stared, 32% sleepy). A value tuned by hand stays.
    if (c.restingEyelids == 0f || c.restingEyelids == 0.4f) c.restingEyelids = 0.65f;
    c.prefab = prefab;
    c.animations = set;
    c.lipSync = tuning;
    UnityEditor.EditorUtility.SetDirty(c);
    return c;
}
var maya = Character("Maya", female);
var iris = Character("Iris", female);
var ethan = Character("Ethan", male);
var leo = Character("Leo", male);

// ---- Profiles ----
ChatbotAI.Dialogue.AvatarProfile Profile(string file, string oldFile, string copyFrom)
{
    string path = $"{avatars}/{file}.asset";
    var p = Load<ChatbotAI.Dialogue.AvatarProfile>(path);
    if (p) return p;
    string old = oldFile != null ? $"{avatars}/{oldFile}.asset" : null;
    if (old != null && Load<ChatbotAI.Dialogue.AvatarProfile>(old))
    {
        string err = UnityEditor.AssetDatabase.MoveAsset(old, path);
        if (!string.IsNullOrEmpty(err)) throw new System.Exception(err);
        log.Append($"\n{oldFile} -> {file}");
        return Load<ChatbotAI.Dialogue.AvatarProfile>(path);
    }
    var source = Load<ChatbotAI.Dialogue.AvatarProfile>($"{avatars}/{copyFrom}.asset");
    p = source ? UnityEngine.Object.Instantiate(source) : UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Dialogue.AvatarProfile>();
    UnityEditor.AssetDatabase.CreateAsset(p, path);
    log.Append($"\n{file} created");
    return p;
}
var pMaya = Profile("Altcore Female - Maya", "DefaultAvatar", null);
var pEthan = Profile("Altcore Male - Ethan", "Kevin", null);
var pLeo = Profile("Chat Male - Leo", "Kevin Open Chat", null);
var pIris = Profile("Chat Female - Iris", null, "Chat Male - Leo");

void Set(ChatbotAI.Dialogue.AvatarProfile p, string id, string name, ChatbotAI.Avatar.AvatarCharacter character,
         ChatbotAI.Dialogue.AvatarProfile.ConversationMode mode, string english, string hindi, string persona)
{
    p.avatarId = id;
    p.displayName = name;
    p.character = character;
    p.mode = mode;
    p.englishVoice = english;
    p.hindiVoice = hindi;
    p.speechSpeed = 1f;
    p.personaPrompt = persona;
    UnityEditor.EditorUtility.SetDirty(p);
}
const string altcorePersona =
    "You are {0}, the friendly front-desk assistant for Altscape and Altcore. You are warm, confident and knowledgeable, " +
    "with a light touch of wit, and you talk like a helpful expert rather than a brochure. Keep answers short and " +
    "conversational, as if spoken out loud.";
var knowledge = ChatbotAI.Dialogue.AvatarProfile.ConversationMode.KnowledgeOnly;
var chat = ChatbotAI.Dialogue.AvatarProfile.ConversationMode.OpenChat;
Set(pMaya, "altcore_female", "Maya", maya, knowledge, "af_heart", "veena_kavya", string.Format(altcorePersona, "Maya"));
Set(pEthan, "altcore_male", "Ethan", ethan, knowledge, "am_michael", "veena_vinaya", string.Format(altcorePersona, "Ethan"));
Set(pLeo, "chat_male", "Leo", leo, chat, "am_fenrir", "veena_vinaya",
    "You are Leo, a friendly, curious and quick-witted companion in his forties. You love a good conversation about " +
    "almost anything - science and space, films and music, food and cooking, travel, cricket and football, technology, " +
    "books, and the small puzzles of everyday life. You have real opinions and share them with a light, dry sense of " +
    "humour, but you listen, you're kind, and you never lecture. You like a good story and a clever analogy. You're " +
    "honest when you don't know something, and you admit when you're guessing.");
Set(pIris, "chat_female", "Iris", iris, chat, "af_bella", "hf_alpha",
    "You are Iris, a warm, upbeat and curious companion in her early thirties. You love talking about art and design, " +
    "music, films and books, travel and food, wellness, and the latest ideas in science and technology. You're playful " +
    "and encouraging, with a bright sense of humour, and you're genuinely interested in people - you ask thoughtful " +
    "questions back. You give honest, practical opinions without preaching. You're honest when you don't know " +
    "something, and you admit when you're guessing.");
// The two knowledge bots answer from the same Altscape/Altcore documents; the chat bots have none.
pEthan.knowledgeDocuments = new System.Collections.Generic.List<UnityEngine.Object>(pMaya.knowledgeDocuments);
pEthan.topicNames = new System.Collections.Generic.List<string>(pMaya.topicNames);
pLeo.knowledgeDocuments.Clear();
pIris.knowledgeDocuments.Clear();
UnityEditor.AssetDatabase.SaveAssets();

// Knowledge copies per avatar id; the old ids' copies go (the sidecar would still load and embed them).
foreach (var p in new[] { pMaya, pEthan, pLeo, pIris }) ChatbotAI.EditorTools.KnowledgeSync.Sync(p);
foreach (string oldId in new[] { "default", "kevin", "kevin_chat" })
{
    string folder = $"Assets/StreamingAssets/Knowledge/{oldId}";
    if (UnityEditor.AssetDatabase.IsValidFolder(folder)) { UnityEditor.AssetDatabase.DeleteAsset(folder); log.Append($"\nremoved old knowledge copy {oldId}"); }
    UnityEngine.PlayerPrefs.DeleteKey("avatar-settings/" + oldId);   // in-app changes saved under the old ids
}
UnityEngine.PlayerPrefs.Save();

// The old Kevin character asset: its model and motions were deleted with the old folder.
if (Load<ChatbotAI.Avatar.AvatarCharacter>("Assets/New Character.asset") is var oldKevin && oldKevin && !oldKevin.prefab)
{
    UnityEditor.AssetDatabase.DeleteAsset("Assets/New Character.asset");
    log.Append("\nremoved Assets/New Character.asset (its model was deleted)");
}

// ---- Scene: registry order, stage, lip-sync profiles for the new voices ----
// The scene as it's open in the Editor (reopening it would drop unsaved changes); opened only if it isn't.
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
if (!scene.isLoaded) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
foreach (var go in scene.GetRootGameObjects())
{
    foreach (var registry in go.GetComponentsInChildren<ChatbotAI.Dialogue.AvatarRegistry>(true))
    {
        var so = new UnityEditor.SerializedObject(registry);
        var list = so.FindProperty("profiles");
        var order = new[] { pMaya, pEthan, pIris, pLeo };
        list.arraySize = order.Length;
        for (int i = 0; i < order.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
        so.FindProperty("activeIndex").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Append("\nregistry: Maya, Ethan, Iris, Leo");
    }
    foreach (var stage in go.GetComponentsInChildren<ChatbotAI.Avatar.AvatarStage>(true))
    {
        var so = new UnityEditor.SerializedObject(stage);
        so.FindProperty("fallbackAnimations").objectReferenceValue = female;
        so.ApplyModifiedPropertiesWithoutUndo();
        ChatbotAI.EditorTools.AvatarStageEditor.ShowInScene(stage, maya);
        stage.FrameCamera();
        log.Append("\nstage shows Maya");
    }
    foreach (var voices in go.GetComponentsInChildren<ChatbotAI.Audio.LipSyncVoiceProfiles>(true))
    {
        // Calibrated per voice where a profile exists; the new voices borrow the closest calibrated one.
        const string dir = "Assets/Avatar LipSync";
        var borrow = new (string voice, string from)[] { ("am_fenrir", "am_michael"), ("af_bella", "veena_kavya"), ("af_heart", "veena_kavya"), ("veena_maitri", "veena_kavya"), ("hf_alpha", "veena_kavya") };
        foreach (var (voice, from) in borrow)
        {
            if (voices.profiles.Exists(e => e.voice == voice)) continue;
            var profile = Load<uLipSync.Profile>($"{dir}/uLipSync-Profile-{from}.asset");
            if (!profile) continue;
            voices.profiles.Add(new ChatbotAI.Audio.LipSyncVoiceProfiles.Entry { voice = voice, profile = profile });
            log.Append($"\nlip-sync: {voice} uses {from}'s profile");
        }
        UnityEditor.EditorUtility.SetDirty(voices);
    }
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.Debug.Log(log.ToString());
return log.ToString();
