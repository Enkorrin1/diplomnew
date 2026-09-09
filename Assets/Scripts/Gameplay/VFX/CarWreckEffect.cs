using System.Collections;
using UnityEngine;
using RogueDrive.Audio;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Эффект эффектной кинематографичной гибели автомобиля при 0 HP:
    /// - Замедление времени (Slow-Motion 0.22x)
    /// - Вспышка мощного взрыва и сотрясение камеры
    /// - Отрыв и разлет горящих обломков, капота и колес по физике
    /// - Обугливание кузова автомобиля
    /// </summary>
    public sealed class CarWreckEffect : MonoBehaviour
    {
        [Header("Wreck Parameters")]
        [SerializeField, Range(0.1f, 0.5f)] private float slowMoScale = 0.22f;
        [SerializeField, Min(0.5f)] private float slowMoRealDuration = 1.4f;
        [SerializeField, Min(10f)] private float explosionForce = 350f;

        bool hasWrecked;
        ArcadeCarController car;

        private void Awake()
        {
            car = GetComponent<ArcadeCarController>();
        }

        private void Start()
        {
            if (car != null && car.Run != null)
            {
                car.Run.HealthChanged += OnHealthChanged;
            }
        }

        private void OnDestroy()
        {
            if (car != null && car.Run != null)
            {
                car.Run.HealthChanged -= OnHealthChanged;
            }

            // Гарантированный возврат скорости времени
            Time.timeScale = 1f;
        }

        void OnHealthChanged(float currentHealth, float maxHealth)
        {
            if (currentHealth <= 0f && !hasWrecked)
            {
                TriggerWreck();
            }
        }

        public void TriggerWreck()
        {
            if (hasWrecked) return;
            hasWrecked = true;

            // 1. Мощный взрывной звук и встряска камеры
            AudioManager.Instance?.PlayExplosion(1.2f);
            ArcadeCameraFollow.Instance?.TriggerShake(1.2f, 0.65f);

            // 2. Вспышка взрыва
            CreateExplosionFlash();

            // 3. Разлет физических обломков (колеса, капот, бампер)
            SpawnFlyingDebris();

            // 4. Обугливание кузова
            ScorchedBody();

            // 5. Запуск замедления времени Slow-Motion
            StartCoroutine(SlowMoSequence());
        }

        void CreateExplosionFlash()
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "WreckExplosionFlash";
            flash.transform.position = transform.position + Vector3.up * 0.8f;
            flash.transform.localScale = Vector3.one * 5.5f;

            Collider c = flash.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer r = flash.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                m.color = new Color(1f, 0.45f, 0.1f, 0.9f);
                r.sharedMaterial = m;
            }

            Destroy(flash, 0.22f);
        }

        void SpawnFlyingDebris()
        {
            Vector3 center = transform.position + Vector3.up * 0.5f;

            // 4 колеса, разлетающиеся по физике
            Vector3[] wheelDirs = { new Vector3(-1.2f, 0.5f, 1f), new Vector3(1.2f, 0.5f, 1f), new Vector3(-1.2f, 0.5f, -1f), new Vector3(1.2f, 0.5f, -1f) };
            for (int i = 0; i < wheelDirs.Length; i++)
            {
                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"WreckWheel_{i + 1}";
                wheel.transform.position = center + wheelDirs[i];
                wheel.transform.localScale = new Vector3(0.6f, 0.3f, 0.6f);
                wheel.transform.rotation = Random.rotation;

                Renderer r = wheel.GetComponent<Renderer>();
                if (r != null)
                {
                    Material m = new Material(Shader.Find("Standard"));
                    m.color = new Color(0.12f, 0.12f, 0.12f);
                    r.sharedMaterial = m;
                }

                Rigidbody rb = wheel.AddComponent<Rigidbody>();
                rb.mass = 25f;
                rb.AddExplosionForce(explosionForce, center, 8f, 1.5f, ForceMode.Impulse);
                rb.angularVelocity = Random.insideUnitSphere * 15f;

                Destroy(wheel, 6f);
            }

            // Металлический капот
            GameObject hood = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hood.name = "WreckHood";
            hood.transform.position = center + Vector3.forward * 1.2f + Vector3.up * 0.4f;
            hood.transform.localScale = new Vector3(1.4f, 0.1f, 1.2f);
            hood.transform.rotation = transform.rotation;

            Renderer hoodR = hood.GetComponent<Renderer>();
            if (hoodR != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.2f, 0.2f, 0.22f);
                hoodR.sharedMaterial = m;
            }

            Rigidbody hoodRb = hood.AddComponent<Rigidbody>();
            hoodRb.mass = 35f;
            hoodRb.AddExplosionForce(explosionForce * 1.2f, center, 8f, 2.5f, ForceMode.Impulse);
            hoodRb.angularVelocity = Random.insideUnitSphere * 12f;

            Destroy(hood, 6f);
        }

        void ScorchedBody()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sharedMaterial != null)
                {
                    Material m = new Material(renderers[i].sharedMaterial);
                    m.color = new Color(0.12f, 0.12f, 0.14f); // Обугленный чёрный металл
                    renderers[i].sharedMaterial = m;
                }
            }
        }

        IEnumerator SlowMoSequence()
        {
            Time.timeScale = slowMoScale;
            Time.fixedDeltaTime = 0.02f * slowMoScale;

            float elapsed = 0f;
            while (elapsed < slowMoRealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Плавное восстановление нормального течения времени
            float restoreTime = 0.4f;
            float t = 0f;
            while (t < restoreTime)
            {
                t += Time.unscaledDeltaTime;
                float progress = t / restoreTime;
                Time.timeScale = Mathf.Lerp(slowMoScale, 1.0f, progress);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return null;
            }

            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
