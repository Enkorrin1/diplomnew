using System;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Изменение поведения снарядов: рикошет, замедление цели, поджиг.
    /// Собирается в единый набор параметров, применяемый ко всем турелям.
    /// </summary>
    [Serializable]
    public sealed class ProjectileEffect : ModifierEffect
    {
        [Tooltip("Дополнительных отскоков за уровень.")]
        [Min(0)] public int BouncesPerLevel;

        [Tooltip("Доля замедления цели за уровень.")]
        [Range(0f, 0.9f)] public float SlowPerLevel;

        [Tooltip("Урон поджига в секунду за уровень.")]
        [Min(0f)] public float BurnPerLevel;

        public override void Contribute(EffectContext context, int level)
        {
            context.Projectiles.AddBounces(BouncesPerLevel * level);
            context.Projectiles.AddSlow(SlowPerLevel * level);
            context.Projectiles.AddBurn(BurnPerLevel * level);
        }
    }
}
