using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Золотая монета, выпадающая из врагов и ящиков на трассе.
    /// Притягивается к автомобилю в радиусе подбора и зачисляет валюту в GameRunController.
    /// При завершении заезда монеты навсегда сохраняются в мета-прогресс через SaveService.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CoinPickup : MonoBehaviour
    {
        [SerializeField, Min(1)] private int coinValue = 1;
        [SerializeField, Min(1f)] private float magnetFlySpeed = 24f;
        [SerializeField, Min(0f)] private float rotationSpeed = 180f;

        Transform playerCar;
        GameRunController runController;
        bool isMagnetized;
        float currentFlySpeed;

        public void SetValue(int value)
        {
            coinValue = Mathf.Max(1, value);
        }

        private void Awake()
        {
            Ensure3DVisual();
        }

        private void OnEnable()
        {
            isMagnetized = false;
            currentFlySpeed = magnetFlySpeed;
            FindPlayer();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Вращение монеты
            transform.Rotate(Vector3.up, rotationSpeed * dt, Space.World);

            if (playerCar == null)
            {
                FindPlayer();
                if (playerCar == null)
                    return;
            }

            // Автодеспавн далеко позади машины с учетом направления движения
            Vector3 toPickup = transform.position - playerCar.position;
            float behindDist = -Vector3.Dot(toPickup, playerCar.forward);
            if (behindDist > 40f && toPickup.sqrMagnitude > 45f * 45f)
            {
                DespawnSelf();
                return;
            }

            float distToCar = Vector3.Distance(transform.position, playerCar.position);

            ArcadeCarController carController = playerCar.GetComponent<ArcadeCarController>();
            float pickupRadius = carController != null ? carController.PickupRadius : 6.5f;

            if (!isMagnetized && distToCar <= pickupRadius)
            {
                isMagnetized = true;
            }

            if (isMagnetized)
            {
                transform.position = Vector3.MoveTowards(transform.position, playerCar.position + Vector3.up * 0.5f, currentFlySpeed * dt);
                currentFlySpeed += 16f * dt;

                if (distToCar <= 1.2f)
                {
                    Collect();
                }
            }
        }

        void DespawnSelf()
        {
            if (GameplayPool.Instance != null)
                GameplayPool.Instance.Despawn(gameObject);
            else
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<ArcadeCarController>() != null)
            {
                Collect();
            }
        }

        void Collect()
        {
            if (runController == null)
                runController = FindFirstObjectByType<GameRunController>();

            if (runController != null)
            {
                runController.AddCoins(coinValue);
            }

            RogueDrive.Audio.AudioManager.Instance?.PlayCoin();

            DespawnSelf();
        }

        void FindPlayer()
        {
            ArcadeCarController car = FindFirstObjectByType<ArcadeCarController>();
            if (car != null)
            {
                playerCar = car.transform;
            }
            if (runController == null)
            {
                runController = FindFirstObjectByType<GameRunController>();
            }
        }

        void Ensure3DVisual()
        {
            Transform existing = transform.Find("VisualModel");
            if (existing != null)
            {
                HideRootRenderer();
                return;
            }

            GameObject boltPrefab = null;
#if UNITY_EDITOR
            boltPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GarageAssetPack/Prefabs/Bolt.prefab");
#endif
            if (boltPrefab != null)
            {
                GameObject bolt = Instantiate(boltPrefab, transform);
                bolt.name = "VisualModel";
                bolt.transform.localPosition = Vector3.zero;
                bolt.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                bolt.transform.localScale = Vector3.one * 1.5f;

                Collider[] cols = bolt.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    Destroy(cols[i]);
                }
            }

            HideRootRenderer();
        }

        void HideRootRenderer()
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }
    }
}
