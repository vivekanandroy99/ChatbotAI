var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject bootstrapGO = null;
foreach (var go in goRoots) { if (go.name == "GpuBootstrap") bootstrapGO = go; }
var bootstrap = bootstrapGO.GetComponent<ChatbotAI.Audio.StaggeredGpuBootstrap>();

var type = bootstrap.GetType();
var waitForLoadedField = type.GetField("waitForLoaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var secondStageField = type.GetField("secondStageGameObjects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

var waitForLoaded = waitForLoadedField.GetValue(bootstrap) as Whisper.WhisperManager[];
var secondStage = secondStageField.GetValue(bootstrap) as UnityEngine.GameObject[];

UnityEngine.Debug.Log($"FIELDS: waitForLoaded={(waitForLoaded == null ? "null" : waitForLoaded.Length.ToString())} secondStage={(secondStage == null ? "null" : secondStage.Length.ToString())}");
if (waitForLoaded != null) foreach (var w in waitForLoaded) UnityEngine.Debug.Log($"  waitForLoaded entry: {(w == null ? "NULL" : w.name)} loaded={(w == null ? "?" : w.IsLoaded.ToString())}");
if (secondStage != null) foreach (var s in secondStage) UnityEngine.Debug.Log($"  secondStage entry: {(s == null ? "NULL" : s.name)}");
