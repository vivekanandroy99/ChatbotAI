// Play mode: asks a question and checks the eyes (angle between each eye and the camera,
// glances), the jaw, and that the speech review recorder saved the sentences. Screenshot to
// %TEMP%\avatar_eyes.png.
UnityEngine.GameObject dialogueGO = null, avatarGO = null, outGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
{
    if (go.name == "DialogueBrain") dialogueGO = go;
    if (go.name == "Avatar") avatarGO = go;
    if (go.name == "SpeechOutput") outGO = go;
}
var controller = dialogueGO.GetComponent<ChatbotAI.Dialogue.DialogueController>();
var output = outGO.GetComponent<ChatbotAI.Audio.SpeechOutputController>();
var eyes = avatarGO.GetComponent<ChatbotAI.Avatar.AvatarEyes>();
var animator = avatarGO.GetComponentInChildren<UnityEngine.Animator>();
var leftEye = animator.GetBoneTransform(UnityEngine.HumanBodyBones.LeftEye);
var fwdField = typeof(ChatbotAI.Avatar.AvatarEyes).GetField("leftForwardInEye", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var face = ChatbotAI.Avatar.AvatarFace.FindFace(avatarGO);
int jaw = ChatbotAI.Avatar.AvatarFace.IndexOf(face.sharedMesh, "A25_Jaw_Open");
var cam = UnityEngine.Camera.main.transform;

async void Run()
{
    bool finished = false;
    System.Action onFinish = () => finished = true;
    output.OnSpeechFinished += onFinish;
    controller.Ask("What is Altcore?");
    float t0 = UnityEngine.Time.realtimeSinceStartup, maxJaw = 0;
    int n = 0, onTarget = 0, glancing = 0, shot = 0;
    var angles = new System.Collections.Generic.List<float>();
    UnityEngine.Quaternion last = leftEye.localRotation;
    int moved = 0;
    while (!finished && UnityEngine.Time.realtimeSinceStartup - t0 < 60)
    {
        await System.Threading.Tasks.Task.Delay(50);
        var fwd = leftEye.rotation * (UnityEngine.Vector3)fwdField.GetValue(eyes);
        float angle = UnityEngine.Vector3.Angle(fwd, cam.position - leftEye.position);
        n++;
        if (angle < 4f) onTarget++;
        if (angle > 7f) glancing++;
        if (UnityEngine.Quaternion.Angle(last, leftEye.localRotation) > 0.3f) moved++;
        last = leftEye.localRotation;
        maxJaw = UnityEngine.Mathf.Max(maxJaw, face.GetBlendShapeWeight(jaw));
        if (n == 60 && shot++ == 0) UnityEngine.ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "avatar_eyes.png"));
    }
    output.OnSpeechFinished -= onFinish;
    UnityEngine.Debug.Log($"EYES looking at the camera (within 4 deg) {100 * onTarget / n}% of the time, glancing away {100 * glancing / n}%, eye moved in {100 * moved / n}% of samples; jaw max {maxJaw:0}");
    string root = System.IO.Path.Combine(System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName, "SpeechReview");
    var latest = System.Linq.Enumerable.LastOrDefault(System.Linq.Enumerable.OrderBy(System.IO.Directory.GetDirectories(root), d => d));
    UnityEngine.Debug.Log($"EYES review recorder: {System.IO.Directory.GetFiles(latest, "*.wav").Length} sentence WAVs in {latest}");
    UnityEngine.Debug.Log("EYES_DONE");
}
Run();
