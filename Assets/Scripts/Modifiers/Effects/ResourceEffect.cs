using System;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Восстановление ресурса по счётчику убийств: топливный вампиризм,
    /// ремонтный комплект.
    /// </summary>
    [Serializable]
    public sealed class ResourceEffect : ModifierEffect
    {
        public ResourceKind Kind = ResourceKind.Fuel;

        [Tooltip("Через сколько убийств срабатывает восстановление.")]
        [Min(1)] public int KillsPerTrigger = 50;

        [Tooltip("Доля от максимума, восстанавливаемая за срабатывание.")]
        public AnimationCurve AmountPerLevel = AnimationCurve.Linear(1f, 0.03f, 3f, 0.08f);

        public override void Contribute(EffectContext context, int level)
        {
            float amount = AmountPerLevel != null ? AmountPerLevel.Evaluate(level) : 0f;
            context.Resources.RegisterKillTrigger(Kind, KillsPerTrigger, amount);
        }
    }
}
