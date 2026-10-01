using System.Collections;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Физически разрушаемое препятствие на трассе и обочине (поддоны, бочки, мусорные баки, деревянные ящики).
    /// При наезде автомобиля на скорости разлетается на 3D-обломки с физикой PhysX,
    /// сотрясает камеру, воспроизводит звук удара и может сбивать подошедших зомби.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DestructibleTrackObstacle : MonoBehaviour, IDamageable
    {
        public enum ObstacleType
        {
            WoodPallet,
            MetalBarrel,
            CardboardBox,
            TrashBin,
            ConcreteBarrier
        }

        [Header("Obstacle Settings")]
        [SerializeField] private ObstacleType type = ObstacleType.WoodPallet;
        [SerializeField] private float minBreakSpeed = 3.5f;
        [SerializeField] private int debrisCount = 5;
        [SerializeField] private bool explosive = false;
        [SerializeField] private float explosionRadius = 4.5f;
        [SerializeField] private float explosionDamage = 50f;

        private bool isBroken = false;
        public bool IsDead => isBroken;

        public void Configure(ObstacleType obstacleType, bool isExplosive = false)
        {
            type = obstacleType;
            explosive = isExplosive;
        }

        private void OnCollisionEnter(Collision collision)
        {
            CheckHit(collision.gameObject, collision.relativeVelocity.magnitude, collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
        }

        private void OnTriggerEnter(Collider other)
        {
            // На случай если коллайдер был триггером
            var car = other.GetComponentInParent<ArcadeCarController>();
            if (car != null)
            {
                var rb = car.GetComponent<Rigidbody>();
                float speed = rb != null ? rb.linearVelocity.magnitude : 10f;
                CheckHit(car.gameObject, speed, other.ClosestPoint(transform.position));
            }
        }

        public void TakeDamage(float amount, float slowFactor = 0f, float burnDamage = 0f)
        {
            if (isBroken) return;
            Break(Vector3.up * 3f, transform.position);
        }

        private void CheckHit(GameObject target, float speed, Vector3 hitPoint)
        {
            if (isBroken) return;

            var car = target.GetComponentInParent<ArcadeCarController>();
            if (car != null && speed >= minBreakSpeed)
            {
                var carRb = car.GetComponent<Rigidbody>();
                Vector3 impactVelocity = carRb != null ? carRb.linearVelocity : car.transform.forward * speed;
                Break(impactVelocity, hitPoint);
            }
        }

        public void Break(Vector3 impactVelocity, Vector3 hitPoint)
        {
            if (isBroken) return;
            isBroken = true;

            // 1. Звуковой отклик
            if (AudioManager.Instance != null)
            {
                if (explosive)
                {
                    AudioManager.Instance.PlayExplosion(1.0f);
                }
                else if (type == ObstacleType.MetalBarrel || type == ObstacleType.TrashBin)
                {
                    AudioManager.Instance.PlayCrash(0.85f);
                }
                else
                {
                    AudioManager.Instance.PlayCrateBreak();
                }
            }

            // 2. Сотрясение камеры
            ArcadeCameraFollow.Instance?.TriggerShake(explosive ? 0.6f : 0.35f, 0.25f);

            // 3. Физический урон врагам поблизости
            float rad = explosive ? explosionRadius : 2.5f;
            Collider[] colliders = Physics.OverlapSphere(transform.position, rad);
            foreach (var col in colliders)
            {
                if (col.gameObject == gameObject) continue;
                var enemy = col.GetComponentInParent<EncounterZombie>();
                if (enemy != null)
                {
                    float dmg = explosive ? explosionDamage : 25f;
                    enemy.TakeDamage(dmg);
                }
                else
                {
                    var damageable = col.GetComponentInParent<IDamageable>();
                    if (damageable != null && !ReferenceEquals(damageable, this))
                    {
                        damageable.TakeDamage(explosive ? explosionDamage : 20f);
                    }
                }
            }

            // 4. Генерация физических 3D-обломков
            SpawnDebris(impactVelocity);

            // 5. Удаляем сам объект препятствия
            Destroy(gameObject);
        }

        private void SpawnDebris(Vector3 impactVel)
        {
            Color debrisColor = type switch
            {
                ObstacleType.WoodPallet => new Color(0.62f, 0.44f, 0.26f),
                ObstacleType.MetalBarrel => new Color(0.25f, 0.35f, 0.45f),
                ObstacleType.CardboardBox => new Color(0.72f, 0.58f, 0.38f),
                ObstacleType.TrashBin => new Color(0.2f, 0.5f, 0.25f),
                _ => new Color(0.5f, 0.5f, 0.5f)
            };

            Vector3 baseScale = type switch
            {
                ObstacleType.WoodPallet => new Vector3(0.12f, 0.08f, 0.85f),
                ObstacleType.MetalBarrel => new Vector3(0.25f, 0.35f, 0.08f),
                ObstacleType.CardboardBox => new Vector3(0.3f, 0.2f, 0.05f),
                _ => new Vector3(0.2f, 0.2f, 0.2f)
            };

            int count = Mathf.Max(3, debrisCount);
            for (int i = 0; i < count; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "ObstacleDebris";
                shard.transform.position = transform.position + Random.insideUnitSphere * 0.4f + Vector3.up * 0.3f;
                shard.transform.localScale = Vector3.Scale(baseScale, new Vector3(Random.Range(0.7f, 1.3f), Random.Range(0.7f, 1.3f), Random.Range(0.7f, 1.3f)));
                shard.transform.rotation = Random.rotation;

                // Убираем коллизии между осколками для производительности
                shard.layer = 2; // Ignore Raycast

                var rend = shard.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = debrisColor;
                    rend.sharedMaterial = mat;
                }

                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 2.5f;
                // Импульс от удара + разлет в стороны
                Vector3 blastDir = (shard.transform.position - transform.position).normalized + Vector3.up * 0.5f;
                Vector3 force = impactVel * 0.4f + blastDir * Random.Range(4f, 9f);
                rb.AddForce(force, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);

                Destroy(shard, Random.Range(3.5f, 5.0f));
            }
        }
    }
}
