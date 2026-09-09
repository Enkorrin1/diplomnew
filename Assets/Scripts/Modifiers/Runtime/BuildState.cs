using System.Collections.Generic;
using System.Text;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Состав билда за один заезд: уровни выбранных модификаторов и активные синергии.
    /// Модификаторы не переносятся между заездами, поэтому объект живёт в рамках сессии.
    /// </summary>
    public sealed class BuildState
    {
        readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
        readonly List<SynergyDefinition> _synergies = new List<SynergyDefinition>();

        public IReadOnlyDictionary<string, int> Levels => _levels;
        public IReadOnlyList<SynergyDefinition> ActiveSynergies => _synergies;

        /// <summary>Общее число сделанных выборов, включая повышения уровня.</summary>
        public int TotalPicks { get; private set; }

        public int GetLevel(string modifierId)
        {
            if (string.IsNullOrEmpty(modifierId))
                return 0;

            return _levels.TryGetValue(modifierId, out int level) ? level : 0;
        }

        public bool Has(string modifierId) => GetLevel(modifierId) > 0;

        public int Increment(string modifierId)
        {
            int level = GetLevel(modifierId) + 1;
            _levels[modifierId] = level;
            TotalPicks++;
            return level;
        }

        public bool HasSynergy(SynergyDefinition synergy) =>
            synergy != null && _synergies.Contains(synergy);

        public void AddSynergy(SynergyDefinition synergy)
        {
            if (synergy != null && !_synergies.Contains(synergy))
                _synergies.Add(synergy);
        }

        public void Reset()
        {
            _levels.Clear();
            _synergies.Clear();
            TotalPicks = 0;
        }

        /// <summary>
        /// Устойчивое строковое представление билда для группировки прогонов
        /// при расчёте энтропии распределения билдов.
        /// </summary>
        public string Signature()
        {
            if (_levels.Count == 0)
                return string.Empty;

            var ids = new List<string>(_levels.Keys);
            ids.Sort(System.StringComparer.Ordinal);

            var builder = new StringBuilder();

            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                    builder.Append('|');

                builder.Append(ids[i]).Append(':').Append(_levels[ids[i]]);
            }

            return builder.ToString();
        }
    }
}
