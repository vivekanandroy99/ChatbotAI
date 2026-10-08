using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChatbotAI.Audio
{
    /// The voice (text-to-speech) model files on disk - listing, adding and
    /// deleting them. Used by the voice model list in TTSProcessManager's
    /// Inspector and usable from in-app UI. Which file each engine runs is picked
    /// on TTSProcessManager and passed to the voice server when it starts.
    ///
    /// Kokoro .onnx and Veena .gguf files live in TTSServer/models; IndicF5 lives
    /// in the Hugging Face cache. A new kind of voice model needs an engine adapter
    /// in the voice server (like TTSServer/veena_engine.py) before it can be added.
    public static class VoiceModelLibrary
    {
        public enum Engine { Kokoro, KokoroV11, Veena, IndicF5 }

        /// Engines that can be turned off (kept on disk, never loaded - Kokoro speaks for them), in menu order.
        public static readonly Engine[] Optional = { Engine.Veena, Engine.IndicF5, Engine.KokoroV11 };

        public struct ModelFile
        {
            public Engine engine;
            public string fileName;  // in TTSServer/models (IndicF5: its cache folder)
            public string path;
            public long bytes;
            public bool isFolder;
            public string Label => DescribedAs(fileName);
            public string SizeText => $"{bytes / (1024f * 1024f * 1024f):0.0} GB";
        }

        static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            ["kokoro-v1.0.onnx"] = "Kokoro 82M - English + Hindi voices",
            ["kokoro-v1.1-zh.onnx"] = "Kokoro v1.1 - English voices Maple, Sol, Vale",
            ["Veena-q8_0.gguf"] = "Veena Q8 - slightly better, may lag behind speech",
            ["Veena-q6_k_m.gguf"] = "Veena Q6 - near-best quality, less memory",
            ["Veena-q4_k_m.gguf"] = "Veena Q4 - recommended: keeps up, less memory",
            [VoiceEngineDownloads.IndicF5.repoFolder] = "IndicF5 (experimental)",
        };

        public static string ModelsFolder => VoiceEngineDownloads.ModelsDir;

        public static string DescribedAs(string fileName) =>
            Descriptions.TryGetValue(fileName, out var d) ? d : Path.GetFileNameWithoutExtension(fileName);

        public static string EngineName(Engine engine) => engine switch
        {
            Engine.Kokoro => "Kokoro (English + basic Hindi, required)",
            Engine.KokoroV11 => "Kokoro v1.1 (three more English voices)",
            Engine.Veena => "Veena (natural Hindi)",
            _ => "IndicF5 (experimental Hindi)",
        };

        /// The voice server's name for an engine (VOICE_ENGINES_OFF).
        public static string EngineId(Engine engine) => engine switch
        {
            Engine.KokoroV11 => VoiceEngineDownloads.KokoroV11.id,
            Engine.Veena => VoiceEngineDownloads.Veena.id,
            Engine.IndicF5 => VoiceEngineDownloads.IndicF5.id,
            _ => "kokoro",
        };

        /// The engine that speaks a voice from VoiceCatalog.
        public static Engine EngineOf(VoiceCatalog.Voice voice) =>
            voice.engine == VoiceEngineDownloads.Veena ? Engine.Veena
            : voice.engine == VoiceEngineDownloads.KokoroV11 ? Engine.KokoroV11
            : voice.engine == VoiceEngineDownloads.IndicF5 ? Engine.IndicF5
            : Engine.Kokoro;

        /// Which engine runs a model file, judged by its name; false if none can.
        public static bool TryEngineFor(string fileName, out Engine engine)
        {
            engine = Engine.Kokoro;
            string name = Path.GetFileName(fileName).ToLowerInvariant();
            if (name.StartsWith("kokoro-v1.1") && name.EndsWith(".onnx"))
            {
                engine = Engine.KokoroV11;
                return true;
            }
            if (name.EndsWith(".onnx")) return true;
            if (name.EndsWith(".gguf") && name.Contains("veena"))
            {
                engine = Engine.Veena;
                return true;
            }
            return false;
        }

        public static List<ModelFile> List()
        {
            var result = new List<ModelFile>();
            if (Directory.Exists(ModelsFolder))
            {
                foreach (string path in Directory.GetFiles(ModelsFolder))
                {
                    if (!TryEngineFor(path, out var engine)) continue;
                    result.Add(new ModelFile { engine = engine, fileName = Path.GetFileName(path), path = path, bytes = new FileInfo(path).Length });
                }
            }
            string indic = Path.Combine(VoiceEngineDownloads.HubCache, VoiceEngineDownloads.IndicF5.repoFolder);
            if (VoiceEngineDownloads.IsDownloaded(VoiceEngineDownloads.IndicF5))
            {
                long bytes = new DirectoryInfo(indic).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                result.Add(new ModelFile
                {
                    engine = Engine.IndicF5, fileName = VoiceEngineDownloads.IndicF5.repoFolder, path = indic, bytes = bytes, isFolder = true
                });
            }
            result.Sort((a, b) => a.engine != b.engine ? a.engine.CompareTo(b.engine) : b.bytes.CompareTo(a.bytes));
            return result;
        }

        /// Copies a model file from anywhere on disk into TTSServer/models.
        public static bool Add(string sourcePath, out string fileName, out string error)
        {
            fileName = Path.GetFileName(sourcePath);
            error = null;
            if (!File.Exists(sourcePath))
            {
                error = "file not found";
                return false;
            }
            if (!TryEngineFor(sourcePath, out _))
            {
                error = "Not a voice model this app can run yet. Supported: Kokoro .onnx files and Veena .gguf files " +
                        "(name containing \"veena\"). A new kind of voice model needs an engine adapter in the voice " +
                        "server (TTSServer) first.";
                return false;
            }
            string dest = Path.Combine(ModelsFolder, fileName);
            if (File.Exists(dest))
            {
                error = "a model with that file name is already there";
                return false;
            }
            try
            {
                Directory.CreateDirectory(ModelsFolder);
                File.Copy(sourcePath, dest);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        /// Deletes a model. inUseReason: why it can't go (null if it can).
        public static bool Delete(ModelFile model, string inUseReason, out string error)
        {
            error = inUseReason;
            if (error != null) return false;
            try
            {
                if (model.isFolder) Directory.Delete(model.path, true);
                else File.Delete(model.path);
                // Files that only this model uses.
                if (model.engine == Engine.KokoroV11)
                    foreach (string companion in new[] { "voices-v1.1-zh.bin", "kokoro-v1.1-zh.config.json" })
                        if (File.Exists(Path.Combine(ModelsFolder, companion))) File.Delete(Path.Combine(ModelsFolder, companion));
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;  // e.g. the voice server has it loaded right now
                return false;
            }
        }
    }
}
