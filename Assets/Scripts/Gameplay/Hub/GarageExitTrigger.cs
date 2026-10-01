using System.Collections;
using RogueDrive.Audio;
using RogueDrive.UI;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Триггер выезда из бункера на выездной рампе/дороге за гермоворотами.
    /// Фиксирует проезд автомобиля игрока сквозь створ ворот и выполняет
    /// переход на первую боевую локацию (Stage1_Outskirts).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GarageExitTrigger : MonoBehaviour
    {
        [Header("Target Location")]
        [SerializeField] private string targetSceneName = "Stage1_Outskirts";
        [SerializeField] private float transitionDelay = 0.5f;
        [SerializeField] private SeamlessBunkerRoad seamlessRoad;
        [SerializeField] private GarageSceneExitCinematic cinematic;

        private bool hasTriggered = false;

        private void Reset()
        {
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.isTrigger = true;
                box.size = new Vector3(12f, 5f, 4f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasTriggered) return;

            var session = RogueDrive.Gameplay.Coop.CoopSession.Instance;
            bool isCoop = session != null && session.Manager != null && session.Manager.IsListening;

            if (isCoop)
            {
                var coopCar = other.GetComponentInParent<RogueDrive.Gameplay.Coop.CoopVehicle>();
                if (coopCar != null)
                {
                    var qm = RogueDrive.Gameplay.Coop.CoopQuestManager.Instance;
                    if ((qm == null || qm.QuestGatesOpened.Value) && coopCar.transform.position.z >= 12.0f)
                    {
                        hasTriggered = true;
                        StartCoroutine(ExitRoutine(null));
                    }
                }
                return;
            }

            if (cinematic != null)
            {
                var vehicle = other.attachedRigidbody != null
                    ? other.attachedRigidbody.GetComponent<GarageDriveOutVehicle>() : null;
                hasTriggered = cinematic.TryBegin(vehicle);
                return;
            }

            if (seamlessRoad != null)
            {
                var vehicle = other.attachedRigidbody != null
                    ? other.attachedRigidbody.GetComponent<GarageDriveOutVehicle>() : null;
                if (vehicle != null && seamlessRoad.TryBeginRun(vehicle)) hasTriggered = true;
                return;
            }

            var driveOut = other.GetComponentInParent<GarageDriveOutController>();
            var driveVehicle = other.GetComponentInParent<GarageDriveOutVehicle>();
            var carController = other.GetComponentInParent<ArcadeCarController>();
            var rb = other.attachedRigidbody;

            bool isPlayerCar = driveOut != null || driveVehicle != null || carController != null || (rb != null && rb.gameObject.name.Contains("Car"));
            if (!isPlayerCar) return;

            hasTriggered = true;
            StartCoroutine(ExitRoutine(driveOut));
        }

        private void OnTriggerStay(Collider other)
        {
            if (cinematic != null && !hasTriggered) OnTriggerEnter(other);
        }

        private IEnumerator ExitRoutine(GarageDriveOutController driveOut)
        {
            Debug.Log("[GarageExitTrigger] Автомобиль выехал за пределы бункера! Переход на трассу...");

            ArcadeCarController.JustDroveOutOfBunker = true;

            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.MarkPrologueCompleted();
                GaragePrologueManager.Instance.ShowNotification("ВЫЕЗД В ПРИГОРОД... ПРИГОТОВЬТЕСЬ К БОЮ!", 3.5f);
            }

            // Сохраняем состояние автомобиля и инвентаря для бесшовного переноса в Сектор 01
            var modular = FindFirstObjectByType<VehicleModularState>();
            if (modular != null)
            {
                PlayerPrefs.SetFloat("BunkerSaved_Fuel", modular.FuelLiters);
                PlayerPrefs.SetFloat("BunkerSaved_Water", modular.RadiatorWater);
            }
            else
            {
                PlayerPrefs.SetFloat("BunkerSaved_Fuel", 20f);
                PlayerPrefs.SetFloat("BunkerSaved_Water", 10f);
            }

            var trunk = FindFirstObjectByType<VehicleCargoTrunk>();
            if (trunk != null && trunk.Items.Count > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < trunk.Items.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append((int)trunk.Items[i]);
                }
                PlayerPrefs.SetString("BunkerSaved_Trunk", sb.ToString());
            }
            else
            {
                PlayerPrefs.DeleteKey("BunkerSaved_Trunk");
            }

            var pocket = FindFirstObjectByType<PlayerPocketInventory>();
            if (pocket != null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < PlayerPocketInventory.SlotCount; i++)
                {
                    var slot = pocket.GetSlot(i);
                    if (!slot.IsEmpty)
                    {
                        if (sb.Length > 0) sb.Append(";");
                        sb.Append($"{slot.id}:{slot.displayName}:{slot.count}");
                    }
                }
                PlayerPrefs.SetString("BunkerSaved_Pocket", sb.ToString());
            }

            PlayerPrefs.SetInt("BunkerDataSaved", 1);
            PlayerPrefs.Save();

            yield return new WaitForSeconds(transitionDelay);

            string sceneToLoad = targetSceneName;
            if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
            {
                sceneToLoad = "Stage1_Outskirts";
            }

            var session = RogueDrive.Gameplay.Coop.CoopSession.Instance;
            if (session != null && session.Manager != null && session.Manager.IsListening)
            {
                if (session.Manager.IsServer)
                {
                    var qm = RogueDrive.Gameplay.Coop.CoopQuestManager.Instance;
                    if (qm != null) qm.QuestBunkerDeparted.Value = true;
                    session.Manager.SceneManager.LoadScene(sceneToLoad, UnityEngine.SceneManagement.LoadSceneMode.Single);
                }
                yield break;
            }

            SceneTransitionManager.SwitchScene(sceneToLoad);
        }
    }
}
