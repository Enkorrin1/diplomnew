using RogueDrive.Audio;
using RogueDrive.Gameplay;
using UnityEngine;

namespace RogueDrive.Gameplay.Combat
{
    /// <summary>
    /// Контактная ловушка с шипами и миной, сбрасываемая позади автомобиля по клавише [Q].
    /// При наезде машины рейдера или толпы преследователей наносит критический урон
    /// и вызывает занос с потерей управления.
    /// </summary>
    public sealed class SpikeTrap : MonoBehaviour
    {
        [SerializeField] private float damage = 130f;
        [SerializeField] private float triggerRadius = 2.2f;
        [SerializeField] private float lifetime = 18f;

        private bool isTriggered;
        private float aliveTime;
        private float slowDuration = 1.5f;

        public void Configure(float bonusDamage = 0f, float customSlowDuration = 1.5f)
        {
            damage += bonusDamage;
            slowDuration = customSlowDuration;
        }

        private void Start()
        {
            // Прижимаем к дорожному полотну
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 3f))
            {
                transform.position = hit.point + Vector3.up * 0.05f;
            }
        }

        private void Update()
        {
            aliveTime += Time.deltaTime;
            if (aliveTime >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (isTriggered) return;

            // Проверка проезжающих врагов
            Collider[] colliders = Physics.OverlapSphere(transform.position, triggerRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col.CompareTag("Player")) continue;

                var damageable = col.GetComponentInParent<IDamageable>() ?? col.GetComponent<IDamageable>();
                var raider = col.GetComponentInParent<RaiderVehicleAI>() ?? col.GetComponent<RaiderVehicleAI>();

                if (damageable != null || raider != null)
                {
                    TriggerTrap(damageable, raider);
                    break;
                }
            }
        }

        private void TriggerTrap(IDamageable damageable, RaiderVehicleAI raider)
        {
            isTriggered = true;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCrash(1.2f);
                AudioManager.Instance.PlayExplosion(0.7f);
            }

            if (raider != null)
            {
                raider.TakeDamage(damage);
                raider.TriggerSpinout(slowDuration);
            }
            else if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}
