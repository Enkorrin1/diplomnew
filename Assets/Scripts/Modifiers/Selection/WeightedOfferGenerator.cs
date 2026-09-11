using System.Collections.Generic;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Адаптивное взвешивание пула — предлагаемый в работе алгоритм.
    ///
    ///     w(i) = w_base(rarity) * k_stack(level) * k_syn * k_role
    ///
    /// Экспериментальная группа. При нейтральной конфигурации вырождается
    /// в равновероятную выборку, что даёт непрерывный ряд промежуточных настроек.
    /// Правила пула (гарантия синергии, укомплектованная машина) берутся из той же
    /// конфигурации, поэтому абляция любого элемента алгоритма сводится к правке ассета.
    /// </summary>
    public sealed class WeightedOfferGenerator : OfferGeneratorBase
    {
        readonly WeightingConfig _config;

        public WeightedOfferGenerator(ModifierCatalog catalog,
                                      ISocketProvider sockets,
                                      IUnlockProvider unlocks,
                                      IRandomSource random,
                                      WeightingConfig config,
                                      SynergyResolver synergies)
            : base(catalog, sockets, unlocks, random, synergies, config != null ? config.GetPoolRules() : PoolRules.Disabled)
        {
            _config = config;
        }

        protected override float GetWeight(ModifierDefinition definition, BuildState build, RunContext context)
        {
            float weight = _config.GetRarityWeight(definition.Rarity);
            weight *= _config.GetStackFalloff(build.GetLevel(definition.Id));

            if (ClosesSynergy(definition, build))
                weight *= _config.SynergyBonus;

            if (CompensatesWeakness(definition, context))
                weight *= _config.RoleCompensation;

            return weight;
        }

        /// <summary>
        /// Кандидат закрывает слабое место: ресурс просел ниже порога, а модификаторов
        /// соответствующей категории в билде ещё нет.
        /// </summary>
        bool CompensatesWeakness(ModifierDefinition definition, RunContext context)
        {
            if (definition.Category != ModifierCategory.Defensive || context == null)
                return false;

            bool lowResource = context.HealthFraction < _config.LowResourceThreshold
                            || context.FuelFraction < _config.LowResourceThreshold;

            if (!lowResource)
                return false;

            return !HasCategory(context.Build, ModifierCategory.Defensive);
        }

        bool HasCategory(BuildState build, ModifierCategory category)
        {
            foreach (KeyValuePair<string, int> pair in build.Levels)
            {
                if (pair.Value <= 0)
                    continue;

                if (Catalog.TryGet(pair.Key, out ModifierDefinition definition)
                    && definition.Category == category)
                    return true;
            }

            return false;
        }
    }
}
