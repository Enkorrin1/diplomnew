using System;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Координатор стартовой сборки Седана в Бункере 07:
    /// 1. Монтаж переднего левого колеса со стеллажа на ступицу.
    /// 2. Установка силового аккумулятора с верстака в гнездо под капотом.
    /// 3. Заливка первой канистры топлива в бензобак.
    /// После выполнения всех 3 шагов снимается блокировка с ворот и посадки в авто.
    /// </summary>
    public sealed class BunkerStarterCarAssembly : MonoBehaviour
    {
        public static BunkerStarterCarAssembly Instance { get; private set; }

        public event Action AssemblyCompleted;

        [Header("Assembly Hotspots")]
        [SerializeField] private BunkerCarAssemblyHotspot wheelHotspot;
        [SerializeField] private BunkerCarAssemblyHotspot batteryHotspot;
        [SerializeField] private BunkerCarAssemblyHotspot fuelHotspot;

        [Header("Car Visuals")]
        [SerializeField] private Light[] carHeadlights;
        [SerializeField] private GameObject jackStandObject;
        [SerializeField] private GameObject assembledWheelVisual;
        [SerializeField] private GameObject batteryInBayVisual;

        [Header("Editor & Testing")]
        [Tooltip("При запуске в редакторе Unity всегда начинать со сборки авто с нуля")]
        [SerializeField] private bool forceFreshAssemblyInEditor = true;

        [Header("Progress State")]
        [SerializeField] private bool isWheelInstalled = false;
        [SerializeField] private bool isBatteryInstalled = false;
        [SerializeField] private bool isFuelFilled = false;
        [SerializeField] private bool isAssemblyComplete = false;

        public bool IsWheelInstalled => isWheelInstalled;
        public bool IsBatteryInstalled => isBatteryInstalled;
        public bool IsFuelFilled => isFuelFilled;
        public bool IsAssemblyComplete => isAssemblyComplete;

        private void Awake()
        {
            Instance = this;

            AutoResolveReferences();

            // Если пролог уже был пройден ранее и не форсирован новый пролог, автомобиль собран по умолчанию
            int completedRuns = PlayerPrefs.GetInt("GaragePrologueDone", 0);
#if UNITY_EDITOR
            if (forceFreshAssemblyInEditor)
            {
                completedRuns = 0;
            }
#endif
            if (completedRuns > 0 && !GaragePrologueManager.ForcePrologueAwakening)
            {
                isWheelInstalled = true;
                isBatteryInstalled = true;
                isFuelFilled = true;
                isAssemblyComplete = true;
                SetHeadlights(true);
                if (jackStandObject != null) jackStandObject.SetActive(false);
                if (assembledWheelVisual != null) assembledWheelVisual.SetActive(true);
                if (batteryInBayVisual != null) batteryInBayVisual.SetActive(true);
            }
            else
            {
                isWheelInstalled = false;
                isBatteryInstalled = false;
                isFuelFilled = false;
                isAssemblyComplete = false;
                SetHeadlights(false);
                if (jackStandObject != null) jackStandObject.SetActive(true);
                if (assembledWheelVisual != null) assembledWheelVisual.SetActive(false);
                if (batteryInBayVisual != null) batteryInBayVisual.SetActive(false);
                var tank = GetComponentInChildren<VehicleModularState>();
                if (tank != null) tank.SetFuelForGaragePreparation(0f);
            }
        }

        private void AutoResolveReferences()
        {
            Transform carObj = transform.Find("Classic Car_9");
            if (carObj == null)
            {
#if UNITY_EDITOR
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Downloads/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Classic Car_9.prefab");
                if (prefab != null)
                {
                    var spawned = Instantiate(prefab, transform);
                    spawned.name = "Classic Car_9";
                    spawned.transform.localPosition = Vector3.zero;
                    spawned.transform.localRotation = Quaternion.identity;
                    carObj = spawned.transform;
                }
#endif
            }
            bool isCoop = RogueDrive.Gameplay.Coop.CoopSession.Instance != null && 
                          RogueDrive.Gameplay.Coop.CoopSession.Instance.Manager != null && 
                          RogueDrive.Gameplay.Coop.CoopSession.Instance.Manager.IsListening;
            if (!isCoop && carObj != null && !carObj.gameObject.activeSelf)
            {
                carObj.gameObject.SetActive(true);
            }
            else if (isCoop && carObj != null)
            {
                carObj.gameObject.SetActive(false);
            }

            if (assembledWheelVisual == null)
            {
                var fl = transform.Find("Classic Car_9/Classic Car_9 FL Tire");
                if (fl != null) assembledWheelVisual = fl.gameObject;
            }
            if (jackStandObject == null)
            {
                var js = transform.Find("Placeholder_JackStand");
                if (js != null) jackStandObject = js.gameObject;
            }
            if (batteryInBayVisual == null)
            {
                var bb = transform.Find("Hotspot_Battery_EngineBay/Placeholder_MountedBattery");
                if (bb != null) batteryInBayVisual = bb.gameObject;
            }
            if (wheelHotspot == null) wheelHotspot = transform.Find("Hotspot_Wheel_FL")?.GetComponent<BunkerCarAssemblyHotspot>();
            if (batteryHotspot == null) batteryHotspot = transform.Find("Hotspot_Battery_EngineBay")?.GetComponent<BunkerCarAssemblyHotspot>();
            if (fuelHotspot == null) fuelHotspot = transform.Find("Hotspot_FuelTank_Inlet")?.GetComponent<BunkerCarAssemblyHotspot>();
            if (carHeadlights == null || carHeadlights.Length == 0)
            {
                var lightsGroup = transform.Find("Car_Headlights");
                if (lightsGroup != null)
                {
                    carHeadlights = lightsGroup.GetComponentsInChildren<Light>(true);
                }
            }

            if (carObj != null)
            {
                GarageDriveOutController.AttachAllComponentsToCar(carObj);
                EnsureBunkerSlotSystemsAndProps(carObj);
            }
        }

        private void EnsureBunkerSlotSystemsAndProps(Transform carObj)
        {
            if (carObj != null)
            {
                // 1. Багажник (VehicleCargoTrunk)
                if (carObj.GetComponent<RogueDrive.Gameplay.VehicleCargoTrunk>() == null)
                {
                    carObj.gameObject.AddComponent<RogueDrive.Gameplay.VehicleCargoTrunk>();
                }

                // 2. Модульное состояние авто (VehicleModularState)
                if (carObj.GetComponent<RogueDrive.Gameplay.VehicleModularState>() == null)
                {
                    carObj.gameObject.AddComponent<RogueDrive.Gameplay.VehicleModularState>();
                }

                // 3. Хотспот багажника со слотами (Inspection_Trunk)
                Transform trunkSpot = carObj.Find("Inspection_Trunk");
                if (trunkSpot == null)
                {
                    var go = new GameObject("Inspection_Trunk");
                    go.transform.SetParent(carObj, false);
                    go.transform.localPosition = new Vector3(0f, 0.85f, -1.85f);
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(1.4f, 0.9f, 1.1f);
                    go.AddComponent<VehicleInspectionHotspot>().Configure(VehicleInspectionHotspot.HotspotType.Trunk);
                }

                // 4. Хотспот капота и радиатора (Inspection_EngineHood)
                Transform hoodSpot = carObj.Find("Inspection_EngineHood");
                if (hoodSpot == null)
                {
                    var go = new GameObject("Inspection_EngineHood");
                    go.transform.SetParent(carObj, false);
                    go.transform.localPosition = new Vector3(0f, 0.85f, 1.65f);
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(1.4f, 0.9f, 1.1f);
                    go.AddComponent<VehicleInspectionHotspot>().Configure(VehicleInspectionHotspot.HotspotType.EngineHood);
                }
            }

            // 5. Контекстная панель интерфейса (VehicleDashboardPanelsUI)
            if (RogueDrive.UI.VehicleDashboardPanelsUI.Instance == null)
            {
                var uiGo = new GameObject("VehicleDashboardPanelsUI", typeof(RogueDrive.UI.VehicleDashboardPanelsUI));
                DontDestroyOnLoad(uiGo);
            }

            // 6. Интерактивный топор Axe в бункере
            GameObject axeObj = GameObject.Find("Axe");
            if (axeObj != null)
            {
                var col = axeObj.GetComponent<Collider>();
                if (col == null)
                {
                    var box = axeObj.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.35f, 0.75f, 0.15f);
                }
                var pickup = axeObj.GetComponent<PocketItemPickup>() ?? axeObj.AddComponent<PocketItemPickup>();
                pickup.Configure("weapon_axe", "Пожарный топор", 1);
            }

            // 7. Карта эвакуации — объект сцены (Evacuation_Map), см. BunkerScenesAuthoring.

            // 8. Запасная канистра воды для радиатора
            EnsureSpareWaterCanister();
        }

        private void EnsureSpareWaterCanister()
        {
            if (GameObject.Find("Pickup_WaterCanister") != null) return;

            Transform shelf = GameObject.Find("StorageShelfFull")?.transform ?? GameObject.Find("Assembly_Workbench")?.transform;
            Vector3 pos = shelf != null ? shelf.position + Vector3.up * 0.9f : new Vector3(-6.5f, 0.5f, 2.5f);

            GameObject can = GameObject.CreatePrimitive(PrimitiveType.Cube);
            can.name = "Pickup_WaterCanister";
            can.transform.position = pos;
            can.transform.localScale = new Vector3(0.32f, 0.45f, 0.28f);

            var r = can.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(0.15f, 0.55f, 0.85f); // синяя канистра воды
            }

            var item = can.AddComponent<BunkerAssemblyItem>();
            item.Configure(BunkerAssemblyItemType.WaterCanister, "Канистра дистиллированной воды (10 л)");
            can.AddComponent<FluidContainer>().Configure(BunkerFluidType.Water, 20f, 10f);
            can.AddComponent<GarageItemUse>().Configure(GarageItemUse.UseKind.Fluid);
        }

        private void Start()
        {
            Transform carObj = transform.Find("Classic Car_9") ?? GameObject.Find("Classic Car_9")?.transform;
            if (carObj != null)
            {
                GarageDriveOutController.AttachAllComponentsToCar(carObj);
                EnsureBunkerSlotSystemsAndProps(carObj);
            }
            BindHotspots();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BindHotspots()
        {
            wheelHotspot?.RestoreInstalledState(isWheelInstalled);
            batteryHotspot?.RestoreInstalledState(isBatteryInstalled);
            fuelHotspot?.RestoreInstalledState(isFuelFilled);
            if (wheelHotspot != null)
            {
                wheelHotspot.Assembled -= OnWheelAssembled;
                wheelHotspot.Assembled += OnWheelAssembled;
                if (assembledWheelVisual != null && jackStandObject != null)
                {
                    wheelHotspot.BindVisuals(assembledWheelVisual, jackStandObject);
                }
            }

            if (batteryHotspot != null)
            {
                batteryHotspot.Assembled -= OnBatteryAssembled;
                batteryHotspot.Assembled += OnBatteryAssembled;
                if (batteryInBayVisual != null)
                {
                    batteryHotspot.BindVisuals(batteryInBayVisual, null);
                }
            }

            if (fuelHotspot != null)
            {
                fuelHotspot.Assembled -= OnFuelAssembled;
                fuelHotspot.Assembled += OnFuelAssembled;
            }
        }

        public void WheelRemoved()
        {
            isWheelInstalled=false;isAssemblyComplete=false;
            if(jackStandObject!=null)jackStandObject.SetActive(true);
            GaragePrologueManager.Instance?.RefreshObjective();
        }
        private void OnWheelAssembled(VehiclePartHotspot spot)
        {
            isWheelInstalled = true;
            if (jackStandObject != null) jackStandObject.SetActive(false);
            if (assembledWheelVisual != null) assembledWheelVisual.SetActive(true);

            NotifyStep("✔ Колесо закручено! Седан встал на амортизаторы.");
            if (GaragePrologueManager.Instance != null) GaragePrologueManager.Instance.RefreshObjective();
            CheckOverallAssembly();
        }

        public void InstallWheel()
        {
            if (isWheelInstalled) return;
            OnWheelAssembled(null);
        }

        public void InstallBattery()
        {
            if (isBatteryInstalled) return;
            OnBatteryAssembled(null);
        }

        public void AddFuel(float liters)
        {
            isFuelFilled = true;
            NotifyStep($"✔ Залито {liters:0} л бензина в бак!");
            if (GaragePrologueManager.Instance != null) GaragePrologueManager.Instance.RefreshObjective();
            CheckOverallAssembly();
        }

        public void RestorePreparedAssembly()
        {
            isWheelInstalled = isBatteryInstalled = isFuelFilled = isAssemblyComplete = true;
            SetHeadlights(true);
            if (jackStandObject != null) jackStandObject.SetActive(false);
            if (assembledWheelVisual != null) assembledWheelVisual.SetActive(true);
            if (batteryInBayVisual != null) batteryInBayVisual.SetActive(true);
            BindHotspots();
        }

        private void OnBatteryAssembled(VehiclePartHotspot spot)
        {
            isBatteryInstalled = true;
            if (batteryInBayVisual != null) batteryInBayVisual.SetActive(true);

            // Оживает электрика автомобиля
            SetHeadlights(true);

            NotifyStep("✔ Аккумулятор подключен! Бортовая сеть и фары ожили.");
            if (GaragePrologueManager.Instance != null) GaragePrologueManager.Instance.RefreshObjective();
            CheckOverallAssembly();
        }

        private void OnFuelAssembled(VehiclePartHotspot spot)
        {
            isFuelFilled = true;
            NotifyStep($"Заправлено {spot.LastTransferredLiters:0.#} л бензина. Двигатель готов к запуску.");
            if (GaragePrologueManager.Instance != null) GaragePrologueManager.Instance.RefreshObjective();
            CheckOverallAssembly();
        }

        private void CheckOverallAssembly()
        {
            if (isWheelInstalled && isBatteryInstalled && isFuelFilled && !isAssemblyComplete)
            {
                isAssemblyComplete = true;
                AssemblyCompleted?.Invoke();

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayLevelUp();
                }

                if (GaragePrologueManager.Instance != null)
                {
                    GaragePrologueManager.Instance.RefreshObjective();
                    GaragePrologueManager.Instance.ShowNotification(
                        GarageDeparturePreparation.Instance != null ? "Автомобиль готов. Изучите карту маршрута у стеллажей [E]." : GaragePrologueManager.Instance.HasCarKeys
                            ? "Автомобиль готов. Откройте ворота на пульте у выезда."
                            : "Автомобиль готов. Заберите ключи с верстака.", 5f);
                }
            }
        }

        private void NotifyStep(string message)
        {
            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.ShowNotification(message, 3.5f);
            }
        }

        private void SetHeadlights(bool on)
        {
            if (carHeadlights == null) return;
            for (int i = 0; i < carHeadlights.Length; i++)
            {
                if (carHeadlights[i] != null) carHeadlights[i].enabled = on;
            }
        }
    }
}

