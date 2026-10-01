using UnityEngine;
using RogueDrive.UI;
using System.Collections.Generic;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>Authored entry to Stage1. Preloaded roots sleep until the cut.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class StageGarageArrival : MonoBehaviour
    {
        [SerializeField] private GameObject worldRoot;
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private GameObject standalonePlayerRoot;
        [SerializeField] private Transform gatePlane;
        [SerializeField] private Transform exteriorShot;
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;

        public Transform GatePlane => gatePlane;
        public Transform ExteriorShot => exteriorShot;
        private readonly List<GameObject> preloadedExtraRoots = new List<GameObject>();

        private void Awake()
        {
            // Artists may save Stage1 with its geometry visible. Runtime preloading must
            // explicitly suspend it instead of relying on the scene's saved active flags.
            if (GarageSceneExitCinematic.Pending != null)
            {
                SuspendForGarage();
                return;
            }
            worldRoot.SetActive(true);
            standalonePlayerRoot.SetActive(true);
            var prepared = standalonePlayerRoot.GetComponent<StageDirectStart>();
            if (prepared != null)
            {
                BeginGameplay(prepared.Car);
                PrepareEnvironment();
            }
            else gameplayRoot.SetActive(true);
        }

        public bool IsConfigured => worldRoot != null && gameplayRoot != null
            && standalonePlayerRoot != null && gatePlane != null && exteriorShot != null;

        public void SuspendForGarage()
        {
            if (worldRoot != null) worldRoot.SetActive(false);
            if (gameplayRoot != null) gameplayRoot.SetActive(false);
            if (standalonePlayerRoot != null) standalonePlayerRoot.SetActive(false);
            // Also cover new top-level scenery that has not yet been grouped under Stage_World.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root == transform.root.gameObject || root == worldRoot || root == gameplayRoot
                    || root == standalonePlayerRoot || !root.activeSelf) continue;
                preloadedExtraRoots.Add(root);
                root.SetActive(false);
            }
        }

        public void Reveal(Transform sourceGate)
        {
            // Match the actual door pose, including an interrupted opening animation.
            var left = sourceGate.Find("Door_Left_Hinge");
            var right = sourceGate.Find("Door_Right_Hinge");
            if (leftDoor != null && left != null) leftDoor.localRotation = left.localRotation;
            if (rightDoor != null && right != null) rightDoor.localRotation = right.localRotation;
            worldRoot.SetActive(true);
            foreach (var root in preloadedExtraRoots)
                if (root != null) root.SetActive(true);
            preloadedExtraRoots.Clear();
        }

        public void BeginGameplay(ArcadeCarController car)
        {
            var run = gameplayRoot.GetComponentInChildren<GameRunController>(true);
            car.Configure(run);
            foreach (var session in gameplayRoot.GetComponentsInChildren<GameSessionCoordinator>(true))
                session.BindArrivingCar(car);
            foreach (var generator in gameplayRoot.GetComponentsInChildren<ProceduralTrackGenerator>(true))
                generator.BindArrivingCar(car.transform);
            foreach (var hud in gameplayRoot.GetComponentsInChildren<PrototypeHud>(true))
                hud.Configure(run, car);
            foreach (var view in gameplayRoot.GetComponentsInChildren<SceneUIView>(true))
                view.BindArrivingCar(car);
            CampaignMapModal.SelectedStartSector = 1;
            car.enabled = true; // Reset travel origin after the scene-space translation.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var entry = root.GetComponent<JourneyStreamEntry>();
                if (entry != null) { entry.Begin(car); break; }
            }
            gameplayRoot.SetActive(true);
        }

        public void PrepareEnvironment()
        {
            gameplayRoot.GetComponentInChildren<ProceduralTrackGenerator>(true)?.PrepareArrivalEnvironment();
        }
    }
}
