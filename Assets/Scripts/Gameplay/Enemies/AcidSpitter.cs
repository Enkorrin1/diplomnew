using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Зомби-плевака / Кислотный мутант: держится на дистанции или на обочине, стреляет кислотой по машине.</summary>
    public sealed class AcidSpitter : EnemyBase
    {
        [Header("Ranged Attack")]
        [SerializeField, Min(5f)] private float preferredDistance = 22f;
        [SerializeField, Min(0.5f)] private float attackCooldown = 3.2f;
        [SerializeField] private GameObject acidProjectilePrefab;
        [SerializeField] private Transform shootPoint;

        float attackTimer;

        protected override void Awake()
        {
            maxHealth = 65f;
            baseSpeed = 3.5f;
            contactDamage = 10f;
            xpReward = 3;
            resistsRamming = false;
            ramVulnerability = 1.8f;
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            attackTimer = UnityEngine.Random.Range(0.5f, attackCooldown);
        }

        protected override void MoveTowardsPlayer(float dt)
        {
            if (playerTarget == null)
                return;

            float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);

            // Если ближе оптимальной дистанции — останавливается или пятится
            if (distToPlayer > preferredDistance)
            {
                base.MoveTowardsPlayer(dt);
            }
            else
            {
                // Поворачиваемся лицом к машине
                Vector3 toPlayer = (playerTarget.position - transform.position);
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.1f)
                {
                    transform.rotation = Quaternion.LookRotation(toPlayer, Vector3.up);
                }
            }

            // Стрельба
            attackTimer -= dt;
            if (attackTimer <= 0f && distToPlayer <= preferredDistance * 1.5f)
            {
                ShootAcid();
                attackTimer = attackCooldown;
            }
        }

        void ShootAcid()
        {
            if (acidProjectilePrefab == null || playerTarget == null)
                return;

            Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 1.2f;
            Vector3 targetAim = playerTarget.position + Vector3.up * 0.5f;
            Vector3 shootDir = targetAim - spawnPos;
            Quaternion shootRot = shootDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(shootDir) : Quaternion.identity;

            GameObject projObj = GameplayPool.Instance != null
                ? GameplayPool.Instance.Spawn(acidProjectilePrefab, spawnPos, shootRot)
                : Instantiate(acidProjectilePrefab, spawnPos, shootRot);

            AcidProjectile proj = projObj.GetComponent<AcidProjectile>();
            if (proj != null)
            {
                proj.Launch(shootDir.sqrMagnitude > 0.001f ? shootDir.normalized : Vector3.forward);
            }
        }
    }
}
