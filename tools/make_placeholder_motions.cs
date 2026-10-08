// Edit mode: two stand-in body motions made from muscle values (no motion files needed): Placeholder Idle (arms
// relaxed at the sides, slow breathing, a little sway) and Placeholder Talk (the same with small head nods and hand
// movement). For characters whose exported motions aren't usable yet - replace them with real idles/talks when those
// arrive. Writes Assets/Characters/Placeholder Idle.anim / Placeholder Talk.anim. Safe to re-run (overwrites).
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return "PLACEHOLDER: in Play mode - not changed";
const string outDir = "Assets/Characters";
float armDown = -0.62f, armForward = 0.28f, forearm = 0.62f;   // muscle values (see Unity's Humanoid muscle ranges)
var log = new System.Text.StringBuilder("PLACEHOLDER:");

var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/ActorCore Model/Actors/Ethan/Prefabs/Ethan.prefab");
var inst = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(prefab);
inst.hideFlags = UnityEngine.HideFlags.DontSave;
var anim = inst.GetComponentInChildren<UnityEngine.Animator>();
var handler = new UnityEngine.HumanPoseHandler(anim.avatar, anim.transform);
var rest = new UnityEngine.HumanPose();
handler.GetHumanPose(ref rest);
UnityEngine.Object.DestroyImmediate(inst);

int Muscle(string name) => System.Array.IndexOf(UnityEngine.HumanTrait.MuscleName, name);

UnityEngine.AnimationClip Make(string name, float seconds, bool talk)
{
    var clip = new UnityEngine.AnimationClip { name = name, frameRate = 30 };
    float[] m = (float[])rest.muscles.Clone();
    foreach (string side in new[] { "Left", "Right" })
    {
        m[Muscle($"{side} Arm Down-Up")] = armDown;
        m[Muscle($"{side} Arm Front-Back")] = armForward;
        m[Muscle($"{side} Forearm Stretch")] = forearm;
        m[Muscle($"{side} Forearm Twist In-Out")] = 0.25f;
        m[Muscle($"{side} Shoulder Down-Up")] = -0.15f;
        m[Muscle($"{side} Hand Down-Up")] = 0.1f;
    }
    // Moving muscles: (name, amplitude, cycles per clip, phase)
    var moving = new System.Collections.Generic.List<(string muscle, float amp, int cycles, float phase)>
    {
        ("Chest Front-Back", 0.04f, 1, 0f),          // breathing
        ("Spine Front-Back", 0.02f, 1, 0.25f),
        ("Spine Left-Right", 0.025f, 1, 0.5f),       // slow weight sway
        ("Neck Nod Down-Up", 0.03f, 1, 0.1f),
        ("Head Turn Left-Right", 0.04f, 1, 0.7f),
    };
    if (talk)
    {
        moving.Add(("Head Nod Down-Up", 0.09f, 4, 0f));
        moving.Add(("Head Tilt Left-Right", 0.05f, 2, 0.3f));
        moving.Add(("Right Forearm Stretch", 0.10f, 2, 0f));
        moving.Add(("Right Arm Front-Back", 0.06f, 2, 0.2f));
        moving.Add(("Left Forearm Stretch", 0.07f, 3, 0.5f));
    }
    for (int i = 0; i < m.Length; i++)
    {
        string muscle = UnityEngine.HumanTrait.MuscleName[i];
        var curve = new UnityEngine.AnimationCurve();
        var motion = moving.Find(x => x.muscle == muscle);
        int steps = motion.muscle == null ? 1 : 24;
        for (int s = 0; s <= steps; s++)
        {
            float t = seconds * s / steps;
            float v = m[i];
            if (motion.muscle != null) v += motion.amp * UnityEngine.Mathf.Sin(2 * UnityEngine.Mathf.PI * (motion.cycles * s / (float)steps + motion.phase));
            curve.AddKey(new UnityEngine.Keyframe(t, v));
        }
        for (int k = 0; k < curve.length; k++) curve.SmoothTangents(k, 0f);
        UnityEditor.AnimationUtility.SetEditorCurve(clip, UnityEditor.EditorCurveBinding.FloatCurve("", typeof(UnityEngine.Animator), muscle), curve);
    }
    // Body stays where it is.
    void Const(string prop, float v) => UnityEditor.AnimationUtility.SetEditorCurve(clip, UnityEditor.EditorCurveBinding.FloatCurve("", typeof(UnityEngine.Animator), prop), UnityEngine.AnimationCurve.Constant(0, seconds, v));
    Const("RootT.x", 0); Const("RootT.y", rest.bodyPosition.y); Const("RootT.z", 0);
    Const("RootQ.x", 0); Const("RootQ.y", 0); Const("RootQ.z", 0); Const("RootQ.w", 1);
    var settings = UnityEditor.AnimationUtility.GetAnimationClipSettings(clip);
    settings.loopTime = true;
    UnityEditor.AnimationUtility.SetAnimationClipSettings(clip, settings);
    string path = $"{outDir}/{name}.anim";
    UnityEditor.AssetDatabase.DeleteAsset(path);
    UnityEditor.AssetDatabase.CreateAsset(clip, path);
    log.Append($" {path} ({clip.length:0.0}s, humanoid {clip.humanMotion});");
    return clip;
}
var idle = Make("Placeholder Idle", 6f, false);
var talk = Make("Placeholder Talk", 4f, true);
UnityEditor.AssetDatabase.SaveAssets();
return log.ToString();
