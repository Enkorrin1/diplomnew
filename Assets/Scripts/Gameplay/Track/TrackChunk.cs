using UnityEngine;

namespace RogueDrive.Gameplay
{
    public enum ChunkType
    {
        Straight,
        CurveLeft,
        CurveRight,
        Bottleneck,
        Fork
    }

    /// <summary>
    /// Модульный сегмент трассы. Хранит габариты, точку стыковки со следующим чанком
    /// и точки размещения препятствий, врагов и ящиков с ресурсами.
    /// </summary>
    public sealed class TrackChunk : MonoBehaviour
    {
        [Header("Chunk Properties")]
        [SerializeField] private ChunkType type = ChunkType.Straight;
        [SerializeField, Min(10f)] private float length = 100f;
        [SerializeField] private Transform connectionPoint;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Transform[] obstacleSpawnPoints;

        public ChunkType Type => type;
        public float Length => length;

        public Vector3 EndPosition => connectionPoint != null
            ? connectionPoint.position
            : transform.position + transform.forward * length;

        public Quaternion EndRotation => connectionPoint != null
            ? connectionPoint.rotation
            : transform.rotation;

        public Transform[] EnemySpawnPoints => enemySpawnPoints;
        public Transform[] ObstacleSpawnPoints => obstacleSpawnPoints;

        public void Configure(Transform connection, Transform[] enemySpawns, Transform[] obstacleSpawns, float chunkLength = 100f)
        {
            connectionPoint = connection;
            enemySpawnPoints = enemySpawns;
            obstacleSpawnPoints = obstacleSpawns;
            length = chunkLength;
        }

        public void Populate(GameObject[] enemyPrefabs, GameObject[] obstaclePrefabs, float difficultyMultiplier)
        {
            // Спавн препятствий
            if (obstaclePrefabs != null && obstaclePrefabs.Length > 0 && obstacleSpawnPoints != null)
            {
                for (int i = 0; i < obstacleSpawnPoints.Length; i++)
                {
                    if (Random.value > 0.45f) // частичная случайность размещения
                    {
                        GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
                        Instantiate(prefab, obstacleSpawnPoints[i].position, obstacleSpawnPoints[i].rotation, transform);
                    }
                }
            }

            // Спавн врагов
            if (enemyPrefabs != null && enemyPrefabs.Length > 0 && enemySpawnPoints != null)
            {
                int maxEnemies = Mathf.RoundToInt(enemySpawnPoints.Length * Mathf.Clamp(difficultyMultiplier, 0.5f, 2.0f));
                for (int i = 0; i < Mathf.Min(maxEnemies, enemySpawnPoints.Length); i++)
                {
                    if (Random.value > 0.3f)
                    {
                        GameObject enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                        Vector3 spawnPos = enemySpawnPoints[i].position + Random.insideUnitSphere * 1.5f;
                        spawnPos.y = 0.5f;

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
}
