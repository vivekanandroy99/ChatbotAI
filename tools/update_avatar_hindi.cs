var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");
var defaults = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Dialogue.AvatarProfile>();
profile.hindiRefusalLines = new System.Collections.Generic.List<string>(defaults.hindiRefusalLines);
profile.passagesPerQuestion = defaults.passagesPerQuestion;
UnityEngine.Object.DestroyImmediate(defaults);
UnityEditor.EditorUtility.SetDirty(profile);
UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
UnityEngine.Debug.Log($"AVATAR_HINDI_OK: {profile.hindiRefusalLines.Count} Hindi refusals, passages={profile.passagesPerQuestion}");
