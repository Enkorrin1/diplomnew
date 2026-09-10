using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Наполняет каждую Stage-сцену биомными декорациями вдоль обочин.
    /// Требует предварительно запечённой геометрии дороги через
    /// «RogueDrive → Scene Authoring → Bake All Stage Worlds For Editing».
    /// </summary>
    public static class StageSceneDresser
    {
        // ── Пути к префабам ─────────────────────────────────────────────────────────

        // Здания
        const string PGarage     = "Assets/Cartoon Buildings/Prefabs/Garage.prefab";
        const string PGasStation = "Assets/Cartoon Buildings/Prefabs/Gas_station.prefab";
        const string PStation    = "Assets/Cartoon Buildings/Prefabs/Station.prefab";

        // Автомобили (Awbmecreations)
        const string PCarClassic  = "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Classic Car_9.prefab";
        const string PCarPolice   = "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Police Car N_4.prefab";
        const string PCarMilitary = "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Military Vehicle_3.prefab";
        const string PCarVan      = "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/N Van_10.prefab";
        const string PCarPickup   = "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Pick Up_11.prefab";

        // Камни
        const string PStone1 = "Assets/Low Poly Stones/Prefabs/ST_Stone1.prefab";
        const string PStone2 = "Assets/Low Poly Stones/Prefabs/ST_Stone2.prefab";
        const string PStone3 = "Assets/Low Poly Stones/Prefabs/ST_Stone3.prefab";
        const string PStone4 = "Assets/Low Poly Stones/Prefabs/ST_Stone4.prefab";
        const string PStone5 = "Assets/Low Poly Stones/Prefabs/ST_Stone5.prefab";

        // Гаражные пропы (GarageAssetPack)
        const string PBarrel          = "Assets/GarageAssetPack/Prefabs/Barrelfbx.prefab";
        const string PWoodenPallet    = "Assets/GarageAssetPack/Prefabs/WoodenPallet.prefab";
        const string PStorageShelfFull= "Assets/GarageAssetPack/Prefabs/StorageShelfFull.prefab";
        const string PStorageShelf    = "Assets/GarageAssetPack/Prefabs/StorageShelf.prefab";
        const string PWorkbenchFull   = "Assets/GarageAssetPack/Prefabs/WorkbenchFull.prefab";
        const string PJerrycan        = "Assets/GarageAssetPack/Prefabs/Jerrycan.prefab";
        const string PJerrycanLarge   = "Assets/GarageAssetPack/Prefabs/JerrycanLarge.prefab";
        const string PTrashCan        = "Assets/GarageAssetPack/Prefabs/TrashCan.prefab";
        const string PCarWheel        = "Assets/GarageAssetPack/Prefabs/CarWheel.prefab";

        // Контейнер
        const string PContainer = "Assets/FREE Low Poly Shipping Container/Prefabs/Low Poly Shipping Container.prefab";

        // LowPoly survival (Assets/Prefabs)
        const string PLowPolyTent     = "Assets/Prefabs/LowPolyTent.prefab";
        const string PLowPolyPallet   = "Assets/Prefabs/LowPolyPallet.prefab";
        const string PLowPolyBarrel   = "Assets/Prefabs/LowPolyBarrel.prefab";
        const string PLowPolyStump    = "Assets/Prefabs/LowPolyStump.prefab";
        const string PLowPolyWoodPile = "Assets/Prefabs/LowPolyWoodPile.prefab";
        const string PLowPolyPole     = "Assets/Prefabs/LowPolyPole.prefab";
        const string PLowPolyStairs   = "Assets/Prefabs/LowPolyStairs.prefab";

        // ── Описание 4 Stage-сцен ───────────────────────────────────────────────────

        static readonly (string ScenePath, int BiomeIndex, string Label)[] Stages =
        {
            ("Assets/Scenes/Stage1_Outskirts.unity",  0, "Городские Окраины"),
            ("Assets/Scenes/Stage2_Wasteland.unity",  1, "Пылевая Пустошь"),
            ("Assets/Scenes/Stage3_Industrial.unity", 2, "Затопленная Промзона"),
            ("Assets/Scenes/Stage4_Citadel.unity",    3, "Подступы к Цитадели"),
        };

        // ── Точки входа (меню) ──────────────────────────────────────────────────────

        [MenuItem("RogueDrive/Dress All Stage Scenes")]
        public static void DressAllStages()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[StageSceneDresser] Остановите Play Mode перед запуском!");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            BiomeConfig[] biomes = BiomeConfig.GetDefaultBiomes();
            int totalDressed = 0;

            foreach (var stage in Stages)
            {
                if (!File.Exists(stage.ScenePath))
                {
                    Debug.LogWarning($"[StageSceneDresser] Сцена не найдена: {stage.ScenePath} — пропускаем.");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(stage.ScenePath, OpenSceneMode.Single);
                BiomeConfig biome = biomes[stage.BiomeIndex];

                int dressed = DressOpenScene(stage.BiomeIndex, biome);
                totalDressed += dressed;

                ApplyBiomeLighting(biome);
                SceneAuthoringMigration.PersistGeneratedAssets(scene.GetRootGameObjects());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Debug.Log($"[StageSceneDresser] ✔ {stage.Label}: {dressed} чанков задекорировано → {stage.ScenePath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[StageSceneDresser] ✅ Готово! Всего задекорировано чанков: {totalDressed}");
        }

        [MenuItem("RogueDrive/Dress Current Stage Scene")]
        public static void DressCurrentStageScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[StageSceneDresser] Остановите Play Mode перед запуском!");
                return;
            }

            string sceneName = EditorSceneManager.GetActiveScene().name;
            int biomeIndex = 0;
            if      (sceneName.Contains("Stage2") || sceneName.Contains("Wasteland"))   biomeIndex = 1;
            else if (sceneName.Contains("Stage3") || sceneName.Contains("Industrial")) biomeIndex = 2;
            else if (sceneName.Contains("Stage4") || sceneName.Contains("Citadel"))    biomeIndex = 3;

            BiomeConfig biome = BiomeConfig.GetDefaultBiomes()[biomeIndex];
            int count = DressOpenScene(biomeIndex, biome);
            ApplyBiomeLighting(biome);

            var scene = EditorSceneManager.GetActiveScene();
            SceneAuthoringMigration.PersistGeneratedAssets(scene.GetRootGameObjects());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[StageSceneDresser] ✅ Текущая сцена «{sceneName}» (биом {biomeIndex}): задекорировано {count} чанков.");
        }

        [MenuItem("RogueDrive/Undress All Stage Scenes (Clear Scenery)")]
        public static void UndressAllStages()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.SaveOpenScenes();

            foreach (var stage in Stages)
            {
                if (!File.Exists(stage.ScenePath)) continue;
                var scene = EditorSceneManager.OpenScene(stage.ScenePath, OpenSceneMode.Single);
                int removed = RemoveScenery();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[StageSceneDresser] 🗑 {stage.Label}: удалено {removed} Scenery-объектов.");
            }
            AssetDatabase.SaveAssets();
        }

        // ── Основная логика ──────────────────────────────────────────────────────────

        static int DressOpenScene(int biomeIndex, BiomeConfig biome)
        {
            // Ищем генератор трассы (он является контейнером запечённых чанков)
            var generator = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (generator == null)
            {
                Debug.LogError("[StageSceneDresser] ProceduralTrackGenerator не найден в сцене!");
                return 0;
            }

            // Пробуем найти AuthoredStageWorld через SerializedObject
            Transform authoredRoot = null;
            var so = new SerializedObject(generator);
            var authoredProp = so.FindProperty("authoredCampaign");
            if (authoredProp != null && authoredProp.objectReferenceValue is Transform t)
                authoredRoot = t;

            // Запасной вариант — поиск по имени в иерархии
            if (authoredRoot == null)
                authoredRoot = generator.transform.Find("AuthoredStageWorld");

            if (authoredRoot == null)
            {
                Debug.LogError(
                    "[StageSceneDresser] Запечённый мир не найден в сцене!\n" +
                    "Сначала выполните: RogueDrive → Scene Authoring → Bake All Stage Worlds For Editing");
                return 0;
            }

            // Получаем все TrackChunk в порядке от начала трассы
            TrackChunk[] chunks = authoredRoot
                .GetComponentsInChildren<TrackChunk>(true)
                .OrderBy(c =>
                {
                    var baked = c.GetComponent<BakedMapChunk>();
                    return baked != null ? baked.Order : 0;
                })
                .ToArray();

            if (chunks.Length == 0)
            {
                Debug.LogWarning($"[StageSceneDresser] TrackChunk-и не найдены в «{authoredRoot.name}».");
                return 0;
            }

            // Загружаем биомные наборы пропов (large / medium / hazard / accent)
            GameObject[][] propSets = BuildBiomePropSets(biomeIndex);
            Color groundColor = GetGroundColor(biomeIndex);

            int dressed = 0;
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];

                // Не перезаписываем уже задекорированные чанки
                if (chunk.transform.Find("Scenery") != null ||
                    chunk.transform.Find("Scenery_RoadAligned") != null)
                    continue;

                DressChunk(chunk, biomeIndex, i, propSets, groundColor);
                dressed++;
            }

            return dressed;
        }

        static void DressChunk(TrackChunk chunk, int biomeIndex, int chunkIndex,
                               GameObject[][] propSets, Color groundColor)
        {
            var root = new GameObject("Scenery");
            root.transform.SetParent(chunk.transform, false);

            // ── Земля по обе стороны от дороги ──────────────────────────────────────
            Cube(root.transform, "Landscape_Left",
                new Vector3(-60f, -0.65f, 50f), new Vector3(90f, 1f, 110f), groundColor);
            Cube(root.transform, "Landscape_Right",
                new Vector3( 60f, -0.65f, 50f), new Vector3(90f, 1f, 110f), groundColor);

            // ── Обочинные декорации (левая и правая сторона) ─────────────────────────
            foreach (int side in new[] { -1, 1 })
                PlaceSideProps(root.transform, biomeIndex, chunkIndex, side, propSets);

            // ── Дорожное препятствие каждые 3 чанка ────────────────────────────────
            if (chunkIndex % 3 == 0 && propSets[2] != null && propSets[2].Length > 0)
            {
                float xPos = (chunkIndex % 2 == 0) ? -5f : 5f;
                var hazardPrefab = propSets[2][chunkIndex % propSets[2].Length];
                var hazard = Place(root.transform, hazardPrefab,
                    new Vector3(xPos, 0f, 50f), new Vector3(2.2f, 2.5f, 2.2f), 0f);
                if (hazard != null)
                {
                    var box        = hazard.AddComponent<BoxCollider>();
                    box.center     = new Vector3(0f, 0.75f, 0f);
                    box.size       = new Vector3(2f, 1.5f, 2f);
                    hazard.AddComponent<TrackObstacle>().Configure(false);
                }
            }

            // ── Выравнивание декораций по дорожным сегментам чанка ─────────────────
            AlignSceneryToRoad(chunk, root.transform, groundColor);
        }

        static void PlaceSideProps(Transform root, int biomeIndex, int chunkIndex,
                                   int side, GameObject[][] propSets)
        {
            // propSets[0] = крупные фоновые объекты (здания, контейнеры)
            // propSets[1] = средние объекты (машины, бочки, паллеты)
            // propSets[2] = дорожные препятствия (только для hazard)
            // propSets[3] = мелкие акцентные детали (камни, обломки)

            // Крупные объекты на дальнем краю обочины
            if (propSets[0] != null && propSets[0].Length > 0)
            {
                for (int n = 0; n < 2; n++)
                {
                    var prefab = propSets[0][(chunkIndex * 2 + n + (side + 1) / 2) % propSets[0].Length];
                    float xOff = side * (30f + n * 8f);
                    Place(root, prefab,
                        new Vector3(xOff, 0f, 18f + n * 42f),
                        new Vector3(14f, 16f, 18f),
                        side * 90f);
                }
            }

            // Средние объекты ближе к краю дороги
            if (propSets[1] != null && propSets[1].Length > 0)
            {
                for (int n = 0; n < 2; n++)
                {
                    int idx = (chunkIndex + n + (side + 1) / 2 * 3) % propSets[1].Length;
                    var prefab = propSets[1][idx];
                    float xOff = side * (16f + n * 3f);
                    float rot  = (chunkIndex * 37 + n * 73) % 180;
                    Place(root, prefab,
                        new Vector3(xOff, 0f, 32f + n * 34f),
                        new Vector3(4f, 5f, 4f),
                        rot);
                }
            }

            // Мелкие акцентные детали вдоль края
            if (propSets[3] != null && propSets[3].Length > 0)
            {
                int idx = (chunkIndex + (side + 1) / 2 * 5) % propSets[3].Length;
                Place(root, propSets[3][idx],
                    new Vector3(side * 14f, 0f, 67f),
                    new Vector3(3f, 3.5f, 3f),
                    side * (float)((chunkIndex * 53) % 180));
            }
        }

        // ── Биомные наборы ───────────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает 4 массива для каждого биома:
        /// [0] = крупные фоновые объекты
        /// [1] = средние объекты обочины
        /// [2] = дорожные препятствия
        /// [3] = мелкие акцентные детали
        /// </summary>
        static GameObject[][] BuildBiomePropSets(int biomeIndex)
        {
            switch (biomeIndex)
            {
                case 0: // Городские Окраины: гаражи, брошенные машины, камни
                    return new[]
                    {
                        Load(PGarage, PGasStation, PStation),                                                    // large
                        Load(PCarClassic, PCarPolice, PCarVan, PWoodenPallet, PLowPolyTent, PLowPolyPole),       // medium
                        Load(PBarrel, PLowPolyBarrel),                                                           // hazards
                        Load(PStone1, PStone2, PStone3, PStone4, PStone5, PLowPolyStump),                       // accents
                    };

                case 1: // Пылевая Пустошь: контейнеры, военная техника, камни
                    return new[]
                    {
                        Load(PContainer, PLowPolyTent, PContainer),                                             // large
                        Load(PCarMilitary, PBarrel, PWoodenPallet, PLowPolyBarrel, PCarPickup),                 // medium
                        Load(PBarrel, PLowPolyBarrel),                                                           // hazards
                        Load(PStone1, PStone3, PStone5, PLowPolyStump, PLowPolyWoodPile),                       // accents
                    };

                case 2: // Затопленная Промзона: склады, оборудование, канистры
                    return new[]
                    {
                        Load(PStorageShelfFull, PContainer, PStorageShelf),                                     // large
                        Load(PWorkbenchFull, PJerrycan, PJerrycanLarge, PWoodenPallet, PLowPolyStairs),         // medium
                        Load(PBarrel, PLowPolyBarrel),                                                           // hazards
                        Load(PTrashCan, PLowPolyPallet, PLowPolyBarrel, PCarWheel),                             // accents
                    };

                case 3: // Подступы к Цитадели: укрепления, военная техника, баррикады
                    return new[]
                    {
                        Load(PContainer, PStorageShelf, PLowPolyTent),                                          // large
                        Load(PCarMilitary, PTrashCan, PWoodenPallet, PLowPolyWoodPile, PJerrycan),              // medium
                        Load(PBarrel, PLowPolyBarrel),                                                           // hazards
                        Load(PStone2, PStone4, PLowPolyPallet, PCarWheel),                                      // accents
                    };

                default:
                    return new[] { new GameObject[0], new GameObject[0], new GameObject[0], new GameObject[0] };
            }
        }

        static Color GetGroundColor(int biomeIndex)
        {
            switch (biomeIndex)
            {
                case 0: return new Color(0.30f, 0.24f, 0.18f); // серо-коричневая городская грязь
                case 1: return new Color(0.48f, 0.36f, 0.20f); // тёплый песчаный
                case 2: return new Color(0.12f, 0.18f, 0.14f); // тёмный токсичный
                case 3: return new Color(0.16f, 0.14f, 0.16f); // почти чёрный бетон
                default: return new Color(0.28f, 0.26f, 0.22f);
            }
        }

        // ── Загрузка префабов ────────────────────────────────────────────────────────

        static GameObject[] Load(params string[] paths)
        {
            var result = new List<GameObject>();
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    result.Add(prefab);
                else
                    Debug.LogWarning($"[StageSceneDresser] Префаб не найден: {path}");
            }
            return result.ToArray();
        }

        // ── Освещение и туман биома ──────────────────────────────────────────────────

        static void ApplyBiomeLighting(BiomeConfig biome)
        {
            RenderSettings.fog               = true;
            RenderSettings.fogColor          = biome.fogColor;
            RenderSettings.fogDensity        = biome.fogDensity;
            RenderSettings.ambientSkyColor    = biome.fogColor * 1.15f;
            RenderSettings.ambientEquatorColor= biome.fogColor * 0.80f;
            RenderSettings.ambientGroundColor = biome.roadColor * 0.60f;

            Light sun = RenderSettings.sun ?? Object.FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.color     = Color.Lerp(biome.fogColor, Color.white, 0.45f);
                sun.intensity = biome.type == BiomeType.DustyWasteland ? 1.4f : 1.1f;
            }
        }

        // ── Выравнивание декораций по кривой дороги ──────────────────────────────────

        static void AlignSceneryToRoad(TrackChunk chunk, Transform sceneryRoot, Color groundColor)
        {
            if (chunk.RoadRenderers == null || chunk.RoadRenderers.Length == 0) return;

            // Снимаем список дочерних до модификации иерархии
            var children = sceneryRoot.Cast<Transform>().ToList();

            foreach (var child in children)
            {
                // Terrain-плиты выравниваем отдельно, просто пропускаем
                if (child.name.StartsWith("Landscape_")) continue;

                Vector3 local    = child.localPosition;
                Quaternion localRot = child.localRotation;

                // Ближайший дорожный сегмент по Z-координате
                Renderer road = chunk.RoadRenderers
                    .Where(r => r != null)
                    .OrderBy(r => Mathf.Abs(
                        chunk.transform.InverseTransformPoint(r.transform.position).z - local.z))
                    .First();

                float roadLocalZ = chunk.transform.InverseTransformPoint(road.transform.position).z;
                float dot        = Mathf.Max(0.3f, Vector3.Dot(road.transform.forward, chunk.transform.forward));

                Vector3 worldPos = road.transform.position
                    + road.transform.forward * ((local.z - roadLocalZ) / dot)
                    + road.transform.right   * local.x;
                worldPos.y = chunk.transform.position.y + local.y;

                child.SetPositionAndRotation(worldPos, road.transform.rotation * localRot);
            }

            // Добавляем грунтовые полосы вплотную к каждому дорожному сегменту
            int idx = 0;
            foreach (var road in chunk.RoadRenderers)
            {
                if (road == null) continue;
                foreach (int side in new[] { -1, 1 })
                {
                    var terrain = new GameObject($"Roadside_{idx}_{side}");
                    terrain.transform.SetParent(sceneryRoot, false);
                    terrain.transform.SetPositionAndRotation(road.transform.position, road.transform.rotation);
                    float segLen = road.transform.lossyScale.z + 2f;
                    Cube(terrain.transform, "Ground",
                        new Vector3(side * 60f, -0.65f, 0f),
                        new Vector3(90f, 1f, segLen),
                        groundColor);
                }
                idx++;
            }

            sceneryRoot.name = "Scenery_RoadAligned";
        }

        // ── Утилита: удалить Scenery из всех чанков в открытой сцене ─────────────────

        static int RemoveScenery()
        {
            int count = 0;
            foreach (var chunk in Object.FindObjectsByType<TrackChunk>(FindObjectsSortMode.None))
            {
                foreach (Transform child in chunk.transform.Cast<Transform>().ToArray())
                {
                    if (child.name == "Scenery" || child.name == "Scenery_RoadAligned")
                    {
                        Object.DestroyImmediate(child.gameObject);
                        count++;
                    }
                }
            }
            return count;
        }

        // ── Хелперы размещения ────────────────────────────────────────────────────────

        static GameObject Place(Transform parent, GameObject prefab,
                                Vector3 localPos, Vector3 maxSize, float yawDeg)
        {
            if (prefab == null) return null;

            var holder = new GameObject(prefab.name);
            holder.transform.SetParent(parent, false);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                Vector3 size  = bounds.size;
                float   scale = Mathf.Min(
                    maxSize.x / Mathf.Max(0.01f, size.x),
                    maxSize.y / Mathf.Max(0.01f, size.y),
                    maxSize.z / Mathf.Max(0.01f, size.z));

                Vector3 offset = holder.transform.InverseTransformPoint(bounds.center);
                visual.transform.localScale    *= scale;
                visual.transform.localPosition  = -offset * scale + Vector3.up * size.y * scale * 0.5f;
            }

            // Коллайдеры пропов-декораций отключены — они лишь декорация
            foreach (var c  in visual.GetComponentsInChildren<Collider>())   c.enabled      = false;
            foreach (var rb in visual.GetComponentsInChildren<Rigidbody>()) rb.isKinematic  = true;

            holder.transform.localPosition = localPos;
            holder.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            return holder;
        }

        static void Cube(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale    = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Standard")) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
