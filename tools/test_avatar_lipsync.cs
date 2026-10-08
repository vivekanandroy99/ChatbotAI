// Play mode: asks a question and samples the avatar while it answers - mouth blendshape
// weights (lip-sync), blinks, and whether the body is in its Talk animation. Saves a
// screenshot mid-sentence to %TEMP%\avatar_speaking.png.
UnityEngine.GameObject dialogueGO = null, outGO = null, avatarGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "SpeechOutput") outGO = go;
    if (go.name == "Avatar") avatarGO = go;
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var output = outGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
var face = ChatbotAI.Avatar.AvatarFace.FindFace(avatarGO);
var mesh = face.sharedMesh;
var animator = avatarGO.GetComponentInChildren<UnityEngine.Animator>();
int jaw = ChatbotAI.Avatar.AvatarFace.IndexOf(mesh, "A25_Jaw_Open"), open = ChatbotAI.Avatar.AvatarFace.IndexOf(mesh, "V_Open");
int wide = ChatbotAI.Avatar.AvatarFace.IndexOf(mesh, "V_Wide"), round = ChatbotAI.Avatar.AvatarFace.IndexOf(mesh, "V_Tight-O");
int blink = ChatbotAI.Avatar.AvatarFace.IndexOf(mesh, "Eye_Blink_L");
string[] questions = { "What is Altcore?", "Altscape क्या है?" };

async void Run()
{
    foreach (string q in questions)
    {
        bool started = false, finished = false, shot = false;
        System.Action onStart = () => started = true;
        System.Action onFinish = () => finished = true;
        output.OnSpeechStarted += onStart;
        output.OnSpeechFinished += onFinish;
        controller.Ask(q);
        float t0 = UnityEngine.Time.realtimeSinceStartup, maxJaw = 0, sumJaw = 0;
        int samples = 0, moving = 0, talkFrames = 0, blinks = 0, changes = 0;
        bool wasClosed = true, eyeClosed = false;
        string lastShape = "";
        while (!finished && UnityEngine.Time.realtimeSinceStartup - t0 < 90)
        {
            await System.Threading.Tasks.Task.Delay(40);
            if (!started) continue;
            float j = face.GetBlendShapeWeight(jaw);
            maxJaw = UnityEngine.Mathf.Max(maxJaw, j);
            sumJaw += j;
            samples++;
            if (j > 5) moving++;
            // Which mouth shape leads right now - changes show it follows the vowels, not just volume.
            float o = face.GetBlendShapeWeight(open), w = face.GetBlendShapeWeight(wide), r = face.GetBlendShapeWeight(round);
            string lead = j < 5 ? "closed" : o >= w && o >= r ? "A" : w >= r ? "I/E" : "O/U";
            if (lead != lastShape) { changes++; lastShape = lead; }
            bool closed = face.GetBlendShapeWeight(blink) > 80;
            if (closed && !eyeClosed) blinks++;
            eyeClosed = closed;
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Talk")) talkFrames++;
            if (!shot && samples == 40)
            {
                UnityEngine.ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), q.StartsWith("What") ? "avatar_speaking_en.png" : "avatar_speaking_hi.png"));
                shot = true;
            }
        }
        output.OnSpeechStarted -= onStart;
        output.OnSpeechFinished -= onFinish;
        UnityEngine.Debug.Log($"LIPSYNC {q}: {samples * 0.04f:0.0}s sampled, jaw max {maxJaw:0} avg {(samples > 0 ? sumJaw / samples : 0):0}, mouth moving {100f * moving / UnityEngine.Mathf.Max(samples, 1):0}% of the time, {changes} mouth-shape changes, {blinks} blinks, Talk animation {100f * talkFrames / UnityEngine.Mathf.Max(samples, 1):0}% of the time");
        await System.Threading.Tasks.Task.Delay(3000);
        UnityEngine.Debug.Log($"LIPSYNC after speaking: jaw {face.GetBlendShapeWeight(jaw):0}, state {(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") ? "Idle" : "Talk")}");
    }
    UnityEngine.Debug.Log("LIPSYNC_DONE");
}
Run();
