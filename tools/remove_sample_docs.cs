var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");
var toDelete = new System.Collections.Generic.List<string>();
foreach (var d in profile.knowledgeDocuments)
{
    string p = UnityEditor.AssetDatabase.GetAssetPath(d);
    if (System.IO.Path.GetFileName(p).StartsWith("SAMPLE_")) toDelete.Add(p);
}
profile.knowledgeDocuments.RemoveAll(d => d != null && System.IO.Path.GetFileName(UnityEditor.AssetDatabase.GetAssetPath(d)).StartsWith("SAMPLE_"));
UnityEditor.EditorUtility.SetDirty(profile);
UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
System.Type.GetType("ChatbotAI.EditorTools.KnowledgeSync, Assembly-CSharp-Editor").GetMethod("Sync").Invoke(null, new object[] { profile });

var sb = new System.Text.StringBuilder($"SAMPLE_CLEANUP: list now {profile.knowledgeDocuments.Count} entries; runtime folder has:");
foreach (var f in System.IO.Directory.GetFiles(profile.KnowledgeFolderPath)) sb.Append(" " + System.IO.Path.GetFileName(f));
foreach (var p in toDelete) UnityEditor.AssetDatabase.DeleteAsset(p);
UnityEngine.Debug.Log(sb.ToString());
