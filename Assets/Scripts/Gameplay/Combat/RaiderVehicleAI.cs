using System.Collections;
using RogueDrive.Audio;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.VFX;
using UnityEngine;

namespace RogueDrive.Gameplay.Combat
{
    /// <summary>
    /// Искусственный интеллект боевой машины рейдера (Mad Max Interceptor).
    /// Преследует игрока на высокой скорости по шоссе, идет на таран,
    /// подрезает и бортует, пытаясь впечатать автомобиль игрока в отбойники.
    /// При уничтожении эффектно детонирует и осыпает трассу монетами.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RaiderVehicleAI : MonoBehaviour, IDamageable
    {
        [Header("Vehicle Stats")]
        [SerializeField] private float maxHealth = 220f;
        [SerializeField] private float topSpeedMps = 32f;
        [SerializeField] private float acceleration = 25f;
        [SerializeField] private float steerSharpness = 4.5f;
        [SerializeField] private float rammingDamageToPlayer = 20f;
        [SerializeField] private int coinRewardCount = 5;

        private float currentHealth;
        private Rigidbody body;
        private ArcadeCarController playerCar;

        private float spinoutTimer;
        private float attackCooldown;
        private bool isDead;

        private Renderer[] renderers;
        private Color[] originalColors;

        // UI Health Bar
        private static GUIStyle hpBgStyle;
        private static GUIStyle hpFillStyle;
        private static Texture2D hpBgTex;
        private static Texture2D hpFillTex;

        public float Health => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = 1600f;
            body.linearDamping = 0.4f;
            body.angularDamping = 2.0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            currentHealth = maxHealth;
            CacheRenderers();
        }

        private void Start()
        {
            playerCar = FindFirstObjectByType<ArcadeCarController>();
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                originalColors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_Color"))
                    {
                        originalColors[i] = renderers[i].material.color;
                    }
                }
            }
        }

        public void TakeDamage(float amount, float slowFactor = 0f, float burnDamage = 0f)
        {
            if (isDead) return;

            currentHealth -= amount;
            StartCoroutine(FlashDamageRoutine());

            if (slowFactor > 0f)
            {
                TriggerSpinout(slowFactor);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void TriggerSpinout(float duration = 1.5f)
        {
            spinoutTimer = duration;
            if (body != null)
            {
                body.angularVelocity = new Vector3(0f, Random.Range(-12f, 12f), 0f);
            }
        }

        public void ApplyRamForce(Vector3 force)
        {
            if (body != null)
            {
                body.AddForce(force * body.mass * 0.04f, ForceMode.Impulse);
            }
        }

        private void FixedUpdate()
        {
            if (isDead) return;

            if (playerCar == null)
            {
                playerCar = FindFirstObjectByType<ArcadeCarController>();
                if (playerCar == null) return;
            }

            // 1. Состояние заноса (Spinout от шипов/мин)
            if (spinoutTimer > 0f)
            {
                spinoutTimer -= Time.fixedDeltaTime;
                return;
            }

            Vector3 toPlayer = playerCar.transform.position - transform.position;
            float distance = toPlayer.magnitude;

            // Если слишком далеко позади (> 70м) — самоуничтожение
            if (Vector3.Dot(toPlayer, playerCar.transform.forward) > 65f)
            {
                Destroy(gameObject);
                return;
            }

            // 2. Расчет желаемой скорости и траектории
            float targetSpeed = Mathf.Min(topSpeedMps, playerCar.SpeedMps + 4.5f);
            Vector3 desiredVelocity = transform.forward * targetSpeed;

            // Плавное руление в сторону игрока
            Vector3 forwardTarget = playerCar.transform.position;

            // Агрессивный таран сбоку
            attackCooldown -= Time.fixedDeltaTime;
            if (attackCooldown <= 0f && distance < 12f)
            {
                // Рывок в бок игрока
                float lateralOffset = toPlayer.x > 0 ? 1f : -1f;
                desiredVelocity += transform.right * (lateralOffset * 8f);
                attackCooldown = 2.5f;
            }

            // Коррекция скорости
            Vector3 velDelta = desiredVelocity - body.linearVelocity;
            velDelta.y = 0f;
            body.AddForce(velDelta * acceleration, ForceMode.Acceleration);

            // Плавный поворот в сторону движения
            Vector3 lookDir = new Vector3(toPlayer.x, 0f, toPlayer.z);
            if (lookDir.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, steerSharpness * Time.fixedDeltaTime);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isDead) return;

            var hitCar = collision.gameObject.GetComponentInParent<ArcadeCarController>();
            if (hitCar != null)
            {
                // Таран игрока
                if (hitCar.Run != null)
                {
                    hitCar.Run.TakeDamage(rammingDamageToPlayer);
                }

                // Сам рейдер получает встречный урон
                TakeDamage(hitCar.SpeedKmh * 0.8f);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayCrash(1.0f);
                }

                ArcadeCameraFollow.Instance?.TriggerShake(0.7f, 0.3f);
            }
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayExplosion(1.2f);
            }

            ArcadeCameraFollow.Instance?.TriggerShake(1.2f, 0.45f);

            // Дроп монет
            for (int i = 0; i < coinRewardCount; i++)
            {
                Vector3 coinPos = transform.position + Vector3.up * 1f + Random.insideUnitSphere * 1.5f;
                GameObject coin = new GameObject("Coin_RaiderDrop");
                coin.transform.position = coinPos;
                coin.AddComponent<BoxCollider>().isTrigger = true;
                var pickup = coin.AddComponent<CoinPickup>();
                pickup.SetValue(3);
            }

            // Огненная вспышка и исчезновение
            Destroy(gameObject, 0.15f);
        }

        private IEnumerator FlashDamageRoutine()
        {
            if (renderers == null) yield break;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
                {
                    renderers[i].material.color = Color.red;
                }
            }

            yield return new WaitForSeconds(0.08f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material.HasProperty("_Color") && originalColors != null && i < originalColors.Length)
                {
                    renderers[i].material.color = originalColors[i];
                }
            }
        }

        private void OnGUI()
        {
            if (isDead || Camera.main == null) return;

            // 3D полоска здоровья над машиной
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.4f);
            if (screenPos.z <= 0f) return;

            EnsureHpStyles();

            float barW = 80f;
            float barH = 8f;
            float x = screenPos.x - barW * 0.5f;
            float y = Screen.height - screenPos.y;

            GUI.Box(new Rect(x, y, barW, barH), string.Empty, hpBgStyle);
            float fillPct = Mathf.Clamp01(currentHealth / maxHealth);
            GUI.Box(new Rect(x, y, barW * fillPct, barH), string.Empty, hpFillStyle);
        }

        private static void EnsureHpStyles()
        {
            if (hpBgTex == null)
            {
                hpBgTex = new Texture2D(1, 1);
                hpBgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.85f));
                hpBgTex.Apply();

                hpFillTex = new Texture2D(1, 1);
                hpFillTex.SetPixel(0, 0, new Color(0.95f, 0.25f, 0.2f, 0.95f));
                hpFillTex.Apply();

                hpBgStyle = new GUIStyle { normal = { background = hpBgTex } };
                hpFillStyle = new GUIStyle { normal = { background = hpFillTex } };
            }
        }
    }
}
