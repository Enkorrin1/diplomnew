using System;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Editor
{
    /// <summary>
    /// Редакторский инструмент расстановки статических объектов бункера в GarageScene.
    /// Создает постоянные GameObjects в сцене (жилой угол, койку, рацию, пандус, распашные гермоворота,
    /// зоны осмотра машины и игрока от первого лица).
    /// Объекты сохраняются прямо в файл сцены GarageScene.unity — пользователь может свободно
    /// перемещать их в окне Scene и заменять на свои модели из Blender!
    /// </summary>
    public static class BunkerSceneAuthoring
    {
        [MenuItem("RogueDrive/Расставить объекты бункера в сцену (Bake Layout)")]
        public static void BakeBunkerLayoutMenu()
        {
            BakeBunkerLayout(forceSave: true);
        }

        public static void BakeBunkerLayout(bool forceSave)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.name.Equals("GarageScene", StringComparison.OrdinalIgnoreCase))
            {
                // Если открыта другая сцена, открываем GarageScene
                string scenePath = "Assets/Scenes/GarageScene.unity";
                if (System.IO.File.Exists(scenePath))
                {
                    activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                else
                {
                    Debug.LogWarning("[BunkerSceneAuthoring] GarageScene.unity не найдена по пути " + scenePath);
                    return;
                }
            }

            Transform hubRoot = GameObject.Find("GarageHubRoot")?.transform;
            if (hubRoot == null)
            {
                hubRoot = new GameObject("GarageHubRoot").transform;
                Undo.RegisterCreatedObjectUndo(hubRoot.gameObject, "Create GarageHubRoot");
            }

            Material concreteMat = GetOrCreateMaterial("Bunker_Concrete_Mat", new Color(0.22f, 0.24f, 0.26f));
            Material steelMat = GetOrCreateMaterial("Bunker_Steel_Mat", new Color(0.18f, 0.20f, 0.22f));
            Material armyGreenMat = GetOrCreateMaterial("Bunker_ArmyGreen_Mat", new Color(0.20f, 0.32f, 0.24f));
            Material woodMat = GetOrCreateMaterial("Bunker_Wood_Mat", new Color(0.35f, 0.26f, 0.18f));
            Material hazardMat = GetOrCreateMaterial("Bunker_Hazard_Mat", new Color(0.95f, 0.75f, 0.1f));
            Material darkPlasticMat = GetOrCreateMaterial("Bunker_DarkPlastic_Mat", new Color(0.12f, 0.13f, 0.14f));

            // =========================================================================
            // 1. ЗОНА 1: ЖИЛОЙ УГОЛ (Living_Corner)
            // =========================================================================
            Transform livingCorner = hubRoot.Find("Living_Corner");
            if (livingCorner == null)
            {
                livingCorner = new GameObject("Living_Corner").transform;
                livingCorner.SetParent(hubRoot, false);
                livingCorner.localPosition = new Vector3(-8.5f, 0f, -8.5f);
                Undo.RegisterCreatedObjectUndo(livingCorner.gameObject, "Create Living_Corner");
            }

            // Койка / раскладушка
            Transform bed = livingCorner.Find("Placeholder_Bed");
            if (bed == null)
            {
                GameObject bedObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bedObj.name = "Placeholder_Bed";
                bedObj.transform.SetParent(livingCorner, false);
                bedObj.transform.localPosition = new Vector3(0f, 0.225f, 0f);
                bedObj.transform.localScale = new Vector3(1.2f, 0.45f, 2.0f);
                bedObj.GetComponent<Renderer>().sharedMaterial = armyGreenMat;
                bed = bedObj.transform;

                // Подушка
                GameObject pillow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillow.name = "Placeholder_Pillow";
                pillow.transform.SetParent(bed, false);
                pillow.transform.localPosition = new Vector3(0f, 0.35f, -0.65f);
                pillow.transform.localScale = new Vector3(0.85f, 0.25f, 0.35f);
                pillow.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Bunker_Khaki_Mat", new Color(0.48f, 0.45f, 0.38f));

                // Спальник / плед
                GameObject blanket = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blanket.name = "Placeholder_Blanket";
                blanket.transform.SetParent(bed, false);
                blanket.transform.localPosition = new Vector3(0f, 0.3f, 0.2f);
                blanket.transform.localScale = new Vector3(1.15f, 0.2f, 1.3f);
                blanket.GetComponent<Renderer>().sharedMaterial = armyGreenMat;
            }

            // Прикроватная тумбочка (ящик)
            Transform nightstand = livingCorner.Find("Placeholder_Nightstand");
            if (nightstand == null)
            {
                GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Placeholder_Nightstand";
                table.transform.SetParent(livingCorner, false);
                table.transform.localPosition = new Vector3(1.2f, 0.325f, -0.6f);
                table.transform.localScale = new Vector3(0.6f, 0.65f, 0.5f);
                table.GetComponent<Renderer>().sharedMaterial = woodMat;
                nightstand = table.transform;

                // Полевая рация на тумбочке
                GameObject radio = GameObject.CreatePrimitive(PrimitiveType.Cube);
                radio.name = "Placeholder_Radio";
                radio.transform.SetParent(nightstand, false);
                radio.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                radio.transform.localScale = new Vector3(0.35f, 0.25f, 0.22f);
                radio.GetComponent<Renderer>().sharedMaterial = darkPlasticMat;

                // Антенна рации
                GameObject ant = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ant.name = "Placeholder_Antenna";
                ant.transform.SetParent(radio.transform, false);
                ant.transform.localPosition = new Vector3(0.12f, 0.4f, 0f);
                ant.transform.localScale = new Vector3(0.03f, 0.6f, 0.03f);
                ant.GetComponent<Renderer>().sharedMaterial = steelMat;
            }

            // Навешиваем скрипт катсцены пробуждения
            var cutscene = hubRoot.GetComponent<BunkerPrologueCutscene>();
            if (cutscene == null) cutscene = hubRoot.gameObject.AddComponent<BunkerPrologueCutscene>();

            // =========================================================================
            // 2. ЗОНА 2: СИЛОВАЯ ЗОНА (Дизель-генератор на полу)
            // =========================================================================
            Transform generatorBox = hubRoot.Find("Environment/GeneratorSwitchBox") ?? GameObject.Find("GeneratorSwitchBox")?.transform;
            if (generatorBox != null)
            {
                Transform genFloor = hubRoot.Find("Placeholder_DieselGenerator");
                if (genFloor == null)
                {
                    GameObject gen = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gen.name = "Placeholder_DieselGenerator";
                    gen.transform.SetParent(hubRoot, false);
                    gen.transform.localPosition = new Vector3(11.2f, 0.55f, 0f);
                    gen.transform.localScale = new Vector3(0.8f, 1.1f, 1.4f);
                    gen.GetComponent<Renderer>().sharedMaterial = steelMat;

                    // Выхлопная труба
                    GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pipe.name = "Exhaust_Pipe";
                    pipe.transform.SetParent(gen.transform, false);
                    pipe.transform.localPosition = new Vector3(0.35f, 0.6f, 0f);
                    pipe.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                    pipe.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f);
                    pipe.GetComponent<Renderer>().sharedMaterial = darkPlasticMat;
                }
            }

            // =========================================================================
            // 3. ЗОНА 5: ВЫЕЗДНОЙ ПАНДУС И РАСПАШНЫЕ ВОРОТА (Exit_Ramp_And_Gate)
            // =========================================================================
            Transform exitRampGroup = hubRoot.Find("Exit_Ramp_And_Gate");
            if (exitRampGroup == null)
            {
                exitRampGroup = new GameObject("Exit_Ramp_And_Gate").transform;
                exitRampGroup.SetParent(hubRoot, false);
                exitRampGroup.localPosition = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(exitRampGroup.gameObject, "Create Exit_Ramp_And_Gate");
            }

            // Наклонный пандус вверх
            Transform ramp = exitRampGroup.Find("Placeholder_InclineRamp");
            if (ramp == null)
            {
                GameObject rampObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rampObj.name = "Placeholder_InclineRamp";
                rampObj.transform.SetParent(exitRampGroup, false);
                rampObj.transform.localPosition = new Vector3(0f, 0.8f, 9.5f);
                rampObj.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                rampObj.transform.localScale = new Vector3(8.0f, 0.3f, 10.0f);
                rampObj.GetComponent<Renderer>().sharedMaterial = concreteMat;
            }

            // Распашные броневорота
            Transform gateGroup = exitRampGroup.Find("Bunker_SwingBlastGate");
            if (gateGroup == null)
            {
                GameObject gateObj = new GameObject("Bunker_SwingBlastGate");
                gateObj.transform.SetParent(exitRampGroup, false);
                gateObj.transform.localPosition = new Vector3(0f, 1.8f, 13.8f);

                var gateCtrl = gateObj.AddComponent<GarageSwingGateController>();

                // Левая петля и створка
                GameObject leftHinge = new GameObject("Door_Left_Hinge");
                leftHinge.transform.SetParent(gateObj.transform, false);
                leftHinge.transform.localPosition = new Vector3(-3.5f, 0f, 0f);

                GameObject leftPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftPlate.name = "Door_Left_Plate";
                leftPlate.transform.SetParent(leftHinge.transform, false);
                leftPlate.transform.localPosition = new Vector3(1.75f, 1.8f, 0f);
                leftPlate.transform.localScale = new Vector3(3.5f, 3.8f, 0.35f);
                leftPlate.GetComponent<Renderer>().sharedMaterial = steelMat;

                // Правая петля и створка
                GameObject rightHinge = new GameObject("Door_Right_Hinge");
                rightHinge.transform.SetParent(gateObj.transform, false);
                rightHinge.transform.localPosition = new Vector3(3.5f, 0f, 0f);

                GameObject rightPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightPlate.name = "Door_Right_Plate";
                rightPlate.transform.SetParent(rightHinge.transform, false);
                rightPlate.transform.localPosition = new Vector3(-1.75f, 1.8f, 0f);
                rightPlate.transform.localScale = new Vector3(3.5f, 3.8f, 0.35f);
                rightPlate.GetComponent<Renderer>().sharedMaterial = steelMat;

                // Пульт ворот на стене
                GameObject console = GameObject.CreatePrimitive(PrimitiveType.Cube);
                console.name = "Gate_Control_Terminal";
                console.transform.SetParent(gateObj.transform, false);
                console.transform.localPosition = new Vector3(4.2f, 1.2f, -0.6f);
                console.transform.localScale = new Vector3(0.4f, 0.8f, 0.5f);
                console.GetComponent<Renderer>().sharedMaterial = hazardMat;

                var gateInter = console.AddComponent<GarageGateSwitchInteractable>();
            }
            else
            {
                Transform terminal = gateGroup.Find("Gate_Control_Terminal");
                if (terminal != null)
                {
                    var gateCtrl = gateGroup.GetComponent<GarageSwingGateController>();
                    var gateInter = terminal.GetComponent<GarageGateSwitchInteractable>();
                    if (gateInter == null)
                    {
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(terminal.gameObject);
                        gateInter = terminal.gameObject.AddComponent<GarageGateSwitchInteractable>();
                    }
                    if (gateCtrl != null) gateInter.Configure(gateCtrl);
                }
            }

            // =========================================================================
            // 4. ПЕРСОНАЖ ОТ 1-ГО ЛИЦА (Garage_FP_Player)
            // =========================================================================
            var existingPlayer = GameObject.FindFirstObjectByType<GaragePlayerController>();
            if (existingPlayer != null)
            {
                if (existingPlayer.GetComponent<PlayerPocketInventory>() == null)
                {
                    existingPlayer.gameObject.AddComponent<PlayerPocketInventory>();
                }
            }
            else
            {
                GameObject playerObj = new GameObject("Garage_FP_Player");
                playerObj.transform.position = new Vector3(-8.5f, 0.05f, -7.0f);
                playerObj.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

                var cc = playerObj.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0f, 0.9f, 0f);

                playerObj.AddComponent<GaragePlayerController>();
                playerObj.AddComponent<GarageInteractionRaycaster>();
                playerObj.AddComponent<BunkerPlayerInventory>();
                playerObj.AddComponent<PlayerPocketInventory>();

                GameObject camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(playerObj.transform, false);
                camObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);

                Camera cam = camObj.AddComponent<Camera>();
                cam.fieldOfView = 75f;
                cam.nearClipPlane = 0.1f;
                camObj.tag = "MainCamera";

                Undo.RegisterCreatedObjectUndo(playerObj, "Create Garage_FP_Player");
            }

            // =========================================================================
            // 5. МАШИННОЕ МЕСТО, СТАРТОВАЯ СБОРКА И 3D ХОТСПАТЫ
            // =========================================================================
            Transform podiumAnchor = GameObject.Find("PodiumAnchor")?.transform;
            if (podiumAnchor != null)
            {
                var rig = podiumAnchor.GetComponent<CarInspectionRig>();
                if (rig == null) rig = podiumAnchor.gameObject.AddComponent<CarInspectionRig>();
                rig.SetupHotspots();

                // Убеждаемся, что зона посадки расположена у водительской двери
                Transform boardingZone = podiumAnchor.Find("VehicleBoardingZone");
                if (boardingZone == null)
                {
                    var bObj = new GameObject("VehicleBoardingZone");
                    bObj.transform.SetParent(podiumAnchor, false);
                    bObj.transform.localPosition = new Vector3(-1.3f, 0.8f, 0.15f);
                    boardingZone = bObj.transform;
                }
                var bCol = boardingZone.GetComponent<BoxCollider>();
                if (bCol == null) bCol = boardingZone.gameObject.AddComponent<BoxCollider>();
                bCol.isTrigger = true;
                bCol.size = new Vector3(1.2f, 1.8f, 1.8f);
                bCol.center = Vector3.zero;

                if (boardingZone.GetComponent<GarageVehicleBoarding>() == null)
                {
                    boardingZone.gameObject.AddComponent<GarageVehicleBoarding>();
                }

                var podiumBoarding = podiumAnchor.GetComponent<GarageVehicleBoarding>();
                if (podiumBoarding != null) UnityEngine.Object.DestroyImmediate(podiumBoarding);

                SetupStarterCarAssemblyAuthoring(podiumAnchor, steelMat, hazardMat, darkPlasticMat);
            }

            // =========================================================================
            // 6. КЛЮЧИ ЗАЖИГАНИЯ НА ВЕРСТАКЕ (CarKeysItem)
            // =========================================================================
            Transform workshopProps = GameObject.Find("WorkshopProps")?.transform ?? hubRoot.Find("WorkshopProps");
            if (workshopProps == null)
            {
                workshopProps = new GameObject("WorkshopProps").transform;
                workshopProps.SetParent(hubRoot, false);
            }

            GameObject oldKeys = GameObject.Find("CarKeysItem");
            if (oldKeys != null)
            {
                UnityEngine.Object.DestroyImmediate(oldKeys);
            }

            GameObject keysObj = new GameObject("CarKeysItem");
            keysObj.transform.SetParent(workshopProps, false);
            keysObj.transform.position = new Vector3(-4.95f, 1.05f, 3.1f);
            keysObj.transform.rotation = Quaternion.Euler(0f, 40f, 0f);

            var sphereCol = keysObj.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 0.35f;
            sphereCol.center = Vector3.zero;

            GameObject keysVisual = new GameObject("Keys_VisualModel");
            keysVisual.transform.SetParent(keysObj.transform, false);
            keysVisual.transform.localPosition = Vector3.zero;
            keysVisual.transform.localRotation = Quaternion.identity;

            GameObject fob = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fob.name = "Fob";
            fob.transform.SetParent(keysVisual.transform, false);
            fob.transform.localPosition = Vector3.zero;
            fob.transform.localScale = new Vector3(0.12f, 0.03f, 0.20f);
            fob.GetComponent<Renderer>().sharedMaterial = darkPlasticMat;
            var fobCol = fob.GetComponent<Collider>();
            if (fobCol != null) UnityEngine.Object.DestroyImmediate(fobCol);

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.SetParent(keysVisual.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0f, -0.12f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(0.08f, 0.015f, 0.08f);
            ring.GetComponent<Renderer>().sharedMaterial = steelMat;
            var ringCol = ring.GetComponent<Collider>();
            if (ringCol != null) UnityEngine.Object.DestroyImmediate(ringCol);

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(keysVisual.transform, false);
            blade.transform.localPosition = new Vector3(0f, 0f, 0.16f);
            blade.transform.localScale = new Vector3(0.035f, 0.015f, 0.14f);
            blade.GetComponent<Renderer>().sharedMaterial = steelMat;
            var bladeCol = blade.GetComponent<Collider>();
            if (bladeCol != null) UnityEngine.Object.DestroyImmediate(bladeCol);

            Transform oldGlow = keysObj.transform.Find("ItemGlowLight");
            if (oldGlow != null) UnityEngine.Object.DestroyImmediate(oldGlow.gameObject);

            var carKeys = keysObj.AddComponent<GarageCarKeys>();
            var keysSo = new SerializedObject(carKeys);
            var visualProp = keysSo.FindProperty("visualModel");
            if (visualProp != null) visualProp.objectReferenceValue = keysVisual;
            keysSo.ApplyModifiedProperties();

            // =========================================================================
            // 7. СТЕЛЛАЖ И ПРЕДМЕТЫ ДЛЯ СБОРКИ АВТОМОБИЛЯ В БУНКЕРЕ
            // =========================================================================
            SetupAssemblyPickupsAuthoring(hubRoot, workshopProps, steelMat, armyGreenMat);

            // Отключаем дубликаты процедурного генератора
            var envSetup = hubRoot.GetComponent<BunkerEnvironmentSetup>();
            if (envSetup != null)
            {
                UnityEngine.Object.DestroyImmediate(envSetup);
            }

            // Сохраняем сцену
            if (forceSave)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("[BunkerSceneAuthoring] ✔ Все объекты бункера успешно расставлены и сохранены в GarageScene.unity!");
            }
        }

        private static void SetupStarterCarAssemblyAuthoring(Transform car, Material steelMat, Material hazardMat, Material darkMat)
        {
            var assembly = car.GetComponent<BunkerStarterCarAssembly>();
            if (assembly == null) assembly = car.gameObject.AddComponent<BunkerStarterCarAssembly>();

            // 0. Размещаем 3D-модель кузова автомобиля Classic Car_9 под PodiumAnchor
            Transform carBodyTransform = car.Find("Classic Car_9");
            GameObject carBody = carBodyTransform != null ? carBodyTransform.gameObject : null;

            if (carBody == null)
            {
                string carPrefabPath = "Assets/Downloads/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Classic Car_9.prefab";
                GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(carPrefabPath);
                if (carPrefab != null)
                {
                    carBody = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, car);
                    carBody.name = "Classic Car_9";
                    carBody.transform.localPosition = Vector3.zero;
                    carBody.transform.localRotation = Quaternion.identity;
                    carBody.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(carBody, "Instantiate Classic Car_9");
                }
                else
                {
                    Debug.LogWarning("[BunkerSceneAuthoring] Префаб Classic Car_9 не найден по пути " + carPrefabPath);
                }
            }

            if (carBody != null)
            {
                // Физический коллайдер кузова, чтобы игрок мог осматривать авто и садиться в него
                var bodyCol = carBody.GetComponent<BoxCollider>();
                if (bodyCol == null) bodyCol = carBody.AddComponent<BoxCollider>();
                bodyCol.center = new Vector3(0f, 0.72f, 0f);
                bodyCol.size = new Vector3(1.75f, 1.25f, 4.3f);
            }

            // 1. Переднее левое колесо на модели кузова (снято в прологе)
            Transform flTire = carBody != null ? carBody.transform.Find("Classic Car_9 FL Tire") : null;
            if (flTire != null)
            {
                int runs = PlayerPrefs.GetInt("GaragePrologueDone", 0);
                bool prologueDone = runs > 0 && !GaragePrologueManager.ForcePrologueAwakening;
                flTire.gameObject.SetActive(prologueDone);
            }

            // 2. Домкрат под передней левой ступицей
            Transform jackStand = car.Find("Placeholder_JackStand");
            if (jackStand == null)
            {
                Transform oldJack = car.Find("Hotspot_Wheel_FL/Placeholder_JackStand");
                if (oldJack != null)
                {
                    oldJack.SetParent(car, true);
                    jackStand = oldJack;
                }
                else
                {
                    GameObject jackObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    jackObj.name = "Placeholder_JackStand";
                    jackObj.transform.SetParent(car, false);
                    jackStand = jackObj.transform;
                }
            }
            jackStand.localPosition = new Vector3(-0.78f, 0.18f, 1.38f);
            jackStand.localScale = new Vector3(0.26f, 0.36f, 0.26f);
            var jackRen = jackStand.GetComponent<Renderer>();
            if (jackRen != null) jackRen.sharedMaterial = hazardMat;

            // 3. Хотспот переднего левого колеса
            Transform wheelSpot = car.Find("Hotspot_Wheel_FL");
            if (wheelSpot == null)
            {
                var spotObj = new GameObject("Hotspot_Wheel_FL");
                spotObj.transform.SetParent(car, false);
                wheelSpot = spotObj.transform;
            }
            wheelSpot.localPosition = new Vector3(-0.95f, 0.39f, 1.38f);

            var wheelBox = wheelSpot.GetComponent<BoxCollider>();
            if (wheelBox == null) wheelBox = wheelSpot.gameObject.AddComponent<BoxCollider>();
            wheelBox.isTrigger = true;
            wheelBox.size = new Vector3(0.85f, 0.85f, 0.85f);
            wheelBox.center = Vector3.zero;

            // Удаляем старый примитив-колесо Placeholder_MountedWheel если был
            Transform oldWheelPrim = wheelSpot.Find("Placeholder_MountedWheel");
            if (oldWheelPrim != null) UnityEngine.Object.DestroyImmediate(oldWheelPrim.gameObject);

            var wheelHotspot = wheelSpot.GetComponent<BunkerCarAssemblyHotspot>();
            if (wheelHotspot == null) wheelHotspot = wheelSpot.gameObject.AddComponent<BunkerCarAssemblyHotspot>();
            wheelHotspot.Configure(BunkerAssemblyItemType.Wheel, "Передняя левая ступица", "на стеллаже у стены");

            // 4. Хотспот аккумулятора под капотом
            Transform battSpot = car.Find("Hotspot_Battery_EngineBay");
            if (battSpot == null)
            {
                var spotObj = new GameObject("Hotspot_Battery_EngineBay");
                spotObj.transform.SetParent(car, false);
                battSpot = spotObj.transform;
            }
            battSpot.localPosition = new Vector3(0.35f, 0.85f, 1.55f);

            var battBox = battSpot.GetComponent<BoxCollider>();
            if (battBox == null) battBox = battSpot.gameObject.AddComponent<BoxCollider>();
            battBox.isTrigger = true;
            battBox.size = new Vector3(0.7f, 0.6f, 0.7f);
            battBox.center = Vector3.zero;

            var battHotspot = battSpot.GetComponent<BunkerCarAssemblyHotspot>();
            if (battHotspot == null) battHotspot = battSpot.gameObject.AddComponent<BunkerCarAssemblyHotspot>();
            battHotspot.Configure(BunkerAssemblyItemType.Battery, "Гнездо аккумулятора под капотом", "на верстаке в жилой зоне");

            Transform battVisual = battSpot.Find("Placeholder_MountedBattery");
            if (battVisual == null)
            {
                GameObject batt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                batt.name = "Placeholder_MountedBattery";
                batt.transform.SetParent(battSpot, false);
                batt.transform.localPosition = Vector3.zero;
                batt.transform.localScale = new Vector3(0.28f, 0.22f, 0.2f);
                var bRen = batt.GetComponent<Renderer>();
                if (bRen != null) bRen.sharedMaterial = steelMat;
                var bCol = batt.GetComponent<Collider>();
                if (bCol != null) UnityEngine.Object.DestroyImmediate(bCol);
                battVisual = batt.transform;
            }
            battVisual.gameObject.SetActive(false);

            // 5. Хотспот горловины бензобака
            Transform fuelSpot = car.Find("Hotspot_FuelTank_Inlet");
            if (fuelSpot == null)
            {
                var spotObj = new GameObject("Hotspot_FuelTank_Inlet");
                spotObj.transform.SetParent(car, false);
                fuelSpot = spotObj.transform;
            }
            fuelSpot.localPosition = new Vector3(-0.95f, 0.72f, -1.25f);

            var fuelBox = fuelSpot.GetComponent<BoxCollider>();
            if (fuelBox == null) fuelBox = fuelSpot.gameObject.AddComponent<BoxCollider>();
            fuelBox.isTrigger = true;
            fuelBox.size = new Vector3(0.6f, 0.6f, 0.6f);
            fuelBox.center = Vector3.zero;

            var fuelHotspot = fuelSpot.GetComponent<BunkerCarAssemblyHotspot>();
            if (fuelHotspot == null) fuelHotspot = fuelSpot.gameObject.AddComponent<BunkerCarAssemblyHotspot>();
            fuelHotspot.Configure(BunkerAssemblyItemType.FuelCanister, "Горловина бензобака", "рядом с дизель-генератором");

            // 6. Фары автомобиля
            Transform headlightsGroup = car.Find("Car_Headlights");
            if (headlightsGroup == null)
            {
                headlightsGroup = new GameObject("Car_Headlights").transform;
                headlightsGroup.SetParent(car, false);
                headlightsGroup.localPosition = Vector3.zero;
            }

            Light leftLight = null;
            Transform lLightTr = headlightsGroup.Find("Headlight_L");
            if (lLightTr == null)
            {
                GameObject lObj = new GameObject("Headlight_L");
                lObj.transform.SetParent(headlightsGroup, false);
                lObj.transform.localPosition = new Vector3(-0.62f, 0.65f, 2.15f);
                leftLight = lObj.AddComponent<Light>();
                leftLight.type = LightType.Spot;
                leftLight.range = 18f;
                leftLight.spotAngle = 60f;
                leftLight.color = new Color(1f, 0.95f, 0.8f);
                leftLight.intensity = 2.5f;
                leftLight.enabled = false;
            }
            else
            {
                leftLight = lLightTr.GetComponent<Light>();
            }

            Light rightLight = null;
            Transform rLightTr = headlightsGroup.Find("Headlight_R");
            if (rLightTr == null)
            {
                GameObject rObj = new GameObject("Headlight_R");
                rObj.transform.SetParent(headlightsGroup, false);
                rObj.transform.localPosition = new Vector3(0.62f, 0.65f, 2.15f);
                rightLight = rObj.AddComponent<Light>();
                rightLight.type = LightType.Spot;
                rightLight.range = 18f;
                rightLight.spotAngle = 60f;
                rightLight.color = new Color(1f, 0.95f, 0.8f);
                rightLight.intensity = 2.5f;
                rightLight.enabled = false;
            }
            else
            {
                rightLight = rLightTr.GetComponent<Light>();
            }

            // 7. Зона посадки водителя (водительская дверь)
            Transform boardingZone = car.Find("VehicleBoardingZone");
            if (boardingZone == null)
            {
                GameObject bzObj = new GameObject("VehicleBoardingZone");
                bzObj.transform.SetParent(car, false);
                boardingZone = bzObj.transform;
            }
            boardingZone.localPosition = new Vector3(-1.30f, 0.80f, 0.15f);
            var bzBox = boardingZone.GetComponent<BoxCollider>();
            if (bzBox == null) bzBox = boardingZone.gameObject.AddComponent<BoxCollider>();
            bzBox.isTrigger = true;
            bzBox.size = new Vector3(0.9f, 1.5f, 1.2f);
            bzBox.center = Vector3.zero;

            // 8. Связывание SerializedObject
            SerializedObject assemblySo = new SerializedObject(assembly);
            var propWheel = assemblySo.FindProperty("wheelHotspot");
            if (propWheel != null) propWheel.objectReferenceValue = wheelHotspot;
            var propBatt = assemblySo.FindProperty("batteryHotspot");
            if (propBatt != null) propBatt.objectReferenceValue = battHotspot;
            var propFuel = assemblySo.FindProperty("fuelHotspot");
            if (propFuel != null) propFuel.objectReferenceValue = fuelHotspot;
            var propJack = assemblySo.FindProperty("jackStandObject");
            if (propJack != null) propJack.objectReferenceValue = jackStand.gameObject;
            var propFLTire = assemblySo.FindProperty("assembledWheelVisual");
            if (propFLTire != null) propFLTire.objectReferenceValue = flTire != null ? flTire.gameObject : null;
            var propBattVis = assemblySo.FindProperty("batteryInBayVisual");
            if (propBattVis != null) propBattVis.objectReferenceValue = battVisual.gameObject;

            var propLights = assemblySo.FindProperty("carHeadlights");
            if (propLights != null)
            {
                propLights.arraySize = 2;
                propLights.GetArrayElementAtIndex(0).objectReferenceValue = leftLight;
                propLights.GetArrayElementAtIndex(1).objectReferenceValue = rightLight;
            }
            assemblySo.ApplyModifiedProperties();

            assembly.BindHotspots();
        }

        private static void SetupAssemblyPickupsAuthoring(Transform hubRoot, Transform workshopProps, Material steelMat, Material armyGreenMat)
        {
            // 1. Аккумулятор на верстаке
            if (workshopProps.Find("Pickup_Battery") == null)
            {
                GameObject battObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                battObj.name = "Pickup_Battery";
                battObj.transform.SetParent(workshopProps, false);
                battObj.transform.localPosition = new Vector3(-4.4f, 1.05f, 2.8f);
                battObj.transform.localScale = new Vector3(0.35f, 0.25f, 0.25f);
                battObj.GetComponent<Renderer>().sharedMaterial = steelMat;

                var boxCol = battObj.GetComponent<BoxCollider>();
                if (boxCol != null) boxCol.isTrigger = true;

                var battItem = battObj.AddComponent<BunkerAssemblyItem>();
                battItem.Configure(BunkerAssemblyItemType.Battery, "Силовой аккумулятор 12V");
            }

            // 2. Колесо на полу у стены
            Transform tireRack = hubRoot.Find("Tire_Rack_Spot");
            if (tireRack == null)
            {
                tireRack = new GameObject("Tire_Rack_Spot").transform;
                tireRack.SetParent(hubRoot, false);
                tireRack.localPosition = new Vector3(-7.5f, 0f, 2.5f);

                GameObject wheelObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelObj.name = "Pickup_SpareWheel";
                wheelObj.transform.SetParent(tireRack, false);
                wheelObj.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                wheelObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelObj.transform.localScale = new Vector3(0.75f, 0.28f, 0.75f);
                wheelObj.GetComponent<Renderer>().sharedMaterial = steelMat;

                var cylCol = wheelObj.GetComponent<Collider>();
                if (cylCol != null) cylCol.isTrigger = true;

                var wheelItem = wheelObj.AddComponent<BunkerAssemblyItem>();
                wheelItem.Configure(BunkerAssemblyItemType.Wheel, "Колесо со ступичным креплением");
            }

            // 3. Канистра топлива возле генератора
            Transform genCorner = hubRoot.Find("Power_Corner") ?? hubRoot.Find("Generator_Corner") ?? hubRoot.Find("Living_Corner");
            if (genCorner != null && genCorner.Find("Pickup_FuelCanister") == null)
            {
                GameObject canObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                canObj.name = "Pickup_FuelCanister";
                canObj.transform.SetParent(genCorner, false);
                canObj.transform.localPosition = new Vector3(1.2f, 0.35f, 0f);
                canObj.transform.localScale = new Vector3(0.35f, 0.55f, 0.45f);
                canObj.GetComponent<Renderer>().sharedMaterial = armyGreenMat;

                var canCol = canObj.GetComponent<BoxCollider>();
                if (canCol != null) canCol.isTrigger = true;

                var canItem = canObj.AddComponent<BunkerAssemblyItem>();
                canItem.Configure(BunkerAssemblyItemType.FuelCanister, "Канистра бензина (15 л)");
            }
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"Assets/Content/SceneMaterials/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                {
                    color = color
                };
                if (!System.IO.Directory.Exists("Assets/Content/SceneMaterials"))
                {
                    System.IO.Directory.CreateDirectory("Assets/Content/SceneMaterials");
                }
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }
    }
}
