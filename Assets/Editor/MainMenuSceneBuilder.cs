#if UNITY_EDITOR
using System.IO;
using RogueDrive.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Автоматизированный строитель сцены Главного меню (MainMenuScene.unity).
    /// Создает 3D-подиум, настраивает камеру, драматическое освещение и меню.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/MainMenuScene.unity";

        [MenuItem("RogueDrive/Создать сцену Главного Меню", priority = 10)]
        public static void BuildMainMenu()
        {
            Directory.CreateDirectory("Assets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.12f, 0.15f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.12f);
            RenderSettings.fogDensity = 0.035f;

            // 1. Камера
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            cam.fieldOfView = 50f;
            camObj.transform.position = new Vector3(0f, 2.2f, -5.5f);
            camObj.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            camObj.tag = "MainCamera";
            camObj.AddComponent<AudioListener>();

            // 2. Освещение (драматический прожектор на машину)
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.85f, 0.9f, 1f);
            dirLight.intensity = 0.8f;
            lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            GameObject spotObj = new GameObject("Podium Spotlight");
            Light spotLight = spotObj.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.color = new Color(0.2f, 0.9f, 1f);
            spotLight.intensity = 2.5f;
            spotLight.range = 15f;
            spotLight.spotAngle = 65f;
            spotObj.transform.position = new Vector3(0f, 6f, 0f);
            spotObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 3. Круглый подиум
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "PodiumPedestal";
            pedestal.transform.position = new Vector3(0f, -0.1f, 0f);
            pedestal.transform.localScale = new Vector3(4.5f, 0.2f, 4.5f);
            Renderer pedR = pedestal.GetComponent<Renderer>();
            if (pedR != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.1f, 0.12f, 0.16f);
                m.SetFloat("_Glossiness", 0.75f);
                m.SetFloat("_Metallic", 0.5f);
                pedR.sharedMaterial = m;
            }

            // Светящееся неоновое кольцо подиума
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "NeonRing";
            ring.transform.position = new Vector3(0f, -0.05f, 0f);
            ring.transform.localScale = new Vector3(4.7f, 0.05f, 4.7f);
            Renderer ringR = ring.GetComponent<Renderer>();
            if (ringR != null)
            {
                Material rm = new Material(Shader.Find("Standard"));
                rm.color = new Color(0f, 0.85f, 1f);
                rm.EnableKeyword("_EMISSION");
                rm.SetColor("_EmissionColor", new Color(0f, 0.85f, 1f) * 1.5f);
                ringR.sharedMaterial = rm;
            }

            // 4. Подиум для вращения машины
            GameObject anchor = new GameObject("PodiumAnchor");
            anchor.transform.position = new Vector3(0f, 0.2f, 0f);

            // 5. Менеджер переходов между сценами (отдельный постоянный объект)
            GameObject transitionRoot = new GameObject("SceneTransitionManager");
            transitionRoot.AddComponent<SceneTransitionManager>();

            // 6. Корневой объект меню (уничтожается при смене сцены)
            GameObject menuRoot = new GameObject("MainMenuController");
            MainMenuController menuController = menuRoot.AddComponent<MainMenuController>();

            // Настройка связей
            SerializedObject so = new SerializedObject(menuController);
            so.FindProperty("podiumAnchor").objectReferenceValue = anchor.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Сохранение сцены
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[MainMenuSceneBuilder] ✅ Сцена Главного Меню успешно создана: {ScenePath}");

            // Обновление Build Settings
            SyncBuildSettings();
        }

        public static void SyncBuildSettings()
        {
            string[] required = new[]
            {
                "Assets/Scenes/MainMenuScene.unity",
                "Assets/Scenes/GarageScene.unity",
                "Assets/Scenes/RogueDrivePrototype.unity"
            };

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var path in required)
            {
                if (File.Exists(path))
                {
                    list.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[MainMenuSceneBuilder] 📋 Build Settings синхронизированы: {list.Count} сцен зарегистрировано.");
        }
    }
}
#endif
