using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Бронированный громила: огромный запас HP, сильный урон при таране, сбивает скорость автомобиля.</summary>
    public sealed class ArmoredBrute : EnemyBase
    {
        protected override void Awake()
        {
            maxHealth = 320f;
            baseSpeed = 3.2f;
            contactDamage = 35f;
            xpReward = 5;
            resistsRamming = true;
            ramVulnerability = 0.5f;
            visualModelScale = new Vector3(1.35f, 1.35f, 1.35f);
            visualModelOffset = new Vector3(0f, -0.65f, 0f);
            base.Awake();
        }
    }
}
