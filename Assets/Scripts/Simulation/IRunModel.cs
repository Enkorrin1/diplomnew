using RogueDrive.Modifiers;

namespace RogueDrive.Simulation
{
    public struct StepOutcome
    {
        public int Kills;
        public float DamageTaken;
        public float FuelSpent;
        public float DistanceGained;
    }

    /// <summary>
    /// Модель течения заезда. Вынесена в интерфейс сознательно.
    ///
    /// Изучаемая часть системы — отбор и применение модификаторов — в симуляции та же,
    /// что и в игре: используются те же каталог, генератор предложений и сервис.
    /// Боевая часть в массовых прогонах заменяется аналитической моделью ради скорости;
    /// интерфейс оставляет возможность подставить прогон настоящей сцены на ускоренном
    /// времени для проверки согласованности модели с игрой.
    /// </summary>
    public interface IRunModel
    {
        StepOutcome Step(RunContext context, EffectContext effects, float deltaTime);
        bool IsFinished(RunContext context);
    }
}
