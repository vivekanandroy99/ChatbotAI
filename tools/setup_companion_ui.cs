// Edit mode: adds the companion screen (UI Toolkit, Assets/UI/Companion) to the open scene and switches off the
// old test UI (Canvas, TestHarness, Avatar Picker UI) - those stay in the scene, inactive, so they can come back.
// Safe to re-run.  Run with: unity command eval_file tools/setup_companion_ui.cs
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return "COMPANION: in Play mode - not changed";
const string folder = "Assets/UI/Companion";
var report = new System.Text.StringBuilder("COMPANION:\n");

UnityEditor.AssetDatabase.Refresh();

string panelPath = folder + "/CompanionPanel.asset";
var panel = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(panelPath);
if (!panel)
{
    panel = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
    UnityEditor.AssetDatabase.CreateAsset(panel, panelPath);
    report.AppendLine("Created " + panelPath);
}
panel.themeStyleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>(folder + "/CompanionTheme.tss");
panel.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
panel.referenceResolution = new UnityEngine.Vector2Int(1280, 720);
panel.screenMatchMode = UnityEngine.UIElements.PanelScreenMatchMode.MatchWidthOrHeight;
panel.match = 1f;   // scale with the window's height
panel.sortingOrder = 10;
UnityEditor.EditorUtility.SetDirty(panel);

var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(folder + "/Companion.uxml");
if (!tree) return "COMPANION: Companion.uxml didn't import";

var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject Find(string name)
{
    foreach (var go in scene.GetRootGameObjects())
        if (go.name == name) return go;
    return null;
}

var screen = Find("Companion UI");
if (!screen)
{
    screen = new UnityEngine.GameObject("Companion UI");
    UnityEditor.Undo.RegisterCreatedObjectUndo(screen, "Add companion UI");
    report.AppendLine("Added 'Companion UI' to the scene");
}
var doc = screen.GetComponent<UnityEngine.UIElements.UIDocument>();
if (!doc) doc = UnityEditor.Undo.AddComponent<UnityEngine.UIElements.UIDocument>(screen);
doc.panelSettings = panel;
doc.visualTreeAsset = tree;
var ui = screen.GetComponent<ChatbotAI.UI.CompanionUI>();
if (!ui) ui = UnityEditor.Undo.AddComponent<ChatbotAI.UI.CompanionUI>(screen);
var so = new UnityEditor.SerializedObject(ui);
so.FindProperty("avatarCamera").objectReferenceValue = UnityEngine.Camera.main;
so.ApplyModifiedProperties();

foreach (string old in new[] { "Canvas", "TestHarness", "Avatar Picker UI" })
{
    var go = Find(old);
    if (go && go.activeSelf)
    {
        UnityEditor.Undo.RecordObject(go, "Hide old UI");
        go.SetActive(false);
        report.AppendLine($"Switched off '{old}' (kept in the scene)");
    }
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
report.AppendLine("Scene saved.");
return report.ToString();
