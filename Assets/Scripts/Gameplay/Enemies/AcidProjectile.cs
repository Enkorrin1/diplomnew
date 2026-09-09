using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Кислотный снаряд зомби-плеваки: летит по дуге/направлению, наносит урон машине при попадании.</summary>
    public sealed class AcidProjectile : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float speed = 18f;
        [SerializeField, Min(0.1f)] private float lifeTime = 3.5f;
        [SerializeField, Min(1f)] private float damage = 16f;

        Vector3 direction;
        float age;

        public void Launch(Vector3 dir)
        {
            direction = dir.normalized;
            age = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            if (age >= lifeTime)
            {
                Despawn();
                return;
            }

            transform.position += direction * (speed * dt);
        }

        private void OnTriggerEnter(Collider other)
        {
            ArcadeCarController car = other.GetComponentInParent<ArcadeCarController>();
            GameRunController run = car != null && car.Run != null ? car.Run : other.GetComponentInParent<GameRunController>();
            if (run == null && car != null)
                run = FindFirstObjectByType<GameRunController>();

            if (run != null)
            {
                run.TakeDamage(damage);
                ArcadeCameraFollow.Instance?.TriggerShake(0.35f, 0.15f);
                Despawn();
                return;
            }

            // Столкновение с препятствием/дорогой
            if (other.gameObject.isStatic)
            {
                Despawn();
            }
        }

        void Despawn()
        {
            if (GameplayPool.Instance != null)
                GameplayPool.Instance.Despawn(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
