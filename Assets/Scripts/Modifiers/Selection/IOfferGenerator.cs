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
