// Edit mode: the two P&I (Pavilions & Interiors) bots, from the CC4 actors Pearl and Peter (built by
// tools/import_actors.cs, cleaned by tools/fix_cc5_materials.cs):
//   P&I Female - Pearl  (knowledge only, the P&I document)   af_heart / veena_kavya
//   P&I Male   - Peter  (knowledge only, same document)      am_michael / veena_vinaya
// Settings not set here (gate thresholds, refusal lines, style, stage) are copied from Maya; the characters' camera
// framing and eyelids from Maya / Ethan. Visitors may say P&I as "PNI", "P and I"...: AvatarProfile.heardAs.
// Safe to re-run (values set here are set again; a hand-tuned character keeps its framing).
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { UnityEngine.Debug.Log("PNI: in Play mode - not changed"); return null; }
const string actors = "Assets/ActorCore Model/Actors";
const string outDir = "Assets/Characters";
const string avatars = "Assets/Resources/Avatars";
const string scenePath = "Assets/Scenes/AltcoreBot_v5.unity";
// The owner's P&I document minus its chatbot-team sections - made by tools/make_pni_document.py.
const string document = "Assets/KnowledgeDocuments/pni_female/Pavilions and Interiors Knowledge Base.docx";
var log = new System.Text.StringBuilder("PNI:");
T Load<T>(string path) where T : UnityEngine.Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);

// ---- Characters: model + the shared motion set + its own lip-sync tuning ----
ChatbotAI.Avatar.AvatarCharacter Character(string name, string likeName, string motions)
{
    string path = $"{outDir}/{name}.asset";
    var prefab = Load<UnityEngine.GameObject>($"{actors}/{name}/Prefabs/{name}.prefab");
    if (!prefab) throw new System.Exception($"no prefab for {name} - run tools/import_actors.cs first");
    string tuningPath = $"{outDir}/{name} Lip Sync.asset";
    var tuning = Load<ChatbotAI.Avatar.AvatarLipSyncTuning>(tuningPath);
    if (!tuning)
    {
        var face = ChatbotAI.Avatar.AvatarFace.FindFace(prefab);
        tuning = ChatbotAI.Avatar.AvatarLipSyncTuning.CreateFor(face.sharedMesh, out string family);
        tuning.jawStrength = 0.8f;
        UnityEditor.AssetDatabase.CreateAsset(tuning, tuningPath);
        log.Append($"\n{name}: lip-sync from {face.name} ({family}, {tuning.sounds.Count} sounds, jaw bone {tuning.jawBoneDegrees} deg)");
    }
    var c = Load<ChatbotAI.Avatar.AvatarCharacter>(path);
    if (!c)
    {
        // Same body type as an existing character: start from its camera framing, eyelids etc.
        c = UnityEngine.Object.Instantiate(Load<ChatbotAI.Avatar.AvatarCharacter>($"{outDir}/{likeName}.asset"));
        UnityEditor.AssetDatabase.CreateAsset(c, path);
        log.Append($"\n{name}: character created (framing from {likeName})");
    }
    c.name = name;
    c.displayName = name;
    c.prefab = prefab;
    c.animations = Load<ChatbotAI.Avatar.AvatarAnimationSet>($"{outDir}/{motions}.asset");
    c.lipSync = tuning;
    UnityEditor.EditorUtility.SetDirty(c);
    return c;
}
var pearl = Character("Pearl", "Maya", "Female Motions");
var peter = Character("Peter", "Ethan", "Male Motions");

// ---- Profiles ----
var maya = Load<ChatbotAI.Dialogue.AvatarProfile>($"{avatars}/Altcore Female - Maya.asset");
ChatbotAI.Dialogue.AvatarProfile Profile(string file)
{
    string path = $"{avatars}/{file}.asset";
    var p = Load<ChatbotAI.Dialogue.AvatarProfile>(path);
    if (p) return p;
    p = UnityEngine.Object.Instantiate(maya);
    UnityEditor.AssetDatabase.CreateAsset(p, path);
    log.Append($"\n{file} created (from Maya)");
    return p;
}
var pPearl = Profile("PNI Female - Pearl");
var pPeter = Profile("PNI Male - Peter");

const string persona =
    "You are {0}, the friendly front-desk assistant for P&I - Pavilions & Interiors, an Indian design and execution " +
    "company for exhibitions, events, museums, interiors and sports. You are warm, clear and professional, and you talk " +
    "like a helpful expert rather than a brochure. Keep answers short and conversational, as if spoken out loud.";
var docAsset = Load<UnityEngine.Object>(document);
if (!docAsset) throw new System.Exception("missing " + document);

