using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Gameplay.Combat
{
    /// <summary>
    /// Спавнер машин рейдеров на трассе.
    /// Периодически вызывает перехватчиков мародёров, которые выезжают на шоссе
    /// и устраивают дуэли на выживание.
    /// </summary>
    public sealed class RaiderSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private float minSpawnInterval = 18f;
        [SerializeField] private float maxSpawnInterval = 32f;
        [SerializeField] private int maxConcurrentRaiders = 2;
        [SerializeField] private float minPlayerSpeedToSpawn = 12f;

        [Header("Vehicle Prefab Paths")]
        [SerializeField] private string[] raiderPrefabPaths = new[]
        {
            "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Military Vehicle_3.prefab",
            "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Monster Truck_12.prefab",
            "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Pick Up_11.prefab"
        };

        private ArcadeCarController player;
        private float spawnTimer;
        private readonly List<RaiderVehicleAI> activeRaiders = new List<RaiderVehicleAI>();

        private void Start()
        {
            player = FindFirstObjectByType<ArcadeCarController>();
            ResetTimer();
        }

        private void ResetTimer()
        {
            spawnTimer = Random.Range(minSpawnInterval, maxSpawnInterval);
        }

        private void Update()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<ArcadeCarController>();
                if (player == null) return;
            }

            if (player.Run != null && player.Run.IsGameOver) return;

            // Очистка мертвых рейдеров из списка
            for (int i = activeRaiders.Count - 1; i >= 0; i--)
            {
                if (activeRaiders[i] == null || activeRaiders[i].IsDead)
                {
                    activeRaiders.RemoveAt(i);
                }
            }

            // Спавним только при достаточной скорости заезда
            if (player.SpeedMps < minPlayerSpeedToSpawn) return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && activeRaiders.Count < maxConcurrentRaiders)
            {
                SpawnRaider();
                ResetTimer();
            }
        }

        private void SpawnRaider()
        {
            if (player == null) return;

            // Выбираем случайную модель
            string path = raiderPrefabPaths[Random.Range(0, raiderPrefabPaths.Length)];
            GameObject prefab = null;

#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif

            GameObject raiderObj = null;
            if (prefab != null)
            {
                raiderObj = Instantiate(prefab);
            }
            else
            {
                // Фоллбэк: процедурный броневик
                raiderObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                raiderObj.transform.localScale = new Vector3(2.2f, 1.4f, 4.2f);
                var r = raiderObj.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.2f, 0.22f, 0.25f);
            }

            raiderObj.name = "Raider_Interceptor";

            // Спавним сзади игрока на дистанции 35м в соседней полосе
            float laneOffset = (Random.value > 0.5f ? 1f : -1f) * Random.Range(3f, 7f);
            Vector3 spawnPos = player.transform.position - player.transform.forward * 35f + player.transform.right * laneOffset + Vector3.up * 0.5f;

            raiderObj.transform.position = spawnPos;
            raiderObj.transform.rotation = player.transform.rotation;

            // Убеждаемся в наличии коллайдера и Rigidbody
            var col = raiderObj.GetComponent<Collider>();
            if (col == null)
            {
                var box = raiderObj.AddComponent<BoxCollider>();
                box.size = new Vector3(2.2f, 1.4f, 4.4f);
                box.center = new Vector3(0f, 0.7f, 0f);
            }

            var rb = raiderObj.GetComponent<Rigidbody>();
            if (rb == null) rb = raiderObj.AddComponent<Rigidbody>();
            rb.linearVelocity = player.transform.forward * (player.SpeedMps + 5f);

            var raiderAI = raiderObj.AddComponent<RaiderVehicleAI>();
            activeRaiders.Add(raiderAI);

            // Уведомление игроку о приближении рейдера
            PrototypeHud.Instance?.ShowBiomeNotification("ВНИМАНИЕ: РЕЙДЕР НА ХВОСТЕ!", "Берегитесь тарана и используйте спецспособности", new Color(1f, 0.35f, 0.2f));

            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlaySiren(0.85f);
            }
        }
    }
}
