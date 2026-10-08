var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");
var defaults = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Dialogue.AvatarProfile>();

// The old lines were all untouched defaults using {topics} (which read out file names).
profile.refusalLines = new System.Collections.Generic.List<string>(defaults.refusalLines);
profile.hindiRefusalLines = new System.Collections.Generic.List<string>(defaults.hindiRefusalLines);
UnityEngine.Object.DestroyImmediate(defaults);

string before = profile.personaPrompt;
// This sentence demanded pure Hindi, which contradicts the Hinglish rule the code now adds.
profile.personaPrompt = profile.personaPrompt
    .Replace(" Always reply in the same language the user asked in: if their question is in Hindi (Devanagari script), reply entirely in Hindi; if it's in English, reply in English.", "")
    .Replace("for Altcape and Altcore", "for Altscape and Altcore")
    .Replace("products. . You", "products. You");

UnityEditor.EditorUtility.SetDirty(profile);
UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
UnityEngine.Debug.Log($"AVATAR_UPDATED: {profile.refusalLines.Count} English + {profile.hindiRefusalLines.Count} Hinglish refusals\nPERSONA NOW: {profile.personaPrompt}");
