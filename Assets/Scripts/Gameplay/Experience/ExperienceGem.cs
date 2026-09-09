using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Сфера опыта, выпадающая из врагов и ящиков.
    /// Автоматически притягивается к автомобилю в радиусе подбора (PickupRadius)
    /// и начисляет опыт заезда при контакте.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ExperienceGem : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float xpValue = 1f;
        [SerializeField, Min(1f)] private float magnetFlySpeed = 24f;
        [SerializeField, Min(0f)] private float rotationSpeed = 120f;

        Transform playerCar;
        RunExperienceManager xpManager;
        bool isMagnetized;

        float currentFlySpeed;

        public void SetValue(float value)
        {
            xpValue = value;
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

            // Визуальное вращение
            transform.Rotate(Vector3.up, rotationSpeed * dt, Space.World);

            if (playerCar == null)
            {
                FindPlayer();
                if (playerCar == null)
                    return;
            }

            // Автоматический возврат в пул, если машина уехала далеко вперед
            float zDiff = transform.position.z - playerCar.position.z;
            if (zDiff < -40f && Vector3.Distance(transform.position, playerCar.position) > 45f)
            {
                DespawnSelf();
                return;
            }

            float distToCar = Vector3.Distance(transform.position, playerCar.position);

            // Определение радиуса подбора (из контроллера автомобиля с учётом модификаторов)
            ArcadeCarController carController = playerCar.GetComponent<ArcadeCarController>();
            float pickupRadius = carController != null ? carController.PickupRadius : 6.5f;

            if (!isMagnetized && distToCar <= pickupRadius)
            {
                isMagnetized = true;
            }

            if (isMagnetized)
            {
                // Полёт к машине с ускорением
                transform.position = Vector3.MoveTowards(transform.position, playerCar.position + Vector3.up * 0.5f, currentFlySpeed * dt);
                currentFlySpeed += 15f * dt;

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
            if (xpManager == null)
                xpManager = FindFirstObjectByType<RunExperienceManager>();

            if (xpManager != null)
            {
                xpManager.AddExperience(xpValue);
            }

            if (GameplayPool.Instance != null)
                GameplayPool.Instance.Despawn(gameObject);
            else
                Destroy(gameObject);
        }

        void FindPlayer()
        {
            ArcadeCarController car = FindFirstObjectByType<ArcadeCarController>();
            if (car != null)
            {
                playerCar = car.transform;
            }
            if (xpManager == null)
            {
                xpManager = FindFirstObjectByType<RunExperienceManager>();
            }
        }
    }
}
