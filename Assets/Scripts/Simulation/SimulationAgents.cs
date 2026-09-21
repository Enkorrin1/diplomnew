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
        /// <param name="carFull">Все сокеты заняты — ставки идут со скидкой.</param>
        CasinoBet ChooseBet(int tokens, ModifierSession session, CasinoBetPricing pricing, bool carFull);

        /// <summary>
        /// Уйти с пункта, не потратив оставшиеся жетоны. Пронесённые жетоны попадают
        /// в банк казино и на следующем пункте дают начисление, а на финише этапа —
        /// монеты. Ложь означает прежнее поведение: тратить всё до последнего жетона.
        /// </summary>
        bool HoldTokens(int tokens, ModifierSession session, CasinoBetPricing pricing, bool lastStop);
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

        public CasinoBet ChooseBet(int tokens, ModifierSession session, CasinoBetPricing pricing, bool carFull) => CasinoBet.Standard;

        public bool HoldTokens(int tokens, ModifierSession session, CasinoBetPricing pricing, bool lastStop) => false;
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

        public CasinoBet ChooseBet(int tokens, ModifierSession session, CasinoBetPricing pricing, bool carFull)
        {
            return CasinoBetHeuristics.Greedy(tokens, session, pricing, carFull);
        }

        public bool HoldTokens(int tokens, ModifierSession session, CasinoBetPricing pricing, bool lastStop) => false;
    }

    /// <summary>Общая жадная политика ставок: самая узкая доступная ставка.</summary>
    static class CasinoBetHeuristics
    {
        public static CasinoBet Greedy(int tokens, ModifierSession session, CasinoBetPricing pricing, bool carFull)
        {
            IOfferGenerator generator = session.OfferGenerator;
            pricing = pricing ?? CasinoBetPricing.Default;

            if (tokens >= pricing.Cost(CasinoBet.SynergyHunt, carFull)
                && generator.HasCandidates(session.Build, session.Context, OfferConstraint.ForBet(CasinoBet.SynergyHunt)))
                return CasinoBet.SynergyHunt;

            if (tokens >= pricing.Cost(CasinoBet.RareGuaranteed, carFull)
                && generator.HasCandidates(session.Build, session.Context, OfferConstraint.ForBet(CasinoBet.RareGuaranteed)))
                return CasinoBet.RareGuaranteed;

            return CasinoBet.Standard;
        }
    }

    /// <summary>
    /// Вкладчик: ставит только тогда, когда жетонов хватает на суженный пул, а остаток
    /// проносит мимо пункта — на следующем пункте банк казино начислит за него надбавку.
    /// Проверяет, окупается ли отложенный спин по сравнению с немедленным обычным.
    /// </summary>
    public sealed class CasinoBankerAgent : ISimulationAgent, ICasinoPolicy
    {
        public string Name => "casino_banker";

        public int Choose(IReadOnlyList<ModifierDefinition> offers, RunContext context)
        {
            return offers.Count == 0 ? -1 : 0;
        }

        public CasinoBet ChooseBet(int tokens, ModifierSession session, CasinoBetPricing pricing, bool carFull)
        {
            return CasinoBetHeuristics.Greedy(tokens, session, pricing, carFull);
        }

        public bool HoldTokens(int tokens, ModifierSession session, CasinoBetPricing pricing, bool lastStop)
        {
            pricing = pricing ?? CasinoBetPricing.Default;

            // На последнем пункте копить не для чего: банк больше не начислит.
            if (lastStop || tokens <= 0 || pricing.CarryOverBonusPer <= 0)
                return false;

            // Остаток слишком мал для ставки, но достаточен, чтобы банк за него доплатил.
            return tokens < pricing.Cost(CasinoBet.RareGuaranteed)
                && tokens >= pricing.CarryOverBonusPer;
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
