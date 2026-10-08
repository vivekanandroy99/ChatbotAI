using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// The language-model files on disk (StreamingAssets/Models/LLM) - listing and
    /// deleting them. Used by the Inspector's model list and usable from in-app UI.
    /// Everything in that folder ships with a build, so unused models cost disk
    /// space twice.
    public static class LLMModelLibrary
    {
        public const string RelativeFolder = "Models/LLM";

        public struct ModelFile
        {
            public string fileName;
            public long bytes;
            public string Label => DescribedAs(fileName);
            public string SizeText => $"{bytes / (1024f * 1024f * 1024f):0.0} GB";
        }

        // Known models, from the 2026-09-24 comparison (tools/test_models.cs).
        static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            ["gemma-3-4b-it-Q4_K_M.gguf"] = "Gemma 3 4B - recommended: fastest, best Hindi",
            ["Qwen3.5-4B-Q4_K_M.gguf"] = "Qwen3.5 4B - good English, weaker Hindi",
            ["Qwen3.5-9B-Q4_K_M.gguf"] = "Qwen3.5 9B - slow, no better Hindi",
            ["Qwen2.5-7B-Instruct-Q4_K_M.gguf"] = "Qwen2.5 7B - older, weak Hindi",
            ["Qwen2.5-3B-Instruct-Q4_K_M.gguf"] = "Qwen2.5 3B - older, less accurate",
            ["Qwen2.5-1.5B-Instruct-Q4_K_M.gguf"] = "Qwen2.5 1.5B - older, least accurate",
        };

        public static string FolderPath => Path.Combine(Application.streamingAssetsPath, RelativeFolder);

        public static string DescribedAs(string fileName) =>
            Descriptions.TryGetValue(fileName, out var d) ? d : Path.GetFileNameWithoutExtension(fileName);

        /// The .gguf files present, largest first.
        public static List<ModelFile> List()
        {
            var result = new List<ModelFile>();
            if (!Directory.Exists(FolderPath)) return result;
            foreach (string path in Directory.GetFiles(FolderPath, "*.gguf"))
                result.Add(new ModelFile { fileName = Path.GetFileName(path), bytes = new FileInfo(path).Length });
            result.Sort((a, b) => b.bytes.CompareTo(a.bytes));
            return result;
        }

        /// Copies a .gguf model from anywhere on disk into the models folder.
        public static bool Add(string sourcePath, out string fileName, out string error)
        {
            fileName = Path.GetFileName(sourcePath);
            error = null;
            if (!File.Exists(sourcePath))
            {
                error = "file not found";
                return false;
            }
            if (!IsGguf(sourcePath))
            {
                error = "Not a GGUF model. The brain runs llama.cpp models: .gguf files (Q4_K_M is a good size/quality balance).";
                return false;
            }
            string dest = Path.Combine(FolderPath, fileName);
            if (File.Exists(dest))
            {
                error = "a model with that file name is already there";
                return false;
            }
            try
            {
                File.Copy(sourcePath, dest);
                return true;
            }
            catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        static bool IsGguf(string path)
        {
            try
            {
                using var file = File.OpenRead(path);
                var magic = new byte[4];
                return file.Read(magic, 0, 4) == 4 && magic[0] == 'G' && magic[1] == 'G' && magic[2] == 'U' && magic[3] == 'F';
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// Deletes a model file. Refuses the one in use (inUseFileName) - switch first.
        public static bool Delete(string fileName, string inUseFileName, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(fileName) || fileName != Path.GetFileName(fileName) || !fileName.EndsWith(".gguf"))
            {
                error = "not a model file name";
                return false;
            }
            if (fileName == inUseFileName)
            {
                error = "that model is in use - switch to another one first";
                return false;
            }
            string path = Path.Combine(FolderPath, fileName);
            if (!File.Exists(path))
            {
                error = "file not found";
                return false;
            }
            try
            {
                File.Delete(path);
                if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
                return true;
            }
            catch (IOException e)
            {
                error = e.Message;  // e.g. the file is loaded right now
                return false;
            }
        }
    }
}
