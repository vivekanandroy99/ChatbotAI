var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null;
foreach (var go in goRoots)
{
    if (go.name == "WhisperEnglish") { whisperEnGO = go; break; }
}
var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();

// 2 seconds of low-amplitude noise at 16kHz mono - not real speech, just
// enough to exercise a real whisper_full() call on the GPU path safely.
int sampleCount = 16000 * 2;
float[] buffer = new float[sampleCount];
var rng = new System.Random(42);
for (int i = 0; i < sampleCount; i++) buffer[i] = (float)(rng.NextDouble() * 0.01 - 0.005);

var sw = System.Diagnostics.Stopwatch.StartNew();
_ = whisperEn.GetTextAsync(buffer, 16000, 1).ContinueWith(t => {
    sw.Stop();
    if (t.Exception != null)
    {
        UnityEngine.Debug.LogError($"WHISPER_GPU_TEST_FAILED: {t.Exception}");
    }
    else
    {
        var result = t.Result;
        UnityEngine.Debug.Log($"WHISPER_GPU_TEST_OK ({sw.ElapsedMilliseconds}ms): [{result?.Result}]");
    }
}, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
UnityEngine.Debug.Log("Whisper GPU synthetic test triggered.");
