using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.UI
{
    /// <summary>Apply the kit after scene Start, without changing UI events or game state.</summary>
    public sealed class LowPolyCanvasTheme : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            nextScan = 0;
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.isRootCanvas && canvas.GetComponent<LowPolyCanvasTheme>() == null)
                        canvas.gameObject.AddComponent<LowPolyCanvasTheme>();
        }

        IEnumerator Start()
        {
            yield return null;
            LowPolyUi.Apply(transform);
        }

        static float nextScan;
        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1f;
            // Inventory, service and combat canvases can be created after sceneLoaded.
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace && canvas.GetComponent<LowPolyCanvasTheme>() == null)
                    canvas.gameObject.AddComponent<LowPolyCanvasTheme>();
        }
    }
}
