using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChatbotAI.Audio
{
    /// The speech-recognition (Whisper) model files on disk
    /// (StreamingAssets/Models/Whisper) - listing, adding and deleting them. Used by
    /// SpeechInputController's Inspector and usable from in-app UI. Any whisper.cpp
    /// GGML model works (ggml-*.bin, e.g. from huggingface.co/ggerganov/whisper.cpp).
    /// Everything in that folder ships with a build.
    public static class WhisperModelLibrary
    {
        public const string RelativeFolder = "Models/Whisper";

        public struct ModelFile
        {
            public string fileName;
            public long bytes;
            public string Label => DescribedAs(fileName);
            public string SizeText => $"{bytes / (1024f * 1024f * 1024f):0.0} GB";
            public string RelativePath => RelativeFolder + "/" + fileName;
        }

        static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            ["ggml-large-v3-turbo-q5_0.bin"] = "Whisper large-v3 turbo - recommended, English + Hindi",
            ["ggml-hindi-medium-f16.bin"] = "Whisper medium, Hindi fine-tuned - tested worse",
        };

        public static string FolderPath => Path.Combine(Application.streamingAssetsPath, RelativeFolder);

        public static string DescribedAs(string fileName) =>
            Descriptions.TryGetValue(fileName, out var d) ? d : Path.GetFileNameWithoutExtension(fileName);

        /// The model files present, largest first.
        public static List<ModelFile> List()
        {
            var result = new List<ModelFile>();
            if (!Directory.Exists(FolderPath)) return result;
            foreach (string path in Directory.GetFiles(FolderPath, "*.bin"))
                result.Add(new ModelFile { fileName = Path.GetFileName(path), bytes = new FileInfo(path).Length });
            result.Sort((a, b) => b.bytes.CompareTo(a.bytes));
            return result;
        }

        /// whisper.cpp models start with the GGML magic number.
        public static bool IsWhisperModel(string path)
        {
            try
            {
                using var file = File.OpenRead(path);
                var magic = new byte[4];
                return file.Read(magic, 0, 4) == 4 && BitConverter.ToUInt32(magic, 0) == 0x67676d6c;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// Copies a model file from anywhere on disk into the models folder.
        public static bool Add(string sourcePath, out string fileName, out string error)
        {
            fileName = Path.GetFileName(sourcePath);
            error = null;
            if (!File.Exists(sourcePath))
            {
                error = "file not found";
                return false;
            }
            if (!IsWhisperModel(sourcePath))
            {
                error = "Not a whisper.cpp (GGML) model. Use the ggml-*.bin files made for whisper.cpp - not PyTorch .bin or .safetensors files.";
                return false;
            }
            if (!fileName.EndsWith(".bin")) fileName += ".bin";
            string dest = Path.Combine(FolderPath, fileName);
            if (File.Exists(dest))
            {
                error = "a model with that file name is already there";
                return false;
            }
            try
            {
                Directory.CreateDirectory(FolderPath);
                File.Copy(sourcePath, dest);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        /// Deletes a model file. inUseReason: why it can't go (null if it can).
        public static bool Delete(string fileName, string inUseReason, out string error)
        {
            error = inUseReason;
            if (error != null) return false;
            if (string.IsNullOrEmpty(fileName) || fileName != Path.GetFileName(fileName))
            {
                error = "not a model file name";
                return false;
            }
            string path = Path.Combine(FolderPath, fileName);
            try
            {
                File.Delete(path);
                if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;  // e.g. loaded right now
                return false;
            }
        }
    }
}
