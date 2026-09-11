using System.Collections.Generic;
using RogueDrive.Modifiers;

namespace RogueDrive.Simulation
{
    /// <summary>
    /// Стратегия выбора модификатора, заменяющая игрока в симуляции.
    /// Разные стратегии позволяют отделить влияние алгоритма предложений
    /// от влияния поведения игрока.
    /// </summary>
    public interface ISimulationAgent
    {
        string Name { get; }
        int Choose(IReadOnlyList<ModifierDefinition> offers, RunContext context);
    }

    /// <summary>
    /// Политика ставок в казино: сколько жетонов поставить при очередном вращении.
    /// Отделена от выбора модификатора, потому что в казино выбора нет — есть только ставка.
    /// </summary>
    public interface ICasinoPolicy
    {
        CasinoBet ChooseBet(int tokens, ModifierSession session);
    }

    /// <summary>Случайный выбор: нижняя граница качества решений.</summary>
    public sealed class RandomAgent : ISimulationAgent
    {
        readonly IRandomSource _random;

        public RandomAgent(IRandomSource random) => _random = random;

        public string Name => "random";

        public int Choose(IReadOnlyList<ModifierDefinition> offers, RunContext context)
        {
            if (offers.Count == 0)
                return -1;

            int index = (int)(_random.NextFloat() * offers.Count);
            return index >= offers.Count ? offers.Count - 1 : index;
        }
    }

    /// <summary>
    /// Казино: выбора нет, берётся первый элемент выборки генератора — ровно так
    /// придорожный пункт в игре выдаёт баф за жетон. Жетоны копятся по одному за
    /// уровень и тратятся только в пунктах на трассе, всегда обычной ставкой.
    /// Позволяет измерить вклад алгоритма взвешивания в чистом виде.
    /// </summary>
    public sealed class CasinoAgent : ISimulationAgent, ICasinoPolicy
    {
        public string Name => "casino";

        public int Choose(IReadOnlyList<ModifierDefinition> offers, RunContext context)
        {
            return offers.Count == 0 ? -1 : 0;
        }

        public CasinoBet ChooseBet(int tokens, ModifierSession session) => CasinoBet.Standard;
    }

    /// <summary>
    /// Азартный игрок казино: при трёх жетонах и незамкнутой синергии ставит на
    /// синергию, при двух — на редкость, иначе обычный спин. Измеряет, что даёт
    /// игроку сам рычаг ставок поверх того же алгоритма взвешивания.
    /// </summary>
    public sealed class CasinoBettorAgent : ISimulationAgent, ICasinoPolicy
    {
        public string Name => "casino_bettor";

        public int Choose(IReadOnlyList<ModifierDefinition> offers, RunContext context)
        {
            return offers.Count == 0 ? -1 : 0;
        }

        public CasinoBet ChooseBet(int tokens, ModifierSession session)
        {
            IOfferGenerator generator = session.OfferGenerator;

            if (tokens >= CasinoBetRules.Cost(CasinoBet.SynergyHunt)
                && generator.HasCandidates(session.Build, session.Context, OfferConstraint.ForBet(CasinoBet.SynergyHunt)))
                return CasinoBet.SynergyHunt;

            if (tokens >= CasinoBetRules.Cost(CasinoBet.RareGuaranteed)
                && generator.HasCandidates(session.Build, session.Context, OfferConstraint.ForBet(CasinoBet.RareGuaranteed)))
                return CasinoBet.RareGuaranteed;

            return CasinoBet.Standard;
        }
    }

    /// <summary>
    /// Осторожный игрок: берёт защиту при просевшем ресурсе, иначе усиливает атаку.
    /// Приближает поведение среднего игрока без знания синергий.
    /// </summary>
    public sealed class PriorityAgent : ISimulationAgent
    {
        readonly float _lowResourceThreshold;

        public PriorityAgent(float lowResourceThreshold = 0.4f)
        {
            _lowResourceThreshold = lowResourceThreshold;
        }

        public string Name => "priority";

        public int Choose(IReadOnlyList<ModifierDefinition> offers, RunContext context)
        {
            if (offers.Count == 0)
                return -1;

            bool needsDefense = context.HealthFraction < _lowResourceThreshold
                             || context.FuelFraction < _lowResourceThreshold;

            ModifierCategory preferred = needsDefense
                ? ModifierCategory.Defensive
                : ModifierCategory.Offensive;

            for (int i = 0; i < offers.Count; i++)
                if (offers[i].Category == preferred)
                    return i;

            return 0;
        }
    }

    /// <summary>
    /// Опытный игрок: осознанно достраивает синергии.
    /// Верхняя граница качества решений.
    /// </summary>
    public sealed class SynergySeekingAgent : ISimulationAgent
    {
        readonly SynergyResolver _synergies;
        readonly ISimulationAgent _fallback;

        public SynergySeekingAgent(SynergyResolver synergies, ISimulationAgent fallback)
        {
            _synergies = synergies;
            _fallback = fallback;
        }

        public string Name => "synergy";

        public int Choose(IReadOnlyList<ModifierDefinition> offers, RunContext context)
        {
            for (int i = 0; i < offers.Count; i++)
                if (_synergies.WouldActivate(context.Build, offers[i].Id))
                    return i;

            return _fallback.Choose(offers, context);
        }
    }
}
