#if UNITY_EDITOR
using System.IO;
using RogueDrive.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.EditorTools
{
    public static class GarageSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/GarageScene.unity";
        const string PrototypeScenePath = "Assets/Scenes/RogueDrivePrototype.unity";
        const string CatalogPath = "Assets/Content/GarageCatalog.asset";

        [MenuItem("RogueDrive/Создать сцену Гаража")]
        public static void CreateGarageScene()
        {
            // Убеждаемся, что контент каталога настроен
            GarageContentBuilder.BuildAllGarageContent();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.22f);

            // 1. Освещение ангара
            GameObject dirLight = new GameObject("Key_Directional_Light");
            Light l1 = dirLight.AddComponent<Light>();
            l1.type = LightType.Directional;
            l1.intensity = 1.3f;
            l1.color = new Color(1f, 0.96f, 0.9f);
            dirLight.transform.rotation = Quaternion.Euler(38f, -40f, 0f);

            GameObject spotLight = new GameObject("Podium_Spotlight");
            Light l2 = spotLight.AddComponent<Light>();
            l2.type = LightType.Spot;
            l2.intensity = 3.5f;
            l2.range = 15f;
            l2.spotAngle = 60f;
            l2.color = new Color(0.85f, 0.92f, 1f);
            spotLight.transform.position = new Vector3(0f, 6.5f, 0f);
            spotLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 2. Окружение гаража (пол, стены, подиум)
            GameObject garageEnv = new GameObject("GarageEnvironment");

            // Бетонный пол
            CreatePrimitive(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(30f, 0.5f, 30f), new Color(0.1f, 0.11f, 0.14f), garageEnv.transform);

            // Задняя стена ангара
            CreatePrimitive(PrimitiveType.Cube, "BackWall", new Vector3(0f, 5f, 10f), new Vector3(30f, 10f, 1f), new Color(0.13f, 0.15f, 0.18f), garageEnv.transform);

            // Боковые балки ангара
            CreatePrimitive(PrimitiveType.Cube, "Pillar_L", new Vector3(-8f, 5f, 8f), new Vector3(1f, 10f, 1f), new Color(0.2f, 0.22f, 0.26f), garageEnv.transform);
            CreatePrimitive(PrimitiveType.Cube, "Pillar_R", new Vector3(8f, 5f, 8f), new Vector3(1f, 10f, 1f), new Color(0.2f, 0.22f, 0.26f), garageEnv.transform);

            // 3. Круглый вращающийся подиум
            GameObject podiumBase = CreatePrimitive(PrimitiveType.Cylinder, "PodiumBase", new Vector3(0f, 0.1f, 0f), new Vector3(6.5f, 0.2f, 6.5f), new Color(0.18f, 0.2f, 0.25f), garageEnv.transform);

            // Светящаяся неоновая окантовка подиума
            GameObject ring = CreatePrimitive(PrimitiveType.Cylinder, "PodiumRing", new Vector3(0f, 0.12f, 0f), new Vector3(6.8f, 0.05f, 6.8f), new Color(0.1f, 0.7f, 1f), garageEnv.transform);

            // Точка привязки для вращения машины
            GameObject podiumAnchor = new GameObject("PodiumAnchor");
            podiumAnchor.transform.position = new Vector3(0f, 0.2f, 0f);

            // 4. Камера гаража
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 2.2f, -5.8f);
            camObj.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.1f);
            cam.fieldOfView = 50f;

            // 5. Контроллер UI Гаража
            GameObject managerObj = new GameObject("GarageManager");
            GarageUIController controller = managerObj.AddComponent<GarageUIController>();

            GarageCatalog catalog = AssetDatabase.LoadAssetAtPath<GarageCatalog>(CatalogPath);
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.FindProperty("podiumAnchor").objectReferenceValue = podiumAnchor.transform;
            so.FindProperty("autoRotationSpeed").floatValue = 18f;
            so.ApplyModifiedProperties();

            // 6. Сохранение сцены и обновление Build Settings
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(PrototypeScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[GarageSceneBuilder] Сцена Гаража успешно создана: {ScenePath} и включена в Build Settings первым номером!");
        }

        static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (parent != null) go.transform.SetParent(parent, true);

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));
                m.color = color;
                r.sharedMaterial = m;
            }

            return go;
        }
    }
}
#endif
