using UnityEngine;
using RogueDrive.Gameplay.VFX;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Снаряд авто-турели. Поддерживает полет, рикошет по соседним целям,
    /// наложение замедления и периодического урона от огня.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float speed = 45f;
        [SerializeField, Min(0.1f)] private float lifeTime = 2.5f;
        [SerializeField, Min(0.1f)] private float bounceRadius = 12f;

        float currentDamage;
        int remainingBounces;
        float slow;
        float burn;

        Vector3 direction;
        float age;
        Transform lastTarget;

        private void Awake()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        public void Launch(Vector3 dir, float damage, int bounces = 0, float slowFactor = 0f, float burnDmg = 0f)
        {
            direction = dir.normalized;
            currentDamage = damage;
            remainingBounces = bounces;
            slow = slowFactor;
            burn = burnDmg;
            age = 0f;
            lastTarget = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            if (age >= lifeTime)
            {
                DespawnSelf();
                return;
            }

            Vector3 currentPos = transform.position;
            float stepDist = speed * dt;

            // Непрерывный SphereCast вдоль траектории полета:
            // Исключает пропуск коллизий на высокой скорости и надежно поражает врагов
            if (Physics.SphereCast(currentPos, 0.45f, direction, out RaycastHit hit, stepDist, ~0, QueryTriggerInteraction.Collide))
            {
                if (HandleHit(hit.collider))
                {
                    return;
                }
            }

            transform.position = currentPos + direction * stepDist;
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleHit(other);
        }

        bool HandleHit(Collider other)
        {
            if (other == null) return false;

            // Игнорируем автомобиль игрока
            if (other.GetComponentInParent<ArcadeCarController>() != null)
                return false;

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead)
                return false;

            Transform targetTransform = other.transform;
            if (targetTransform == lastTarget)
                return false;

            lastTarget = targetTransform;
            target.TakeDamage(currentDamage, slow, burn);
            RogueDrive.Audio.AudioManager.Instance?.PlayHit(0.55f);
            CombatVfxCatalog.Instance?.SpawnBulletHit(transform.position, Quaternion.LookRotation(-direction));

            if (remainingBounces > 0 && TryBounce(targetTransform.position))
            {
                remainingBounces--;
                currentDamage *= 0.85f; // легкое угасание урона при рикошете
                RogueDrive.Audio.AudioManager.Instance?.PlayRicochet();
            }
            else
            {
                DespawnSelf();
            }

            return true;
        }

        bool TryBounce(Vector3 currentHitPos)
        {
            Collider[] colliders = Physics.OverlapSphere(currentHitPos, bounceRadius);
            Transform bestCandidate = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                IDamageable potential = colliders[i].GetComponentInParent<IDamageable>();
                if (potential == null || potential.IsDead)
                    continue;

                Transform candidateTransform = colliders[i].transform;
                if (candidateTransform == lastTarget)
                    continue;

                float dist = Vector3.Distance(currentHitPos, candidateTransform.position);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestCandidate = candidateTransform;
                }
            }

            if (bestCandidate != null)
            {
                Vector3 newDir = (bestCandidate.position - transform.position).normalized;
                newDir.y = 0f; // стрельба в плоскости дороги
                if (newDir.sqrMagnitude > 0.01f)
                {
                    direction = newDir.normalized;
                    transform.rotation = Quaternion.LookRotation(direction);
                    return true;
                }
            }

            return false;
        }

        void DespawnSelf()
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
    }
}
