using System.Collections;
using System.Collections.Generic;
using RogueDrive.Audio;
using RogueDrive.Gameplay;
using UnityEngine;

namespace RogueDrive.Gameplay.Combat
{
    /// <summary>
    /// Самонаводящаяся микро-ракета, запускаемая залпом с автомобиля игрока.
    /// Автоматически захватывает ближайшую цель (машину рейдера или элитного зомби),
    /// разгоняется с дымным шлейфом и взрывается при попадании.
    /// </summary>
    public sealed class HomingMissile : MonoBehaviour
    {
        [SerializeField] private float initialSpeed = 25f;
        [SerializeField] private float maxSpeed = 65f;
        [SerializeField] private float acceleration = 50f;
        [SerializeField] private float turnRate = 260f;
        [SerializeField] private float explosionRadius = 4.5f;
        [SerializeField] private float damage = 85f;
        [SerializeField] private float lifetime = 4.0f;

        private Transform target;
        private float currentSpeed;
        private float aliveTime;
        private TrailRenderer trail;

        public void Launch(Transform targetEnemy, float bonusDamage = 0f)
        {
            target = targetEnemy;
            damage += bonusDamage;
            currentSpeed = initialSpeed;

            SetupTrail();
        }

        private void SetupTrail()
        {
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.35f;
                trail.startWidth = 0.22f;
                trail.endWidth = 0.02f;
                trail.material = new Material(Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Standard"));
                trail.startColor = new Color(1f, 0.65f, 0.15f, 0.9f);
                trail.endColor = new Color(0.8f, 0.1f, 0.1f, 0f);
            }
        }

        private void Update()
        {
            aliveTime += Time.deltaTime;
            if (aliveTime >= lifetime)
            {
                Explode();
                return;
            }

            // Постепенный разгон
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);

            // Наведение на цель
            if (target != null && target.gameObject.activeInHierarchy)
            {
                Vector3 toTarget = (target.position + Vector3.up * 0.5f) - transform.position;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnRate * Time.deltaTime);
                }
            }

            Vector3 step = transform.forward * (currentSpeed * Time.deltaTime);

            // Проверка столкновений лучом
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step.magnitude * 1.2f))
            {
                transform.position = hit.point;
                Explode();
                return;
            }

            transform.position += step;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) return;
            Explode();
        }

        private void Explode()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayExplosion(0.85f);
            }

            // Нанесение AoE урона целям в радиусе
            Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col.CompareTag("Player")) continue;

                // Урон всем целям (рейдеры, зомби, боссы, бочки)
                var damageable = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage);
                }

                // Физический отброс Rigidbodies
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddExplosionForce(750f, transform.position, explosionRadius, 1.2f, ForceMode.Impulse);
                }
            }

            Destroy(gameObject);
        }
    }
}
