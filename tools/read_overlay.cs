UnityEngine.GameObject canvasGO = null;
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (go.name == "Canvas") canvasGO = go;
var overlay = canvasGO.transform.Find("StartupOverlay");
var status = overlay.Find("Status").GetComponent<UnityEngine.UI.Text>().text;
var fill = overlay.Find("Bar/Fill").GetComponent<UnityEngine.RectTransform>().anchorMax.x;
UnityEngine.Debug.Log($"OVERLAY: active={overlay.gameObject.activeSelf} bar={fill:P0} text='{status}'");
