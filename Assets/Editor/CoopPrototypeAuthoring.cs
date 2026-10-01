#if UNITY_EDITOR
using System.IO;
using System.Linq;
using RogueDrive.Gameplay.Coop;
using RogueDrive.Gameplay.Hub;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RogueDrive.EditorTools
{
    public static class CoopPrototypeAuthoring
    {
        public const string ScenePath = "Assets/Scenes/Coop_Outskirts.unity";
        private const string Folder = "Assets/Content/Coop";
        private static Font font;
        [MenuItem("RogueDrive/Coop/Create or refresh prototype scene")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before authoring.");
            if (SceneManager.GetActiveScene().isDirty) throw new System.InvalidOperationException("Save current scene changes before authoring.");
            Directory.CreateDirectory(Folder);
            var source = EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");
            var original = Object.FindFirstObjectByType<GarageDriveOutVehicle>(FindObjectsInactive.Include);
            if (original == null) throw new System.InvalidOperationException("Authored stage vehicle missing.");
            var car = Object.Instantiate(original.gameObject);
            car.name = "CoopVehicle"; car.SetActive(false);
            foreach (var behaviour in car.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                if (behaviour != null && !(behaviour is GarageDriveOutVehicle)) Object.DestroyImmediate(behaviour);
            foreach (var childCamera in car.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(childCamera.gameObject);
            car.AddComponent<NetworkObject>(); car.AddComponent<NetworkTransform>(); car.AddComponent<CoopVehicle>();
            car.SetActive(true);
            var vehiclePrefab = PrefabUtility.SaveAsPrefabAsset(car, Folder + "/CoopVehicle.prefab");
            Object.DestroyImmediate(car);
            var player = new GameObject("CoopPlayer");
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .35f; cc.center = Vector3.up * .9f;
            player.AddComponent<NetworkObject>(); player.AddComponent<NetworkTransform>();
            var playerLogic = player.AddComponent<CoopPlayer>();
            var avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            avatar.name = "Survivor silhouette";
            avatar.transform.SetParent(player.transform, false); avatar.transform.localPosition = Vector3.up * .9f;
            avatar.transform.localScale = new Vector3(.65f, .9f, .65f);
            Object.DestroyImmediate(avatar.GetComponent<Collider>());
            var material = new Material(Shader.Find("Standard")) { color = new Color(.85f, .55f, .18f) };
            if (!AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Crew.mat")) AssetDatabase.CreateAsset(material, Folder + "/Crew.mat");
            else Object.DestroyImmediate(material);
            avatar.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Crew.mat");
            Set(playerLogic, "avatar", avatar.transform);
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, Folder + "/CoopPlayer.prefab");
            Object.DestroyImmediate(player);
            EditorSceneManager.SaveScene(source, ScenePath, true);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Stage_World")
                {
                    root.SetActive(true);
                    foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                        if (script != null) Object.DestroyImmediate(script);
                }
                else Object.DestroyImmediate(root);
            }
            var light = new GameObject("Coop Sun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(42, -28, 0);
            RenderSettings.ambientLight = new Color(.5f, .55f, .6f);
            var camera = new GameObject("Crew lobby camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(-8, 7, 7); camera.transform.LookAt(new Vector3(0, 1, 18));
            camera.farClipPlane = 1500;
            var spawn = new GameObject("Shared vehicle spawn").transform; spawn.position = new Vector3(0, .6f, 16);
            var net = new GameObject("Crew session");
            var transport = net.AddComponent<UnityTransport>();
            var manager = net.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, PlayerPrefab = playerPrefab, ConnectionApproval = true,
                EnableSceneManagement = false, TickRate = 30, ProtocolVersion = 1
            };
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = vehiclePrefab });
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
            var session = net.AddComponent<CoopSession>();
            Set(session, "manager", manager); Set(session, "vehiclePrefab", vehiclePrefab);
            Set(session, "vehicleSpawn", spawn); Set(session, "lobbyCamera", camera);
            CreateUI(session);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[Coop] Prototype scene authored: " + ScenePath);
        }

        private static void CreateUI(CoopSession session)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Crew UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var panel = Rect("Crew menu", root.transform, new Vector2(580, 500), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(.065f, .085f, .10f, .97f);
            Label(panel, "Title", "ЭКИПАЖ", 32, new Vector2(500, 45), new Vector2(0, 200));
            Label(panel, "Subtitle", "Двое. Одна машина. Общая дорога.", 20, new Vector2(520, 30), new Vector2(0, 155));
            var inputRect = Rect("Host address", panel, new Vector2(500, 48), new Vector2(0, 94));
            inputRect.gameObject.AddComponent<Image>().color = new Color(.16f, .20f, .23f);
            var input = inputRect.gameObject.AddComponent<InputField>();
            input.textComponent = Label(inputRect, "Value", "", 22, new Vector2(470, 48), Vector2.zero);
            input.textComponent.alignment = TextAnchor.MiddleLeft; input.text = "127.0.0.1"; input.characterLimit = 45;
            var host = Button(panel, "Создать экипаж", new Vector2(-130, 28));
            var join = Button(panel, "Подключиться", new Vector2(130, 28));
            var resume = Button(panel, "Продолжить", new Vector2(-130, -42));
            var leave = Button(panel, "Выйти из экипажа", new Vector2(130, -42));
            var status = Label(panel, "Status", "", 18, new Vector2(500, 80), new Vector2(0, -112));
            Label(panel, "Network help", "Адрес компьютера хоста в LAN / VPN · порт 7777\nНовый участник может войти на стартовой площадке.", 16, new Vector2(520, 60), new Vector2(0, -205));
            var hud = Label(root.transform, "Crew HUD", "", 18, new Vector2(1200, 70), Vector2.zero);
            hud.rectTransform.anchorMin = hud.rectTransform.anchorMax = new Vector2(.5f, 1);
            hud.rectTransform.anchoredPosition = new Vector2(0, -50);
            hud.gameObject.AddComponent<Outline>().effectColor = Color.black;
            var notice = Label(root.transform, "Action feedback", "", 23, new Vector2(1000, 70), new Vector2(0, -230));
            notice.gameObject.AddComponent<Outline>().effectColor = Color.black;
            var ui = root.AddComponent<CoopSessionUI>();
            Set(ui, "session", session); Set(ui, "menu", panel.gameObject); Set(ui, "address", input);
            Set(ui, "host", host); Set(ui, "join", join); Set(ui, "resume", resume); Set(ui, "leave", leave);
            Set(ui, "status", status); Set(ui, "hud", hud); Set(ui, "notice", notice);
            new GameObject("Crew EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static Text Label(Transform parent, string name, string value, int size, Vector2 dimensions, Vector2 position)
        {
            var label = Rect(name, parent, dimensions, position).gameObject.AddComponent<Text>();
            label.font = font; label.text = value; label.fontSize = size; label.color = new Color(.94f, .94f, .89f);
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
        private static Button Button(Transform parent, string title, Vector2 position)
        {
            var rect = Rect(title, parent, new Vector2(240, 54), position);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.34f, .28f, .15f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Label(rect, "Label", title, 20, new Vector2(235, 50), Vector2.zero); return button;
        }
        private static void Set(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(name).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        [MenuItem("RogueDrive/Coop/Build Windows prototype")]
        public static void Build()
        {
            Directory.CreateDirectory("Builds/Coop");
            var result = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Coop/RogueDriveCoop.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
            File.WriteAllText("Builds/Coop/build-result.txt", result.summary.result + " errors=" + result.summary.totalErrors);
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Coop build failed.");
        }
    }
}
#endif
