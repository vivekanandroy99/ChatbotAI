// Play mode: checks recorded speech (SpeechReviewRecorder, SpeechReview/<session>/) - the latest
// session, flagged replies only if any were flagged with F8, else everything. Each sentence is
// transcribed by Whisper in its own language and compared with what was meant to be said:
// words that didn't come through are listed, so problem words can be found without listening.
UnityEngine.GameObject inputGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    if (go.name == "SpeechInput") inputGO = go;
var stt = inputGO.GetComponent<ChatbotAI.Audio.SpeechInputController>();
string root = System.IO.Path.Combine(System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName, "SpeechReview");
var sessions = System.IO.Directory.GetDirectories(root);
System.Array.Sort(sessions);
string session = sessions[sessions.Length - 1];
var lines = System.IO.File.ReadAllLines(System.IO.Path.Combine(session, "index.jsonl"));
var flagged = new System.Collections.Generic.HashSet<long>();
var entries = new System.Collections.Generic.List<Newtonsoft.Json.Linq.JObject>();
foreach (var line in lines)
{
    var e = Newtonsoft.Json.Linq.JObject.Parse(line);
    if ((long)e["flagged"] > 0) flagged.Add((long)e["flagged"]);
    else if (!string.IsNullOrEmpty((string)e["file"])) entries.Add(e);
}
var wordRx = new System.Text.RegularExpressions.Regex(@"[A-Za-z]+|[ऀ-ॿ]+");

async void Run()
{
    UnityEngine.Debug.Log($"REVIEW {session}: {entries.Count} sentences, flagged replies: {(flagged.Count > 0 ? string.Join(", ", flagged) : "none - checking all")}");
    int found = 0, total = 0;
    foreach (var e in entries)
    {
        if (flagged.Count > 0 && !flagged.Contains((long)e["reply"])) continue;
        string text = (string)e["text"];
        bool hindi = System.Text.RegularExpressions.Regex.IsMatch(text, @"[ऀ-ॿ]");
        stt.SetLanguage(hindi ? ChatbotAI.Audio.SpeechInputController.InputLanguage.Hindi : ChatbotAI.Audio.SpeechInputController.InputLanguage.English);
        var clip = ChatbotAI.Audio.WavUtility.ToAudioClip(System.IO.File.ReadAllBytes(System.IO.Path.Combine(session, (string)e["file"])));
        // Half a second of silence either side: Whisper's voice detection drops short clips that start at once.
        int pad = clip.frequency / 2;
        var samples = new float[clip.samples * clip.channels + 2 * pad];
        var raw = new float[clip.samples * clip.channels];
        clip.GetData(raw, 0);
        System.Array.Copy(raw, 0, samples, pad, raw.Length);
        var (heard, _) = await stt.TranscribeAsync(samples, clip.frequency, clip.channels);
        UnityEngine.Object.Destroy(clip);
        var got = new System.Collections.Generic.HashSet<string>();
        foreach (System.Text.RegularExpressions.Match m in wordRx.Matches(heard ?? "")) got.Add(m.Value.ToLowerInvariant());
        var missing = new System.Collections.Generic.List<string>();
        int n = 0;
        foreach (System.Text.RegularExpressions.Match m in wordRx.Matches(text))
        {
            n++;
            if (got.Contains(m.Value.ToLowerInvariant())) found++;
            else missing.Add(m.Value);
        }
        total += n;
        UnityEngine.Debug.Log($"REVIEW {e["file"]} [{e["voice"]}]\n  meant: {text}\n  heard: {heard}\n  not heard as written: {string.Join(", ", missing)}");
    }
    stt.SetLanguage(ChatbotAI.Audio.SpeechInputController.InputLanguage.Auto);
    UnityEngine.Debug.Log($"REVIEW_DONE {100 * found / System.Math.Max(total, 1)}% of words heard back (English words said in Hindi are often written in Devanagari by the recogniser - those count as misses)");
}
Run();
