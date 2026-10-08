var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject bootstrapGO = null;
foreach (var go in goRoots) { if (go.name == "GpuBootstrap") bootstrapGO = go; }
var bootstrap = bootstrapGO.GetComponent<ChatbotAI.Audio.StaggeredGpuBootstrap>();

var type = bootstrap.GetType();
var method = type.GetMethod("ActivateDeferred", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
UnityEngine.Debug.Log("FORCE_ACTIVATE: calling ActivateDeferred() manually");
method.Invoke(bootstrap, null);
