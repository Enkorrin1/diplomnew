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
            base.Awake();
        }
    }
}
