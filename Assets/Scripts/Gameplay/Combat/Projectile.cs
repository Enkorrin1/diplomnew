using UnityEngine;

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

            Vector3 nextPos = transform.position + direction * (speed * dt);
            transform.position = nextPos;
        }

        private void OnTriggerEnter(Collider other)
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead)
                return;

            Transform targetTransform = other.transform;
            if (targetTransform == lastTarget)
                return;

            lastTarget = targetTransform;
            target.TakeDamage(currentDamage, slow, burn);

            if (remainingBounces > 0 && TryBounce(targetTransform.position))
            {
                remainingBounces--;
                currentDamage *= 0.85f; // легкое угасание урона при рикошете
            }
            else
            {
                DespawnSelf();
            }
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
