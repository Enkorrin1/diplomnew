using System;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Изменение числовой характеристики: плоская прибавка и множитель,
    /// заданные кривыми от уровня модификатора.
    /// </summary>
    [Serializable]
    public sealed class StatEffect : ModifierEffect
    {
        public StatId Target;

        [Tooltip("Прибавка к базовому значению. По оси X — уровень модификатора.")]
        public AnimationCurve AdditivePerLevel = AnimationCurve.Constant(1f, 3f, 0f);

        [Tooltip("Множитель итогового значения. Единица означает отсутствие влияния.")]
        public AnimationCurve MultiplierPerLevel = AnimationCurve.Constant(1f, 3f, 1f);

        public override void Contribute(EffectContext context, int level)
        {
            if (AdditivePerLevel != null)
                context.Stats.AddFlat(Target, AdditivePerLevel.Evaluate(level));

            if (MultiplierPerLevel != null)
                context.Stats.AddMultiplier(Target, MultiplierPerLevel.Evaluate(level));
        }
    }
}
