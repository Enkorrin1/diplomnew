using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Процедурный генератор трассы. Стыкует чанки перед движущимся автомобилем,
    /// удаляет пройденные участки позади, расставляет препятствия и спавнит волны врагов.
    /// </summary>
    public sealed class ProceduralTrackGenerator : MonoBehaviour
    {
        [Header("Target & Chunks")]
        [SerializeField] private Transform targetCar;
        [SerializeField] private TrackChunk[] chunkPrefabs;
        [SerializeField, Min(2)] private int activeChunksAhead = 4;
        [SerializeField, Min(50f)] private float despawnDistanceBehind = 140f;

        [Header("Prefabs for Spawning")]
        [SerializeField] private GameObject[] enemyPrefabs;
        [SerializeField] private GameObject[] obstaclePrefabs;

        readonly List<TrackChunk> activeChunks = new List<TrackChunk>();
        Vector3 nextSpawnPosition = Vector3.zero;
        Quaternion nextSpawnRotation = Quaternion.identity;

        float totalDistanceGenerated;

        private void Start()
        {
            if (targetCar == null)
            {
                ArcadeCarController car = FindFirstObjectByType<ArcadeCarController>();
                if (car != null)
                    targetCar = car.transform;
            }

            // Начальная генерация нескольких чанков вперед от конца стартовой дороги
            if (nextSpawnPosition == Vector3.zero)
            {
                nextSpawnPosition = new Vector3(0f, 0f, 150f);
            }

            for (int i = 0; i < activeChunksAhead; i++)
            {
                SpawnNextChunk(false);
            }
        }

        private void Update()
        {
            if (targetCar == null)
                return;

            // Проверка необходимости спавна нового чанка впереди
            if (activeChunks.Count > 0)
            {
                TrackChunk furthestChunk = activeChunks[activeChunks.Count - 1];
                float distToFurthest = Vector3.Distance(targetCar.position, furthestChunk.transform.position);

                if (distToFurthest < activeChunksAhead * 80f)
                {
                    SpawnNextChunk(false);
                }
            }

            // Удаление чанков, оставшихся далеко позади
            for (int i = activeChunks.Count - 1; i >= 0; i--)
            {
                TrackChunk chunk = activeChunks[i];
                float signedDist = targetCar.position.z - chunk.EndPosition.z;

                if (signedDist > despawnDistanceBehind)
                {
                    activeChunks.RemoveAt(i);
                    Destroy(chunk.gameObject);
                }
            }
        }

        void SpawnNextChunk(bool isSafeStart)
        {
            TrackChunk newChunk = null;

            if (chunkPrefabs != null && chunkPrefabs.Length > 0)
            {
                TrackChunk prefab = chunkPrefabs[Random.Range(0, chunkPrefabs.Length)];
                newChunk = Instantiate(prefab, nextSpawnPosition, nextSpawnRotation, transform);
            }
            else
            {
                // Процедурное создание базового чанка при отсутствии готовых ассетов
                newChunk = CreateProceduralChunk(nextSpawnPosition, nextSpawnRotation);
            }

            if (!isSafeStart)
            {
                float difficultyFactor = 1f + (totalDistanceGenerated / 600f);
                newChunk.Populate(enemyPrefabs, obstaclePrefabs, difficultyFactor);
            }

            activeChunks.Add(newChunk);
            nextSpawnPosition = newChunk.EndPosition;
            nextSpawnRotation = newChunk.EndRotation;
            totalDistanceGenerated += newChunk.Length;
        }

        TrackChunk CreateProceduralChunk(Vector3 pos, Quaternion rot)
        {
            const float chunkLen = 100f;
            const float roadWidth = 24f;

            GameObject chunkObj = new GameObject($"ProceduralChunk_{totalDistanceGenerated:0}m");
            chunkObj.transform.position = pos;
            chunkObj.transform.rotation = rot;
            chunkObj.transform.SetParent(transform, true);

            // Дорожное полотно
            GameObject roadMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roadMesh.name = "Road";
            roadMesh.transform.SetParent(chunkObj.transform, false);
            roadMesh.transform.localPosition = new Vector3(0f, -0.2f, chunkLen * 0.5f);
            roadMesh.transform.localScale = new Vector3(roadWidth, 0.4f, chunkLen);
            roadMesh.isStatic = true;

            Renderer r = roadMesh.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(r.sharedMaterial);
                m.color = new Color(0.12f, 0.13f, 0.17f);
                r.sharedMaterial = m;
            }

            // Отбойники
            CreateSideGuardrail(chunkObj.transform, -(roadWidth * 0.5f) - 0.2f, chunkLen);
            CreateSideGuardrail(chunkObj.transform, (roadWidth * 0.5f) + 0.2f, chunkLen);

            // Точка стыковки следующего чанка
            GameObject conn = new GameObject("ConnectionPoint");
            conn.transform.SetParent(chunkObj.transform, false);
            conn.transform.localPosition = new Vector3(0f, 0f, chunkLen);

            // Точки спавна врагов
            Vector3[] enemyOffsets =
            {
                new Vector3(-6f, 0.5f, 20f),
                new Vector3(6f, 0.5f, 25f),
                new Vector3(-3f, 0.5f, 45f),
                new Vector3(3f, 0.5f, 50f),
                new Vector3(-7f, 0.5f, 70f),
                new Vector3(7f, 0.5f, 75f),
                new Vector3(0f, 0.5f, 85f),
                new Vector3(-4f, 0.5f, 95f)
            };
            Transform[] enemySpawns = new Transform[enemyOffsets.Length];
            for (int i = 0; i < enemyOffsets.Length; i++)
            {
                GameObject sp = new GameObject($"EnemySpawn_{i + 1}");
                sp.transform.SetParent(chunkObj.transform, false);
                sp.transform.localPosition = enemyOffsets[i];
                enemySpawns[i] = sp.transform;
            }

            // Точки спавна препятствий
            Vector3[] obstacleOffsets =
            {
                new Vector3(-5f, 0.5f, 32f),
                new Vector3(5f, 0.5f, 42f),
                new Vector3(0f, 0.5f, 62f),
                new Vector3(-4f, 0.5f, 82f)
            };
            Transform[] obstacleSpawns = new Transform[obstacleOffsets.Length];
            for (int i = 0; i < obstacleOffsets.Length; i++)
            {
                GameObject ob = new GameObject($"ObstacleSpawn_{i + 1}");
                ob.transform.SetParent(chunkObj.transform, false);
                ob.transform.localPosition = obstacleOffsets[i];
                obstacleSpawns[i] = ob.transform;
            }

            TrackChunk chunkComp = chunkObj.AddComponent<TrackChunk>();
            chunkComp.Configure(conn.transform, enemySpawns, obstacleSpawns, chunkLen);
            return chunkComp;
        }

        void CreateSideGuardrail(Transform parent, float x, float length)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Guardrail";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = new Vector3(x, 0.6f, length * 0.5f);
            wall.transform.localScale = new Vector3(0.35f, 1.2f, length);
            wall.isStatic = true;
        }
    }
}
