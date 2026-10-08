var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject whisperEnGO = null, whisperHiGO = null, dialogueGO = null;
foreach (var go in goRoots)
{
    if (go.name == "WhisperEnglish") whisperEnGO = go;
    if (go.name == "WhisperHindi") whisperHiGO = go;
    if (go.name == "DialogueBrain") dialogueGO = go;
}
var whisperEn = whisperEnGO.GetComponent<Whisper.WhisperManager>();
var whisperHi = whisperHiGO.GetComponent<Whisper.WhisperManager>();
var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();

int sampleCount = 16000 * 5;
float[] buffer = new float[sampleCount];
var rng = new System.Random(7);
for (int i = 0; i < sampleCount; i++) buffer[i] = (float)(rng.NextDouble() * 0.01 - 0.005);

async void RunBench()
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var r1 = await whisperEn.GetTextAsync(buffer, 16000, 1);
    UnityEngine.Debug.Log($"BENCH_AFTER_WHISPER_EN ({sw.ElapsedMilliseconds}ms)");
    sw.Restart();
    var r2 = await whisperHi.GetTextAsync(buffer, 16000, 1);
    UnityEngine.Debug.Log($"BENCH_AFTER_WHISPER_HI ({sw.ElapsedMilliseconds}ms)");

    sw.Restart();
    string reply = await agent.Chat("What are your store hours?", null, null, addToHistory: false);
    UnityEngine.Debug.Log($"BENCH_AFTER_LLM ({sw.ElapsedMilliseconds}ms): [{reply}]");
    UnityEngine.Debug.Log("BENCH_AFTER_DONE");
}
RunBench();
UnityEngine.Debug.Log("Bench (after, v2) triggered.");
