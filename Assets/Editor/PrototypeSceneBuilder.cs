#if UNITY_EDITOR
using RogueDrive.Gameplay;
using RogueDrive.Modifiers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.EditorTools
{
    /// <summary>Создает первый играбельный срез с аркадной физикой, широкой дорогой, динамической камерой и сокетами.</summary>
    public static class PrototypeSceneBuilder
    {
        const string SceneFolder = "Assets/Scenes";
        const string ScenePath = SceneFolder + "/RogueDrivePrototype.unity";

        [MenuItem("RogueDrive/Создать прототип заезда")]
        public static void CreatePrototype()
        {
            EnsureSceneFolder();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.3f);

            GameObject gameRoot = new GameObject("RogueDrivePrototype");
            GameRunController run = gameRoot.AddComponent<GameRunController>();

            CreateLight();
            CreateRoad();
            ArcadeCarController car = CreateArcadeCar(run);
            CreateCamera(car.transform);
            CreateObstacles();

            PrototypeHud hud = gameRoot.AddComponent<PrototypeHud>();
            hud.Configure(run, car);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"Прототип аркадного заезда успешно создан: {ScenePath}");
        }

        static void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        static void CreateRoad()
        {
            const float roadWidth = 24f;
            const float roadLength = 500f;
            const float roadZ = roadLength * 0.5f;

            // Дорожное полотно (широкая трасса)
            GameObject road = CreatePrimitive(PrimitiveType.Cube, "Road", new Vector3(0f, -0.2f, roadZ), new Vector3(roadWidth, 0.4f, roadLength), new Color(0.13f, 0.14f, 0.18f));
            road.isStatic = true;

            // Разделительные полосы (4 полосы движения)
            float[] markerX = { -6f, -2f, 2f, 6f };
            for (int i = 0; i < markerX.Length; i++)
            {
                GameObject marker = CreatePrimitive(PrimitiveType.Cube, $"Lane Marker {i + 1}", new Vector3(markerX[i], 0.03f, roadZ), new Vector3(0.12f, 0.03f, roadLength), new Color(0.92f, 0.72f, 0.15f));
                marker.isStatic = true;
            }

            // Отбойники по краям широкой дороги
            float guardrailX = (roadWidth * 0.5f) + 0.3f;
            GameObject leftWall = CreatePrimitive(PrimitiveType.Cube, "Left Guardrail", new Vector3(-guardrailX, 0.6f, roadZ), new Vector3(0.35f, 1.2f, roadLength), new Color(0.36f, 0.4f, 0.46f));
            GameObject rightWall = CreatePrimitive(PrimitiveType.Cube, "Right Guardrail", new Vector3(guardrailX, 0.6f, roadZ), new Vector3(0.35f, 1.2f, roadLength), new Color(0.36f, 0.4f, 0.46f));
            leftWall.isStatic = true;
            rightWall.isStatic = true;
        }

        static ArcadeCarController CreateArcadeCar(GameRunController run)
        {
            GameObject car = new GameObject("Player Car");
            car.transform.position = new Vector3(0f, 0.65f, 5f);

            BoxCollider collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.8f, 0.9f, 3.6f);
            collider.center = new Vector3(0f, 0.45f, 0f);

            Rigidbody body = car.AddComponent<Rigidbody>();
            body.mass = 1200f;
            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            ArcadeCarController controller = car.AddComponent<ArcadeCarController>();
            controller.Configure(run);

            // Визуальный кузов (Visual Body) для наклона при рулении
            GameObject visualBody = new GameObject("VisualBody");
            visualBody.transform.SetParent(car.transform, false);

            CreateCarPart(visualBody.transform, "Chassis", new Vector3(0f, 0.35f, 0f), new Vector3(1.75f, 0.55f, 3.5f), new Color(0.08f, 0.55f, 0.95f));
            CreateCarPart(visualBody.transform, "Cabin", new Vector3(0f, 0.85f, -0.2f), new Vector3(1.35f, 0.55f, 1.6f), new Color(0.68f, 0.88f, 1f));
            CreateCarPart(visualBody.transform, "FrontBumper", new Vector3(0f, 0.25f, 1.75f), new Vector3(1.82f, 0.35f, 0.25f), new Color(0.2f, 0.2f, 0.25f));

            // Фары
            CreateCarPart(visualBody.transform, "Headlight_L", new Vector3(-0.65f, 0.35f, 1.78f), new Vector3(0.3f, 0.18f, 0.1f), Color.yellow);
            CreateCarPart(visualBody.transform, "Headlight_R", new Vector3(0.65f, 0.35f, 1.78f), new Vector3(0.3f, 0.18f, 0.1f), Color.yellow);

            // Колёса (визуальные)
            Vector3[] wheelOffsets =
            {
                new Vector3(-0.92f, 0.25f, 1.15f),
                new Vector3(0.92f, 0.25f, 1.15f),
                new Vector3(-0.92f, 0.25f, -1.15f),
                new Vector3(0.92f, 0.25f, -1.15f)
            };
            for (int i = 0; i < wheelOffsets.Length; i++)
            {
                CreateCarPart(visualBody.transform, $"Wheel_{i + 1}", wheelOffsets[i], new Vector3(0.22f, 0.55f, 0.55f), new Color(0.1f, 0.1f, 0.12f));
            }

            // Настройка физических сокетов на кузове (SocketRegistry)
            SocketRegistry socketRegistry = car.AddComponent<SocketRegistry>();
            SetupCarSockets(car.transform, socketRegistry);

            return controller;
        }

        static void SetupCarSockets(Transform carTransform, SocketRegistry registry)
        {
            Transform socketRoof = CreateSocketAnchor(carTransform, "Socket_Roof", new Vector3(0f, 1.18f, -0.2f));
            Transform socketHood = CreateSocketAnchor(carTransform, "Socket_Hood", new Vector3(0f, 0.68f, 1.1f));
            Transform socketBumper = CreateSocketAnchor(carTransform, "Socket_Bumper", new Vector3(0f, 0.32f, 1.85f));
            Transform socketExhaust = CreateSocketAnchor(carTransform, "Socket_Exhaust", new Vector3(0f, 0.2f, -1.82f));
            Transform socketSideL = CreateSocketAnchor(carTransform, "Socket_Side_L", new Vector3(-0.92f, 0.45f, 0f));
            Transform socketSideR = CreateSocketAnchor(carTransform, "Socket_Side_R", new Vector3(0.92f, 0.45f, 0f));

            SerializedObject so = new SerializedObject(registry);
            SerializedProperty slotsProp = so.FindProperty("_slots");

            var socketConfigs = new (SocketType type, Transform anchor)[]
            {
                (SocketType.Roof, socketRoof),
                (SocketType.Hood, socketHood),
                (SocketType.Bumper, socketBumper),
                (SocketType.Exhaust, socketExhaust),
                (SocketType.Side, socketSideL),
                (SocketType.Side, socketSideR)
            };

            slotsProp.arraySize = socketConfigs.Length;
            for (int i = 0; i < socketConfigs.Length; i++)
            {
                SerializedProperty element = slotsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Type").enumValueIndex = (int)socketConfigs[i].type;
                element.FindPropertyRelative("Anchor").objectReferenceValue = socketConfigs[i].anchor;
            }
            so.ApplyModifiedProperties();
        }

        static Transform CreateSocketAnchor(Transform parent, string name, Vector3 localPosition)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            return anchor.transform;
        }

        static void CreateCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = target.position + new Vector3(0f, 6.5f, -10.5f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
            camera.fieldOfView = 60f;

            ArcadeCameraFollow follow = cameraObject.AddComponent<ArcadeCameraFollow>();
            follow.Configure(target);
        }

        static void CreateObstacles()
        {
            // Размещение препятствий по всей ширине трассы для свободного маневрирования
            float[] obstacleX = { -7f, 5f, 0f, -4f, 6f, -6f, 3f, -2f, 7f, -5f, 1f, -7f, 4f, -1f, 6f };

            for (int i = 0; i < obstacleX.Length; i++)
            {
                float z = 35f + i * 26f;
                // Чередуем: красные баррикады и оранжевые взрывные бочки
                bool isBarrel = (i % 3 == 0);
                Color color = isBarrel ? new Color(1f, 0.45f, 0.1f) : new Color(0.92f, 0.22f, 0.18f);
                string name = isBarrel ? $"Explosive_Barrel_{i + 1}" : $"Barrier_{i + 1}";
                Vector3 scale = isBarrel ? new Vector3(1.3f, 1.6f, 1.3f) : new Vector3(2.4f, 1.4f, 1.4f);

                GameObject obstacle = CreatePrimitive(PrimitiveType.Cube, name, new Vector3(obstacleX[i], scale.y * 0.5f, z), scale, color);
                BoxCollider collider = obstacle.GetComponent<BoxCollider>();
                collider.isTrigger = true;
                obstacle.AddComponent<TrackObstacle>();
            }
        }

        static GameObject CreatePrimitive(PrimitiveType type, string objectName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject result = GameObject.CreatePrimitive(type);
            result.name = objectName;
            result.transform.position = position;
            result.transform.localScale = scale;

            Renderer renderer = result.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(renderer.sharedMaterial);
                material.color = color;
                renderer.sharedMaterial = material;
            }

            return result;
        }

        static void CreateCarPart(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject part = CreatePrimitive(PrimitiveType.Cube, objectName, Vector3.zero, localScale, color);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
        }

        static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }
}
#endif
