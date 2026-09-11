using System.Collections.Generic;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Формирование набора предложений на экране повышения уровня.
    /// Подмена реализации составляет суть эксперимента: равновероятная выборка
    /// служит контрольной группой, взвешенная — экспериментальной.
    /// </summary>
    public interface IOfferGenerator
    {
        IReadOnlyList<ModifierDefinition> Generate(BuildState build, RunContext context, int count);

        /// <summary>Выборка с ограничением пула (ставка казино, гарантия синергии).</summary>
        IReadOnlyList<ModifierDefinition> Generate(BuildState build, RunContext context, int count, OfferConstraint constraint);

        /// <summary>Есть ли хотя бы один допустимый кандидат под ограничением. Состояние не меняет.</summary>
        bool HasCandidates(BuildState build, RunContext context, OfferConstraint constraint);

        /// <summary>Порог гарантии синергии; ноль означает, что гарантия выключена.</summary>
        int PityThreshold { get; }

        /// <summary>Сколько выборов сделано с момента последнего замыкания синергии.</summary>
        int PicksSinceLastSynergy { get; }

        /// <summary>Сколько раз гарантия принудительно вмешалась в выборку за сессию.</summary>
        int PityTriggers { get; }
    }

    /// <summary>Источник случайных чисел с фиксируемым зерном — требование воспроизводимости.</summary>
    public interface IRandomSource
    {
        float NextFloat();
    }

    public sealed class SeededRandom : IRandomSource
    {
        readonly System.Random _random;

        public int Seed { get; }

        public SeededRandom(int seed)
        {
            Seed = seed;
            _random = new System.Random(seed);
        }

        public float NextFloat() => (float)_random.NextDouble();
    }

    /// <summary>Доступность модификаторов, определяемая покупками в гараже.</summary>
    public interface IUnlockProvider
    {
        bool IsUnlocked(string modifierId);
    }

    public sealed class AllUnlocked : IUnlockProvider
    {
        public bool IsUnlocked(string modifierId) => true;
    }
}
