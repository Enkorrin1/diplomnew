using UnityEngine;
using UnityEngine.SceneManagement;
using RogueDrive.Gameplay.Hub;

namespace RogueDrive.Gameplay.Coop
{
    // Only active during a network game; scene assets remain usable in solo mode.
    public sealed class CoopScenePresentation : MonoBehaviour
    {
        private CoopSessionUI crewUi;
        private float nextScan;
        private GameObject crewUiPrefab;
        public void Configure(GameObject prefab) => crewUiPrefab = prefab;

        private void LateUpdate()
        {
            var session = CoopSession.Instance;
            if (session == null || session.Manager == null || !session.Manager.IsListening)
            {
                if (crewUi != null) Destroy(crewUi.gameObject);
                return;
            }
            if (SceneManager.GetActiveScene().name == "MainMenuScene") return;
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + .25f;
            Apply();
        }

        public void Apply()
        {
            // Offline scripts may create their canvases in Start, so repeat this small
            // presentation pass after scene load. Never disable a network actor.
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (b is BunkerPrologueCutscene || b is GarageSceneExitCinematic || b is GarageExitTrigger ||
                    b is StageDirectStart || b is GarageDriveOutController ||
                    b is RogueDrive.UI.PauseMenuUI || b is GarageInteractionUI || b is RogueDrive.UI.VehicleDashboardPanelsUI)
                {
                    b.StopAllCoroutines();
                    b.enabled = false;
                }
                if ((b is GaragePlayerController || b is ArcadeCarController || b is VehiclePassengerEntry) &&
                    b.GetComponentInParent<CoopVehicle>() == null && b.GetComponentInParent<CoopPlayer>() == null)
                    b.gameObject.SetActive(false);
            }
            var interfaces = FindObjectsByType<CoopSessionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (interfaces.Length == 0 && crewUiPrefab != null)
            {
                var root = Instantiate(crewUiPrefab);
                root.name = "CrewUI";
                interfaces = root.GetComponentsInChildren<CoopSessionUI>(true);
            }
            foreach (var ui in interfaces)
            {
                if (crewUi == null)
                {
                    crewUi = ui;
                    var overlay = ui.GetComponent<Canvas>();
                    if (overlay != null) overlay.renderMode = RenderMode.ScreenSpaceOverlay;
                    ui.transform.SetParent(null);
                    DontDestroyOnLoad(ui.gameObject);
                    ui.gameObject.SetActive(true);
                }
                else if (ui != crewUi) ui.gameObject.SetActive(false);
            }
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                canvas.enabled = crewUi != null && canvas.GetComponentInParent<CoopSessionUI>() == crewUi;

            Time.timeScale = 1f;
        }
    }
}
