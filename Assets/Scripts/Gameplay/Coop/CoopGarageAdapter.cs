using System.Collections;
using UnityEngine;
using Unity.Netcode;
using RogueDrive.Gameplay.Hub;

namespace RogueDrive.Gameplay.Coop
{
    /// <summary>
    /// Adapts GarageScene for co-op multiplayer session.
    /// When CoopSession is active, hides single-player offline player and offline car,
    /// and activates co-op UI and quest systems.
    /// In single-player mode, leaves offline garage components completely intact.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CoopGarageAdapter : MonoBehaviour
    {
        [Header("Single-Player Objects to Hide in Co-op")]
        [SerializeField] private GameObject fpPlayer;
        [SerializeField] private GameObject offlineCar;
        [SerializeField] private GameObject singlePlayerCanvas;
        [SerializeField] private BunkerPrologueCutscene prologueCutscene;

        [Header("Co-op Objects to Enable")]
        [SerializeField] private GameObject crewUi;
        [SerializeField] private CoopQuestManager questManager;

        private void Awake()
        {
            bool isCoop = CoopSession.Instance != null && 
                          CoopSession.Instance.Manager != null && 
                          CoopSession.Instance.Manager.IsListening;

            if (isCoop)
            {
                ApplyCoopMode();
            }
            else
            {
                ApplySinglePlayerMode();
            }
        }

        private void Start()
        {
            bool isCoop = CoopSession.Instance != null && 
                          CoopSession.Instance.Manager != null && 
                          CoopSession.Instance.Manager.IsListening;

            if (isCoop)
            {
                ApplyCoopMode();
            }
        }

        public void ApplyCoopMode()
        {
            if (fpPlayer == null) fpPlayer = GameObject.Find("FP_GaragePlayer") ?? GameObject.Find("GarageHubRoot/FP_GaragePlayer");
            if (fpPlayer != null) fpPlayer.SetActive(false);

            if (offlineCar == null)
            {
                var cars = FindObjectsByType<RogueDrive.Gameplay.ArcadeCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in cars)
                {
                    if (c.GetComponent<NetworkObject>() == null)
                    {
                        offlineCar = c.gameObject;
                        break;
                    }
                }
            }
            if (offlineCar != null) offlineCar.SetActive(false);

            if (singlePlayerCanvas == null) singlePlayerCanvas = GameObject.Find("GarageInteractionCanvas") ?? GameObject.Find("GarageHubRoot/GarageInteractionCanvas");
            if (singlePlayerCanvas != null) singlePlayerCanvas.SetActive(false);

            if (prologueCutscene == null)
            {
                prologueCutscene = FindFirstObjectByType<BunkerPrologueCutscene>(FindObjectsInactive.Include);
            }
            if (prologueCutscene != null)
            {
                prologueCutscene.StopAllCoroutines();
                prologueCutscene.enabled = false;
            }

            if (crewUi != null) crewUi.SetActive(true);
        }

        public void ApplySinglePlayerMode()
        {
            if (crewUi != null) crewUi.SetActive(false);
        }
    }
}
