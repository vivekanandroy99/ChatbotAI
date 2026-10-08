var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, whisperHiGO = null;
foreach (var go in goRoots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
}
var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();

int sampleCount = 16000 * 2;
float[] buffer = new float[sampleCount];
var rng = new System.Random(7);
for (int i = 0; i < sampleCount; i++) buffer[i] = (float)(rng.NextDouble() * 0.01 - 0.005);

async void RunSequential()
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        var r1 = await whisperEn.GetTextAsync(buffer, 16000, 1);
        UnityEngine.Debug.Log($"SEQ_GPU_TEST_1_OK ({sw.ElapsedMilliseconds}ms): [{r1?.Result}]");
        sw.Restart();
        var r2 = await whisperHi.GetTextAsync(buffer, 16000, 1);
        UnityEngine.Debug.Log($"SEQ_GPU_TEST_2_OK ({sw.ElapsedMilliseconds}ms): [{r2?.Result}]");
        UnityEngine.Debug.Log("SEQ_GPU_TEST_ALL_DONE");
    }
    catch (System.Exception ex)
    {
        UnityEngine.Debug.LogError("SEQ_GPU_TEST_FAILED: " + ex);
    }
}
RunSequential();
UnityEngine.Debug.Log("Sequential dual-GPU test triggered.");
