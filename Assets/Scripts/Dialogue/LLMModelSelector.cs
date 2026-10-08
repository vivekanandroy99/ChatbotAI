using LLMUnity;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// Which language model to run, picked from the files in
    /// StreamingAssets/Models/LLM (see LLMModelLibrary). The Inspector lists them
    /// with sizes and can delete unused ones. Change it outside Play mode - the
    /// model is loaded when Play starts.
    [RequireComponent(typeof(LLM))]
    [DefaultExecutionOrder(-1000)]  // before the LLM's own Awake, which loads the model
    public class LLMModelSelector : MonoBehaviour
    {
        [Tooltip("Model file in StreamingAssets/Models/LLM. Change outside Play mode.")]
        [SerializeField] string modelFile = "gemma-3-4b-it-Q4_K_M.gguf";

        public string ModelFile => modelFile;
        public string SelectedModelPath => LLMModelLibrary.RelativeFolder + "/" + modelFile;

        /// The model loaded this run: the one picked in the app's menu (ModelChoices), else the Inspector's.
        public string RunningModelFile { get; private set; }

        void Awake()
        {
            RunningModelFile = modelFile;
            string picked = ModelChoices.Get(ModelChoices.Brain);
            if (string.IsNullOrEmpty(picked) || picked == modelFile) return;
            if (!System.IO.File.Exists(System.IO.Path.Combine(LLMModelLibrary.FolderPath, picked)))
            {
                Debug.LogWarning($"LLMModelSelector: the model picked in the menu ({picked}) is gone - using {modelFile}.");
                return;
            }
            GetComponent<LLM>().model = LLMModelLibrary.RelativeFolder + "/" + picked;
            RunningModelFile = picked;
            Debug.Log($"LLMModelSelector: using {picked} (picked in the menu).");
        }

#if UNITY_EDITOR
        public void Select(string fileName)
        {
            UnityEditor.Undo.RecordObject(this, "Select model");
            modelFile = fileName;
            UnityEditor.EditorUtility.SetDirty(this);
            Apply();
        }

        void OnValidate()
        {
            if (Application.isPlaying) return;
            // Changing another component's model from inside OnValidate isn't allowed directly.
            UnityEditor.EditorApplication.delayCall += Apply;
        }

        /// Points the LLM at the selected file, and makes it the only model LLMUnity
        /// registers for builds.
        public void Apply()
        {
            if (this == null) return;
            string path = Application.streamingAssetsPath + "/" + SelectedModelPath;
            if (!System.IO.File.Exists(path)) return;
            if (LLMManager.Get(path) == null) LLMManager.LoadModel(path, false, System.IO.Path.GetFileNameWithoutExtension(modelFile));
            foreach (var entry in LLMManager.modelEntries.ToArray())
                if (!entry.lora) LLMManager.SetIncludeInBuild(entry, System.IO.Path.GetFileName(entry.filename) == modelFile);
            var llm = GetComponent<LLM>();
            if (llm.model != SelectedModelPath) llm.model = SelectedModelPath;
        }
#endif
    }
}
