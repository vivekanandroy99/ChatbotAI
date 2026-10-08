// Play mode: asks a batch of (mostly Hindi) questions. The speech review recorder saves every
// sentence for TTSServer/speech_check.py; every frame, the audio level, mouth blendshapes,
// recognised vowel and body animation state go to %TEMP%\lipsync_trace.csv (for lag / mouth-in-
// silence analysis), and the time from the voice stopping to the body reaching Idle is logged.
UnityEngine.GameObject dialogueGO = null, outGO = null, avatarGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "SpeechOutput") outGO = go;
    if (go.name == "Avatar") avatarGO = go;
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var output = outGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
var source = outGO.GetComponent<UnityEngine.AudioSource>();
var lipSync = outGO.GetComponentInChildren<uLipSync.uLipSync>();
var face = ChatbotAI.Avatar.AvatarFace.FindFace(avatarGO);
var mesh = face.sharedMesh;
var body = avatarGO.GetComponent<ChatbotAI.Avatar.AvatarAnimator>();
int jaw = mesh.GetBlendShapeIndex("A25_Jaw_Open"), open = mesh.GetBlendShapeIndex("V_Open");
int wide = mesh.GetBlendShapeIndex("V_Wide"), round = mesh.GetBlendShapeIndex("V_Tight-O");
int press = mesh.GetBlendShapeIndex("V_Explosive"), lipTeeth = mesh.GetBlendShapeIndex("V_Dental_Lip"), teeth = mesh.GetBlendShapeIndex("V_Affricate");
string[] questions =
{
    "Altcore क्या है?",
    "Altscape क्या करता है?",
    "Altscape किन real estate projects के लिए बना है?",
    "क्या Altscape mobile पर भी चलता है?",
    "Altcore से कैसे संपर्क करें?",
    "Altscape sales team की कैसे मदद करता है?",
    "Altscape में buyers क्या क्या कर सकते हैं?",
    "नमस्ते! आप कौन हैं?",
    "आज मौसम कैसा है?",
    "What does Altscape do for developers?",
};
var csv = new System.Text.StringBuilder("q,t,audio,jaw,open,wide,round,press,lipteeth,teeth,logvol,phoneme,state,talkState\n");
var buffer = new float[512];

async void Run()
{
    for (int qi = 0; qi < questions.Length; qi++)
    {
        bool finished = false;
        System.Action onFinish = () => finished = true;
        output.OnSpeechFinished += onFinish;
        controller.Ask(questions[qi]);
        float t0 = UnityEngine.Time.realtimeSinceStartup, stoppedAt = -1, idleAt = -1;
        while (UnityEngine.Time.realtimeSinceStartup - t0 < 90)
        {
            await System.Threading.Tasks.Task.Yield();
            source.GetOutputData(buffer, 0);
            float rms = 0;
            foreach (float s in buffer) rms += s * s;
            rms = UnityEngine.Mathf.Sqrt(rms / buffer.Length);
            float lv = lipSync.result.rawVolume > 0 ? UnityEngine.Mathf.Log10(lipSync.result.rawVolume) : -9;
            bool talkState = body.CurrentState == "Talk";
            csv.Append($"{qi},{UnityEngine.Time.realtimeSinceStartup - t0:0.000},{rms:0.0000},{face.GetBlendShapeWeight(jaw):0.0},{face.GetBlendShapeWeight(open):0.0},{face.GetBlendShapeWeight(wide):0.0},{face.GetBlendShapeWeight(round):0.0},{face.GetBlendShapeWeight(press):0.0},{face.GetBlendShapeWeight(lipTeeth):0.0},{face.GetBlendShapeWeight(teeth):0.0},{lv:0.00},{lipSync.result.phoneme},{body.CurrentState},{talkState}\n");
            if (finished && stoppedAt < 0) stoppedAt = UnityEngine.Time.realtimeSinceStartup;
            if (stoppedAt > 0 && !talkState) { idleAt = UnityEngine.Time.realtimeSinceStartup; break; }
        }
        output.OnSpeechFinished -= onFinish;
        UnityEngine.Debug.Log($"MEASURE q{qi} {questions[qi]}: body out of Talk {(idleAt > 0 ? idleAt - stoppedAt : -1):0.00}s after the voice stopped");
        await System.Threading.Tasks.Task.Delay(800);
    }
    System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lipsync_trace.csv"), csv.ToString());
    UnityEngine.Debug.Log("MEASURE_DONE");
}
Run();
