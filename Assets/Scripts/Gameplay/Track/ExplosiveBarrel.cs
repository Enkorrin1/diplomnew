using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Взрывная бочка на трассе.
    /// Реагирует на выстрелы авто-турели, таран автомобилем и соседние взрывы.
    /// Наносит мощный АОЕ-урон всем врагам в радиусе и запускает цепную детонацию.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ExplosiveBarrel : MonoBehaviour, IDamageable
    {
        [Header("Explosion Parameters")]
        [SerializeField, Min(1f)] private float maxHealth = 15f;
        [SerializeField, Min(1f)] private float explosionRadius = 8.5f;
        [SerializeField, Min(10f)] private float explosionDamage = 180f;

        float currentHealth;
        bool hasExploded;

        public bool IsDead => hasExploded;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount, float slowFactor = 0f, float burnDamage = 0f)
        {
            if (hasExploded) return;

            currentHealth -= amount;
            if (currentHealth <= 0f)
            {
                Explode();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            CheckCarRam(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            CheckCarRam(collision.gameObject);
        }

        void CheckCarRam(GameObject target)
        {
            if (hasExploded) return;

            ArcadeCarController car = target.GetComponentInParent<ArcadeCarController>();
            if (car != null)
            {
                Explode();
            }
        }

        public void Explode()
        {
            if (hasExploded) return;
            hasExploded = true;

            // Сотрясение камеры
            ArcadeCameraFollow.Instance?.TriggerShake(0.9f, 0.45f);

            // Поиск всех объектов в радиусе взрыва
            Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col.gameObject == gameObject) continue;

                // Нанесение урона врагам и соседним бочкам
                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(explosionDamage);
                }

                // Урон машине игрока, если она оказалась в эпицентре
                ArcadeCarController car = col.GetComponentInParent<ArcadeCarController>();
                if (car != null && car.Run != null)
                {
                    car.Run.TakeDamage(18f);
                }
            }

            // Визуальный эффект взрывной вспышки
            CreateExplosionVFX(transform.position);

            Destroy(gameObject);
        }

        void CreateExplosionVFX(Vector3 pos)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "BarrelExplosionFlash";
            flash.transform.position = pos + Vector3.up * 0.5f;
            flash.transform.localScale = Vector3.one * (explosionRadius * 0.8f);

            Collider c = flash.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer r = flash.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                m.color = new Color(1f, 0.55f, 0.1f, 0.85f);
                r.sharedMaterial = m;
            }

            Destroy(flash, 0.18f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
