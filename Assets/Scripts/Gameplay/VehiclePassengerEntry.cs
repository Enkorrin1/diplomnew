using RogueDrive.Audio;
using RogueDrive.Gameplay.Hub;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Контроллер посадки и высадки из автомобиля (Dual-Perspective Entry/Exit).
    /// Обеспечивает бесшовный переход:
    /// - За рулем: 3-е лицо (ArcadeCarController + ArcadeCameraFollow / GarageDriveOutController).
    /// - Пешком: 1-е лицо (GaragePlayerController / FPS Controller) на клавишу [E].
    /// - Защита от черного экрана: гарантирует активность камеры во всех режимах.
    /// - Разделение ролей: пассажирское/штурманское место доступно только в кооп-режиме; в одиночной игре только водительское.
    /// </summary>
    [RequireComponent(typeof(ArcadeCarController))]
    public sealed class VehiclePassengerEntry : MonoBehaviour, IGarageInteractable
    {
        [Header("References")]
        [SerializeField] private ArcadeCarController carController;
        [SerializeField] private Transform driverExitPoint;
        [SerializeField] private GameObject driverDoorColliderObj;
        [SerializeField] private GameObject passengerDoorColliderObj;

        [Header("Settings")]
        [SerializeField, Min(1f)] private float maxExitSpeedMps = 4.0f; // ~15 км/ч
        [SerializeField] private Vector3 defaultExitOffset = new Vector3(-1.9f, 0.2f, 0.2f);

        [Header("Current State")]
        [SerializeField] private bool isDriverInside = true;

        private GaragePlayerController fpPlayer;
        private Camera carCamera;

        public bool IsDriverInside => isDriverInside;

        public bool IsCoopActive => RogueDrive.Gameplay.Coop.CoopSession.Instance != null &&
                                    RogueDrive.Gameplay.Coop.CoopSession.Instance.Manager != null &&
                                    RogueDrive.Gameplay.Coop.CoopSession.Instance.Manager.IsListening;

        private void Awake()
        {
            if (carController == null)
            {
                carController = GetComponent<ArcadeCarController>();
            }

            // Создаем точку выхода слева у двери водителя
            if (driverExitPoint == null)
            {
                GameObject exitObj = new GameObject("DriverExitPoint");
                exitObj.transform.SetParent(transform, false);
                exitObj.transform.localPosition = defaultExitOffset;
                driverExitPoint = exitObj.transform;
            }

            // Создаем триггер-зоны дверей для посадки снаружи
            EnsureDoorTriggers();
        }

        private void Start()
        {
            FindCamerasAndPlayer();

            // В Бункере / Гараже игрок начинает пешком — машина свободна для посадки
            if (GarageDriveOutController.Instance != null && !GarageDriveOutController.Instance.IsDriving)
            {
                isDriverInside = false;
            }
            else if (FindFirstObjectByType<GaragePlayerController>(FindObjectsInactive.Include) != null)
            {
                isDriverInside = false;
            }
        }

        private void FindCamerasAndPlayer()
        {
            if (fpPlayer == null)
            {
                fpPlayer = FindFirstObjectByType<GaragePlayerController>(FindObjectsInactive.Include);
            }

            if (ArcadeCameraFollow.Instance != null)
            {
                carCamera = ArcadeCameraFollow.Instance.GetComponent<Camera>();
            }
        }

        private void Update()
        {
            // Если водитель за рулем: высадка по клавише [E] при низкой скорости
            if (isDriverInside)
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    TryExitVehicle();
                }
            }
        }

        public bool TryExitVehicle()
        {
            if (!isDriverInside) return false;

            // Проверяем скорость: нельзя выпрыгивать на полном ходу
            Rigidbody rb = carController != null ? carController.GetComponent<Rigidbody>() : GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.magnitude > maxExitSpeedMps)
            {
                if (GaragePrologueManager.Instance != null)
                {
                    GaragePrologueManager.Instance.ShowNotification("Слишком высокая скорость для высадки! Затормозите [Пробел].", 2.0f);
                }
                return false;
            }

            ExitVehicle();
            return true;
        }

        public void ExitVehicle()
        {
            isDriverInside = false;

            if (GarageDriveOutController.Instance != null && GarageDriveOutController.Instance.IsDriving)
            {
                GarageDriveOutController.Instance.ExitCar();
                return;
            }

            if (fpPlayer == null)
            {
                FindCamerasAndPlayer();
            }

            // Глушим газ машины
            if (carController != null)
            {
                carController.enabled = false;
            }

            // Позиционируем игрока у водительской двери
            Vector3 exitPos = driverExitPoint != null ? driverExitPoint.position : transform.position + transform.TransformDirection(defaultExitOffset);
            Quaternion exitRot = Quaternion.Euler(0f, transform.eulerAngles.y - 90f, 0f);

            if (fpPlayer == null)
            {
                CreateProceduralFpPlayer(exitPos, exitRot);
            }
            else
            {
                var cc = fpPlayer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                fpPlayer.transform.position = exitPos;
                fpPlayer.transform.rotation = exitRot;
                if (cc != null) cc.enabled = true;
                fpPlayer.gameObject.SetActive(true);
                fpPlayer.enabled = true;
                fpPlayer.SetMovementLocked(false);
            }

            // Переключаем камеры: отключаем камеру машины, включаем камеру игрока
            if (carCamera != null && (fpPlayer == null || carCamera != fpPlayer.PlayerCamera))
            {
                carCamera.enabled = false;
                var carListener = carCamera.GetComponent<AudioListener>();
                if (carListener != null) carListener.enabled = false;
            }

            if (fpPlayer != null && fpPlayer.PlayerCamera != null)
            {
                fpPlayer.PlayerCamera.enabled = true;
                var playerListener = fpPlayer.PlayerCamera.GetComponent<AudioListener>();
                if (playerListener != null) playerListener.enabled = true;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayImpact();
            }
        }

        public void EnterVehicle(GaragePlayerController player)
        {
            // 1. Если находимся в Гараже/Бункере — управление передается GarageDriveOutController
            if (GarageDriveOutController.Instance != null)
            {
                isDriverInside = true;
                fpPlayer = player;
                GarageDriveOutController.Instance.StartDriveOut(player, gameObject);
                return;
            }

            isDriverInside = true;
            fpPlayer = player;

            // 2. Включаем управление автомобилем
            if (carController != null)
            {
                carController.enabled = true;
            }

            // 3. Безопасное подключение камеры следования (гарантия отсутствия черного экрана)
            if (carCamera == null)
            {
                if (ArcadeCameraFollow.Instance != null)
                {
                    carCamera = ArcadeCameraFollow.Instance.GetComponent<Camera>();
                }
                if (carCamera == null)
                {
                    carCamera = Camera.main;
                }
            }

            // Если сторонней камеры нет, берем камеру игрока и отцепляем ее для следования
            if (carCamera == null && fpPlayer != null && fpPlayer.PlayerCamera != null)
            {
                carCamera = fpPlayer.PlayerCamera;
                carCamera.transform.SetParent(null, true);
            }

            if (carCamera != null)
            {
                var follow = carCamera.GetComponent<ArcadeCameraFollow>();
                if (follow == null)
                {
                    follow = carCamera.gameObject.AddComponent<ArcadeCameraFollow>();
                }
                follow.Configure(transform);

                carCamera.gameObject.SetActive(true);
                carCamera.enabled = true;
                var carListener = carCamera.GetComponent<AudioListener>();
                if (carListener != null) carListener.enabled = true;
            }
            else
            {
                Debug.LogWarning("[VehiclePassengerEntry] Fallback: создаем временную камеру вождения.");
                GameObject camObj = new GameObject("CarFollowCamera_Fallback", typeof(Camera), typeof(AudioListener), typeof(ArcadeCameraFollow));
                carCamera = camObj.GetComponent<Camera>();
                carCamera.GetComponent<ArcadeCameraFollow>().Configure(transform);
            }

            // 4. Отключаем пешего персонажа только ПОСЛЕ успешного включения камеры машины
            if (fpPlayer != null)
            {
                fpPlayer.SetMovementLocked(true);
                fpPlayer.enabled = false;
                if (fpPlayer.PlayerCamera != null && fpPlayer.PlayerCamera != carCamera)
                {
                    fpPlayer.PlayerCamera.enabled = false;
                    var playerListener = fpPlayer.PlayerCamera.GetComponent<AudioListener>();
                    if (playerListener != null) playerListener.enabled = false;
                }
                fpPlayer.gameObject.SetActive(false);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLevelUp();
            }
        }

        private void CreateProceduralFpPlayer(Vector3 spawnPos, Quaternion spawnRot)
        {
            GameObject playerGo = new GameObject("FPS_Survivor_Player");
            playerGo.transform.position = spawnPos;
            playerGo.transform.rotation = spawnRot;

            CharacterController cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            GameObject camGo = new GameObject("SurvivorCamera");
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camGo.transform.localRotation = Quaternion.identity;

            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 65f;

            camGo.AddComponent<GarageInteractionRaycaster>();

            fpPlayer = playerGo.AddComponent<GaragePlayerController>();
            if (playerGo.GetComponent<BunkerPlayerInventory>() == null)
            {
                playerGo.AddComponent<BunkerPlayerInventory>();
            }

            if (FindFirstObjectByType<GarageInteractionUI>() == null)
            {
                new GameObject("GarageInteractionUI", typeof(GarageInteractionUI));
            }

            fpPlayer.SetMovementLocked(false);
        }

        public bool IsPassengerSide(Transform playerTransform)
        {
            if (playerTransform == null) return false;
            return transform.InverseTransformPoint(playerTransform.position).x > 0f;
        }

        public string GetPromptText(bool isPassengerSide)
        {
            if (isDriverInside) return string.Empty;

            if (isPassengerSide)
            {
                if (!IsCoopActive)
                {
                    return "Пассажирское место (недоступно в одиночной игре — садитесь за руль слева)";
                }
                return "[E] Занять место штурмана (Пассажир)";
            }

            var boarding = GetComponentInChildren<GarageVehicleBoarding>() ?? FindFirstObjectByType<GarageVehicleBoarding>();
            if (boarding != null)
            {
                return boarding.GetPromptText();
            }

            return "[E] Сесть за руль автомобиля";
        }

        public bool CanInteract(bool isPassengerSide)
        {
            if (isDriverInside) return false;

            if (isPassengerSide)
            {
                return IsCoopActive;
            }

            var boarding = GetComponentInChildren<GarageVehicleBoarding>() ?? FindFirstObjectByType<GarageVehicleBoarding>();
            if (boarding != null)
            {
                return boarding.CanInteract();
            }

            return true;
        }

        // IGarageInteractable реализация для посадки снаружи (на основном коллайдере кузова)
        public string GetPromptText()
        {
            var p = fpPlayer ?? FindFirstObjectByType<GaragePlayerController>();
            bool passSide = p != null && IsPassengerSide(p.transform);
            return GetPromptText(passSide);
        }

        public bool CanInteract()
        {
            var p = fpPlayer ?? FindFirstObjectByType<GaragePlayerController>();
            bool passSide = p != null && IsPassengerSide(p.transform);
            return CanInteract(passSide);
        }

        public void Interact(GaragePlayerController player)
        {
            bool passSide = player != null && IsPassengerSide(player.transform);
            if (passSide && !IsCoopActive)
            {
                GaragePrologueManager.Instance?.ShowNotification("Пассажирское место недоступно в одиночной игре. Садитесь за руль с водительской стороны (слева).", 2.5f);
                return;
            }

            var boarding = GetComponentInChildren<GarageVehicleBoarding>() ?? FindFirstObjectByType<GarageVehicleBoarding>();
            if (boarding != null && !boarding.CanInteract())
            {
                boarding.Interact(player);
                return;
            }

            EnterVehicle(player);
        }

        private void EnsureDoorTriggers()
        {
            if (driverDoorColliderObj == null)
            {
                driverDoorColliderObj = new GameObject("DriverDoor_Hotspot");
                driverDoorColliderObj.transform.SetParent(transform, false);
                driverDoorColliderObj.transform.localPosition = new Vector3(-1.15f, 0.75f, 0.2f);

                BoxCollider box = driverDoorColliderObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.9f, 1.2f, 1.2f);

                var proxy = driverDoorColliderObj.AddComponent<DoorInteractableProxy>();
                proxy.Configure(this, isPassengerDoor: false);
            }

            if (passengerDoorColliderObj == null)
            {
                Transform existing = transform.Find("PassengerDoor_Hotspot");
                if (existing != null)
                {
                    passengerDoorColliderObj = existing.gameObject;
                }
                else
                {
                    passengerDoorColliderObj = new GameObject("PassengerDoor_Hotspot");
                    passengerDoorColliderObj.transform.SetParent(transform, false);
                    passengerDoorColliderObj.transform.localPosition = new Vector3(1.15f, 0.75f, 0.2f);

                    BoxCollider box = passengerDoorColliderObj.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(0.9f, 1.2f, 1.2f);

                    var proxy = passengerDoorColliderObj.AddComponent<DoorInteractableProxy>();
                    proxy.Configure(this, isPassengerDoor: true);
                }
            }
        }

        /// <summary>
        /// Вспомогательный прокси-компонент на дверях, перенаправляющий вызовы в VehiclePassengerEntry.
        /// </summary>
        public sealed class DoorInteractableProxy : MonoBehaviour, IGarageInteractable
        {
            private VehiclePassengerEntry entry;
            private bool isPassengerDoor;

            public void Configure(VehiclePassengerEntry targetEntry, bool isPassengerDoor)
            {
                entry = targetEntry;
                this.isPassengerDoor = isPassengerDoor;
            }

            public string GetPromptText()
            {
                if (entry != null) return entry.GetPromptText(isPassengerDoor);
                return isPassengerDoor ? "Пассажирское место" : "[E] Сесть в машину";
            }

            public bool CanInteract()
            {
                if (entry != null) return entry.CanInteract(isPassengerDoor);
                return !isPassengerDoor;
            }

            public void Interact(GaragePlayerController player)
            {
                if (entry == null) return;
                if (isPassengerDoor && !entry.IsCoopActive)
                {
                    GaragePrologueManager.Instance?.ShowNotification("Пассажирское место недоступно в одиночной игре. Садитесь за руль с водительской стороны (слева).", 2.5f);
                    return;
                }
                entry.Interact(player);
            }
        }
    }
}
