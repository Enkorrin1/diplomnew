namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Ставка в придорожном казино. Игрок не выбирает модификатор, но решает,
    /// сколько жетонов поставить и тем самым сужает пул, из которого слот-машина
    /// делает выборку. Алгоритм взвешивания при этом остаётся единственным
    /// механизмом отбора — ставка лишь задаёт ограничение на кандидатов.
    /// </summary>
    public enum CasinoBet
    {
        /// <summary>Один жетон, полный пул.</summary>
        Standard = 0,

        /// <summary>Два жетона, в пуле остаются только редкие и эпические модификаторы.</summary>
        RareGuaranteed = 1,

        /// <summary>Три жетона, в пуле остаются только кандидаты, замыкающие синергию.</summary>
        SynergyHunt = 2
    }

    /// <summary>
    /// Цены ставок. Вынесены в конфигурацию, чтобы подбирать их симуляцией:
    /// при слишком дорогих ставках игрок теряет модификаторы и ставки становятся ловушкой,
    /// при слишком дешёвых обычный спин теряет смысл.
    /// </summary>
    [System.Serializable]
    public sealed class CasinoBetPricing
    {
        [UnityEngine.Min(1)] public int RareBetCost = 2;
        [UnityEngine.Min(1)] public int SynergyBetCost = 2;

        [UnityEngine.Tooltip("Сколько жетонов возвращается, если ставка «Синергия» действительно замкнула синергию.")]
        [UnityEngine.Min(0)] public int SynergyBetRefund = 1;

        [UnityEngine.Tooltip("Скидка на ставки, когда все сокеты корпуса заняты. Свободных сокетов больше нет, " +
                             "лишний обычный спин даёт только слабое улучшение — качество выборки должно дешеветь. " +
                             "Ноль выключает правило.")]
        [UnityEngine.Min(0)] public int FullCarDiscount = 1;

        [UnityEngine.Tooltip("Банк казино: за каждые столько жетонов, пронесённых мимо пункта, на следующем " +
                             "начисляется один дополнительный. Ноль выключает правило.")]
        [UnityEngine.Min(0)] public int CarryOverBonusPer = 2;

        public static CasinoBetPricing Default => new CasinoBetPricing();

        public int Cost(CasinoBet bet)
        {
            return Cost(bet, false);
        }

        /// <param name="carFull">Все сокеты корпуса заняты — действует скидка на ставки.</param>
        public int Cost(CasinoBet bet, bool carFull)
        {
            int cost;

            switch (bet)
            {
                case CasinoBet.RareGuaranteed: cost = UnityEngine.Mathf.Max(1, RareBetCost); break;
                case CasinoBet.SynergyHunt: cost = UnityEngine.Mathf.Max(1, SynergyBetCost); break;
                default: return 1;
            }

            if (carFull && FullCarDiscount > 0)
                cost = UnityEngine.Mathf.Max(1, cost - FullCarDiscount);

            return cost;
        }

        public int Refund(CasinoBet bet, bool closedSynergy)
        {
            return Refund(bet, closedSynergy, false);
        }

        public int Refund(CasinoBet bet, bool closedSynergy, bool carFull)
        {
            if (bet != CasinoBet.SynergyHunt || !closedSynergy)
                return 0;

            return UnityEngine.Mathf.Clamp(SynergyBetRefund, 0, Cost(bet, carFull) - 1);
        }

        /// <summary>
        /// Начисление банка при въезде в казино: сколько дополнительных жетонов даётся
        /// за то, что предыдущий пункт был пройден с непотраченными жетонами.
        /// </summary>
        public int CarryOverBonus(int bankedTokens)
        {
            if (CarryOverBonusPer <= 0 || bankedTokens <= 0)
                return 0;

            return bankedTokens / CarryOverBonusPer;
        }
    }

    public static class CasinoBetRules
    {
        public static int Cost(CasinoBet bet, CasinoBetPricing pricing)
        {
            return (pricing ?? CasinoBetPricing.Default).Cost(bet);
        }

        public static string DisplayName(CasinoBet bet)
        {
            switch (bet)
            {
                case CasinoBet.RareGuaranteed: return "Редкий+";
                case CasinoBet.SynergyHunt: return "Синергия";
                default: return "Обычный";
            }
        }
    }

    /// <summary>
    /// Ограничение на кандидатов одной выборки. Пустое ограничение эквивалентно
    /// обычному вызову генератора, поэтому старые вызовы ничего не меняют.
    /// </summary>
    public struct OfferConstraint
    {
        public Rarity MinRarity;
        public bool RequireSynergyClosing;

        public static OfferConstraint None => default;

        public bool IsEmpty => MinRarity == Rarity.Common && !RequireSynergyClosing;

        public static OfferConstraint ForBet(CasinoBet bet)
        {
            switch (bet)
            {
                case CasinoBet.RareGuaranteed:
                    return new OfferConstraint { MinRarity = Rarity.Rare };

                case CasinoBet.SynergyHunt:
                    return new OfferConstraint { RequireSynergyClosing = true };

                default:
                    return None;
            }
        }
    }

    /// <summary>
    /// Правила формирования пула, не зависящие от весов. Часть предлагаемого
    /// алгоритма: гарантия от невезения и запрет новых модулей при укомплектованной
    /// машине. В нейтральной конфигурации оба правила выключены.
    /// </summary>
    public struct PoolRules
    {
        /// <summary>
        /// Сколько выборов подряд без замыкания синергии допускается, прежде чем
        /// следующая выборка обязана содержать замыкающий кандидат. Ноль — выключено.
        /// </summary>
        public int PityThreshold;

        /// <summary>
        /// Когда все сокеты корпуса заняты, новые модификаторы не предлагаются —
        /// только повышение уровня уже установленных.
        /// </summary>
        public bool LockNewModulesWhenSocketsFull;

        public static PoolRules Disabled => default;
    }
}
