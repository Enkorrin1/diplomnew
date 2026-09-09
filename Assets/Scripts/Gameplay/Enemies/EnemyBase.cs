using System;
using UnityEngine;
using RogueDrive.Gameplay.VFX;

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

        [Header("3D Visual Representation")]
        [SerializeField] protected GameObject visualModelPrefab;
        [SerializeField] protected Vector3 visualModelOffset = new Vector3(0f, -0.5f, 0f);
        [SerializeField] protected Vector3 visualModelScale = Vector3.one;
        [SerializeField] protected GameObject weaponPropPrefab;

        protected GameObject spawnedVisualInstance;

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

            // Обеспечиваем наличие кинематического Rigidbody для безотказной регистрации триггерных столкновений
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            EnsureVisualRepresentation();

            if (GetComponent<EnemyVisualBobbing>() == null)
            {
                gameObject.AddComponent<EnemyVisualBobbing>();
            }
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

            // Регистрация в аркадной комбо-системе
            RogueDrive.Gameplay.Combat.ComboScoreSystem.Instance?.RegisterKill(transform.position, xpReward);

            // Сочный визуальный и звуковой эффект ликвидации врага
            SpawnDeathEffect(transform.position);
            CombatVfxCatalog.Instance?.SpawnEnemyDeath(transform.position);

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

        void SpawnDeathEffect(Vector3 pos)
        {
            RogueDrive.Audio.AudioManager.Instance?.PlayCrash(0.45f);

            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "EnemyDeathPuff";
            puff.transform.position = pos + Vector3.up * 0.5f;
            puff.transform.localScale = Vector3.one * 1.2f;
            Destroy(puff.GetComponent<Collider>());

            Renderer r = puff.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                m.color = new Color(0.9f, 0.25f, 0.15f, 0.8f);
                r.sharedMaterial = m;
            }

            Destroy(puff, 0.18f);
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

        protected void EnsureVisualRepresentation()
        {
            Transform existingVisual = transform.Find("VisualModel");
            if (existingVisual != null)
            {
                spawnedVisualInstance = existingVisual.gameObject;
                HideRootRenderer();
                return;
            }

            GameObject prefabToUse = visualModelPrefab;
#if UNITY_EDITOR
            if (prefabToUse == null)
            {
                string assetPath = null;
                if (this is WalkerZombie)
                {
                    assetPath = "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Zombie.prefab";
                }
                else if (this is RunnerMutant)
                {
                    assetPath = "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Evil_Clown.prefab";
                }
                else if (this is ArmoredBrute)
                {
                    assetPath = "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Pumpkinhead.prefab";
                }
                else if (this is AcidSpitter)
                {
                    assetPath = "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_Pacient_04.prefab";
                }
                else if (this is EliteKamikaze)
                {
                    assetPath = "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Evil_Clown.prefab";
                }
                else if (this is EliteJuggernautMinion)
                {
                    assetPath = "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Pumpkinhead.prefab";
                }
                else if (this is ElitePackLeader)
                {
                    assetPath = "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_SuitMan_1.prefab";
                }

                if (!string.IsNullOrEmpty(assetPath))
                {
                    prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                }
            }
#endif

            if (prefabToUse != null)
            {
                spawnedVisualInstance = Instantiate(prefabToUse, transform);
                spawnedVisualInstance.name = "VisualModel";
                spawnedVisualInstance.transform.localPosition = visualModelOffset;
                spawnedVisualInstance.transform.localRotation = Quaternion.identity;
                spawnedVisualInstance.transform.localScale = visualModelScale;

                // Отключаем внутренние коллайдеры дочерней 3D-модели, чтобы не дублировать хитбоксы
                Collider[] childColliders = spawnedVisualInstance.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < childColliders.Length; i++)
                {
                    childColliders[i].enabled = false;
                }

                // Отключаем RootMotion на аниматорах, чтобы процедурная физика управляла движением
                Animator[] animators = spawnedVisualInstance.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < animators.Length; i++)
                {
                    animators[i].applyRootMotion = false;
                }

                AttachWeaponProp(spawnedVisualInstance);
            }

            HideRootRenderer();
        }

        void HideRootRenderer()
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = false;
            }
        }

        void AttachWeaponProp(GameObject visualInstance)
        {
            if (visualInstance == null) return;

            GameObject propPrefab = weaponPropPrefab;
#if UNITY_EDITOR
            if (propPrefab == null)
            {
                if (this is ArmoredBrute || this is EliteJuggernautMinion)
                {
                    propPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AlexMakes3D/Polygon style/Halloween pack/Props/Prefabs/Pitchfork.prefab");
                }
                else if (this is RunnerMutant || this is EliteKamikaze)
                {
                    propPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AlexMakes3D/Polygon style/Halloween pack/Props/Prefabs/Сlown hammer.prefab");
                }
                else if (this is WalkerZombie)
                {
                    propPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AlexMakes3D/Polygon style/Halloween pack/Props/Prefabs/Kitchen cleaver.prefab");
                }
            }
#endif
            if (propPrefab == null) return;

            Transform hand = FindDeepChild(visualInstance.transform, "hand.r")
                          ?? FindDeepChild(visualInstance.transform, "hand_r")
                          ?? FindDeepChild(visualInstance.transform, "c_hand.r")
                          ?? FindDeepChild(visualInstance.transform, "Hand.R");

            if (hand != null)
            {
                GameObject weapon = Instantiate(propPrefab, hand);
                weapon.name = "HeldWeapon";
                weapon.transform.localPosition = new Vector3(0.04f, 0.05f, 0.02f);
                weapon.transform.localRotation = Quaternion.Euler(15f, 90f, 0f);
                weapon.transform.localScale = Vector3.one * 0.85f;

                Collider[] cols = weapon.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    cols[i].enabled = false;
                }
            }
        }

        Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                    return child;
                Transform found = FindDeepChild(child, childName);
                if (found != null)
                    return found;
            }
            return null;
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
