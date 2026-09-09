using RogueDrive.Modifiers;
using UnityEngine;

namespace RogueDrive.Simulation
{
    /// <summary>
    /// Аналитическая модель заезда для массовых прогонов.
    ///
    /// Заезд разбивается на шаги фиксированной длительности. На каждом шаге
    /// рассчитывается урон, наносимый турелями и постоянными поведениями, число
    /// уничтоженных и прорвавшихся противников, расход топлива и вероятность
    /// столкновения с препятствием. Модель оперирует характеристиками из StatBlock,
    /// поэтому любой модификатор влияет на исход через тот же путь, что и в игре.
    /// </summary>
    public sealed class AnalyticRunModel : IRunModel
    {
        const string ShieldBehaviour = "energy_shield";

        readonly DifficultyProfile _profile;
        readonly IRandomSource _random;

        float _killAccumulator;
        float _shieldCooldown;

        public AnalyticRunModel(DifficultyProfile profile, IRandomSource random)
        {
            _profile = profile;
            _random = random;
        }

        public StepOutcome Step(RunContext context, EffectContext effects, float deltaTime)
        {
            var outcome = new StepOutcome();

            StatBlock stats = context.Stats;
            float distance = context.Distance;

            outcome.DistanceGained = stats.Get(StatId.Speed) * deltaTime;

            float spawned = Mathf.Max(0f, _profile.EnemiesPerSecond.Evaluate(distance)) * deltaTime;
            float enemyHealth = Mathf.Max(1f, _profile.EnemyHealth.Evaluate(distance));

            float damagePerSecond = ComputeDamagePerSecond(stats, effects);
            float killable = damagePerSecond * deltaTime / enemyHealth;

            _killAccumulator += Mathf.Min(spawned, killable);
            int wholeKills = Mathf.FloorToInt(_killAccumulator);
            _killAccumulator -= wholeKills;
            outcome.Kills = wholeKills;

            // Прорвавшиеся противники наносят контактный урон; замедление снижает их долю.
            float leaked = Mathf.Max(0f, spawned - killable);
            leaked *= 1f - effects.Projectiles.Slow;
            float damage = leaked * _profile.ContactDamage;

            damage = ApplyShield(effects, damage, deltaTime);
            outcome.DamageTaken = damage;

            outcome.FuelSpent = stats.Get(StatId.FuelDrain) * deltaTime;
            outcome.FuelSpent += RollRamPenalty(distance, deltaTime);

            return outcome;
        }

        public bool IsFinished(RunContext context)
        {
            if (context.Health <= 0f || context.Fuel <= 0f)
                return true;

            return _profile.LevelLength > 0f && context.Distance >= _profile.LevelLength;
        }

        float ComputeDamagePerSecond(StatBlock stats, EffectContext effects)
        {
            float turrets = stats.Get(StatId.Damage) * stats.Get(StatId.FireRate);

            // Рикошет умножает число поражаемых целей, но с убывающей отдачей.
            turrets *= 1f + 0.35f * effects.Projectiles.Bounces;
            turrets += effects.Projectiles.Burn;

            float auras = 0f;

            foreach (var pair in effects.Behaviours.States)
            {
                if (pair.Key == ShieldBehaviour)
                    continue;

                BehaviourState state = pair.Value;
                float interval = state.Interval > 0f ? state.Interval : 1f;

                // Радиус увеличивает число задетых целей, но с убывающей отдачей:
                // прямая пропорциональность давала бы аурам многократный перевес
                // над турелями и обесценивала бы атакующую ветвь модификаторов.
                auras += state.TickDamage / interval * (1f + 0.15f * state.Radius);
            }

            return turrets + auras;
        }

        float ApplyShield(EffectContext effects, float damage, float deltaTime)
        {
            if (!effects.Behaviours.TryGet(ShieldBehaviour, out BehaviourState shield))
                return damage;

            _shieldCooldown -= deltaTime;

            if (_shieldCooldown > 0f)
                return damage;

            float interval = shield.Interval > 0f ? shield.Interval : 15f;
            _shieldCooldown = interval;
            return 0f;
        }

        float RollRamPenalty(float distance, float deltaTime)
        {
            float blocked = Mathf.Clamp01(_profile.BlockedLaneFraction.Evaluate(distance));
            float chance = blocked * _profile.RamChanceAtFullBlock * deltaTime;

            return _random.NextFloat() < chance ? _profile.RamFuelPenalty : 0f;
        }
    }
}
