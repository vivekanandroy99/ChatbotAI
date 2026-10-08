var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = scene.GetRootGameObjects();
UnityEngine.GameObject speechGO = null;
foreach (var go in roots)
{
    if (go.name == "SpeechInput") speechGO = go;
}

var mic = speechGO.GetComponent<Whisper.Utils.MicrophoneRecord>();
mic.vadStop = true;
mic.vadStopTime = 2f;

UnityEditor.EditorUtility.SetDirty(speechGO);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log("MicrophoneRecord: vadStop enabled (auto-stop after 2s of silence) to keep recorded clips short.");
