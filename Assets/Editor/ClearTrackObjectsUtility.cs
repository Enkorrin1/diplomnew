using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueDrive.Gameplay;

namespace RogueDrive.Editor
{
    public static class ClearTrackObjectsUtility
    {
        private static readonly string[] StageScenes = new string[]
        {
            "Assets/Scenes/Stage1_Outskirts.unity",
            "Assets/Scenes/Stage2_Wasteland.unity",
            "Assets/Scenes/Stage3_Industrial.unity",
            "Assets/Scenes/Stage4_Citadel.unity"
        };

        // Автоматическая очистка отключена во избежание непреднамеренного стирания дорожного полотна со сцен.
        // Вызывается только вручную из меню при явной необходимости.
        // [InitializeOnLoadMethod]
        // private static void AutoRunOnce() { ... }

        [MenuItem("RogueDrive/Удалить все объекты трека со всех сцен")]
        public static void CleanAllTracks()
        {
            string originalScenePath = SceneManager.GetActiveScene().path;
            int totalDeleted = 0;

            foreach (string scenePath in StageScenes)
            {
                if (!System.IO.File.Exists(scenePath))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                    continue;

                int sceneDeleted = CleanCurrentScene();
                totalDeleted += sceneDeleted;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[ClearTrackObjects] Сцена {scenePath} успешно очищена: удалено {sceneDeleted} объектов.");
            }

            AssetDatabase.SaveAssets();

            // Возвращаемся в исходную сцену или в Stage1
            string targetReturn = !string.IsNullOrEmpty(originalScenePath) && System.IO.File.Exists(originalScenePath)
                ? originalScenePath
                : StageScenes[0];

            EditorSceneManager.OpenScene(targetReturn, OpenSceneMode.Single);
            Debug.Log($"[ClearTrackObjects] Готово! Все сцены кампании очищены от старых дорог и объектов ({totalDeleted} удалено). На сцене оставлен пустой контейнер 'AuthoredStageWorld' для вашей ручной сборки треков!");
        }

        public static int CleanCurrentScene()
        {
            int deletedCount = 0;

            // 1. RogueDrivePrototype -> AuthoredStageWorld & BakedFirstMap
            var prototypeGO = GameObject.Find("RogueDrivePrototype");
            Transform authoredWorld = null;

            if (prototypeGO != null)
            {
                var generator = prototypeGO.GetComponent<ProceduralTrackGenerator>();
                if (generator != null)
                {
                    generator.SceneAuthoredMode = true;
                    EditorUtility.SetDirty(generator);
                }

                // AuthoredStageWorld: очищаем всех дочерних чанков
                var awTransform = prototypeGO.transform.Find("AuthoredStageWorld");
                if (awTransform != null)
                {
                    authoredWorld = awTransform;
                    for (int i = awTransform.childCount - 1; i >= 0; i--)
                    {
                        GameObject.DestroyImmediate(awTransform.GetChild(i).gameObject);
                        deletedCount++;
                    }
                }
                else
                {
                    // Создаем чистый пустой контейнер для игрока
                    var newAw = new GameObject("AuthoredStageWorld");
                    newAw.transform.SetParent(prototypeGO.transform, false);
                    authoredWorld = newAw.transform;
                }

                if (generator != null)
                {
                    generator.AuthoredCampaign = authoredWorld;
                    EditorUtility.SetDirty(generator);
                }

                // BakedFirstMap: удаляем полностью
                var bakedTransform = prototypeGO.transform.Find("BakedFirstMap");
                if (bakedTransform != null)
                {
                    GameObject.DestroyImmediate(bakedTransform.gameObject);
                    deletedCount++;
                }
            }

            // 2. Удаление объектов BakedMapChunk в сцене
            var allBaked = Object.FindObjectsByType<BakedMapChunk>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in allBaked)
            {
                if (b != null && b.gameObject != null)
                {
                    GameObject.DestroyImmediate(b.gameObject);
                    deletedCount++;
                }
            }

            // 3. Удаление тестовых/заглушечных объектов корня
            string[] namesToDelete = new string[]
            {
                "Initial_Road",
                "Left_Guardrail",
                "Right_Guardrail",
                "Container_Blocker",
                "Rock_Blocker",
                "Road_Barrier",
                "Rock_Blocker_02",
                "Scrubland",
                "Collapsed masonry",
                "Distant abandoned block",
                "Outskirts_Dressing",
                "BakedFirstMap"
            };

            foreach (string name in namesToDelete)
            {
                var gos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var go in gos)
                {
                    if (go != null && go.name == name)
                    {
                        GameObject.DestroyImmediate(go);
                        deletedCount++;
                    }
                }
            }

            // 4. Удаление оставшихся объектов TrackChunk вне AuthoredStageWorld
            var allChunks = Object.FindObjectsByType<TrackChunk>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var chunk in allChunks)
            {
                if (chunk != null && chunk.gameObject != null)
                {
                    if (authoredWorld == null || !chunk.transform.IsChildOf(authoredWorld))
                    {
                        GameObject.DestroyImmediate(chunk.gameObject);
                        deletedCount++;
                    }
                }
            }

            // 5. Освещение и тени для сцены:
            // Включаем мягкие тени и приятный солнечный свет, чтобы мир не был слепым/серым
            var sunLight = RenderSettings.sun;
            if (sunLight == null)
            {
                var dirLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var l in dirLights)
                {
                    if (l.type == LightType.Directional)
                    {
                        sunLight = l;
                        break;
                    }
                }
            }

            if (sunLight != null)
            {
                sunLight.shadows = LightShadows.Soft;
                sunLight.color = new Color(1.0f, 0.96f, 0.88f); // Теплый солнечный свет
                sunLight.intensity = 1.25f;
                RenderSettings.sun = sunLight;
                EditorUtility.SetDirty(sunLight);
            }

            // Настройка тумана, чтобы не заливал серым цветом весь вид
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 1200f;
            RenderSettings.fogColor = new Color(0.65f, 0.75f, 0.85f); // Естественный светлый горизонт

            return deletedCount;
        }
    }
}
