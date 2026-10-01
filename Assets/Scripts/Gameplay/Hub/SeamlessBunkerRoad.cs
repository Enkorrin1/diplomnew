using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>The road is authored beside the bunker. Crossing the exit starts the run in place.</summary>
    public sealed class SeamlessBunkerRoad : MonoBehaviour
    {
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private ArcadeCarController playerCar;
        public bool HasStarted { get; private set; }

        public bool TryBeginRun(GarageDriveOutVehicle vehicle)
        {
            if (HasStarted) return true;
            if (gameplayRoot == null || playerCar == null || vehicle == null
                || vehicle.gameObject != playerCar.gameObject || !vehicle.isActiveAndEnabled
                || GarageDriveOutController.Instance == null || !GarageDriveOutController.Instance.IsDriving)
                return false;

            HasStarted = true;
            // All references point at the existing car. No load, teleport, velocity reset or camera swap.
            CampaignMapModal.SelectedStartSector = 1;
            gameplayRoot.SetActive(true);
            playerCar.enabled = true;
            GaragePrologueManager.Instance?.MarkPrologueCompleted();
            GarageInteractionUI.Instance?.SetObjective(string.Empty);
            GarageInteractionUI.Instance?.ShowBanner("ПРИГОРОД — ДОБЕРИТЕСЬ ДО СЛЕДУЮЩЕГО БЛОКПОСТА", 4f);
            return true;
        }
    }
}
