using System;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Включение постоянно действующего поведения вокруг машины: огненный след,
    /// орбитальные пилы, силовой щит, ударная волна.
    ///
    /// Идентификатор ссылается на компонент, реализующий само поведение в сцене.
    /// Новая комбинация существующих поведений добавляется без кода, принципиально
    /// новое поведение требует нового компонента — это осознанная граница системы.
    /// </summary>
    [Serializable]
    public sealed class AuraEffect : ModifierEffect
    {
        [Tooltip("fire_trail, side_saws, energy_shield, shockwave, chain_lightning")]
        public string BehaviourId;

        public AnimationCurve RadiusPerLevel = AnimationCurve.Linear(1f, 3f, 3f, 6f);
        public AnimationCurve TickDamagePerLevel = AnimationCurve.Linear(1f, 5f, 3f, 15f);

        [Tooltip("Период срабатывания в секундах. Ноль означает непрерывное действие.")]
        public AnimationCurve IntervalPerLevel = AnimationCurve.Constant(1f, 3f, 0f);

        public override void Contribute(EffectContext context, int level)
        {
            if (string.IsNullOrEmpty(BehaviourId))
                return;

            context.Behaviours.Enable(BehaviourId, new BehaviourState
            {
                Level = level,
                Radius = RadiusPerLevel != null ? RadiusPerLevel.Evaluate(level) : 0f,
                TickDamage = TickDamagePerLevel != null ? TickDamagePerLevel.Evaluate(level) : 0f,
                Interval = IntervalPerLevel != null ? IntervalPerLevel.Evaluate(level) : 0f
            });
        }
    }
}
