// Studio lighting for the working scene (AltcoreBot_v4): the studio environment material (StageBackdrop draws a
// softbox studio into it in Play mode - ambient light + reflections) and sharper shadows.
// Shadows: the URP asset is project-wide, so v1-v3 scenes get the sharper shadows too.
// Run with: unity command eval_file tools/setup_studio_lighting.cs
const string scenePath = "Assets/Scenes/AltcoreBot_v5.unity";
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (scene.path != scenePath)
{
    if (scene.isDirty) return "the open scene has unsaved changes - open " + scenePath + " first";
    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
}
var log = new System.Text.StringBuilder();

// 1. The environment material (Skybox/Panoramic, latitude-longitude): kept as an asset so builds include the shader.
const string envPath = "Assets/Stage/Studio Environment.mat";
var env = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(envPath);
if (!env)
{
    env = new UnityEngine.Material(UnityEngine.Shader.Find("Skybox/Panoramic"));
    env.SetFloat("_Mapping", 1f);      // latitude-longitude layout
    env.SetFloat("_ImageType", 0f);    // 360 degrees
    UnityEditor.AssetDatabase.CreateAsset(env, envPath);
    log.Append("created " + envPath + "; ");
}
var backdrop = UnityEngine.Object.FindAnyObjectByType<ChatbotAI.Avatar.StageBackdrop>(UnityEngine.FindObjectsInactive.Include);
if (!backdrop) return "no Stage in the scene - run tools/setup_stage.cs first";
var so = new UnityEditor.SerializedObject(backdrop);
so.FindProperty("environmentSkybox").objectReferenceValue = env;
so.ApplyModifiedProperties();

// 2. Sharper shadows: 4096 shadow map spent on the first 12 m (the character stands 1-6 m from the camera), 2 cascades.
var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
UnityEditor.Undo.RecordObject(rp, "Sharper shadows");
log.Append($"shadows were {rp.mainLightShadowmapResolution} px / {rp.shadowDistance} m / {rp.shadowCascadeCount} cascades / normal bias {rp.shadowNormalBias}; ");
rp.mainLightShadowmapResolution = 4096;
rp.shadowDistance = 12f;
rp.shadowCascadeCount = 2;
rp.cascade2Split = 0.5f;
rp.shadowNormalBias = 0.3f;
UnityEditor.EditorUtility.SetDirty(rp);

// 3. The key light: highest soft-shadow quality, a tighter bias (shadows sit closer under chin, collar and hair).
foreach (var l in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsSortMode.None))
{
    if (l.type != UnityEngine.LightType.Directional || (l.hideFlags & UnityEngine.HideFlags.DontSave) != 0) continue;
    UnityEditor.Undo.RecordObject(l, "Sharper shadows");
    l.shadows = UnityEngine.LightShadows.Soft;
    l.shadowNormalBias = 0.25f;
    l.shadowBias = 0.04f;
    var data = l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
    if (data)
    {
        UnityEditor.Undo.RecordObject(data, "Sharper shadows");
        data.softShadowQuality = UnityEngine.Rendering.Universal.SoftShadowQuality.High;
        UnityEditor.EditorUtility.SetDirty(data);
    }
    UnityEditor.EditorUtility.SetDirty(l);
    log.Append($"key light '{l.name}' soft shadows High; ");
}
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return log.Append("saved " + scene.path).ToString();
