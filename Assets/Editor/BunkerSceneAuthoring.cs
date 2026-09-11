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
    [InitializeOnLoad]
    public static class BunkerSceneAuthoring
    {
        [InitializeOnLoadMethod]
        private static void AutoBakeOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    BakeBunkerLayout(forceSave: true);
                }
            };
        }

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
                gateInter.Configure(gateCtrl);
            }

            // =========================================================================
            // 4. ПЕРСОНАЖ ОТ 1-ГО ЛИЦА (Garage_FP_Player)
            // =========================================================================
            var existingPlayer = GameObject.FindFirstObjectByType<GaragePlayerController>();
            if (existingPlayer == null)
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
            // 5. МАШИННОЕ МЕСТО И 3D ХОТСПАТЫ
            // =========================================================================
            Transform podiumAnchor = GameObject.Find("PodiumAnchor")?.transform;
            if (podiumAnchor != null)
            {
                var rig = podiumAnchor.GetComponent<CarInspectionRig>();
                if (rig == null) rig = podiumAnchor.gameObject.AddComponent<CarInspectionRig>();
                rig.SetupHotspots();

                var boarding = podiumAnchor.GetComponent<GarageVehicleBoarding>();
                if (boarding == null) podiumAnchor.gameObject.AddComponent<GarageVehicleBoarding>();
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

            GameObject lightObj = new GameObject("ItemGlowLight");
            lightObj.transform.SetParent(keysObj.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            var glowLight = lightObj.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.85f, 0.25f);
            glowLight.intensity = 1.2f;
            glowLight.range = 1.5f;

            var carKeys = keysObj.AddComponent<GarageCarKeys>();
            var keysSo = new SerializedObject(carKeys);
            var visualProp = keysSo.FindProperty("visualModel");
            if (visualProp != null) visualProp.objectReferenceValue = keysVisual;
            var lightProp = keysSo.FindProperty("itemHighlightLight");
            if (lightProp != null) lightProp.objectReferenceValue = glowLight;
            keysSo.ApplyModifiedProperties();

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
