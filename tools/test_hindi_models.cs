UnityEngine.GameObject enGO = null, hiGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    if (go.name == "WhisperEnglish") enGO = go;
    if (go.name == "WhisperHindi") hiGO = go;
}
var turbo = enGO.GetComponent<Whisper.WhisperManager>();
var hindi = hiGO.GetComponent<Whisper.WhisperManager>();
string dir = @"C:\Users\Admin\AppData\Local\Temp\claude\C--Users-Admin-ChatbotAI\a6d0f108-adb8-4123-bea6-c336772cbbe8\scratchpad\stt_clips";
string[] clips = { "hi_verticals", "hi_founded" };

async void RunAll()
{
    string savedLang = turbo.language, savedPrompt = turbo.initialPrompt;
    foreach (var name in clips)
    {
        var clip = ChatbotAI.Audio.WavUtility.ToAudioClip(System.IO.File.ReadAllBytes(System.IO.Path.Combine(dir, name + ".wav")));
        var s = new float[clip.samples]; clip.GetData(s, 0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var a = await hindi.GetTextAsync(s, clip.frequency, 1);
        long ta = sw.ElapsedMilliseconds; sw.Restart();

        turbo.language = "hi"; turbo.initialPrompt = null;
        var b = await turbo.GetTextAsync(s, clip.frequency, 1);
        long tb = sw.ElapsedMilliseconds; sw.Restart();

        turbo.initialPrompt = savedPrompt;
        var c = await turbo.GetTextAsync(s, clip.frequency, 1);
        long tc = sw.ElapsedMilliseconds;

        UnityEngine.Debug.Log($"HM [{name}]\n   fine-tuned Hindi ({ta} ms): {a?.Result}\n   turbo, hi forced ({tb} ms): {b?.Result}\n   turbo, hi + name hint ({tc} ms): {c?.Result}");
    }
    turbo.language = savedLang; turbo.initialPrompt = savedPrompt;
    UnityEngine.Debug.Log("HM_DONE");
}
RunAll();
