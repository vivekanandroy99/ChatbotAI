using System.IO;
using System.Linq;
using ChatbotAI.Audio;
using UnityEditor;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// Lip Sync Voice Profiles inspector: the list, plus "Find calibrated profiles", which lists every
    /// uLipSync-Profile-<voice>.asset in Assets/Avatar LipSync (made by tools/calibrate_lipsync.cs).
    [CustomEditor(typeof(LipSyncVoiceProfiles))]
    public class LipSyncVoiceProfilesEditor : Editor
    {
        public const string Folder = "Assets/Avatar LipSync";
        const string Prefix = "uLipSync-Profile-";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button(new GUIContent("Find calibrated profiles", $"Every {Prefix}<voice>.asset in {Folder}")))
                Fill((LipSyncVoiceProfiles)target);
            EditorGUILayout.HelpBox("A new voice: tools/make_lipsync_calibration.py <voice>, then tools/calibrate_lipsync.cs (Play mode), " +
                                    "then this button.", MessageType.None);
        }

        public static int Fill(LipSyncVoiceProfiles component)
        {
            Undo.RecordObject(component, "Find calibrated profiles");
            component.profiles = AssetDatabase.FindAssets(Prefix, new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(Prefix))
                .Select(p => new LipSyncVoiceProfiles.Entry
                {
                    voice = Path.GetFileNameWithoutExtension(p).Substring(Prefix.Length),
                    profile = AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(p),
                })
                .Where(e => e.profile)
                .ToList();
            EditorUtility.SetDirty(component);
            return component.profiles.Count;
        }
    }
}
