using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Быстрый мутант-бегун: высокая скорость, настигает машину с флангов и сзади, атакует борта.</summary>
    public sealed class RunnerMutant : EnemyBase
    {
        [Header("Runner Settings")]
        [SerializeField] private float flankOffset = 3.5f;

        float flankSign = 1f;

        protected override void Awake()
        {
            maxHealth = 35f;
            baseSpeed = 15f;
            contactDamage = 18f;
            xpReward = 2;
            resistsRamming = false;
            ramVulnerability = 1.5f;
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            flankSign = UnityEngine.Random.value > 0.5f ? 1f : -1f;
        }

        protected override void MoveTowardsPlayer(float dt)
        {
            if (playerTarget == null)
                return;

            // Фланговый вектор: сближается к бортам машины
            Vector3 targetSide = playerTarget.position + playerTarget.right * (flankOffset * flankSign);
            Vector3 toTarget = (targetSide - transform.position);
            toTarget.y = 0f;

            float currentSpeed = baseSpeed * (1f - slowFactor);
            if (toTarget.sqrMagnitude > 0.2f)
            {
                Vector3 moveDir = toTarget.normalized;
                transform.position += moveDir * (currentSpeed * dt);
                transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
            }
        }
    }
}
