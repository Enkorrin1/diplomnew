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

        public void SetValue(float value)
        {
            xpValue = value;
        }

        private void OnEnable()
        {
            isMagnetized = false;
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

            float distToCar = Vector3.Distance(transform.position, playerCar.position);

            // Определение радиуса подбора (из контроллера или дефолтный 6м)
            float pickupRadius = 6.5f;
            ArcadeCarController carController = playerCar.GetComponent<ArcadeCarController>();
            if (carController != null && carController.Sockets != null)
            {
                // Если есть модификатор магнита, радиус увеличивается
                pickupRadius = 9.0f;
            }

            if (!isMagnetized && distToCar <= pickupRadius)
            {
                isMagnetized = true;
            }

            if (isMagnetized)
            {
                // Полёт к машине с ускорением
                transform.position = Vector3.MoveTowards(transform.position, playerCar.position + Vector3.up * 0.5f, magnetFlySpeed * dt);
                magnetFlySpeed += 15f * dt;

                if (distToCar <= 1.2f)
                {
                    Collect();
                }
            }
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
