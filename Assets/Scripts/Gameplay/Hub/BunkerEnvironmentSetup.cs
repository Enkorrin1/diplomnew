using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Автоматически выстраивает структуру подземного бункера с временными заглушками:
    /// 1. Жилой угол: кровать со спальником, тумбочка с шипящей рацией, катсцена пробуждения.
    /// 2. Силовой угол: дизель-генератор с рубильником.
    /// 3. Зона выезда: наклонный пандус вверх и двустворчатые распашные гермоворота.
    /// 4. Машина: зоны 3D-инспекции и прокачки по взгляду (CarInspectionRig).
    /// Пользователь в любой момент сможет заменить заглушки на свои модели из Blender!
    /// </summary>
    public sealed class BunkerEnvironmentSetup : MonoBehaviour
    {
        [Header("Setup Options")]
        [SerializeField] private bool buildPlaceholdersOnAwake = false; // Отключено: объекты должны быть статично расставлены в сцене

        private void Awake()
        {
            if (buildPlaceholdersOnAwake)
            {
                SetupBunker();
            }
        }

        [ContextMenu("Создать заглушки в сцене (для ручной настройки)")]
        public void SetupBunker()
        {
            Transform bunkerRoot = transform;

            // 1. ЖИЛОЙ УГОЛ (КРОВАТЬ И РАЦИЯ)
            Transform livingCorner = bunkerRoot.Find("Living_Corner");
            if (livingCorner == null)
            {
                livingCorner = new GameObject("Living_Corner").transform;
                livingCorner.SetParent(bunkerRoot, false);
                livingCorner.localPosition = new Vector3(-8.5f, 0f, -8.5f);

                // Кровать / раскладушка
                GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bed.name = "Placeholder_Bed";
                bed.transform.SetParent(livingCorner, false);
                bed.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                bed.transform.localScale = new Vector3(1.2f, 0.4f, 2.2f);
                SetColor(bed, new Color(0.20f, 0.32f, 0.28f)); // защитный армейский брезент

                // Подушка
                GameObject pillow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillow.name = "Placeholder_Pillow";
                pillow.transform.SetParent(bed.transform, false);
                pillow.transform.localPosition = new Vector3(0f, 0.65f, 0.35f);
                pillow.transform.localScale = new Vector3(0.85f, 0.35f, 0.25f);
                SetColor(pillow, new Color(0.45f, 0.42f, 0.38f));

                // Тумбочка из ящика
                GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Placeholder_Nightstand";
                table.transform.SetParent(livingCorner, false);
                table.transform.localPosition = new Vector3(1.1f, 0.35f, 0.7f);
                table.transform.localScale = new Vector3(0.65f, 0.7f, 0.65f);
                SetColor(table, new Color(0.35f, 0.28f, 0.20f)); // дерево

                // Рация с антенной
                GameObject radio = GameObject.CreatePrimitive(PrimitiveType.Cube);
                radio.name = "Placeholder_Radio";
                radio.transform.SetParent(table.transform, false);
                radio.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                radio.transform.localScale = new Vector3(0.45f, 0.35f, 0.30f);
                SetColor(radio, new Color(0.15f, 0.16f, 0.18f)); // черный военный пластик

                // Антенна
                GameObject ant = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ant.name = "Antenna";
                ant.transform.SetParent(radio.transform, false);
                ant.transform.localPosition = new Vector3(0.35f, 0.8f, 0f);
                ant.transform.localScale = new Vector3(0.04f, 0.6f, 0.04f);
                SetColor(ant, Color.gray);
            }

            // Навешиваем катсцену на жилой угол
            var cutscene = GetComponent<BunkerPrologueCutscene>();
            if (cutscene == null) cutscene = gameObject.AddComponent<BunkerPrologueCutscene>();

            // 2. ВЫЕЗДНОЙ ПАНДУС И РАСПАШНЫЕ ВОРОТА
            Transform exitRamp = bunkerRoot.Find("Exit_Ramp_And_Gate");
            if (exitRamp == null)
            {
                exitRamp = new GameObject("Exit_Ramp_And_Gate").transform;
                exitRamp.SetParent(bunkerRoot, false);
                exitRamp.localPosition = new Vector3(0f, 0f, 10.5f);

                // Наклонная рампа пола (пандус выезда вверх на поверхность)
                GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ramp.name = "Placeholder_InclineRamp";
                ramp.transform.SetParent(exitRamp, false);
                ramp.transform.localPosition = new Vector3(0f, 1.25f, -2.5f);
                ramp.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f); // подъем вверх
                ramp.transform.localScale = new Vector3(8.5f, 0.4f, 12f);
                SetColor(ramp, new Color(0.18f, 0.19f, 0.22f)); // рифленый бетон

                // Распашные гермоворота
                GameObject gateRoot = new GameObject("Bunker_SwingBlastGate");
                gateRoot.transform.SetParent(exitRamp, false);
                gateRoot.transform.localPosition = new Vector3(0f, 2.5f, 3.5f);
                gateRoot.AddComponent<GarageSwingGateController>();

                // Пульт управления воротами на стене
                GameObject console = GameObject.CreatePrimitive(PrimitiveType.Cube);
                console.name = "Gate_Control_Terminal";
                console.transform.SetParent(exitRamp, false);
                console.transform.localPosition = new Vector3(4.8f, 3.2f, 2.8f);
                console.transform.localScale = new Vector3(0.4f, 0.7f, 0.5f);
                SetColor(console, new Color(0.25f, 0.28f, 0.30f));
            }

            // 3. ЗОНЫ СБОРКИ, 3D-ИНСПЕКЦИИ И ПРОКАЧКИ НА МАШИНЕ
            var carPodium = GameObject.Find("PodiumAnchor") ?? GameObject.Find("Car_Podium") ?? GameObject.Find("PlayerCar_Podium");
            if (carPodium != null)
            {
                var rig = carPodium.GetComponent<CarInspectionRig>();
                if (rig == null) rig = carPodium.AddComponent<CarInspectionRig>();
                rig.SetupHotspots();

                // Убеждаемся, что зона посадки расположена у водительской двери
                Transform boardingZone = carPodium.transform.Find("VehicleBoardingZone");
                if (boardingZone == null)
                {
                    var bObj = new GameObject("VehicleBoardingZone");
                    bObj.transform.SetParent(carPodium.transform, false);
                    bObj.transform.localPosition = new Vector3(-1.3f, 0.8f, 0.15f);
                    boardingZone = bObj.transform;
                }
                var bCol = boardingZone.GetComponent<BoxCollider>();
                if (bCol == null) bCol = boardingZone.gameObject.AddComponent<BoxCollider>();
                bCol.isTrigger = true;
                bCol.size = new Vector3(1.2f, 1.8f, 1.8f);

                if (boardingZone.GetComponent<GarageVehicleBoarding>() == null)
                {
                    boardingZone.gameObject.AddComponent<GarageVehicleBoarding>();
                }

                var oldPodiumBoarding = carPodium.GetComponent<GarageVehicleBoarding>();
                if (oldPodiumBoarding != null)
                {
                    Destroy(oldPodiumBoarding);
                }

                // Стартовая сборка автомобиля (колесо, АКБ, топливо)
                Transform targetCar = carPodium.transform.Find("Classic Car_9") ?? carPodium.transform;
                SetupStarterCarAssembly(targetCar);
                GarageDriveOutController.AttachAllComponentsToCar(targetCar);
            }

            // 4. СТЕЛЛАЖ И ВЕРСТАК С ПРЕДМЕТАМИ ДЛЯ СБОРКИ
            SetupAssemblyPickups(bunkerRoot);

            // 5. КОНТРОЛЛЕР ИГРОКА ОТ 1-ГО ЛИЦА
            EnsurePlayerController();
        }

        private void SetupStarterCarAssembly(Transform car)
        {
            var assembly = car.GetComponent<BunkerStarterCarAssembly>();
            if (assembly == null) assembly = car.gameObject.AddComponent<BunkerStarterCarAssembly>();

            // 1. Хотспот переднего левого колеса
            Transform wheelSpot = car.Find("Hotspot_Wheel_FL");
            if (wheelSpot == null)
            {
                var spotObj = new GameObject("Hotspot_Wheel_FL");
                spotObj.transform.SetParent(car, false);
                spotObj.transform.localPosition = new Vector3(-0.95f, 0.35f, 1.2f);

                var box = spotObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.8f, 0.8f, 0.8f);

                var hotspot = spotObj.AddComponent<BunkerCarAssemblyHotspot>();
                hotspot.Configure(BunkerAssemblyItemType.Wheel, "Передняя левая ступица", "на стеллаже у стены");

                // Домкрат (временная подпорка)
                GameObject jack = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                jack.name = "Placeholder_JackStand";
                jack.transform.SetParent(spotObj.transform, false);
                jack.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                jack.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                SetColor(jack, new Color(0.85f, 0.65f, 0.1f)); // желтый технический

                // Колесо (появляется после сборки)
                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Placeholder_MountedWheel";
                wheel.transform.SetParent(spotObj.transform, false);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.65f, 0.25f, 0.65f);
                SetColor(wheel, new Color(0.12f, 0.12f, 0.12f));
                wheel.SetActive(false);

                hotspot.BindVisuals(wheel, jack);
            }

            // 2. Хотспот аккумулятора под капотом
            Transform battSpot = car.Find("Hotspot_Battery_EngineBay");
            if (battSpot == null)
            {
                var spotObj = new GameObject("Hotspot_Battery_EngineBay");
                spotObj.transform.SetParent(car, false);
                spotObj.transform.localPosition = new Vector3(0.35f, 0.75f, 1.45f);

                var box = spotObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.7f, 0.6f, 0.7f);

                var hotspot = spotObj.AddComponent<BunkerCarAssemblyHotspot>();
                hotspot.Configure(BunkerAssemblyItemType.Battery, "Гнездо аккумулятора под капотом", "на верстаке в жилой зоне");

                // АКБ (появляется после сборки)
                GameObject batt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                batt.name = "Placeholder_MountedBattery";
                batt.transform.SetParent(spotObj.transform, false);
                batt.transform.localScale = new Vector3(0.28f, 0.22f, 0.2f);
                SetColor(batt, new Color(0.15f, 0.18f, 0.22f));
                batt.SetActive(false);

                hotspot.BindVisuals(batt, null);
            }

            // 3. Хотспот горловины бензобака
            Transform fuelSpot = car.Find("Hotspot_FuelTank_Inlet");
            if (fuelSpot == null)
            {
                var spotObj = new GameObject("Hotspot_FuelTank_Inlet");
                spotObj.transform.SetParent(car, false);
                spotObj.transform.localPosition = new Vector3(-0.95f, 0.75f, -1.25f);

                var box = spotObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.6f, 0.6f, 0.6f);

                var hotspot = spotObj.AddComponent<BunkerCarAssemblyHotspot>();
                hotspot.Configure(BunkerAssemblyItemType.FuelCanister, "Горловина бензобака", "рядом с дизель-генератором");
            }

            // 4. Контекстные хотспоты обслуживания (ДВС/радиатор, багажник)
            Transform inspHood = car.Find("Inspection_EngineHood");
            if (inspHood == null)
            {
                var hoodObj = new GameObject("Inspection_EngineHood");
                hoodObj.transform.SetParent(car, false);
                hoodObj.transform.localPosition = new Vector3(0f, 0.85f, 1.6f);
                var box = hoodObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.2f, 0.6f, 1.0f);
                hoodObj.AddComponent<VehicleInspectionHotspot>().Configure(VehicleInspectionHotspot.HotspotType.EngineHood);
            }

            Transform inspTrunk = car.Find("Inspection_Trunk");
            if (inspTrunk == null)
            {
                var trunkObj = new GameObject("Inspection_Trunk");
                trunkObj.transform.SetParent(car, false);
                trunkObj.transform.localPosition = new Vector3(0f, 0.85f, -1.8f);
                var box = trunkObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.2f, 0.6f, 1.0f);
                trunkObj.AddComponent<VehicleInspectionHotspot>().Configure(VehicleInspectionHotspot.HotspotType.Trunk);
            }

            // 5. Контроллер посадки / высадки водителя
            if (car.GetComponent<VehiclePassengerEntry>() == null)
            {
                car.gameObject.AddComponent<VehiclePassengerEntry>();
            }

            assembly.BindHotspots();
        }

        private void SetupAssemblyPickups(Transform bunkerRoot)
        {
            Transform workshop = bunkerRoot.Find("Workshop_Corner");
            if (workshop == null)
            {
                workshop = new GameObject("Workshop_Corner").transform;
                workshop.SetParent(bunkerRoot, false);
                workshop.localPosition = new Vector3(-8.5f, 0f, 0f);

                // Стол / верстак
                GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bench.name = "Assembly_Workbench";
                bench.transform.SetParent(workshop, false);
                bench.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                bench.transform.localScale = new Vector3(1.2f, 0.9f, 2.8f);
                SetColor(bench, new Color(0.28f, 0.24f, 0.20f));

                // 1. Аккумулятор на верстаке
                GameObject battObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                battObj.name = "Pickup_Battery";
                battObj.transform.SetParent(bench.transform, false);
                battObj.transform.localPosition = new Vector3(0f, 0.62f, 0.35f);
                battObj.transform.localScale = new Vector3(0.35f, 0.28f, 0.25f);
                SetColor(battObj, new Color(0.18f, 0.22f, 0.30f));
                var battItem = battObj.AddComponent<BunkerAssemblyItem>();
                battItem.Configure(BunkerAssemblyItemType.Battery, "Силовой аккумулятор 12V");

                // 2. Монтировка на верстаке
                GameObject barObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barObj.name = "Pickup_Crowbar";
                barObj.transform.SetParent(bench.transform, false);
                barObj.transform.localPosition = new Vector3(0.2f, 0.58f, -0.4f);
                barObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                barObj.transform.localScale = new Vector3(0.04f, 0.5f, 0.04f);
                SetColor(barObj, new Color(0.7f, 0.15f, 0.12f)); // красный металл
                var barItem = barObj.AddComponent<BunkerAssemblyItem>();
                barItem.Configure(BunkerAssemblyItemType.Crowbar, "Стальная монтировка");
            }

            // 3. Колесо на полу / стеллаже у стены
            Transform tireRack = bunkerRoot.Find("Tire_Rack_Spot");
            if (tireRack == null)
            {
                tireRack = new GameObject("Tire_Rack_Spot").transform;
                tireRack.SetParent(bunkerRoot, false);
                tireRack.localPosition = new Vector3(-8.5f, 0f, 3.5f);

                GameObject wheelObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelObj.name = "Pickup_SpareWheel";
                wheelObj.transform.SetParent(tireRack, false);
                wheelObj.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                wheelObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelObj.transform.localScale = new Vector3(0.75f, 0.28f, 0.75f);
                SetColor(wheelObj, new Color(0.12f, 0.12f, 0.14f));
                var wheelItem = wheelObj.AddComponent<BunkerAssemblyItem>();
                wheelItem.Configure(BunkerAssemblyItemType.Wheel, "Колесо со ступичным креплением");
            }

            // 4. Канистра топлива возле генератора
            Transform genCorner = bunkerRoot.Find("Power_Corner") ?? bunkerRoot.Find("Generator_Corner");
            if (genCorner != null && genCorner.Find("Pickup_FuelCanister") == null)
            {
                GameObject canObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                canObj.name = "Pickup_FuelCanister";
                canObj.transform.SetParent(genCorner, false);
                canObj.transform.localPosition = new Vector3(1.2f, 0.35f, 0f);
                canObj.transform.localScale = new Vector3(0.35f, 0.55f, 0.45f);
                SetColor(canObj, new Color(0.75f, 0.18f, 0.14f)); // красная канистра
                var canItem = canObj.AddComponent<BunkerAssemblyItem>();
                canItem.Configure(BunkerAssemblyItemType.FuelCanister, "Канистра бензина (15 л)");
            }
        }

        private void EnsurePlayerController()
        {
            var existingPlayer = FindFirstObjectByType<GaragePlayerController>();
            if (existingPlayer != null)
            {
                if (existingPlayer.GetComponent<BunkerPlayerInventory>() == null)
                {
                    existingPlayer.gameObject.AddComponent<BunkerPlayerInventory>();
                }
                return;
            }

            GameObject playerObj = new GameObject("Bunker_FP_Player");
            playerObj.transform.position = new Vector3(-8.5f, 0.1f, -7.5f);
            playerObj.transform.rotation = Quaternion.Euler(0f, 45f, 0f); // смотрит в сторону машины

            var cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var pc = playerObj.AddComponent<GaragePlayerController>();
            playerObj.AddComponent<GarageInteractionRaycaster>();
            playerObj.AddComponent<BunkerPlayerInventory>();

            GameObject camObj = new GameObject("PlayerCamera");
            camObj.transform.SetParent(playerObj.transform, false);
            camObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.1f;
            camObj.tag = "MainCamera";

            // Отключаем старые статичные камеры
            Camera[] allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in allCams)
            {
                if (c != cam) c.gameObject.SetActive(false);
            }
        }

        private static void SetColor(GameObject obj, Color color)
        {
            var r = obj.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
            }
        }
    }
}
