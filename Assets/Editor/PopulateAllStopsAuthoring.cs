using System;
using System.IO;
using RogueDrive.Gameplay;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace RogueDrive.Editor
{
    public static class PopulateAllStopsAuthoring
    {
        const string Folder = "Assets/Content/Encounters";
        const string PrefabPath = Folder + "/RoadsideWalker.prefab";

        private struct StopConfig
        {
            public string detourName;
            public string encounterName;
            public Vector3 center;
            public int zombieCount;
            public string navAsset;

            public StopConfig(string dName, string eName, Vector3 pos, int count, string asset)
            {
                detourName = dName;
                encounterName = eName;
                center = pos;
                zombieCount = count;
                navAsset = asset;
            }
        }

        [MenuItem("RogueDrive/Enemies/Populate All 5 Remaining Stops")]
        public static void Populate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Stage1_Outskirts" && scene.name != "Coop_Outskirts") throw new InvalidOperationException("Open Stage1_Outskirts or Coop_Outskirts first.");

            Directory.CreateDirectory(Folder);
            var walkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (walkerPrefab == null) throw new InvalidOperationException("RoadsideWalker.prefab not found at " + PrefabPath);

            StopConfig[] stops = new StopConfig[]
            {
                new StopConfig("Detour_2_Birch_Camp", "Encounter_BirchCamp", new Vector3(264f, 23.5f, 2691f), 3, "BirchCampNavigation.asset"),
                new StopConfig("Detour_3_Suburban_Service", "Encounter_SuburbanService", new Vector3(13f, 13.5f, 4584f), 4, "SuburbanServiceNavigation.asset"),
                new StopConfig("Detour_4_Forestry_Camp", "Encounter_ForestryCamp", new Vector3(827f, 33.3f, 6424f), 3, "ForestryCampNavigation.asset"),
                new StopConfig("Detour_5_Freight_Yard", "Encounter_FreightYard", new Vector3(459f, 20.8f, 8191f), 4, "FreightYardNavigation.asset"),
                new StopConfig("Detour_6_Rocky_Picnic", "Encounter_RockyPicnic", new Vector3(403f, 48.3f, 9913f), 3, "RockyPicnicNavigation.asset")
            };

            int createdEncounters = 0;
            int totalZombies = 0;

            foreach (var stop in stops)
            {
                var site = GameObject.Find(stop.detourName);
                if (site == null)
                {
                    Debug.LogWarning("[PopulateStops] Площадка " + stop.detourName + " не найдена.");
                    continue;
                }

                // Проверяем существующий энкаунтер
                Transform existing = site.transform.Find(stop.encounterName);
                GameObject root;
                if (existing != null)
                {
                    root = existing.gameObject;
                }
                else
                {
                    root = new GameObject(stop.encounterName);
                    root.transform.SetParent(site.transform, false);
                    root.transform.position = stop.center;
                    root.transform.rotation = Quaternion.identity;
                    root.AddComponent<RoadsideEncounter>();
                }

                // Настройка NavMeshSurface
                var surface = root.GetComponent<NavMeshSurface>() ?? root.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Volume;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.center = new Vector3(0f, 2f, 0f);
                surface.size = new Vector3(150f, 36f, 150f);
                surface.overrideVoxelSize = true;
                surface.voxelSize = .13f;

                // Запекание навигации
                BakeSurface(surface, stop.navAsset);

                // Очистка старых зомби внутри этого энкаунтера при перегенерации
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var child = root.transform.GetChild(i);
                    if (child.name.StartsWith("RoadsideWalker"))
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }

                // Спавн зомби на валидных точках навмеша
                for (int i = 0; i < stop.zombieCount; i++)
                {
                    float angle = (i * 360f / stop.zombieCount) * Mathf.Deg2Rad;
                    float dist = 8f + (i % 2) * 6f;
                    Vector3 desired = root.transform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                    if (NavMesh.SamplePosition(desired, out var hit, 10f, NavMesh.AllAreas))
                    {
                        var walker = (GameObject)PrefabUtility.InstantiatePrefab(walkerPrefab, root.transform);
                        walker.name = $"RoadsideWalker_{i + 1}";
                        walker.transform.SetPositionAndRotation(hit.position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                        totalZombies++;
                    }
                    else
                    {
                        // Резервная позиция прямо в центре
                        if (NavMesh.SamplePosition(root.transform.position, out var centerHit, 10f, NavMesh.AllAreas))
                        {
                            var walker = (GameObject)PrefabUtility.InstantiatePrefab(walkerPrefab, root.transform);
                            walker.name = $"RoadsideWalker_{i + 1}";
                            walker.transform.SetPositionAndRotation(centerHit.position, Quaternion.identity);
                            totalZombies++;
                        }
                    }
                }

                createdEncounters++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PopulateStops] Успешно заселены 5 остановок! Создано энкаунтеров: {createdEncounters}, новых зомби размещено: {totalZombies}.");
        }

        static void BakeSurface(NavMeshSurface surface, string assetName)
        {
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                Debug.LogWarning("[PopulateStops] Ошибка запекания для " + surface.name);
                return;
            }
            string path = Folder + "/" + assetName;
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, path);
            }
            else
            {
                var generated = surface.navMeshData;
                surface.RemoveData();
                EditorUtility.CopySerialized(generated, existing);
                surface.navMeshData = existing;
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                surface.AddData();
            }
            AssetDatabase.SaveAssets();
        }
    }
}
