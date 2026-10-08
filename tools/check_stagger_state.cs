var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, whisperHiGO = null, llmGO = null, dialogueGO = null;
foreach (var go in goRoots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
    if (go.name == "LLM") llmGO = go;
    if (go.name == "DialogueBrain") dialogueGO = go;
}
var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();

UnityEngine.Debug.Log($"STAGGER_STATE: whisperEnActive={whisperEnGO.activeInHierarchy} whisperEnLoaded={whisperEn.IsLoaded} whisperHiActive={whisperHiGO.activeInHierarchy} whisperHiLoaded={whisperHi.IsLoaded}");
