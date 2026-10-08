// Edit mode: adds "Kevin - open chat" (Kevin's character and voices, Open Chat mode: talks about anything
// with a personality, no documents / topic gate) and the in-app debug overlay (F1). Safe to re-run.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { UnityEngine.Debug.Log("OPENCHAT: in Play mode - not changed"); return null; }
const string kevinPath = "Assets/Resources/Avatars/Kevin.asset";
const string chatPath = "Assets/Resources/Avatars/Kevin Open Chat.asset";
const string scenePath = "Assets/Scenes/AltcoreBot_v5.unity";
var log = new System.Text.StringBuilder("OPENCHAT:");

var chat = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>(chatPath);
if (!chat)
{
    var kevin = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>(kevinPath);
    chat = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Dialogue.AvatarProfile>();
    chat.avatarId = "kevin_chat";
    chat.displayName = "Kevin";
    chat.mode = ChatbotAI.Dialogue.AvatarProfile.ConversationMode.OpenChat;
    chat.personaPrompt =
        "You are Kevin, a friendly, curious and quick-witted companion in his forties. You love a good conversation about " +
        "almost anything - science and space, films and music, food and cooking, travel, cricket and football, technology, " +
        "books, and the small puzzles of everyday life. You have real opinions and share them with a light, dry sense of " +
        "humour, but you listen, you're kind, and you never lecture. You like a good story and a clever analogy. You're " +
        "honest when you don't know something, and you admit when you're guessing.";
    chat.englishVoice = kevin ? kevin.englishVoice : "am_michael";
    chat.hindiVoice = kevin ? kevin.hindiVoice : "veena_vinaya";
    chat.speechSpeed = 1f;
    chat.character = kevin ? kevin.character : null;
    chat.knowledgeDocuments.Clear();
    chat.topicNames.Clear();
    chat.pronunciations = kevin ? new System.Collections.Generic.List<ChatbotAI.Dialogue.Pronunciation>(kevin.pronunciations) : chat.pronunciations;
    UnityEditor.AssetDatabase.CreateAsset(chat, chatPath);
    log.Append($" created {chatPath} ({chat.englishVoice} / {chat.hindiVoice}, character {(chat.character ? chat.character.displayName : "none")});");
}
UnityEditor.AssetDatabase.SaveAssets();

var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
bool wasOpen = scene.isLoaded;
if (!wasOpen) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
bool hasOverlay = false;
ChatbotAI.Dialogue.AvatarRegistry registry = null;
foreach (var go in scene.GetRootGameObjects())
{
    if (go.GetComponentInChildren<ChatbotAI.UI.DebugOverlay>(true)) hasOverlay = true;
    if (!registry) registry = go.GetComponentInChildren<ChatbotAI.Dialogue.AvatarRegistry>(true);
}
if (!hasOverlay)
{
    var go = new UnityEngine.GameObject("Debug Overlay");
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    go.AddComponent<ChatbotAI.UI.DebugOverlay>();
    log.Append(" added the debug overlay (F1);");
}
// A registry with a hand-made list gets the new avatar too (an empty list loads Resources/Avatars).
var rso = new UnityEditor.SerializedObject(registry);
var list = rso.FindProperty("profiles");
if (list.arraySize > 0)
{
    bool has = false;
    for (int i = 0; i < list.arraySize; i++) has |= list.GetArrayElementAtIndex(i).objectReferenceValue == chat;
    if (!has)
    {
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = chat;
        rso.ApplyModifiedPropertiesWithoutUndo();
        log.Append(" registry lists it;");
    }
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
if (!wasOpen) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
UnityEngine.Debug.Log(log.ToString());
return log.ToString();
