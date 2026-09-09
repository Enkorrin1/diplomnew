using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Процедурный генератор трассы нового поколения.
    /// Поддерживает:
    /// - 4 визуальных биома (Шоссе, Пустошь, Затопленная промзона, Подступы к Цитадели)
    /// - Криволинейные дороги (левые/правые плавные повороты)
    /// - S-образные изгибы (Chicanes / Slalom)
    /// - Бутылочные горлышки (эстакады и мосты с сужением до 11м)
    /// - Тактические развилки путей (Безопасный объезд vs Опасный шорткат)
    /// - Интерактивные объекты (взрывные бочки и ящики с припасами)
    /// </summary>
    public sealed class ProceduralTrackGenerator : MonoBehaviour
    {
        [Header("Target & Chunks")]
        [SerializeField] private Transform targetCar;
        [SerializeField, Min(2)] private int activeChunksAhead = 4;
        [SerializeField, Min(60f)] private float despawnDistanceBehind = 140f;

        [Header("Prefabs for Spawning")]
        [SerializeField] private GameObject[] enemyPrefabs;
        [SerializeField] private GameObject explosiveBarrelPrefab;
        [SerializeField] private GameObject supplyCratePrefab;
        [SerializeField] private GameObject coinPrefab;

        readonly List<TrackChunk> activeChunks = new List<TrackChunk>();
        Vector3 nextSpawnPosition = Vector3.zero;
        Quaternion nextSpawnRotation = Quaternion.identity;

        float totalDistanceGenerated;
        float currentHeadingYaw;
        int currentBiomeIndex = -1;

        BiomeConfig[] biomes;

        private void Awake()
        {
            biomes = BiomeConfig.GetDefaultBiomes();
            EnsureObstaclePrefabs();
        }

        private void Start()
        {
            if (targetCar == null)
            {
                ArcadeCarController car = FindFirstObjectByType<ArcadeCarController>();
                if (car != null)
                    targetCar = car.transform;
            }

            // Стартовая точка генерации сразу после стартовой площадки
            if (nextSpawnPosition == Vector3.zero)
            {
                nextSpawnPosition = new Vector3(0f, 0f, 150f);
            }

            int startSector = CampaignMapModal.SelectedStartSector;
            if (startSector >= 2 && startSector <= 4)
            {
                totalDistanceGenerated = (startSector - 1) * 1000f;
                lastCheckpointSector = startSector - 1;
            }

            // Начальная генерация стартовых чанков
            for (int i = 0; i < activeChunksAhead; i++)
            {
                SpawnNextChunk(i < 2);
            }

            UpdateBiomeEnvironment(totalDistanceGenerated);
        }

        private void Update()
        {
            if (targetCar == null)
                return;

            // Проверка смены биома по текущей позиции игрока
            GameRunController run = FindFirstObjectByType<GameRunController>();
            float playerDist = run != null ? run.Distance : targetCar.position.z;
            UpdateBiomeEnvironment(playerDist);

            // Проверка необходимости спавна нового чанка впереди
            if (activeChunks.Count > 0)
            {
                TrackChunk furthestChunk = activeChunks[activeChunks.Count - 1];
                float distToFurthest = Vector3.Distance(targetCar.position, furthestChunk.transform.position);

                if (distToFurthest < activeChunksAhead * 85f)
                {
                    SpawnNextChunk(false);
                }
            }

            // 3D-деспавн чанков, оставшихся позади машины с учетом вектора движения
            for (int i = activeChunks.Count - 1; i >= 0; i--)
            {
                TrackChunk chunk = activeChunks[i];
                Vector3 toChunkEnd = chunk.EndPosition - targetCar.position;
                float behindDist = -Vector3.Dot(toChunkEnd, targetCar.forward);
                float directDist = Vector3.Distance(targetCar.position, chunk.EndPosition);

                if (behindDist > 50f && directDist > despawnDistanceBehind)
                {
                    activeChunks.RemoveAt(i);
                    Destroy(chunk.gameObject);
                }
            }
        }

        void UpdateBiomeEnvironment(float distance)
        {
            int biomeIdx = GetBiomeIndex(distance);
            if (biomeIdx != currentBiomeIndex)
            {
                currentBiomeIndex = biomeIdx;
                BiomeConfig currentBiome = biomes[biomeIdx];

                // Настройка атмосферного тумана и глобального освещения
                RenderSettings.fog = true;
                RenderSettings.fogColor = currentBiome.fogColor;
                RenderSettings.fogDensity = currentBiome.fogDensity;
                RenderSettings.ambientSkyColor = currentBiome.fogColor * 1.15f;

                // Уведомление в HUD
                PrototypeHud.Instance?.ShowBiomeNotification(
                    currentBiome.title,
                    currentBiome.subtitle,
                    currentBiome.markingColor);
            }
        }

        int GetBiomeIndex(float distance)
        {
            for (int i = 0; i < biomes.Length; i++)
            {
                if (distance >= biomes[i].startDistance && distance < biomes[i].endDistance)
                    return i;
            }
            return biomes.Length - 1;
        }

        BiomeConfig GetBiomeForDistance(float distance)
        {
            return biomes[GetBiomeIndex(distance)];
        }

        void SpawnNextChunk(bool isSafeStart)
        {
            BiomeConfig activeBiome = GetBiomeForDistance(totalDistanceGenerated);
            ChunkType nextType = SelectNextChunkType(isSafeStart);

            TrackChunk newChunk = null;

            switch (nextType)
            {
                case ChunkType.CurveLeft:
                    newChunk = CreateCurvedChunk(nextSpawnPosition, nextSpawnRotation, activeBiome, -22f);
                    currentHeadingYaw -= 22f;
                    break;

                case ChunkType.CurveRight:
                    newChunk = CreateCurvedChunk(nextSpawnPosition, nextSpawnRotation, activeBiome, 22f);
                    currentHeadingYaw += 22f;
                    break;

                case ChunkType.SCurve:
                    newChunk = CreateSCurveChunk(nextSpawnPosition, nextSpawnRotation, activeBiome);
                    break;

                case ChunkType.Bottleneck:
                    newChunk = CreateBottleneckChunk(nextSpawnPosition, nextSpawnRotation, activeBiome);
                    break;

                case ChunkType.Fork:
                    newChunk = CreateForkChunk(nextSpawnPosition, nextSpawnRotation, activeBiome);
                    break;

                case ChunkType.Straight:
                default:
                    newChunk = CreateStraightChunk(nextSpawnPosition, nextSpawnRotation, activeBiome);
                    break;
            }

            if (!isSafeStart)
            {
                float difficultyFactor = 1f + (totalDistanceGenerated / 600f);
                newChunk.Populate(enemyPrefabs, explosiveBarrelPrefab, supplyCratePrefab, difficultyFactor, activeBiome);
            }

            activeChunks.Add(newChunk);
            nextSpawnPosition = newChunk.EndPosition;
            nextSpawnRotation = newChunk.EndRotation;
            totalDistanceGenerated += newChunk.Length;

            CheckSectorCheckpointSpawn(newChunk);
            CheckBossSpawn(newChunk);
        }

        int lastCheckpointSector = 0;
        bool bossSpawned = false;

        void CheckSectorCheckpointSpawn(TrackChunk chunk)
        {
            int sector = Mathf.FloorToInt(totalDistanceGenerated / 1000f);
            if (sector > lastCheckpointSector && sector <= 4)
            {
                lastCheckpointSector = sector;
                GameObject cpObj = new GameObject($"Checkpoint_Sector_{sector}");
                cpObj.transform.position = chunk.transform.position + chunk.transform.forward * 10f;
                cpObj.transform.rotation = chunk.transform.rotation;
                cpObj.transform.SetParent(chunk.transform);
                var cp = cpObj.AddComponent<SectorCheckpoint>();
                string[] names = { "Шоссе: Пригород", "Радиационная Пустошь", "Затопленная Промзона", "Военная Цитадель" };
                cp.Configure(sector, names[Mathf.Clamp(sector - 1, 0, names.Length - 1)]);
            }
        }

        void CheckBossSpawn(TrackChunk chunk)
        {
            if (bossSpawned) return;

            // Босс спавнится в Секторе 4 на дистанции от 3800м
            if (totalDistanceGenerated >= 3800f)
            {
                bossSpawned = true;
                Vector3 bossSpawnPos = chunk.transform.position + chunk.transform.forward * 32f;
                bossSpawnPos.y = 0.5f;

                GameObject bossObj = new GameObject("Boss_Juggernaut");
                bossObj.transform.position = bossSpawnPos;
                bossObj.transform.rotation = chunk.transform.rotation;
                bossObj.AddComponent<BossJuggernaut>();
            }
        }

        ChunkType SelectNextChunkType(bool isSafeStart)
        {
            if (isSafeStart)
                return ChunkType.Straight;

            // Если трасса сильно отклонилась от курса, возвращаем её к центру
            if (currentHeadingYaw > 32f)
                return ChunkType.CurveLeft;
            if (currentHeadingYaw < -32f)
                return ChunkType.CurveRight;

            float roll = Random.value;
            if (roll < 0.30f)
                return ChunkType.Straight;
            if (roll < 0.46f)
                return ChunkType.CurveLeft;
            if (roll < 0.62f)
                return ChunkType.CurveRight;
            if (roll < 0.76f)
                return ChunkType.SCurve;
            if (roll < 0.88f)
                return ChunkType.Bottleneck;

            return ChunkType.Fork;
        }

        #region Procedural Chunk Generators

        TrackChunk CreateStraightChunk(Vector3 pos, Quaternion rot, BiomeConfig biome, float chunkLen = 100f, float roadWidth = 24f)
        {
            GameObject chunkObj = new GameObject($"StraightChunk_{totalDistanceGenerated:0}m");
            chunkObj.transform.position = pos;
            chunkObj.transform.rotation = rot;
            chunkObj.transform.SetParent(transform, true);

            List<Renderer> roads = new List<Renderer>();
            List<Renderer> guardrails = new List<Renderer>();

            // 1. Дорожное полотно
            GameObject roadMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roadMesh.name = "Road";
            roadMesh.transform.SetParent(chunkObj.transform, false);
            roadMesh.transform.localPosition = new Vector3(0f, -0.2f, chunkLen * 0.5f);
            roadMesh.transform.localScale = new Vector3(roadWidth, 0.4f, chunkLen);
            roadMesh.isStatic = true;
            ApplyMaterial(roadMesh, biome.roadColor, roads);

            // 2. Дорожная разметка (прерывистая линия по центру)
            CreateCenterDashes(chunkObj.transform, chunkLen, biome.markingColor);

            // 3. Боковые отбойники
            guardrails.Add(CreateGuardrail(chunkObj.transform, -(roadWidth * 0.5f) - 0.2f, chunkLen * 0.5f, chunkLen, biome.guardrailColor));
            guardrails.Add(CreateGuardrail(chunkObj.transform, (roadWidth * 0.5f) + 0.2f, chunkLen * 0.5f, chunkLen, biome.guardrailColor));

            // 4. Точка стыковки
            Transform conn = CreateConnectionPoint(chunkObj.transform, new Vector3(0f, 0f, chunkLen), Quaternion.identity);

            // 5. Точки спавна врагов и препятствий
            Transform[] enemySpawns = CreateSpawns(chunkObj.transform, "EnemySpawn", new Vector3[]
            {
                new Vector3(-6f, 0.5f, 20f), new Vector3(6f, 0.5f, 25f),
                new Vector3(-3f, 0.5f, 45f), new Vector3(3f, 0.5f, 50f),
                new Vector3(-7f, 0.5f, 70f), new Vector3(7f, 0.5f, 75f),
                new Vector3(0f, 0.5f, 85f),  new Vector3(-4f, 0.5f, 95f)
            });

            Transform[] obstacleSpawns = CreateSpawns(chunkObj.transform, "ObstacleSpawn", new Vector3[]
            {
                new Vector3(-5f, 0.5f, 30f), new Vector3(5f, 0.5f, 40f),
                new Vector3(0f, 0.5f, 60f),  new Vector3(-4f, 0.5f, 80f)
            });

            TrackChunk chunkComp = chunkObj.AddComponent<TrackChunk>();
            chunkComp.Configure(ChunkType.Straight, conn, enemySpawns, obstacleSpawns, chunkLen, roads.ToArray(), guardrails.ToArray());
            return chunkComp;
        }

        TrackChunk CreateCurvedChunk(Vector3 pos, Quaternion rot, BiomeConfig biome, float totalAngle, float length = 100f, float roadWidth = 24f)
        {
            GameObject chunkObj = new GameObject($"CurvedChunk_{(totalAngle > 0 ? "R" : "L")}_{totalDistanceGenerated:0}m");
            chunkObj.transform.position = pos;
            chunkObj.transform.rotation = rot;
            chunkObj.transform.SetParent(transform, true);

            List<Renderer> roads = new List<Renderer>();
            List<Renderer> guardrails = new List<Renderer>();
            List<Transform> enemySpawns = new List<Transform>();
            List<Transform> obstacleSpawns = new List<Transform>();

            const int segments = 5;
            float segLength = length / segments;
            float segAngle = totalAngle / segments;

            Vector3 currentLocalPos = Vector3.zero;
            float currentLocalYaw = 0f;

            for (int i = 0; i < segments; i++)
            {
                float midYaw = currentLocalYaw + (segAngle * 0.5f);
                Quaternion midRot = Quaternion.Euler(0f, midYaw, 0f);
                Vector3 stepDir = midRot * Vector3.forward;
                Vector3 midPos = currentLocalPos + stepDir * (segLength * 0.5f);

                // Сегмент дорожного полотна
                GameObject segObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segObj.name = $"RoadSeg_{i}";
                segObj.transform.SetParent(chunkObj.transform, false);
                segObj.transform.localPosition = midPos + Vector3.down * 0.2f;
                segObj.transform.localRotation = midRot;
                segObj.transform.localScale = new Vector3(roadWidth, 0.4f, segLength * 1.06f);
                segObj.isStatic = true;
                ApplyMaterial(segObj, biome.roadColor, roads);

                // Отбойники сегмента
                Vector3 sideRight = midRot * Vector3.right;
                Vector3 leftGuardPos = midPos - sideRight * (roadWidth * 0.5f + 0.2f);
                Vector3 rightGuardPos = midPos + sideRight * (roadWidth * 0.5f + 0.2f);

                guardrails.Add(CreatePositionedGuardrail(chunkObj.transform, leftGuardPos, midRot, segLength * 1.06f, biome.guardrailColor));
                guardrails.Add(CreatePositionedGuardrail(chunkObj.transform, rightGuardPos, midRot, segLength * 1.06f, biome.guardrailColor));

                // Спавны на дуге
                GameObject eSpawn1 = new GameObject($"EnemySpawn_{i}_A");
                eSpawn1.transform.SetParent(chunkObj.transform, false);
                eSpawn1.transform.localPosition = midPos - sideRight * 4f + Vector3.up * 0.5f;
                enemySpawns.Add(eSpawn1.transform);

                GameObject eSpawn2 = new GameObject($"EnemySpawn_{i}_B");
                eSpawn2.transform.SetParent(chunkObj.transform, false);
                eSpawn2.transform.localPosition = midPos + sideRight * 4f + Vector3.up * 0.5f;
                enemySpawns.Add(eSpawn2.transform);

                if (i % 2 == 1)
                {
                    GameObject obSpawn = new GameObject($"ObstacleSpawn_{i}");
                    obSpawn.transform.SetParent(chunkObj.transform, false);
                    obSpawn.transform.localPosition = midPos + (totalAngle > 0 ? sideRight * 3f : -sideRight * 3f) + Vector3.up * 0.5f;
                    obstacleSpawns.Add(obSpawn.transform);
                }

                currentLocalPos += stepDir * segLength;
                currentLocalYaw += segAngle;
            }

            Transform conn = CreateConnectionPoint(chunkObj.transform, currentLocalPos, Quaternion.Euler(0f, totalAngle, 0f));

            TrackChunk chunkComp = chunkObj.AddComponent<TrackChunk>();
            ChunkType cType = totalAngle > 0 ? ChunkType.CurveRight : ChunkType.CurveLeft;
            chunkComp.Configure(cType, conn, enemySpawns.ToArray(), obstacleSpawns.ToArray(), length, roads.ToArray(), guardrails.ToArray());
            return chunkComp;
        }

        TrackChunk CreateSCurveChunk(Vector3 pos, Quaternion rot, BiomeConfig biome, float length = 120f, float roadWidth = 24f)
        {
            GameObject chunkObj = new GameObject($"SCurveChunk_{totalDistanceGenerated:0}m");
            chunkObj.transform.position = pos;
            chunkObj.transform.rotation = rot;
            chunkObj.transform.SetParent(transform, true);

            List<Renderer> roads = new List<Renderer>();
            List<Renderer> guardrails = new List<Renderer>();
            List<Transform> enemySpawns = new List<Transform>();
            List<Transform> obstacleSpawns = new List<Transform>();

            // S-кривая состоит из 4 сегментов: Вправо(+22°), Влево(-44°), Вправо(+22°), Прямо(0°)
            float[] segmentAngles = { 22f, -44f, 22f, 0f };
            float segLen = length / segmentAngles.Length;

            Vector3 curPos = Vector3.zero;
            float curYaw = 0f;

            for (int i = 0; i < segmentAngles.Length; i++)
            {
                float angleDelta = segmentAngles[i];
                float midYaw = curYaw + angleDelta * 0.5f;
                Quaternion midRot = Quaternion.Euler(0f, midYaw, 0f);
                Vector3 stepDir = midRot * Vector3.forward;
                Vector3 midPos = curPos + stepDir * (segLen * 0.5f);

                // Дорожное полотно
                GameObject segObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segObj.name = $"SRoad_{i}";
                segObj.transform.SetParent(chunkObj.transform, false);
                segObj.transform.localPosition = midPos + Vector3.down * 0.2f;
                segObj.transform.localRotation = midRot;
                segObj.transform.localScale = new Vector3(roadWidth, 0.4f, segLen * 1.08f);
                segObj.isStatic = true;
                ApplyMaterial(segObj, biome.roadColor, roads);

                // Отбойники
                Vector3 side = midRot * Vector3.right;
                guardrails.Add(CreatePositionedGuardrail(chunkObj.transform, midPos - side * (roadWidth * 0.5f + 0.2f), midRot, segLen * 1.08f, biome.guardrailColor));
                guardrails.Add(CreatePositionedGuardrail(chunkObj.transform, midPos + side * (roadWidth * 0.5f + 0.2f), midRot, segLen * 1.08f, biome.guardrailColor));

                // Спавны врагов и бочек на апексах поворота
                GameObject eSpawn = new GameObject($"SEnemy_{i}");
                eSpawn.transform.SetParent(chunkObj.transform, false);
                eSpawn.transform.localPosition = midPos + Vector3.up * 0.5f;
                enemySpawns.Add(eSpawn.transform);

                GameObject oSpawn = new GameObject($"SObstacle_{i}");
                oSpawn.transform.SetParent(chunkObj.transform, false);
                oSpawn.transform.localPosition = midPos + (i % 2 == 0 ? side * 5f : -side * 5f) + Vector3.up * 0.5f;
                obstacleSpawns.Add(oSpawn.transform);

                curPos += stepDir * segLen;
                curYaw += angleDelta;
            }

            Transform conn = CreateConnectionPoint(chunkObj.transform, curPos, Quaternion.Euler(0f, curYaw, 0f));

            TrackChunk chunkComp = chunkObj.AddComponent<TrackChunk>();
            chunkComp.Configure(ChunkType.SCurve, conn, enemySpawns.ToArray(), obstacleSpawns.ToArray(), length, roads.ToArray(), guardrails.ToArray());
            return chunkComp;
        }

        TrackChunk CreateBottleneckChunk(Vector3 pos, Quaternion rot, BiomeConfig biome, float length = 110f)
        {
            GameObject chunkObj = new GameObject($"BottleneckChunk_{totalDistanceGenerated:0}m");
            chunkObj.transform.position = pos;
            chunkObj.transform.rotation = rot;
            chunkObj.transform.SetParent(transform, true);

            List<Renderer> roads = new List<Renderer>();
            List<Renderer> guardrails = new List<Renderer>();

            const float narrowWidth = 11f;

            // 1. Входное сужение (0 - 25м): переход с 24м на 11м
            GameObject enterRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            enterRoad.name = "EnterTaper";
            enterRoad.transform.SetParent(chunkObj.transform, false);
            enterRoad.transform.localPosition = new Vector3(0f, -0.2f, 12.5f);
            enterRoad.transform.localScale = new Vector3(18f, 0.4f, 25f);
            enterRoad.isStatic = true;
            ApplyMaterial(enterRoad, biome.roadColor, roads);

            // Направляющие бетонные барьеры сужения
            guardrails.Add(CreateAngledBarrier(chunkObj.transform, new Vector3(-8.5f, 0.6f, 12.5f), 15f, 26f, biome.guardrailColor));
            guardrails.Add(CreateAngledBarrier(chunkObj.transform, new Vector3(8.5f, 0.6f, 12.5f), -15f, 26f, biome.guardrailColor));

            // 2. Узкий коридор / Мост (25 - 85м, ширина 11м)
            GameObject bridgeRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bridgeRoad.name = "BridgeRoad";
            bridgeRoad.transform.SetParent(chunkObj.transform, false);
            bridgeRoad.transform.localPosition = new Vector3(0f, -0.2f, 55f);
            bridgeRoad.transform.localScale = new Vector3(narrowWidth, 0.4f, 60f);
            bridgeRoad.isStatic = true;
            ApplyMaterial(bridgeRoad, biome.roadColor, roads);

            // Высокие усиленные отбойники моста
            guardrails.Add(CreateHighGuardrail(chunkObj.transform, -(narrowWidth * 0.5f) - 0.25f, 55f, 60f, biome.guardrailColor));
            guardrails.Add(CreateHighGuardrail(chunkObj.transform, (narrowWidth * 0.5f) + 0.25f, 55f, 60f, biome.guardrailColor));

            // Декоративные арки / фермы эстакады
            for (float z = 35f; z <= 75f; z += 20f)
            {
                CreateBridgeArch(chunkObj.transform, z, narrowWidth, biome.guardrailColor);
            }

            // 3. Выходное расширение (85 - 110м): расширение с 11м обратно на 24м
            GameObject exitRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            exitRoad.name = "ExitTaper";
            exitRoad.transform.SetParent(chunkObj.transform, false);
            exitRoad.transform.localPosition = new Vector3(0f, -0.2f, 97.5f);
            exitRoad.transform.localScale = new Vector3(18f, 0.4f, 25f);
            exitRoad.isStatic = true;
            ApplyMaterial(exitRoad, biome.roadColor, roads);

            guardrails.Add(CreateAngledBarrier(chunkObj.transform, new Vector3(-8.5f, 0.6f, 97.5f), -15f, 26f, biome.guardrailColor));
            guardrails.Add(CreateAngledBarrier(chunkObj.transform, new Vector3(8.5f, 0.6f, 97.5f), 15f, 26f, biome.guardrailColor));

            Transform conn = CreateConnectionPoint(chunkObj.transform, new Vector3(0f, 0f, length), Quaternion.identity);

            // Спавны врагов и взрывных бочек прямо в узком коридоре моста
            Transform[] enemySpawns = CreateSpawns(chunkObj.transform, "BottleneckEnemy", new Vector3[]
            {
                new Vector3(-2.5f, 0.5f, 35f), new Vector3(2.5f, 0.5f, 45f),
                new Vector3(0f, 0.5f, 55f),    new Vector3(-2.5f, 0.5f, 68f),
                new Vector3(2.5f, 0.5f, 78f)
            });

            Transform[] obstacleSpawns = CreateSpawns(chunkObj.transform, "BottleneckBarrel", new Vector3[]
            {
                new Vector3(0f, 0.5f, 40f),    new Vector3(-3f, 0.5f, 60f),
                new Vector3(3f, 0.5f, 72f)
            });

            TrackChunk chunkComp = chunkObj.AddComponent<TrackChunk>();
            chunkComp.Configure(ChunkType.Bottleneck, conn, enemySpawns, obstacleSpawns, length, roads.ToArray(), guardrails.ToArray());
            return chunkComp;
        }

        TrackChunk CreateForkChunk(Vector3 pos, Quaternion rot, BiomeConfig biome, float length = 120f)
        {
            GameObject chunkObj = new GameObject($"ForkChunk_{totalDistanceGenerated:0}m");
            chunkObj.transform.position = pos;
            chunkObj.transform.rotation = rot;
            chunkObj.transform.SetParent(transform, true);

            List<Renderer> roads = new List<Renderer>();
            List<Renderer> guardrails = new List<Renderer>();

            const float wideRoad = 32f;

            // 1. Широкое дорожное полотно под 2 ветки
            GameObject forkRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            forkRoad.name = "ForkRoad";
            forkRoad.transform.SetParent(chunkObj.transform, false);
            forkRoad.transform.localPosition = new Vector3(0f, -0.2f, length * 0.5f);
            forkRoad.transform.localScale = new Vector3(wideRoad, 0.4f, length);
            forkRoad.isStatic = true;
            ApplyMaterial(forkRoad, biome.roadColor, roads);

            // 2. Внешние ограждения трассы
            guardrails.Add(CreateGuardrail(chunkObj.transform, -(wideRoad * 0.5f) - 0.2f, length * 0.5f, length, biome.guardrailColor));
            guardrails.Add(CreateGuardrail(chunkObj.transform, (wideRoad * 0.5f) + 0.2f, length * 0.5f, length, biome.guardrailColor));

            // 3. Центральный разделительный островок (Crash Barrier / Divider)
            // Нос разделителя (амортизатор удара с предупреждающими шевронами)
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nose.name = "DividerNose";
            nose.transform.SetParent(chunkObj.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.8f, 18f);
            nose.transform.localScale = new Vector3(2.5f, 0.8f, 2.5f);
            guardrails.Add(ApplyMaterial(nose, new Color(1f, 0.85f, 0.1f)));

            // Центральная бетонная стена
            GameObject dividerWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dividerWall.name = "DividerWall";
            dividerWall.transform.SetParent(chunkObj.transform, false);
            dividerWall.transform.localPosition = new Vector3(0f, 0.8f, 60f);
            dividerWall.transform.localScale = new Vector3(2.2f, 1.6f, 80f);
            dividerWall.isStatic = true;
            guardrails.Add(ApplyMaterial(dividerWall, biome.guardrailColor));

            // Хвост разделителя
            GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tail.name = "DividerTail";
            tail.transform.SetParent(chunkObj.transform, false);
            tail.transform.localPosition = new Vector3(0f, 0.8f, 102f);
            tail.transform.localScale = new Vector3(2.5f, 0.8f, 2.5f);
            guardrails.Add(ApplyMaterial(tail, new Color(1f, 0.85f, 0.1f)));

            Transform conn = CreateConnectionPoint(chunkObj.transform, new Vector3(0f, 0f, length), Quaternion.identity);

            // Левая ветка (Safe lane): ящики с ресурсами, мало врагов
            Transform[] leftSpawns = CreateSpawns(chunkObj.transform, "LeftSafeSpawn", new Vector3[]
            {
                new Vector3(-8.5f, 0.5f, 30f),
                new Vector3(-8.5f, 0.5f, 50f),
                new Vector3(-8.5f, 0.5f, 70f),
                new Vector3(-8.5f, 0.5f, 90f)
            });

            // Правая ветка (Danger/Reward lane): взрывные бочки и плотные орды
            Transform[] rightSpawns = CreateSpawns(chunkObj.transform, "RightDangerSpawn", new Vector3[]
            {
                new Vector3(8.5f, 0.5f, 30f),
                new Vector3(8.5f, 0.5f, 45f),
                new Vector3(8.5f, 0.5f, 60f),
                new Vector3(8.5f, 0.5f, 75f),
                new Vector3(8.5f, 0.5f, 90f)
            });

            TrackChunk chunkComp = chunkObj.AddComponent<TrackChunk>();
            chunkComp.Configure(ChunkType.Fork, conn, null, null, length, roads.ToArray(), guardrails.ToArray(), leftSpawns, rightSpawns);
            return chunkComp;
        }

        #endregion

        #region Helper Geometry Builders

        Renderer CreateGuardrail(Transform parent, float x, float z, float length, Color col)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Guardrail";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = new Vector3(x, 0.6f, z);
            wall.transform.localScale = new Vector3(0.4f, 1.2f, length);
            wall.isStatic = true;
            return ApplyMaterial(wall, col);
        }

        Renderer CreatePositionedGuardrail(Transform parent, Vector3 localPos, Quaternion localRot, float length, Color col)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Guardrail";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPos + Vector3.up * 0.6f;
            wall.transform.localRotation = localRot;
            wall.transform.localScale = new Vector3(0.4f, 1.2f, length);
            wall.isStatic = true;
            return ApplyMaterial(wall, col);
        }

        Renderer CreateHighGuardrail(Transform parent, float x, float z, float length, Color col)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "BridgeHighGuardrail";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = new Vector3(x, 0.9f, z);
            wall.transform.localScale = new Vector3(0.5f, 1.8f, length);
            wall.isStatic = true;
            return ApplyMaterial(wall, col);
        }

        Renderer CreateAngledBarrier(Transform parent, Vector3 localPos, float angleY, float length, Color col)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "AngledBarrier";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPos;
            wall.transform.localRotation = Quaternion.Euler(0f, angleY, 0f);
            wall.transform.localScale = new Vector3(0.45f, 1.2f, length);
            wall.isStatic = true;
            return ApplyMaterial(wall, col);
        }

        void CreateBridgeArch(Transform parent, float z, float width, Color col)
        {
            GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arch.name = "BridgeArch";
            arch.transform.SetParent(parent, false);
            arch.transform.localPosition = new Vector3(0f, 4.5f, z);
            arch.transform.localScale = new Vector3(width + 1.2f, 0.5f, 0.6f);
            ApplyMaterial(arch, col);

            Collider c = arch.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        void CreateCenterDashes(Transform parent, float length, Color col)
        {
            const float dashLen = 7f;
            const float gapLen = 13f;
            const float totalStep = dashLen + gapLen;

            int count = Mathf.FloorToInt(length / totalStep);
            for (int i = 0; i < count; i++)
            {
                float z = (i * totalStep) + (dashLen * 0.5f) + 3f;
                GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dash.name = $"Dash_{i}";
                dash.transform.SetParent(parent, false);
                dash.transform.localPosition = new Vector3(0f, 0.02f, z);
                dash.transform.localScale = new Vector3(0.35f, 0.05f, dashLen);

                Collider colComp = dash.GetComponent<Collider>();
                if (colComp != null) Destroy(colComp);

                ApplyMaterial(dash, col);
            }
        }

        Transform CreateConnectionPoint(Transform parent, Vector3 localPos, Quaternion localRot)
        {
            GameObject conn = new GameObject("ConnectionPoint");
            conn.transform.SetParent(parent, false);
            conn.transform.localPosition = localPos;
            conn.transform.localRotation = localRot;
            return conn.transform;
        }

        Transform[] CreateSpawns(Transform parent, string prefix, Vector3[] positions)
        {
            Transform[] spawns = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject sp = new GameObject($"{prefix}_{i + 1}");
                sp.transform.SetParent(parent, false);
                sp.transform.localPosition = positions[i];
                spawns[i] = sp.transform;
            }
            return spawns;
        }

        Renderer ApplyMaterial(GameObject obj, Color col, List<Renderer> tracker = null)
        {
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(r.sharedMaterial ?? new Material(Shader.Find("Standard")));
                m.color = col;
                m.enableInstancing = true;
                r.sharedMaterial = m;
                if (tracker != null) tracker.Add(r);
            }
            return r;
        }

        #endregion

        #region Obstacle Procedural Prefabs

        void EnsureObstaclePrefabs()
        {
            if (explosiveBarrelPrefab == null)
            {
                explosiveBarrelPrefab = BuildProceduralBarrelPrefab();
            }

            if (supplyCratePrefab == null)
            {
                supplyCratePrefab = BuildProceduralCratePrefab();
            }
        }

        GameObject BuildProceduralBarrelPrefab()
        {
            GameObject barrel = new GameObject("ExplosiveBarrel_Prefab");
            barrel.transform.SetParent(transform, false);

            // Визуальный цилиндр
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "BarrelBody";
            body.transform.SetParent(barrel.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            body.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f);

            Renderer r = body.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.85f, 0.18f, 0.15f); // Ярко-красный цвет опасности
                r.sharedMaterial = m;
            }

            // Желтые предупреждающие кольца
            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "HazardBand";
            band.transform.SetParent(barrel.transform, false);
            band.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            band.transform.localScale = new Vector3(0.92f, 0.15f, 0.92f);
            Renderer bandR = band.GetComponent<Renderer>();
            if (bandR != null)
            {
                Material bm = new Material(Shader.Find("Standard"));
                bm.color = new Color(1f, 0.85f, 0.1f);
                bandR.sharedMaterial = bm;
            }
            Collider bandCol = band.GetComponent<Collider>();
            if (bandCol != null) Destroy(bandCol);

            // Основной коллайдер и логика
            CapsuleCollider capsule = barrel.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.6f, 0f);
            capsule.radius = 0.45f;
            capsule.height = 1.2f;

            Collider bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            barrel.AddComponent<ExplosiveBarrel>();

            barrel.SetActive(false);
            return barrel;
        }

        GameObject BuildProceduralCratePrefab()
        {
            GameObject crate = new GameObject("SupplyCrate_Prefab");
            crate.transform.SetParent(transform, false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CrateBody";
            body.transform.SetParent(crate.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            body.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);

            Renderer r = body.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.62f, 0.44f, 0.26f); // Древесно-коричневый
                r.sharedMaterial = m;
            }

            BoxCollider box = crate.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1.2f, 1.2f, 1.2f);

            Collider bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            crate.AddComponent<SupplyCrate>();

            crate.SetActive(false);
            return crate;
        }

        #endregion
    }
}
