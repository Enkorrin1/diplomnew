using System;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Базовый класс для всех противников.
    /// Реализует получение урона, эффекты замедления и горения, преследование игрока,
    /// контактные столкновения и выпадение опыта при гибели.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable
    {
        public static event Action<EnemyBase> AnyEnemyKilled;

        [Header("Health & Combat")]
        [SerializeField, Min(1f)] protected float maxHealth = 50f;
        [SerializeField, Min(0f)] protected float contactDamage = 15f;
        [SerializeField, Min(0f)] protected float baseSpeed = 5f;
        [SerializeField, Min(0)] protected int xpReward = 1;

        [Header("Ramming Resistance")]
        [SerializeField, Min(0f)] protected float ramVulnerability = 1f; // множитель урона от тарана
        [SerializeField] protected bool resistsRamming = false;

        [Header("Drop Prefabs")]
        [SerializeField] protected GameObject xpGemPrefab;
        [SerializeField] protected GameObject coinPrefab;
        [SerializeField, Min(0)] protected int coinReward = 1;

        protected float currentHealth;
        protected float slowTimer;
        protected float slowFactor;
        protected float burnTimer;
        protected float burnDamagePerSec;

        protected Transform playerTarget;
        protected ArcadeCarController playerCar;

        public bool IsDead => currentHealth <= 0f;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public int XpReward => xpReward;

        protected virtual void Awake()
        {
            currentHealth = maxHealth;
        }

        protected virtual void OnEnable()
        {
            currentHealth = maxHealth;
            slowTimer = 0f;
            slowFactor = 0f;
            burnTimer = 0f;
            burnDamagePerSec = 0f;
            FindPlayer();
        }

        public void SetTarget(ArcadeCarController car)
        {
            playerCar = car;
            playerTarget = car != null ? car.transform : null;
        }

        public virtual void TakeDamage(float amount, float slowAmount = 0f, float burnDmg = 0f)
        {
            if (IsDead)
                return;

            currentHealth -= amount;

            if (slowAmount > 0f)
            {
                slowFactor = Mathf.Max(slowFactor, Mathf.Clamp01(slowAmount));
                slowTimer = Mathf.Max(slowTimer, 2.5f);
            }

            if (burnDmg > 0f)
            {
                burnDamagePerSec = Mathf.Max(burnDamagePerSec, burnDmg);
                burnTimer = Mathf.Max(burnTimer, 3.0f);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        protected virtual void Update()
        {
            if (IsDead)
                return;

            float dt = Time.deltaTime;

            // Обработка периодического урона от горения
            if (burnTimer > 0f)
            {
                burnTimer -= dt;
                TakeDamage(burnDamagePerSec * dt);
                if (IsDead)
                    return;
            }

            // Обработка замедления
            if (slowTimer > 0f)
            {
                slowTimer -= dt;
                if (slowTimer <= 0f)
                    slowFactor = 0f;
            }

            // Автоматический возврат в пул, если машина уехала далеко вперед с учетом направления
            if (playerTarget != null)
            {
                Vector3 toEnemy = transform.position - playerTarget.position;
                float behindDist = -Vector3.Dot(toEnemy, playerTarget.forward);
                if (behindDist > 45f && toEnemy.sqrMagnitude > 50f * 50f)
                {
                    Despawn();
                    return;
                }
            }

            MoveTowardsPlayer(dt);
        }

        protected virtual void MoveTowardsPlayer(float dt)
        {
            if (playerTarget == null)
            {
                FindPlayer();
                if (playerTarget == null)
                    return;
            }

            float currentSpeed = baseSpeed * (1f - slowFactor);
            Vector3 direction = (playerTarget.position - transform.position);
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.1f)
            {
                Vector3 moveDir = direction.normalized;
                transform.position += moveDir * (currentSpeed * dt);
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            HandleCollisionWithPlayer(other.gameObject);
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            HandleCollisionWithPlayer(collision.gameObject);
        }

        void HandleCollisionWithPlayer(GameObject hitObj)
        {
            if (IsDead)
                return;

            ArcadeCarController car = hitObj.GetComponentInParent<ArcadeCarController>();
            if (car == null)
                return;

            GameRunController run = car.Run != null ? car.Run : FindFirstObjectByType<GameRunController>();

            float carSpeed = Mathf.Abs(car.SpeedMps);
            bool isHighSpeedRam = carSpeed >= 12f || car.IsNitroActive;

            if (isHighSpeedRam && !resistsRamming)
            {
                // Таран на высокой скорости: враг погибает мгновенно
                ArcadeCameraFollow.Instance?.TriggerShake(0.5f, 0.2f);
                TakeDamage(maxHealth * 2f);
                if (run != null)
                {
                    run.TakeDamage(contactDamage * 0.2f); // легкий урон автомобилю
                }
            }
            else
            {
                // Контактный урон автомобилю
                if (run != null)
                {
                    run.TakeDamage(contactDamage);
                }

                if (resistsRamming)
                {
                    // Бронированный враг тормозит машину
                    Rigidbody carBody = car.GetComponent<Rigidbody>();
                    if (carBody != null)
                    {
                        carBody.linearVelocity *= 0.5f;
                    }
                    TakeDamage(carSpeed * 10f * ramVulnerability);
                }
                else
                {
                    TakeDamage(maxHealth);
                }
            }
        }

        protected virtual void Die()
        {
            currentHealth = 0f;
            AnyEnemyKilled?.Invoke(this);

            // Спавн сфер опыта
            if (xpGemPrefab != null)
            {
                for (int i = 0; i < xpReward; i++)
                {
                    Vector3 dropPos = transform.position + UnityEngine.Random.insideUnitSphere * 0.5f;
                    dropPos.y = 0.5f;

                    if (GameplayPool.Instance != null)
                    {
                        GameplayPool.Instance.Spawn(xpGemPrefab, dropPos, Quaternion.identity);
                    }
                    else
                    {
                        Instantiate(xpGemPrefab, dropPos, Quaternion.identity);
                    }
                }
            }

            // Спавн монет или прямое начисление
            if (coinReward > 0)
            {
                if (coinPrefab != null)
                {
                    for (int i = 0; i < coinReward; i++)
                    {
                        Vector3 coinPos = transform.position + UnityEngine.Random.insideUnitSphere * 0.4f;
                        coinPos.y = 0.5f;

                        if (GameplayPool.Instance != null)
                        {
                            GameplayPool.Instance.Spawn(coinPrefab, coinPos, Quaternion.identity);
                        }
                        else
                        {
                            Instantiate(coinPrefab, coinPos, Quaternion.identity);
                        }
                    }
                }
                else
                {
                    GameRunController run = playerCar != null && playerCar.Run != null ? playerCar.Run : FindFirstObjectByType<GameRunController>();
                    if (run != null)
                    {
                        run.AddCoins(coinReward);
                    }
                }
            }

            Despawn();
        }

        protected void Despawn()
        {
            if (GameplayPool.Instance != null)
            {
                GameplayPool.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void FindPlayer()
        {
            if (playerCar == null)
                playerCar = FindFirstObjectByType<ArcadeCarController>();

            if (playerCar != null)
                playerTarget = playerCar.transform;
        }
    }
}
