// Transcribes every WAV in %TEMP%\f5_variants with the app's Whisper (forced Hindi),
// plus Kokoro's hf_alpha saying the same sentence, for a side-by-side clarity check.
UnityEngine.GameObject inputGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    if (go.name == "SpeechInput") inputGO = go;
var stt = inputGO.GetComponent<ChatbotAI.Audio.SpeechInputController>();
string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "f5_variants");
string sentence = "Basic Plan की कीमत 499 रुपये प्रति महीना है, और आप इसे कभी भी cancel कर सकते हैं।";

async System.Threading.Tasks.Task Transcribe(string name, byte[] wav)
{
    var clip = ChatbotAI.Audio.WavUtility.ToAudioClip(wav);
    var samples = new float[clip.samples * clip.channels];
    clip.GetData(samples, 0);
    var (heard, _) = await stt.TranscribeAsync(samples, clip.frequency, clip.channels);
    UnityEngine.Debug.Log($"TW {name} ({clip.length:0.0}s): {heard}");
    UnityEngine.Object.Destroy(clip);
}

async void Run()
{
    stt.SetLanguage(ChatbotAI.Audio.SpeechInputController.InputLanguage.Hindi);
    UnityEngine.Debug.Log($"TW expected: {sentence}");
    using (var req = new UnityEngine.Networking.UnityWebRequest("http://127.0.0.1:8765/speak", "POST"))
    {
        req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{\"text\":\"" + sentence + "\",\"voice\":\"hf_alpha\",\"speed\":1}"));
        req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        var op = req.SendWebRequest();
        while (!op.isDone) await System.Threading.Tasks.Task.Yield();
        await Transcribe("kokoro_hf_alpha", req.downloadHandler.data);
    }
    foreach (var path in System.IO.Directory.GetFiles(dir, "*.wav"))
        await Transcribe(System.IO.Path.GetFileNameWithoutExtension(path), System.IO.File.ReadAllBytes(path));
    stt.SetLanguage(ChatbotAI.Audio.SpeechInputController.InputLanguage.Auto);
    UnityEngine.Debug.Log("TW_DONE");
}
Run();
