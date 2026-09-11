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

    public static class CasinoBetRules
    {
        public static int Cost(CasinoBet bet)
        {
            switch (bet)
            {
                case CasinoBet.RareGuaranteed: return 2;
                case CasinoBet.SynergyHunt: return 3;
                default: return 1;
            }
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
