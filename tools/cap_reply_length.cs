var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.GameObject dialogueGO = null;
foreach (var go in scene.GetRootGameObjects()) if (go.name == "DialogueBrain") dialogueGO = go;
var agent = dialogueGO.GetComponent<LLMUnity.LLMAgent>();
agent.numPredict = 256;
UnityEditor.EditorUtility.SetDirty(agent);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log($"CAP_OK numPredict={agent.numPredict} temperature={agent.temperature}");
