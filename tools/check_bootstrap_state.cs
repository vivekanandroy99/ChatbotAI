var goRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
UnityEngine.GameObject bootstrapGO = null, ttsProcGO = null;
foreach (var go in goRoots)
{
    if (go.name == "GpuBootstrap") bootstrapGO = go;
    if (go.name == "TTSProcess") ttsProcGO = go;
}
var bootstrap = bootstrapGO.GetComponent<ChatbotAI.Audio.StaggeredGpuBootstrap>();
UnityEngine.Debug.Log($"BOOTSTRAP_STATE: goActive={bootstrapGO.activeInHierarchy} componentEnabled={bootstrap.enabled} ttsProcActive={ttsProcGO.activeInHierarchy} ttsProcActiveSelf={ttsProcGO.activeSelf}");