ChatbotAI.Dialogue.NameVariants Variants(string name, params string[] variants) =>
    new ChatbotAI.Dialogue.NameVariants { name = name, variants = new System.Collections.Generic.List<string>(variants) };

void Set(ChatbotAI.Dialogue.AvatarProfile p, string id, string name, ChatbotAI.Avatar.AvatarCharacter character, string english, string hindi)
{
    p.avatarId = id;
    p.displayName = name;
    p.character = character;
    p.mode = ChatbotAI.Dialogue.AvatarProfile.ConversationMode.KnowledgeOnly;
    p.englishVoice = english;
    p.hindiVoice = hindi;
    p.personaPrompt = string.Format(persona, name);
    // The document's own rules for the assistant (its section 2), in the bot's style fields.
    p.alwaysDo = "answer the question first, then offer one useful follow-up; for a project enquiry, give the published " +
                 "contact route and offer to help put together a short brief";
    p.neverDo = "invent prices, timelines, staff details, project scope or client relationships; claim that a message was " +
                "sent or a meeting booked; describe a logo or portfolio listing as more than it shows";
    p.knowledgeDocuments = new System.Collections.Generic.List<UnityEngine.Object> { docAsset };
    p.topicNames = new System.Collections.Generic.List<string> { "P&I" };
    // The search models don't read "P&I" as Pavilions & Interiors: searching "the company" instead found the right
    // passage among the 6 in 25 of 26 test questions (17 with P&I); the reranker didn't help here (23).
    p.searchWithoutTopicNames = true;
    p.rerankPassages = false;
    p.pronunciations = new System.Collections.Generic.List<ChatbotAI.Dialogue.Pronunciation>
    {
        new ChatbotAI.Dialogue.Pronunciation { word = "P&I", sayAs = "P and I", sayAsInHindi = "पी एंड आई" },
    };
    // What speech recognition writes when visitors say "P and I" (or "PNI"), in English and Hindi. Not "pani" or "Pandi":
    // real words (पानी = water in Hinglish).
    p.heardAs = new System.Collections.Generic.List<ChatbotAI.Dialogue.NameVariants>
    {
        Variants("P&I",
            "PNI", "PNA", "PNY", "P and I", "P n I", "P & I", "P and Eye", "P and Y", "Pee and eye", "Pee and I",
            "Pandai", "Pee-and-eye", "P.N.I", "PI and I", "BNI", "B and I",
            // Heard by Whisper for "P and I" (2026-10-08) - a real word, but at a P&I kiosk it means P&I.
            "DNA", "PNE", "P and E", "B&I", "P&E",
            "पीएनआई", "पी एन आई", "पी.एन.आई", "पीएंडआई", "पी एंड आई", "पी और आई", "पी & आई", "पी&आई", "पीएनाई"),
        Variants("Pavilions & Interiors",
            "Pavilions and Interiors", "Pavilion and Interiors", "Pavilion & Interiors", "Pavillion and Interiors",
            "Pavillions and Interiors", "Pavilion Interiors", "Pavilions Interiors", "Pavilion and Interior",
            "पवेलियन एंड इंटीरियर्स", "पवेलियंस एंड इंटीरियर्स", "पैवेलियन एंड इंटीरियर्स"),
    };
    UnityEditor.EditorUtility.SetDirty(p);
}
Set(pPearl, "pni_female", "Pearl", pearl, "af_heart", "veena_kavya");
Set(pPeter, "pni_male", "Peter", peter, "am_michael", "veena_vinaya");
UnityEditor.AssetDatabase.SaveAssets();
foreach (var p in new[] { pPearl, pPeter }) ChatbotAI.EditorTools.KnowledgeSync.Sync(p);
log.Append("\nknowledge copied for pni_female, pni_male");

// ---- Scene: registry (after the four Altcore bots) ----
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
if (!scene.isLoaded) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
foreach (var go in scene.GetRootGameObjects())
    foreach (var registry in go.GetComponentsInChildren<ChatbotAI.Dialogue.AvatarRegistry>(true))
    {
        var so = new UnityEditor.SerializedObject(registry);
        var list = so.FindProperty("profiles");
        var have = new System.Collections.Generic.List<UnityEngine.Object>();
        for (int i = 0; i < list.arraySize; i++) have.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
        foreach (var p in new[] { pPearl, pPeter })
        {
            if (have.Contains(p)) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = p;
            have.Add(p);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Append("\nregistry: " + string.Join(", ", have.ConvertAll(o => o ? ((ChatbotAI.Dialogue.AvatarProfile)o).displayName : "-")));
    }
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.Debug.Log(log.ToString());
return log.ToString();
