var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, whisperHiGO = null, llmGO = null;
foreach (var go in goRoots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
    if (go.name == "LLM") llmGO = go;
}
var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();
var llm = llmGO.GetComponent<LLMUnity.LLM>();

// 5 seconds of low-amplitude noise at 16kHz mono - realistic clip length, not real speech.
int sampleCount = 16000 * 5;
float[] buffer = new float[sampleCount];
var rng = new System.Random(7);
for (int i = 0; i < sampleCount; i++) buffer[i] = (float)(rng.NextDouble() * 0.01 - 0.005);

async void RunBench()
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var r1 = await whisperEn.GetTextAsync(buffer, 16000, 1);
    UnityEngine.Debug.Log($"BENCH_BEFORE_WHISPER_EN ({sw.ElapsedMilliseconds}ms)");
    sw.Restart();
    var r2 = await whisperHi.GetTextAsync(buffer, 16000, 1);
    UnityEngine.Debug.Log($"BENCH_BEFORE_WHISPER_HI ({sw.ElapsedMilliseconds}ms)");
    UnityEngine.Debug.Log("BENCH_BEFORE_DONE");
}
RunBench();
UnityEngine.Debug.Log("Bench (before) triggered.");
