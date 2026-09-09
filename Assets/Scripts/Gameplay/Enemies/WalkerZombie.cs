using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Обычный зомби: средняя скорость, контактный урон, погибает от тарана на скорости.</summary>
    public sealed class WalkerZombie : EnemyBase
    {
        [Header("Walker Customization")]
        [SerializeField] private float stumbleWiggle = 0.5f;

        protected override void Awake()
        {
            maxHealth = 45f;
            baseSpeed = 4.5f;
            contactDamage = 12f;
            xpReward = 1;
            resistsRamming = false;
            ramVulnerability = 2.0f;
            visualModelScale = Vector3.one;
            visualModelOffset = new Vector3(0f, -0.5f, 0f);
            base.Awake();
        }

        protected override void MoveTowardsPlayer(float dt)
        {
            base.MoveTowardsPlayer(dt);
            // Небольшое покачивание при ходьбе
            if (stumbleWiggle > 0f)
            {
                transform.Rotate(Vector3.up, Mathf.Sin(Time.time * 6f) * stumbleWiggle);
            }
        }
    }
}
