var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Dialogue.AvatarProfile>("Assets/Resources/Avatars/DefaultAvatar.asset");
profile.hindiVoice = "indicf5_hin_female";
UnityEditor.EditorUtility.SetDirty(profile);
UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
UnityEngine.Debug.Log("SETUP: hindiVoice=" + profile.hindiVoice + " female=" + profile.HasFemaleVoice);