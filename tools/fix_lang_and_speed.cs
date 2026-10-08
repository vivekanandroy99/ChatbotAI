var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, dialogueGO = null;
foreach (var go in roots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "DialogueBrain") dialogueGO = go;
}

var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
whisperEn.language = "";

var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();
agent.numPredict = 120;

UnityEditor.EditorUtility.SetDirty(whisperEnGO);
UnityEditor.EditorUtility.SetDirty(dialogueGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("WhisperEnglish set to auto-detect language; numPredict capped at 120 for faster replies.");
