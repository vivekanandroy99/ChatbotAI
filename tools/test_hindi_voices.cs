// Compares Hindi voices: each sentence is spoken by every voice through the voice
// server (timed), saved as a WAV, then transcribed back with the app's own Whisper.
// How much of the text comes back is a rough, objective clarity score.
UnityEngine.GameObject inputGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    if (go.name == "SpeechInput") inputGO = go;
var stt = inputGO.GetComponent<ChatbotAI.Audio.SpeechInputController>();
string outDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hindi_voice_test");
System.IO.Directory.CreateDirectory(outDir);

string[] voices = { "hf_alpha", "indicf5_hindi", "indicf5_female", "indicf5_male" };
string[] sentences =
{
    "Altcore 2024 में शुरू हुई थी, और ये एक experiential technology firm है।",
    "आप Altcore से +91 90961 91973 पर बात कर सकते हैं, या contact@altcore.co पर email कर सकते हैं।",
    "Basic Plan की कीमत ₹499 प्रति महीना है, और आप इसे कभी भी cancel कर सकते हैं।",
    "हाँ, pets को outdoor seating areas में आने की इजाज़त है, और सभी cafés में free Wi-Fi मिलता है।",
};

System.Collections.Generic.HashSet<string> Words(string s)
{
    var set = new System.Collections.Generic.HashSet<string>();
    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(s, @"[ऀ-ॿ]+")) set.Add(m.Value);
    return set;
}

async void Run()
{
    stt.SetLanguage(ChatbotAI.Audio.SpeechInputController.InputLanguage.Hindi);  // score everything as Hindi
    // Load IndicF5 first so its one-time load time doesn't count against the first sentence.
    using (var warm = new UnityEngine.Networking.UnityWebRequest("http://127.0.0.1:8765/speak", "POST"))
    {
        warm.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{\"text\":\"नमस्ते।\",\"voice\":\"indicf5_hindi\",\"speed\":1}"));
        warm.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        warm.SetRequestHeader("Content-Type", "application/json");
        var sw0 = System.Diagnostics.Stopwatch.StartNew();
        var op0 = warm.SendWebRequest();
        while (!op0.isDone) await System.Threading.Tasks.Task.Yield();
        UnityEngine.Debug.Log($"HV IndicF5 first load + warm-up: {sw0.ElapsedMilliseconds} ms ({warm.result})");
    }
    var totals = new System.Collections.Generic.Dictionary<string, (long ms, int hit, int all)>();
    foreach (var s in sentences)
    {
        foreach (var v in voices)
        {
            string json = "{\"text\":\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"voice\":\"" + v + "\",\"speed\":1}";
            using var req = new UnityEngine.Networking.UnityWebRequest("http://127.0.0.1:8765/speak", "POST");
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var op = req.SendWebRequest();
            while (!op.isDone) await System.Threading.Tasks.Task.Yield();
            long ms = sw.ElapsedMilliseconds;
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { UnityEngine.Debug.Log($"HV FAIL {v}: {req.error}"); continue; }
            byte[] wav = req.downloadHandler.data;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, $"{v}_{System.Array.IndexOf(sentences, s)}.wav"), wav);
            var clip = ChatbotAI.Audio.WavUtility.ToAudioClip(wav);
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            var (heard, lang) = await stt.TranscribeAsync(samples, clip.frequency, clip.channels);
            var expected = Words(s);
            var heardWords = Words(heard);
            int hit = 0;
            foreach (var w in expected) if (heardWords.Contains(w)) hit++;
            var t = totals.TryGetValue(v, out var old) ? old : (0L, 0, 0);
            totals[v] = (t.Item1 + ms, t.Item2 + hit, t.Item3 + expected.Count);
            UnityEngine.Debug.Log($"HV {v} ({ms} ms, {clip.length:0.0}s audio, heard as {lang}, {hit}/{expected.Count} Hindi words)\n   heard: {heard}");
            UnityEngine.Object.Destroy(clip);
        }
    }
    foreach (var kv in totals)
        UnityEngine.Debug.Log($"HV SUMMARY {kv.Key}: avg {kv.Value.ms / sentences.Length} ms per sentence, Whisper recognised {kv.Value.hit}/{kv.Value.all} Hindi words");
    stt.SetLanguage(ChatbotAI.Audio.SpeechInputController.InputLanguage.Auto);
    UnityEngine.Debug.Log($"HV_DONE wavs in {outDir}");
}

Run();
