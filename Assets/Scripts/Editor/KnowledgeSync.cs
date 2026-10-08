using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChatbotAI.Dialogue;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChatbotAI.EditorTools
{
    /// Copies each AvatarProfile's knowledgeDocuments list into
    /// StreamingAssets/Knowledge/<avatarId>/ - the folder the knowledge service
    /// reads, and the only place documents survive into a build. Runs when the
    /// list is edited, when a listed document changes, on entering Play mode,
    /// and before builds. Only ever deletes copies it made itself.
    [InitializeOnLoad]
    public static class KnowledgeSync
    {
        // Leading dot: ignored by Unity and by the knowledge service.
        const string ManifestName = ".synced";

        static KnowledgeSync()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) SyncAll();
            };
        }

        public static void SyncAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AvatarProfile)))
            {
                var profile = AssetDatabase.LoadAssetAtPath<AvatarProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile != null) Sync(profile);
            }
        }

        public static void Sync(AvatarProfile profile)
        {
            string target = profile.KnowledgeFolderPath;
            Directory.CreateDirectory(target);

            // mirror file name -> source file
            var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in profile.knowledgeDocuments)
            {
                if (doc == null) continue;
                string assetPath = AssetDatabase.GetAssetPath(doc);
                if (string.IsNullOrEmpty(assetPath) || !AvatarProfile.IsSupportedDocument(assetPath)) continue;

                string name = Path.GetFileName(assetPath);
                string unique = name;
                for (int n = 2; wanted.ContainsKey(unique); n++)
                    unique = $"{Path.GetFileNameWithoutExtension(name)} ({n}){Path.GetExtension(name)}";
                wanted[unique] = Path.GetFullPath(assetPath);
            }

            string manifestPath = Path.Combine(target, ManifestName);
            var previouslySynced = File.Exists(manifestPath)
                ? new HashSet<string>(File.ReadAllLines(manifestPath), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in Directory.GetFiles(target))
            {
                string fileName = Path.GetFileName(file);
                // Taught answers are written here by the app (menu > Learning) - not in any document list, never removed.
                if (fileName.StartsWith(".") || fileName.EndsWith(".meta") || wanted.ContainsKey(fileName) ||
                    fileName.StartsWith(TaughtAnswers.FilePrefix)) continue;

                if (previouslySynced.Contains(fileName))
                {
                    File.Delete(file);
                    if (File.Exists(file + ".meta")) File.Delete(file + ".meta");
                }
                else
                {
                    Debug.LogWarning($"KnowledgeSync: '{fileName}' is in {target} but not in {profile.name}'s document list - " +
                                     "the avatar still reads it. Add it to the list (or delete it) to keep things tidy.");
                }
            }

            foreach (var pair in wanted)
            {
                var src = new FileInfo(pair.Value);
                var dst = new FileInfo(Path.Combine(target, pair.Key));
                if (!dst.Exists || dst.Length != src.Length || dst.LastWriteTimeUtc != src.LastWriteTimeUtc)
                    File.Copy(src.FullName, dst.FullName, overwrite: true);
            }

            File.WriteAllLines(manifestPath, wanted.Keys);
        }
    }

    class KnowledgeSyncOnBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => KnowledgeSync.SyncAll();
    }

    class KnowledgeSyncOnAssetChange : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool relevant = imported.Concat(deleted).Concat(moved).Any(p =>
                !p.StartsWith("Assets/StreamingAssets/") &&
                (AvatarProfile.IsSupportedDocument(p) || p.EndsWith(".asset")));
            if (relevant) KnowledgeSync.SyncAll();
        }
    }
}
