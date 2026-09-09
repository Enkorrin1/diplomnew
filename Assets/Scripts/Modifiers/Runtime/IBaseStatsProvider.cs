using System.Collections.Generic;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Источник базовых характеристик для пересчёта билда.
    ///
    /// Вынесен в интерфейс, потому что база складывается из двух вкладов:
    /// характеристик архетипа машины и постоянных улучшений, купленных в гараже.
    /// Сессионные модификаторы накладываются поверх уже суммированной базы.
    /// </summary>
    public interface IBaseStatsProvider
    {
        IEnumerable<StatValue> GetBaseStats();
    }

    /// <summary>Голые характеристики машины без учёта мета-прогресса.</summary>
    public sealed class CarBaseStats : IBaseStatsProvider
    {
        readonly CarDefinition _car;

        public CarBaseStats(CarDefinition car) => _car = car;

        public IEnumerable<StatValue> GetBaseStats()
        {
            return _car != null ? _car.BaseStats : System.Array.Empty<StatValue>();
        }
    }
}
