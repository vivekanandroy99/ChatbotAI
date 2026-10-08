var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");
var editorType = System.Type.GetType("ChatbotAI.EditorTools.AvatarProfileEditor, Assembly-CSharp-Editor");
var addDoc = editorType.GetMethod("AddDocument", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
var syncType = System.Type.GetType("ChatbotAI.EditorTools.KnowledgeSync, Assembly-CSharp-Editor");

string src = @"C:\Users\Admin\AppData\Local\Temp\claude\C--Users-Admin-ChatbotAI\a6d0f108-adb8-4123-bea6-c336772cbbe8\scratchpad\external_docs";
var so = new UnityEditor.SerializedObject(profile);
var list = so.FindProperty("knowledgeDocuments");
// Same calls the drop box makes for files dropped from Explorer.
foreach (var file in System.IO.Directory.GetFiles(src))
    addDoc.Invoke(null, new object[] { profile, list, file });
so.ApplyModifiedProperties();
syncType.GetMethod("Sync").Invoke(null, new object[] { profile });
UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);

var sb = new System.Text.StringBuilder("DOC_IMPORT: list now has " + profile.knowledgeDocuments.Count + " entries\n");
foreach (var d in profile.knowledgeDocuments) sb.Append("  list: " + UnityEditor.AssetDatabase.GetAssetPath(d) + "\n");
foreach (var f in System.IO.Directory.GetFiles(profile.KnowledgeFolderPath)) sb.Append("  runtime copy: " + System.IO.Path.GetFileName(f) + "\n");
UnityEngine.Debug.Log(sb.ToString());
