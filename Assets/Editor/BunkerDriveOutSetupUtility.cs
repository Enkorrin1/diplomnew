using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueDrive.Gameplay.Hub;

namespace RogueDrive.EditorScripts
{
    public static class BunkerDriveOutSetupUtility
    {
        // [InitializeOnLoadMethod]
        // private static void OnProjectLoaded()
        // {
        //     EditorApplication.delayCall += SetupBunkerScene;
        // }

        [MenuItem("RogueDrive/Настроить бункер для интерактивного выезда")]
        public static void SetupBunkerScene()
        {
            string scenePath = "Assets/Scenes/GarageScene.unity";
            var currentScene = EditorSceneManager.GetActiveScene();
            bool needRestore = false;
            string originalPath = currentScene.path;

            if (currentScene.path != scenePath)
            {
                currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                needRestore = true;
            }

            Debug.Log("[BunkerDriveOutSetupUtility] Начало настройки сцены Бункера...");

            // 1. Настройка проема в задней стене (Wall_Back)
            Transform env = GameObject.Find("Environment")?.transform;
            if (env != null)
            {
                Transform wallBack = env.Find("Wall_Back") ?? env.Find("Wall_Back_Left");
                Material wallMat = null;
                if (wallBack != null)
                {
                    wallBack.name = "Wall_Back_Left";
                    wallBack.localPosition = new Vector3(-8.5f, 3f, 12.5f);
                    wallBack.localScale = new Vector3(9f, 6f, 1f);
                    var rend = wallBack.GetComponent<MeshRenderer>();
                    if (rend != null) wallMat = rend.sharedMaterial;
                }

                Transform wallRight = env.Find("Wall_Back_Right");
                if (wallRight == null)
                {
                    GameObject rightObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rightObj.name = "Wall_Back_Right";
                    rightObj.transform.SetParent(env, false);
                    rightObj.transform.localPosition = new Vector3(8.5f, 3f, 12.5f);
                    rightObj.transform.localScale = new Vector3(9f, 6f, 1f);
                    if (wallMat != null) rightObj.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
                }

                Transform wallTop = env.Find("Wall_Back_Top");
                if (wallTop == null)
                {
                    GameObject topObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    topObj.name = "Wall_Back_Top";
                    topObj.transform.SetParent(env, false);
                    topObj.transform.localPosition = new Vector3(0f, 5.25f, 12.5f);
                    topObj.transform.localScale = new Vector3(8f, 1.5f, 1f);
                    if (wallMat != null) topObj.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
                }

                // 2. Настройка выездной дороги Outside_Road за воротами
                Transform outsideRoad = env.Find("Outside_Road");
                if (outsideRoad != null)
                {
                    outsideRoad.localPosition = new Vector3(0f, 1.8f, 25f);
                    outsideRoad.localScale = new Vector3(12f, 0.4f, 24f);
                }
            }

            // 3. Создание триггера выезда GarageExitTrigger
            GameObject exitTriggerObj = GameObject.Find("GarageExitTrigger");
            if (exitTriggerObj == null)
            {
                exitTriggerObj = new GameObject("GarageExitTrigger");
                var box = exitTriggerObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(14f, 6f, 4f);
                exitTriggerObj.transform.position = new Vector3(0f, 3.5f, 21f);
                exitTriggerObj.AddComponent<GarageExitTrigger>();
                Debug.Log("[BunkerDriveOutSetupUtility] Создан GarageExitTrigger на z = 21!");
            }

            // 4. Добавление GarageDriveOutController
            GameObject garageMgr = GameObject.Find("GarageManager") ?? GameObject.Find("GarageHubRoot");
            if (garageMgr != null)
            {
                if (garageMgr.GetComponent<GarageDriveOutController>() == null)
                {
                    garageMgr.AddComponent<GarageDriveOutController>();
                    Debug.Log("[BunkerDriveOutSetupUtility] Добавлен GarageDriveOutController на " + garageMgr.name);
                }
            }

            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            Debug.Log("[BunkerDriveOutSetupUtility] Сцена Бункера успешно обновлена и сохранена!");

            if (needRestore && !string.IsNullOrEmpty(originalPath) && originalPath != scenePath)
            {
                EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
            }
        }
    }
}
