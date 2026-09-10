using UnityEngine;
using System.Collections.Generic;

namespace RogueDrive.Gameplay
{
    public enum ChunkType
    {
        Straight = 0,
        CurveLeft = 1,
        CurveRight = 2,
        SCurve = 3,
        Bottleneck = 4,
        Fork = 5
    }

    /// <summary>
    /// Модульный сегмент трассы.
    /// Хранит габариты, конечную точку стыковки со следующим чанком,
    /// точки размещения врагов, взрывных бочек и ящиков с припасами.
    /// Поддерживает процедурное окрашивание под текущий биом.
    /// </summary>
    public sealed class TrackChunk : MonoBehaviour
    {
        static readonly HashSet<TrackChunk> activeRoads = new HashSet<TrackChunk>();
        readonly List<BoxCollider> roadBoxes = new List<BoxCollider>();
        readonly List<Mesh> collisionMeshes = new List<Mesh>();
        bool surfaceBuilt;

        void OnEnable() => activeRoads.Add(this);
        void OnDisable() => activeRoads.Remove(this);
        void Start() => BuildDrivingSurface();
        void OnDestroy()
        {
            activeRoads.Remove(this);
            foreach (Mesh mesh in collisionMeshes) if (mesh != null) Destroy(mesh);
        }

        // Only the road's top face participates in driving contacts. Cuboid end
        // faces at overlapping sections otherwise catch the chassis at seams.
        void BuildDrivingSurface()
        {
            if (surfaceBuilt || roadRenderers == null || roadRenderers.Length == 0) return;
            surfaceBuilt = true;
            foreach (Renderer road in roadRenderers)
            {
                if (road == null) continue;
                BoxCollider box = road.GetComponent<BoxCollider>();
                if (box == null || box.isTrigger) continue;
                roadBoxes.Add(box);
                Vector3 lo = box.center - box.size * 0.5f;
                Vector3 hi = box.center + box.size * 0.5f;
                Mesh mesh = new Mesh { name = "Road top collision" };
                mesh.vertices = new[] { new Vector3(lo.x, hi.y, lo.z), new Vector3(lo.x, hi.y, hi.z),
                    new Vector3(hi.x, hi.y, hi.z), new Vector3(hi.x, hi.y, lo.z) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds();
                MeshCollider top = road.gameObject.AddComponent<MeshCollider>();
                top.sharedMesh = mesh;
                top.sharedMaterial = box.sharedMaterial;
                box.enabled = false;
                collisionMeshes.Add(mesh);
            }
        }

        public static bool TryProjectToRoad(Vector3 position, out Vector3 projected, float margin = 0.7f)
        {
            projected = position;
            float best = float.PositiveInfinity;
            foreach (TrackChunk chunk in activeRoads)
            {
                if (chunk == null) continue;
                chunk.BuildDrivingSurface();
                foreach (BoxCollider box in chunk.roadBoxes)
                {
                    if (box == null) continue;
                    Transform t = box.transform;
                    Vector3 p = t.InverseTransformPoint(position) - box.center;
                    Vector3 half = box.size * 0.5f;
                    float inset = Mathf.Min(half.x * 0.4f, margin / Mathf.Max(0.001f, Mathf.Abs(t.lossyScale.x)));
                    p.x = Mathf.Clamp(p.x, -half.x + inset, half.x - inset);
                    p.z = Mathf.Clamp(p.z, -half.z, half.z);
                    p.y = half.y;
                    Vector3 candidate = t.TransformPoint(p + box.center);
                    float distance = (candidate - position).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    projected = candidate;
                }
            }
            return !float.IsPositiveInfinity(best);
        }
        [Header("Chunk Properties")]
        [SerializeField] private ChunkType type = ChunkType.Straight;
        [SerializeField, Min(10f)] private float length = 100f;
        [SerializeField] private Transform connectionPoint;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Transform[] obstacleSpawnPoints;
        [SerializeField] private Transform[] leftLaneSpawnPoints;
        [SerializeField] private Transform[] rightLaneSpawnPoints;

        [Header("Visual Renderers")]
        [SerializeField] private Renderer[] roadRenderers;
        [SerializeField] private Renderer[] guardrailRenderers;

        public ChunkType Type => type;
        public float Length => length;
        public Renderer[] RoadRenderers => roadRenderers;

        public Vector3 EndPosition => connectionPoint != null
            ? connectionPoint.position
            : transform.position + transform.forward * length;

        public Quaternion EndRotation => connectionPoint != null
            ? connectionPoint.rotation
            : transform.rotation;

        public Transform[] EnemySpawnPoints => enemySpawnPoints;
        public Transform[] ObstacleSpawnPoints => obstacleSpawnPoints;

        public void Configure(
            ChunkType chunkType,
            Transform connection,
            Transform[] enemySpawns,
            Transform[] obstacleSpawns,
            float chunkLength = 100f,
            Renderer[] roads = null,
            Renderer[] guardrails = null,
            Transform[] leftSpawns = null,
            Transform[] rightSpawns = null)
        {
            type = chunkType;
            connectionPoint = connection;
            enemySpawnPoints = enemySpawns ?? new Transform[0];
            obstacleSpawnPoints = obstacleSpawns ?? new Transform[0];
            length = chunkLength;
            roadRenderers = roads ?? new Renderer[0];
            guardrailRenderers = guardrails ?? new Renderer[0];
            leftLaneSpawnPoints = leftSpawns ?? new Transform[0];
            rightLaneSpawnPoints = rightSpawns ?? new Transform[0];
            if (Application.isPlaying) BuildDrivingSurface();
        }

        /// <summary>
        /// Применяет визуальную цветовую схему биома к дорожному полотну и отбойникам.
        /// </summary>
        public void ApplyBiome(BiomeConfig biome)
        {
            if (biome == null) return;

            if (roadRenderers != null)
            {
                for (int i = 0; i < roadRenderers.Length; i++)
                {
                    Renderer r = roadRenderers[i];
                    if (r != null)
                    {
                        Material mat = new Material(r.sharedMaterial);
                        mat.color = biome.roadColor;
                        r.sharedMaterial = mat;
                    }
                }
            }

            if (guardrailRenderers != null)
            {
                for (int i = 0; i < guardrailRenderers.Length; i++)
                {
                    Renderer r = guardrailRenderers[i];
                    if (r != null)
                    {
                        Material mat = new Material(r.sharedMaterial);
                        mat.color = biome.guardrailColor;
                        r.sharedMaterial = mat;
                    }
                }
            }
        }

        /// <summary>
        /// Заселяет чанк врагами, взрывными бочками и ящиками с ресурсами
        /// с учетом типа чанка (развилка, бутылочное горлышко, поворот) и множителя сложности биома.
        /// </summary>
        public void Populate(
            GameObject[] enemyPrefabs,
            GameObject barrelPrefab,
            GameObject cratePrefab,
            float difficultyMultiplier,
            BiomeConfig biome,
            bool spawnObstacles = true,
            bool allowElites = true)
        {
            float barrelChance = biome != null ? biome.barrelSpawnChance : 0.5f;
            float crateChance = biome != null ? biome.crateSpawnChance : 0.4f;

            // 1. Обработка развилок (Fork) со специфическим распределением по веткам
            if (type == ChunkType.Fork && spawnObstacles)
            {
                PopulateForkLanes(enemyPrefabs, barrelPrefab, cratePrefab, difficultyMultiplier);
                return;
            }

            // 2. Стандартный спавн препятствий (бочки и ящики)
            if (spawnObstacles && obstacleSpawnPoints != null && obstacleSpawnPoints.Length > 0)
            {
                for (int i = 0; i < obstacleSpawnPoints.Length; i++)
                {
                    Transform pt = obstacleSpawnPoints[i];
                    if (pt == null) continue;

                    float roll = Random.value;
                    if (roll < barrelChance && barrelPrefab != null)
                    {
                        Instantiate(barrelPrefab, pt.position, pt.rotation, transform).SetActive(true);
                    }
                    else if (roll < (barrelChance + crateChance) && cratePrefab != null)
                    {
                        Instantiate(cratePrefab, pt.position, pt.rotation, transform).SetActive(true);
                    }
                }
            }

            // 3. Спавн врагов
            if (enemyPrefabs != null && enemyPrefabs.Length > 0 && enemySpawnPoints != null && enemySpawnPoints.Length > 0)
            {
                float biomeMult = biome != null ? biome.enemyDensityMultiplier : 1.0f;
                int maxEnemies = Mathf.RoundToInt(enemySpawnPoints.Length * Mathf.Clamp(difficultyMultiplier * biomeMult, 0.5f, 2.5f));
                maxEnemies = Mathf.Min(maxEnemies, enemySpawnPoints.Length);

                for (int i = 0; i < maxEnemies; i++)
                {
                    if (Random.value > 0.35f)
                    {
                        Vector3 spawnPos = enemySpawnPoints[i].position + Random.insideUnitSphere * 1.5f;
                        if (TryProjectToRoad(spawnPos, out Vector3 roadSpawn)) spawnPos = roadSpawn + Vector3.up * 0.5f;

                        // Шанс появления элитного противника растет с дистанцией/сложностью
                        float eliteChance = Mathf.Clamp01((difficultyMultiplier - 1f) * 0.20f);
                        if (allowElites && Random.value < eliteChance)
                        {
                            SpawnEliteEnemy(spawnPos);
                        }
                        else
                        {
                            GameObject enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                            if (GameplayPool.Instance != null)
                            {
                                GameplayPool.Instance.Spawn(enemyPrefab, spawnPos, Quaternion.identity);
                            }
                            else
                            {
                                Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                            }
                        }
                    }
                }
            }
        }

        void SpawnEliteEnemy(Vector3 pos)
        {
            if (TryProjectToRoad(pos, out Vector3 roadSpawn)) pos = roadSpawn + Vector3.up * 0.5f;
            int roll = Random.Range(0, 3);
            GameObject obj;

            switch (roll)
            {
                case 0:
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.name = "Elite_Kamikaze";
                    obj.transform.position = pos;
                    obj.AddComponent<EliteKamikaze>();
                    break;
                case 1:
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.name = "Elite_JuggernautMinion";
                    obj.transform.position = pos;
                    obj.AddComponent<EliteJuggernautMinion>();
                    break;
                case 2:
                default:
                    obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    obj.name = "Elite_PackLeader";
                    obj.transform.position = pos;
                    obj.AddComponent<ElitePackLeader>();
                    break;
            }
        }

        void PopulateForkLanes(
            GameObject[] enemyPrefabs,
            GameObject barrelPrefab,
            GameObject cratePrefab,
            float difficultyMultiplier)
        {
            // Левая ветка (Safe lane): ящики с ресурсами, мало врагов
            if (leftLaneSpawnPoints != null)
            {
                for (int i = 0; i < leftLaneSpawnPoints.Length; i++)
                {
                    Transform pt = leftLaneSpawnPoints[i];
                    if (pt == null) continue;

                    if (Random.value < 0.65f && cratePrefab != null)
                    {
                        Instantiate(cratePrefab, pt.position, pt.rotation, transform).SetActive(true);
                    }
                    else if (Random.value < 0.25f && enemyPrefabs != null && enemyPrefabs.Length > 0)
                    {
                        SpawnEnemyAt(enemyPrefabs[Random.Range(0, enemyPrefabs.Length)], pt.position);
                    }
                }
            }

            // Правая ветка (Danger/Shortcut lane): взрывные бочки, плотная засада врагов
            if (rightLaneSpawnPoints != null)
            {
                for (int i = 0; i < rightLaneSpawnPoints.Length; i++)
                {
                    Transform pt = rightLaneSpawnPoints[i];
                    if (pt == null) continue;

                    if (Random.value < 0.70f && barrelPrefab != null)
                    {
                        Instantiate(barrelPrefab, pt.position, pt.rotation, transform).SetActive(true);
                    }

                    if (enemyPrefabs != null && enemyPrefabs.Length > 0 && Random.value < 0.75f)
                    {
                        SpawnEnemyAt(enemyPrefabs[Random.Range(0, enemyPrefabs.Length)], pt.position + Random.insideUnitSphere * 1.0f);
                    }
                }
            }
        }

        void SpawnEnemyAt(GameObject prefab, Vector3 pos)
        {
            if (TryProjectToRoad(pos, out Vector3 roadSpawn)) pos = roadSpawn + Vector3.up * 0.5f;
            if (GameplayPool.Instance != null)
            {
                GameplayPool.Instance.Spawn(prefab, pos, Quaternion.identity);
            }
            else
            {
                Instantiate(prefab, pos, Quaternion.identity);
            }
        }
    }
}
