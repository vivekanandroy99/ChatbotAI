// Adds the stage backdrop to the working scene (AltcoreBot_v3): a "Stage" object with StageBackdrop, the default
// Stage Look asset (Assets/Stage/Studio Stage.asset) and the StreamingAssets/Stage Pictures folder.
// Only adds what's missing. Run with: unity command eval_file tools/setup_stage.cs
const string scenePath = "Assets/Scenes/AltcoreBot_v5.unity";
const string lookPath = "Assets/Stage/Studio Stage.asset";
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (scene.path != scenePath)
{
    if (scene.isDirty) return "the open scene has unsaved changes - open " + scenePath + " first";
    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
}
var log = new System.Text.StringBuilder();

var look = UnityEditor.AssetDatabase.LoadAssetAtPath<ChatbotAI.Avatar.StageLook>(lookPath);
if (!look)
{
    look = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Avatar.StageLook>();
    UnityEditor.AssetDatabase.CreateAsset(look, lookPath);
    log.Append("created " + lookPath + "; ");
}

const string paperPath = "Assets/Stage/Studio Paper.mat";
var paper = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(paperPath);
if (!paper)
{
    paper = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit"));
    paper.SetFloat("_Smoothness", 0.15f);
    UnityEditor.AssetDatabase.CreateAsset(paper, paperPath);
    log.Append("created " + paperPath + "; ");
}
// Colours added after the asset was made (paper, background light) start empty - fill them from the defaults.
if (look.dark.paper.a < 0.01f || look.light.paper.a < 0.01f)
{
    var defaults = UnityEngine.ScriptableObject.CreateInstance<ChatbotAI.Avatar.StageLook>();
    look.dark = defaults.dark;
    look.light = defaults.light;
    UnityEditor.EditorUtility.SetDirty(look);
    UnityEngine.Object.DestroyImmediate(defaults);
    log.Append("filled the studio colours; ");
}

string pictures =System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, ChatbotAI.Avatar.StageBackdrop.PicturesFolder);
if (!System.IO.Directory.Exists(pictures))
{
    System.IO.Directory.CreateDirectory(pictures);
    System.IO.File.WriteAllText(System.IO.Path.Combine(pictures, "README.txt"),
        "Pictures for the stage behind the avatars (menu > Camera & Stage > Backdrop > Picture).\r\n" +
        "Put .jpg or .png files here - an office, a lobby, brand artwork. Landscape or portrait both work: the picture\r\n" +
        "fills the screen (cropped, never stretched) and is shown softly out of focus. About 2000-4000 px is plenty.\r\n" +
        "In a built app this folder is <App>_Data/StreamingAssets/Stage Pictures - pictures can be added there too.\r\n");
    log.Append("created StreamingAssets/Stage Pictures; ");
}

var backdrop = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Avatar.StageBackdrop>(UnityEngine.FindObjectsInactive.Include);
if (!backdrop)
{
    var go = new UnityEngine.GameObject("Stage");
    UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Add stage");
    backdrop = go.AddComponent<ChatbotAI.Avatar.StageBackdrop>();
    log.Append("added Stage object; ");
}
var so = new UnityEditor.SerializedObject(backdrop);
so.FindProperty("defaultLook").objectReferenceValue = look;
so.FindProperty("paperMaterial").objectReferenceValue = paper;

// Premium look: a global post-processing volume (colour grading, soft bloom, vignette, background blur) and the
// camera set to render post-processing with SMAA edges (StageBackdrop's Display & performance settings switch them).
const string postPath = "Assets/Stage/Studio Post.asset";
var post = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(postPath);
if (!post)
{
    post = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
    UnityEditor.AssetDatabase.CreateAsset(post, postPath);
    var tone = post.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
    tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);
    var grade = post.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
    grade.contrast.Override(10f);
    grade.saturation.Override(6f);
    grade.postExposure.Override(0.1f);
    var balance = post.Add<UnityEngine.Rendering.Universal.WhiteBalance>(true);
    balance.temperature.Override(4f);
    var bloom = post.Add<UnityEngine.Rendering.Universal.Bloom>(true);
    bloom.intensity.Override(0.25f);
    bloom.threshold.Override(1.0f);
    bloom.scatter.Override(0.6f);
    var vignette = post.Add<UnityEngine.Rendering.Universal.Vignette>(true);
    vignette.intensity.Override(0.22f);
    vignette.smoothness.Override(0.45f);
    var dof = post.Add<UnityEngine.Rendering.Universal.DepthOfField>(true);
    dof.mode.Override(UnityEngine.Rendering.Universal.DepthOfFieldMode.Gaussian);
    dof.gaussianStart.Override(4f);
    dof.gaussianEnd.Override(10f);
    foreach (var c in post.components) UnityEditor.AssetDatabase.AddObjectToAsset(c, post);
    UnityEditor.EditorUtility.SetDirty(post);
    log.Append("created " + postPath + "; ");
}
var volume = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>(UnityEngine.FindObjectsInactive.Include);
if (!volume)
{
    var vgo = new UnityEngine.GameObject("Post Processing");
    UnityEditor.Undo.RegisterCreatedObjectUndo(vgo, "Add post processing");
    volume = vgo.AddComponent<UnityEngine.Rendering.Volume>();
    volume.isGlobal = true;
    log.Append("added Post Processing volume; ");
}
volume.sharedProfile = post;
so.FindProperty("postVolume").objectReferenceValue = volume;
var camData = UnityEngine.Camera.main ? UnityEngine.Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
if (camData)
{
    camData.renderPostProcessing = true;
    camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    camData.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
    UnityEditor.EditorUtility.SetDirty(camData);
}
so.FindProperty("shader").objectReferenceValue = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>("Assets/Stage/StageBackdrop.shader");
so.FindProperty("stageCamera").objectReferenceValue = UnityEngine.Camera.main;
so.FindProperty("avatarStage").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Avatar.AvatarStage>();
so.ApplyModifiedProperties();
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return log.Append("wired and saved " + scene.path).ToString();
