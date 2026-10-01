using System.Collections;
using RogueDrive.Audio;
using RogueDrive.Meta;
using RogueDrive.UI;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Координатор посадки и интерактивного выезда из Бункера 07:
    /// 1. Пересаживает игрока из режима ходьбы от 1-го лица за руль собранного автомобиля.
    /// 2. Активирует физику машины и управление на клавиши WASD (газ, тормоз, руль).
    /// 3. Переключает камеру в кинематографичный вид от 3-го лица за автомобилем.
    /// 4. Распахивает тяжелые гермоворота на выездной рампе (со звуком гидравлики и сиреной).
    /// 5. Дает игроку самому нажать [W] и выехать из гаража на поверхность.
    /// </summary>
    public sealed class GarageDriveOutController : MonoBehaviour
    {
        public static GarageDriveOutController Instance { get; private set; }

        [Header("Camera Follow Settings")]
        [SerializeField] private float cameraDistance = 5.4f;
        [SerializeField] private float cameraHeight = 2.2f;
        [SerializeField] private float cameraFollowSharpness = 8.0f;
        [SerializeField] private float cameraLookAhead = 1.0f;

        [Header("Mouse Orbit (Driving Mode)")]
        [SerializeField] private float mouseOrbitSensitivity = 2.5f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 65f;
        private float orbitYaw = 0f;
        private float orbitPitch = 12f;
        private float lastMouseLookTime = 0f;

        [Header("State")]
        private bool isDriving = false;
        private Transform carTransform;
        private GarageDriveOutVehicle carVehicle;
        private Camera activeCamera;
        private GaragePlayerController savedPlayer;
        private GarageVehicleBoarding activeBoarding;

        public bool IsDriving => isDriving;
        public bool CinematicControl { get; set; }
        public Camera DrivingCamera => activeCamera;
        public GaragePlayerController SeatedPlayer => savedPlayer;

        public void GetDefaultFollowPose(out Vector3 position, out Quaternion rotation)
        {
            Quaternion yaw = Quaternion.Euler(0f, carTransform.eulerAngles.y, 0f);
            position = carTransform.position + Vector3.up * cameraHeight
                - yaw * Quaternion.Euler(12f, 0f, 0f) * Vector3.forward * cameraDistance;
            rotation = Quaternion.LookRotation(carTransform.position + Vector3.up * cameraLookAhead
                + yaw * Vector3.forward - position);
        }

        public void FinishCinematic()
        {
            orbitYaw = 0f;
            orbitPitch = 12f;
            lastMouseLookTime = Time.time;
            CinematicControl = false;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartDriveOut(GaragePlayerController player, GameObject carObject)
        {
            if (player != null && player.GetComponent<PlayerFieldNeeds>()?.IsDead == true) return;
            if (isDriving) return;
            isDriving = true;

            StartCoroutine(BoardingSequence(player, carObject));
        }

        private IEnumerator BoardingSequence(GaragePlayerController player, GameObject carObject)
        {
            savedPlayer = player;
            activeBoarding = FindFirstObjectByType<GarageVehicleBoarding>();
            orbitYaw = 0f;
            orbitPitch = 12f;
            lastMouseLookTime = Time.time;

            // 0. Если в руках остался предмет (например пустая канистра), сбрасываем его на пол
            if (BunkerPlayerInventory.Instance != null && BunkerPlayerInventory.Instance.HasItem)
            {
                BunkerPlayerInventory.Instance.DropItem(throwForward: false);
            }
            if (PlayerPocketInventory.Instance != null)
            {
                PlayerPocketInventory.Instance.HideVisuals();
            }

            // 1. Извлекаем и сохраняем камеру игрока ДО отключения персонажа
            if (player != null)
            {
                if (player.PlayerCamera != null)
                {
                    activeCamera = player.PlayerCamera;
                }
                else
                {
                    activeCamera = player.GetComponentInChildren<Camera>(true);
                }
            }

            if (activeCamera == null)
            {
                activeCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            }

            if (activeCamera != null)
            {
                // Отцепляем камеру от персонажа в корень сцены, чтобы она осталась активной
                activeCamera.transform.SetParent(null, true);
                activeCamera.gameObject.SetActive(true);
                activeCamera.enabled = true;

                // Отключаем сокеты удержания оружия и предметов, прикрепленные к камере
                foreach (Transform child in activeCamera.transform)
                {
                    if (child.name.Contains("Socket"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                // Отключаем рейкастер интерактивности при переходе в режим вождения
                var raycaster = activeCamera.GetComponent<GarageInteractionRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
            }

            // 2. Блокируем и скрываем персонажа 1-го лица
            if (player != null)
            {
                player.SetMovementLocked(true);
                player.enabled = false;
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.gameObject.SetActive(false);
            }

            // 3. Определяем автомобиль (гарантируем выбор Classic Car_9, а не триггера посадки или подиума)
            if (carObject == null || carObject.name == "VehicleBoardingZone" || carObject.name == "PodiumAnchor")
            {
                GameObject podium = GameObject.Find("PodiumAnchor");
                Transform carChild = podium != null ? podium.transform.Find("Classic Car_9") : null;
                if (carChild != null)
                {
                    carObject = carChild.gameObject;
                }
                else
                {
                    var assembly = BunkerStarterCarAssembly.Instance ?? FindFirstObjectByType<BunkerStarterCarAssembly>();
                    if (assembly != null)
                    {
                        carChild = assembly.transform.Find("Classic Car_9");
                        if (carChild != null) carObject = carChild.gameObject;
                    }
                    if (carObject == null) carObject = GameObject.Find("Classic Car_9");
                }
            }

            if (carObject == null)
            {
                Debug.LogError("[GarageDriveOutController] Не найден объект автомобиля для выезда!");
                yield break;
            }

            carTransform = carObject.transform;
            // Прикрепляем фары, аккумулятор, колесо и зону посадки к машине
            AttachAllComponentsToCar(carTransform);
            // Отцепляем от подиума, чтобы машина могла свободно ехать по всей сцене
            carTransform.SetParent(null, true);

            // 4. Обеспечиваем физику автомобиля
            Rigidbody rb = carObject.GetComponent<Rigidbody>();
            if (rb == null) rb = carObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 1200f;
            rb.linearDamping = 0.45f;
            rb.angularDamping = 2.5f;
            rb.constraints = RigidbodyConstraints.None;
            rb.centerOfMass = new Vector3(0f, -0.25f, 0f);

            // Обеспечиваем коллайдер кузова со скользящим материалом (с дорожным просветом под ход подвески)
            BoxCollider col = carObject.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = carObject.AddComponent<BoxCollider>();
            }
            col.center = new Vector3(0f, 0.80f, 0f);
            col.size = new Vector3(1.75f, 1.10f, 4.3f);
            col.enabled = true;

            PhysicsMaterial slippery = new PhysicsMaterial("CarBunkerSlippery")
            {
                dynamicFriction = 0.05f,
                staticFriction = 0.05f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            col.material = slippery;

            // Синхронизируем визуал колес с мета-прогрессом
            var wheelUpgrades = carObject.GetComponent<RogueDrive.Gameplay.VFX.CarWheelUpgradeVisuals>();
            if (wheelUpgrades != null)
            {
                wheelUpgrades.RefreshForCurrentProgress("light");
            }

            // Включаем скрипт вождения с физикой подвески и вращением колес
            carVehicle = carObject.GetComponent<GarageDriveOutVehicle>();
            if (carVehicle == null)
            {
                carVehicle = carObject.AddComponent<GarageDriveOutVehicle>();
            }
            carVehicle.enabled = true;
            carVehicle.EnableDriving();

            // 5. Настраиваем камеру
            if (activeCamera != null)
            {
                // Начальная позиция камеры позади автомобиля
                Vector3 initCamPos = carTransform.position - carTransform.forward * cameraDistance + Vector3.up * cameraHeight;
                activeCamera.transform.position = initCamPos;
                Vector3 lookTarget = carTransform.position + Vector3.up * cameraLookAhead + carTransform.forward * 1.5f;
                activeCamera.transform.rotation = Quaternion.LookRotation(lookTarget - initCamPos);
            }

            // 6. Очищаем HUD и подсказки подбора
            if (GarageInteractionUI.Instance != null)
            {
                GarageInteractionUI.Instance.HidePrompt();
                GarageInteractionUI.Instance.HideHeldHint();
                var roadCar = carObject.GetComponent<ArcadeCarController>();
                bool onRoad = roadCar != null && roadCar.enabled && roadCar.UsesGarageDriving;
                GarageInteractionUI.Instance.HideBanner();
                if (!onRoad)
                    GarageInteractionUI.Instance.ShowBanner("ГАЗ [W / ↑] — ВЫЕХАТЬ ИЗ БУНКЕРА", 5f);
            }

            var pocketUI = FindFirstObjectByType<PocketInventoryUI>();
            if (pocketUI != null)
            {
                pocketUI.gameObject.SetActive(false);
            }

            // Активируем тактический HUD приборов автомобиля (спидометр, топливо, радиатор, шины)
            var tacticalHud = FindFirstObjectByType<RogueDrive.UI.VehicleModularTacticalHud>(FindObjectsInactive.Include);
            if (tacticalHud == null)
            {
                var hudObj = new GameObject("VehicleModularTacticalHud");
                tacticalHud = hudObj.AddComponent<RogueDrive.UI.VehicleModularTacticalHud>();
            }
            if (tacticalHud != null)
            {
                tacticalHud.gameObject.SetActive(true);
            }

            // 7. Запуск двигателя и звуки
            if (GaragePresentationDirector.Instance != null)
            {
                GaragePresentationDirector.Instance.PlayIgnition(carObject.transform.position);
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLevelUp();
            }

            // 8. Распахиваем гермоворота
            GarageSwingGateController gate = FindFirstObjectByType<GarageSwingGateController>();
            if (gate != null && !gate.IsOpen)
            {
                gate.OpenGate();
            }

            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.OpenGate();
            }

            // The first departure owns camera and vehicle from boarding to road handoff.
            var departure = GarageSceneExitCinematic.Pending;
            if (departure != null && !departure.Completed)
            {
                CinematicControl = true;
                carVehicle.CinematicControl = true;
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!departure.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
                if (!departure.TryBegin(carVehicle))
                {
                    CinematicControl = false;
                    carVehicle.CinematicControl = false;
                }
            }
        }

        private void Update()
        {
            if (!isDriving || CinematicControl) return;

            // Выход из машины на [E] или [F]
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
            {
                ExitCar();
                return;
            }

            HandleMouseOrbit();
        }

        private void HandleMouseOrbit()
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            if (Mathf.Abs(mouseX) > 0.02f || Mathf.Abs(mouseY) > 0.02f)
            {
                orbitYaw += mouseX * mouseOrbitSensitivity;
                orbitPitch = Mathf.Clamp(orbitPitch - mouseY * mouseOrbitSensitivity, minPitch, maxPitch);
                lastMouseLookTime = Time.time;
            }
            else if (Time.time - lastMouseLookTime > 2.0f && carVehicle != null && carVehicle.SpeedMps > 0.5f)
            {
                // При движении вперед плавно возвращаем камеру строго за корму
                orbitYaw = Mathf.LerpAngle(orbitYaw, 0f, Time.deltaTime * 3.5f);
                orbitPitch = Mathf.Lerp(orbitPitch, 12f, Time.deltaTime * 3.5f);
            }
        }

        public void ExitCar()
        {
            if (!isDriving || CinematicControl) return;
            isDriving = false;

            // 1. Отключаем вождение и останавливаем физику машины
            if (carVehicle != null)
            {
                carVehicle.SimulatedThrottle = 0f;
                carVehicle.SimulatedSteer = 0f;
                carVehicle.enabled = false;
            }

            if (carTransform != null)
            {
                var rb = carTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // 2. Спавним персонажа у водительской двери
            if (savedPlayer == null)
            {
                savedPlayer = FindFirstObjectByType<GaragePlayerController>(FindObjectsInactive.Include);
            }

            if (savedPlayer != null && carTransform != null)
            {
                Vector3 exitPos = carTransform.position - carTransform.right * 1.55f + Vector3.up * 0.1f;
                savedPlayer.transform.position = exitPos;
                savedPlayer.transform.rotation = Quaternion.Euler(0f, carTransform.eulerAngles.y - 90f, 0f);

                savedPlayer.gameObject.SetActive(true);
                savedPlayer.enabled = true;

                var cc = savedPlayer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;

                savedPlayer.SetMovementLocked(false);
                savedPlayer.LockCursor(true);

                // Возвращаем камеру игроку в голову
                if (activeCamera != null)
                {
                    activeCamera.transform.SetParent(savedPlayer.transform, false);
                    activeCamera.transform.localPosition = new Vector3(0f, 1.65f, 0f);
                    activeCamera.transform.localRotation = Quaternion.identity;
                    savedPlayer.SetCameraPitch(0f);

                    var raycaster = activeCamera.GetComponent<GarageInteractionRaycaster>();
                    if (raycaster != null) raycaster.enabled = true;

                    foreach (Transform child in activeCamera.transform)
                    {
                        if (child.name.Contains("Socket"))
                        {
                            child.gameObject.SetActive(true);
                        }
                    }
                }
            }

            // 3. Сбрасываем триггер посадки, чтобы в машину можно было снова сесть
            if (activeBoarding == null)
            {
                activeBoarding = FindFirstObjectByType<GarageVehicleBoarding>(FindObjectsInactive.Include);
            }
            if (activeBoarding != null)
            {
                activeBoarding.ResetBoardingState();
            }

            // 4. Восстанавливаем UI
            if (GarageInteractionUI.Instance != null)
            {
                GarageInteractionUI.Instance.HideBanner();
                GarageInteractionUI.Instance.ShowNotification("Вы вышли из машины. Нажмите [E] у двери, чтобы сесть за руль.", 4.5f);
            }

            var pocketUI = FindFirstObjectByType<PocketInventoryUI>(FindObjectsInactive.Include);
            if (pocketUI != null)
            {
                pocketUI.gameObject.SetActive(true);
            }

            var tacticalHud = FindFirstObjectByType<RogueDrive.UI.VehicleModularTacticalHud>();
            if (tacticalHud != null)
            {
                tacticalHud.gameObject.SetActive(false);
            }
        }

        public static void AttachAllComponentsToCar(Transform car)
        {
            if (car == null) return;

            // 1. Фары автомобиля (Car_Headlights)
            var headlights = GameObject.Find("Car_Headlights");
            if (headlights != null && headlights.transform.parent != car)
            {
                headlights.transform.SetParent(car, true);
                var lights = headlights.GetComponentsInChildren<Light>(true);
                foreach (var l in lights) l.enabled = true;
            }

            // 2. Хотспот аккумулятора в моторном отсеке (Hotspot_Battery_EngineBay)
            var batterySpot = GameObject.Find("Hotspot_Battery_EngineBay");
            if (batterySpot != null && batterySpot.transform.parent != car)
            {
                batterySpot.transform.SetParent(car, true);
            }

            // 3. Хотспот и смонтированное колесо (Hotspot_Wheel_FL)
            var wheelSpot = GameObject.Find("Hotspot_Wheel_FL");
            if (wheelSpot != null && wheelSpot.transform.parent != car)
            {
                wheelSpot.transform.SetParent(car, true);
            }

            // 4. Горловина бензобака (Hotspot_FuelTank_Inlet)
            var fuelSpot = GameObject.Find("Hotspot_FuelTank_Inlet");
            if (fuelSpot != null && fuelSpot.transform.parent != car)
            {
                fuelSpot.transform.SetParent(car, true);
            }

            // 5. Зона посадки (VehicleBoardingZone)
            var boardingZone = GameObject.Find("VehicleBoardingZone");
            if (boardingZone != null && boardingZone.transform.parent != car)
            {
                boardingZone.transform.SetParent(car, true);
            }

            // 6. Хотспоты багажника и капота
            var trunkSpot = GameObject.Find("Inspection_Trunk");
            if (trunkSpot != null && trunkSpot.transform.parent != car)
            {
                trunkSpot.transform.SetParent(car, true);
            }

            var hoodSpot = GameObject.Find("Inspection_EngineHood");
            if (hoodSpot != null && hoodSpot.transform.parent != car)
            {
                hoodSpot.transform.SetParent(car, true);
            }
        }

        private void LateUpdate()
        {
            if (!isDriving || CinematicControl || carTransform == null || activeCamera == null) return;

            // Плавное слежение камеры за машиной от 3-го лица со свободным орбитальным вращением мышью
            Quaternion carYawRot = Quaternion.Euler(0f, carTransform.eulerAngles.y, 0f);
            Quaternion camOrbitRot = carYawRot * Quaternion.Euler(orbitPitch, orbitYaw, 0f);

            Vector3 targetCamPos = carTransform.position + Vector3.up * cameraHeight - (camOrbitRot * Vector3.forward * cameraDistance);
            activeCamera.transform.position = Vector3.Lerp(activeCamera.transform.position, targetCamPos, Time.deltaTime * cameraFollowSharpness);

            Vector3 lookTarget = carTransform.position + Vector3.up * cameraLookAhead + (carYawRot * Vector3.forward * 1.0f);
            Quaternion targetRot = Quaternion.LookRotation(lookTarget - activeCamera.transform.position);
            activeCamera.transform.rotation = Quaternion.Slerp(activeCamera.transform.rotation, targetRot, Time.deltaTime * (cameraFollowSharpness * 1.5f));
        }
    }
}
