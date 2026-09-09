#if UNITY_EDITOR
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.EditorTools
{
    /// <summary>Builds the first playable slice entirely from primitive Unity objects.</summary>
    public static class PrototypeSceneBuilder
    {
        const string SceneFolder = "Assets/Scenes";
        const string ScenePath = SceneFolder + "/RogueDrivePrototype.unity";

        [MenuItem("RogueDrive/Создать прототип заезда")]
        public static void CreatePrototype()
        {
            EnsureSceneFolder();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.18f, 0.2f, 0.28f);

            GameObject gameRoot = new GameObject("RogueDrivePrototype");
            GameRunController run = gameRoot.AddComponent<GameRunController>();
            PrototypeHud hud = gameRoot.AddComponent<PrototypeHud>();
            hud.Configure(run);

            CreateLight();
            CreateRoad();
            Transform car = CreateCar(run);
            CreateCamera(car);
            CreateObstacles();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"Прототип заезда создан: {ScenePath}");
        }

        static void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        static void CreateRoad()
        {
            GameObject road = CreatePrimitive(PrimitiveType.Cube, "Road", new Vector3(0f, -0.2f, 180f), new Vector3(11f, 0.4f, 380f), new Color(0.12f, 0.13f, 0.17f));
            road.isStatic = true;

            for (int lane = -1; lane <= 1; lane += 2)
            {
                GameObject marker = CreatePrimitive(PrimitiveType.Cube, "Lane Marker", new Vector3(lane * 1.5f, 0.03f, 180f), new Vector3(0.08f, 0.03f, 380f), new Color(0.95f, 0.74f, 0.12f));
                marker.isStatic = true;
            }

            GameObject leftWall = CreatePrimitive(PrimitiveType.Cube, "Left Guardrail", new Vector3(-5.7f, 0.55f, 180f), new Vector3(0.25f, 1.1f, 380f), new Color(0.33f, 0.36f, 0.42f));
            GameObject rightWall = CreatePrimitive(PrimitiveType.Cube, "Right Guardrail", new Vector3(5.7f, 0.55f, 180f), new Vector3(0.25f, 1.1f, 380f), new Color(0.33f, 0.36f, 0.42f));
            leftWall.isStatic = true;
            rightWall.isStatic = true;
        }

        static Transform CreateCar(GameRunController run)
        {
            GameObject car = new GameObject("Player Car");
            car.transform.position = new Vector3(0f, 0.65f, 0f);
            BoxCollider collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.7f, 0.8f, 3.4f);

            Rigidbody body = car.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            LaneCarController controller = car.AddComponent<LaneCarController>();
            controller.Configure(run);

            CreateCarPart(car.transform, "Body", new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.65f, 3.2f), new Color(0.08f, 0.56f, 0.95f));
            CreateCarPart(car.transform, "Cabin", new Vector3(0f, 0.52f, -0.15f), new Vector3(1.25f, 0.55f, 1.45f), new Color(0.68f, 0.88f, 1f));
            CreateCarPart(car.transform, "Roof Socket", new Vector3(0f, 0.92f, 0.15f), new Vector3(0.38f, 0.18f, 0.5f), new Color(1f, 0.55f, 0.08f));
            return car.transform;
        }

        static void CreateCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = target.position + new Vector3(0f, 8f, -12f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.09f);
            camera.fieldOfView = 62f;
            PrototypeCameraFollow follow = cameraObject.AddComponent<PrototypeCameraFollow>();
            follow.Configure(target);
        }

        static void CreateObstacles()
        {
            int[] lanes = { -1, 1, 0, -1, 1, 0, 1, -1, 0, 1, -1, 0 };

            for (int i = 0; i < lanes.Length; i++)
            {
                GameObject obstacle = CreatePrimitive(PrimitiveType.Cube, "Obstacle", new Vector3(lanes[i] * 3f, 0.85f, 28f + i * 19f), new Vector3(1.85f, 1.7f, 1.6f), new Color(0.96f, 0.2f, 0.16f));
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
