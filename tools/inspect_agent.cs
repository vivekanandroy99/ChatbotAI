UnityEngine.GameObject dialogueGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == "DialogueBrain") dialogueGO = go;
var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();
var sb = new System.Text.StringBuilder("AGENT_PARAMS:");
foreach (var f in agent.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy))
{
    if (f.FieldType == typeof(float) || f.FieldType == typeof(int)) sb.Append($" {f.Name}={f.GetValue(agent)}");
}
UnityEngine.Debug.Log(sb.ToString());
