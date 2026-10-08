var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject speechOutGO = null;
foreach (var go in goRoots) { if (go.name == "SpeechOutput") speechOutGO = go; }
var audioSource = speechOutGO.GetComponent<UnityEngine.AudioSource>();

if (audioSource.clip == null)
{
    UnityEngine.Debug.Log("AUDIO_STATE: no clip yet (still generating or failed)");
}
else
{
    UnityEngine.Debug.Log($"AUDIO_STATE: clip={audioSource.clip.name} length={audioSource.clip.length}s channels={audioSource.clip.channels} freq={audioSource.clip.frequency} isPlaying={audioSource.isPlaying}");
}
