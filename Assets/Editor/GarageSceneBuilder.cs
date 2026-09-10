#if UNITY_EDITOR
using System.IO;
using RogueDrive.Gameplay.Hub;
using RogueDrive.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.EditorTools
{
    [InitializeOnLoad]
    public static class GarageSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/GarageScene.unity";
        const string PrototypeScenePath = "Assets/Scenes/RogueDrivePrototype.unity";
        const string CatalogPath = "Assets/Content/GarageCatalog.asset";

        static GarageSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !SessionState.GetBool("GarageScene3DDecorated", false))
                {
                    SessionState.SetBool("GarageScene3DDecorated", true);
                    CreateGarageScene();
                }
            };
        }

        [MenuItem("RogueDrive/Создать сцену Гаража (First-Person Hub)")]
        public static void CreateGarageScene()
        {
            GarageContentBuilder.BuildAllGarageContent();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.03f, 0.05f);

            GameObject garageRoot = new GameObject("GarageHubRoot");

            // ── 1. СТРУКТУРА ПОМЕЩЕНИЯ АНГАРА ──────────────────────────────────────────
            Transform envRoot = new GameObject("Environment").transform;
            envRoot.SetParent(garageRoot.transform, false);

            Color concreteFloor = new Color(0.12f, 0.13f, 0.16f);
            Color darkWall = new Color(0.14f, 0.16f, 0.20f);
            Color metalPillar = new Color(0.22f, 0.24f, 0.28f);
            Color hazardYellow = new Color(0.85f, 0.70f, 0.15f);

            // Пол (с коллайдером)
            CreateSolidCube("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(26f, 0.5f, 26f), concreteFloor, envRoot);

            // Потолок с балками
            CreateSolidCube("Ceiling", new Vector3(0f, 6.25f, 0f), new Vector3(26f, 0.5f, 26f), darkWall, envRoot);

            // Задняя стена
            CreateSolidCube("Wall_Back", new Vector3(0f, 3f, 12.5f), new Vector3(26f, 6f, 1f), darkWall, envRoot);

            // Боковые стены
            CreateSolidCube("Wall_Left", new Vector3(-12.5f, 3f, 0f), new Vector3(1f, 6f, 26f), darkWall, envRoot);
            CreateSolidCube("Wall_Right", new Vector3(12.5f, 3f, 0f), new Vector3(1f, 6f, 26f), darkWall, envRoot);

            // Передняя стена с проемом для гермоворот
            CreateSolidCube("Wall_Front_Left", new Vector3(-8.5f, 3f, -12.5f), new Vector3(9f, 6f, 1f), darkWall, envRoot);
            CreateSolidCube("Wall_Front_Right", new Vector3(8.5f, 3f, -12.5f), new Vector3(9f, 6f, 1f), darkWall, envRoot);
            CreateSolidCube("Wall_Front_Top", new Vector3(0f, 5.25f, -12.5f), new Vector3(8f, 1.5f, 1f), darkWall, envRoot);

            // Опорные стальные колонны
            CreateSolidCube("Pillar_1", new Vector3(-11.5f, 3f, 11.5f), new Vector3(1.2f, 6f, 1.2f), metalPillar, envRoot);
            CreateSolidCube("Pillar_2", new Vector3(11.5f, 3f, 11.5f), new Vector3(1.2f, 6f, 1.2f), metalPillar, envRoot);
            CreateSolidCube("Pillar_3", new Vector3(-11.5f, 3f, -11.5f), new Vector3(1.2f, 6f, 1.2f), metalPillar, envRoot);
            CreateSolidCube("Pillar_4", new Vector3(11.5f, 3f, -11.5f), new Vector3(1.2f, 6f, 1.2f), metalPillar, envRoot);

            // Дорога за воротами (вид наружу в ночную трассу)
            CreateSolidCube("Outside_Road", new Vector3(0f, -0.3f, -24f), new Vector3(20f, 0.4f, 22f), new Color(0.08f, 0.09f, 0.11f), envRoot);

            // ── 2. ГЕРМОВОРОТА АНГАРА ─────────────────────────────────────────────────
            GameObject gateObj = new GameObject("Gate_HeavyDoor");
            gateObj.transform.SetParent(envRoot, false);
            gateObj.transform.position = new Vector3(0f, 2.25f, -12.5f);

            GameObject gateVisual = CreateSolidCube("DoorPlate", Vector3.zero, new Vector3(7.8f, 4.5f, 0.4f), new Color(0.18f, 0.20f, 0.25f), gateObj.transform);
            CreateSolidCube("HazardStripe", new Vector3(0f, -1.8f, -0.25f), new Vector3(7.4f, 0.5f, 0.1f), hazardYellow, gateObj.transform);

            var gateController = gateObj.AddComponent<GarageGateController>();
            SerializedObject gateSo = new SerializedObject(gateController);
            gateSo.FindProperty("gateDoorTransform").objectReferenceValue = gateObj.transform;
            gateSo.FindProperty("openHeight").floatValue = 5.2f;
            gateSo.ApplyModifiedProperties();

            // ── 3. ОСВЕЩЕНИЕ АНГАРА (АВАРИЙНЫЙ И ОСНОВНОЙ РЕЖИМЫ) ─────────────────────
            Transform lightsRoot = new GameObject("Lighting").transform;
            lightsRoot.SetParent(garageRoot.transform, false);

            // Аварийный красный маяк (горит в темноте)
            GameObject emergObj = new GameObject("Emergency_RedLight");
            emergObj.transform.SetParent(lightsRoot, false);
            emergObj.transform.position = new Vector3(11.2f, 2.8f, 0f);
            Light emergLight = emergObj.AddComponent<Light>();
            emergLight.type = LightType.Point;
            emergLight.color = new Color(1f, 0.15f, 0.15f);
            emergLight.intensity = 2.5f;
            emergLight.range = 14f;

            // Основной верхний прожектор подиума
            GameObject mainSpotObj = new GameObject("Podium_Spotlight");
            mainSpotObj.transform.SetParent(lightsRoot, false);
            mainSpotObj.transform.position = new Vector3(0f, 5.8f, 0f);
            mainSpotObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Light mainSpot = mainSpotObj.AddComponent<Light>();
            mainSpot.type = LightType.Spot;
            mainSpot.color = new Color(1f, 0.95f, 0.88f);
            mainSpot.intensity = 4.2f;
            mainSpot.range = 16f;
            mainSpot.spotAngle = 65f;
            mainSpot.shadows = LightShadows.Soft;

            // Теплый свет над верстаком
            GameObject benchLightObj = new GameObject("Workbench_WarmLight");
            benchLightObj.transform.SetParent(lightsRoot, false);
            benchLightObj.transform.position = new Vector3(-5.2f, 3.2f, 3.2f);
            Light benchLight = benchLightObj.AddComponent<Light>();
            benchLight.type = LightType.Point;
            benchLight.color = new Color(1f, 0.82f, 0.55f);
            benchLight.intensity = 2.8f;
            benchLight.range = 9f;
            benchLight.shadows = LightShadows.Soft;

            // Мягкий рассеянный свет
            GameObject dirObj = new GameObject("Key_Directional_Light");
            dirObj.transform.SetParent(lightsRoot, false);
            dirObj.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light dirLight = dirObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.7f, 0.85f, 1f);
            dirLight.intensity = 0.85f;
            dirLight.shadows = LightShadows.Soft;

            // ── 4. ПОДИУМ И АВТОМОБИЛЬ ───────────────────────────────────────────────
            Transform podiumRoot = new GameObject("PodiumRoot").transform;
            podiumRoot.SetParent(garageRoot.transform, false);

            CreateSolidCube("PodiumBase", new Vector3(0f, 0.1f, 0f), new Vector3(6.5f, 0.2f, 6.5f), new Color(0.18f, 0.2f, 0.25f), podiumRoot);
            GameObject neonRing = CreateSolidCube("NeonRing", new Vector3(0f, 0.12f, 0f), new Vector3(6.8f, 0.05f, 6.8f), new Color(0.1f, 0.8f, 1f), podiumRoot);

            GameObject podiumAnchor = new GameObject("PodiumAnchor");
            podiumAnchor.transform.SetParent(podiumRoot, false);
            podiumAnchor.transform.position = new Vector3(0f, 0.2f, 0f);

            // Интерактивная зона посадки в автомобиль
            GameObject carTrigger = new GameObject("VehicleBoardingZone");
            carTrigger.transform.SetParent(podiumAnchor.transform, false);
            BoxCollider carCol = carTrigger.AddComponent<BoxCollider>();
            carCol.size = new Vector3(3.5f, 2.2f, 5.0f);
            carCol.center = new Vector3(0f, 1.1f, 0f);
            carTrigger.AddComponent<GarageVehicleBoarding>();

            // ── 5. РУБИЛЬНИК ГЕНЕРАТОРА (НА ПРАВОЙ СТЕНЕ) ─────────────────────────────
            GameObject switchBox = CreateSolidCube("GeneratorSwitchBox", new Vector3(11.9f, 1.8f, 0f), new Vector3(0.35f, 0.7f, 0.5f), new Color(0.25f, 0.28f, 0.32f), envRoot);
            GameObject handle = CreateSolidCube("SwitchHandle", new Vector3(11.7f, 1.8f, 0f), new Vector3(0.12f, 0.4f, 0.12f), Color.red, switchBox.transform);
            var genSwitch = switchBox.AddComponent<GarageGeneratorSwitch>();
            SerializedObject switchSo = new SerializedObject(genSwitch);
            switchSo.FindProperty("switchHandle").objectReferenceValue = handle.transform;
            switchSo.ApplyModifiedProperties();

            // ── 6. ВЕРСТАК И КЛЮЧИ ЗАЖИГАНИЯ (СЛЕВА) ─────────────────────────────────
            Transform propsRoot = new GameObject("WorkshopProps").transform;
            propsRoot.SetParent(garageRoot.transform, false);

            GameObject workbench = SpawnProp("Assets/GarageAssetPack/Prefabs/WorkbenchFull.prefab", new Vector3(-5.2f, 0f, 3.2f), Quaternion.Euler(0f, 40f, 0f), Vector3.one * 1.15f, propsRoot);
            if (workbench != null)
            {
                BoxCollider wbCol = workbench.AddComponent<BoxCollider>();
                wbCol.size = new Vector3(2.5f, 1.8f, 1.4f);
                wbCol.center = new Vector3(0f, 0.9f, 0f);
                workbench.AddComponent<GarageWorkbenchInteractable>();
            }

            // Связка ключей на верстаке
            GameObject keysObj = new GameObject("CarKeysItem");
            keysObj.transform.SetParent(propsRoot, false);
            keysObj.transform.position = new Vector3(-5.0f, 1.08f, 3.1f);
            GameObject keysVisual = CreateSolidCube("KeysFob", Vector3.zero, new Vector3(0.15f, 0.04f, 0.25f), new Color(0.9f, 0.75f, 0.2f), keysObj.transform);
            CreateSolidCube("KeyBlade", new Vector3(0f, 0f, 0.18f), new Vector3(0.04f, 0.02f, 0.16f), Color.gray, keysVisual.transform);
            SphereCollider keysCol = keysObj.AddComponent<SphereCollider>();
            keysCol.radius = 0.5f;
            keysObj.AddComponent<GarageCarKeys>();

            // Дополнительный декор мастерской
            SpawnProp("Assets/GarageAssetPack/Prefabs/TrashCan.prefab", new Vector3(-3.5f, 0f, 4.4f), Quaternion.Euler(0f, 15f, 0f), Vector3.one * 1.1f, propsRoot);
            SpawnProp("Assets/GarageAssetPack/Prefabs/CarWheel.prefab", new Vector3(-4.8f, 0f, 1.8f), Quaternion.Euler(0f, 10f, 0f), Vector3.one * 1.1f, propsRoot);
            SpawnProp("Assets/GarageAssetPack/Prefabs/StorageShelfFull.prefab", new Vector3(5.2f, 0f, 3.2f), Quaternion.Euler(0f, -40f, 0f), Vector3.one * 1.15f, propsRoot);
            SpawnProp("Assets/GarageAssetPack/Prefabs/WoodenPallet.prefab", new Vector3(4.5f, 0f, 1.4f), Quaternion.Euler(0f, -15f, 0f), Vector3.one * 1.1f, propsRoot);
            SpawnProp("Assets/GarageAssetPack/Prefabs/Barrelfbx.prefab", new Vector3(4.4f, 0.15f, 1.4f), Quaternion.identity, Vector3.one * 1.1f, propsRoot);
            SpawnProp("Assets/GarageAssetPack/Prefabs/JerrycanLarge.prefab", new Vector3(3.6f, 0f, 1.2f), Quaternion.Euler(0f, 25f, 0f), Vector3.one * 1.1f, propsRoot);

            // ── 7. ПЕРСОНАЖ ОТ 1-ГО ЛИЦА ─────────────────────────────────────────────
            GameObject playerObj = new GameObject("FP_GaragePlayer");
            playerObj.transform.SetParent(garageRoot.transform, false);
            playerObj.transform.position = new Vector3(0f, 0.1f, 8.0f);
            playerObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // смотрим в сторону машины и ворот

            CharacterController cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var playerCtrl = playerObj.AddComponent<GaragePlayerController>();

            // Голова / Камера игрока
            GameObject headCamObj = new GameObject("PlayerCamera");
            headCamObj.tag = "MainCamera";
            headCamObj.transform.SetParent(playerObj.transform, false);
            headCamObj.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            Camera headCam = headCamObj.AddComponent<Camera>();
            headCam.clearFlags = CameraClearFlags.SolidColor;
            headCam.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
            headCam.fieldOfView = 65f;
            headCamObj.AddComponent<AudioListener>();

            var raycaster = headCamObj.AddComponent<GarageInteractionRaycaster>();

            SerializedObject playerSo = new SerializedObject(playerCtrl);
            playerSo.FindProperty("playerCamera").objectReferenceValue = headCam;
            playerSo.ApplyModifiedProperties();

            SerializedObject raycasterSo = new SerializedObject(raycaster);
            raycasterSo.FindProperty("player").objectReferenceValue = playerCtrl;
            raycasterSo.ApplyModifiedProperties();

            // ── 8. КООРДИНАТОР ПРОЛОГА И АТМОСФЕРА ────────────────────────────────────
            var prologueMgr = garageRoot.AddComponent<GaragePrologueManager>();
            SerializedObject prolSo = new SerializedObject(prologueMgr);
            prolSo.FindProperty("emergencyRedLight").objectReferenceValue = emergLight;
            SerializedProperty mainLightsProp = prolSo.FindProperty("mainWorkshopLights");
            mainLightsProp.arraySize = 3;
            mainLightsProp.GetArrayElementAtIndex(0).objectReferenceValue = mainSpot;
            mainLightsProp.GetArrayElementAtIndex(1).objectReferenceValue = benchLight;
            mainLightsProp.GetArrayElementAtIndex(2).objectReferenceValue = dirLight;
            prolSo.FindProperty("neonPodiumRing").objectReferenceValue = neonRing;
            prolSo.ApplyModifiedProperties();

            garageRoot.AddComponent<GarageAtmosphereEnhancer>();

            // ── 9. МЕНЕДЖЕР ГАРАЖА ДЛЯ КАТАЛОГА И АВТО ────────────────────────────────
            GameObject managerObj = new GameObject("GarageManager");
            managerObj.transform.SetParent(garageRoot.transform, false);
            GarageUIController controller = managerObj.AddComponent<GarageUIController>();

            GarageCatalog catalog = AssetDatabase.LoadAssetAtPath<GarageCatalog>(CatalogPath);
            SerializedObject ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("catalog").objectReferenceValue = catalog;
            ctrlSo.FindProperty("podiumAnchor").objectReferenceValue = podiumAnchor.transform;
            ctrlSo.FindProperty("autoRotationSpeed").floatValue = 15f;
            ctrlSo.ApplyModifiedProperties();

            // ── 10. СОХРАНЕНИЕ СЦЕНЫ ──────────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(PrototypeScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[GarageSceneBuilder] Полноценный First-Person Hub гаража успешно создан: {ScenePath}!");
        }

        static GameObject CreateSolidCube(string name, Vector3 pos, Vector3 scale, Color color, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (parent != null) go.transform.SetParent(parent, true);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));
                m.color = color;
                r.sharedMaterial = m;
            }

            return go;
        }

        static GameObject SpawnProp(string prefabPath, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return null;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = pos;
            instance.transform.rotation = rot;
            instance.transform.localScale = scale;
            if (parent != null) instance.transform.SetParent(parent, true);
            return instance;
        }
    }
}
#endif
