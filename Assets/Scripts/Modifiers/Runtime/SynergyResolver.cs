using System.Collections.Generic;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Проверка условий синергий. Используется дважды: сервисом после применения
    /// модификатора и генератором при расчёте коэффициента k_syn.
    /// </summary>
    public sealed class SynergyResolver
    {
        readonly ModifierCatalog _catalog;
        readonly List<SynergyDefinition> _buffer = new List<SynergyDefinition>();

        public SynergyResolver(ModifierCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>Синергии, условия которых выполнены, но которые ещё не активированы.</summary>
        public IReadOnlyList<SynergyDefinition> FindNewlyActivated(BuildState build)
        {
            _buffer.Clear();

            IReadOnlyList<SynergyDefinition> synergies = _catalog.Synergies;

            for (int i = 0; i < synergies.Count; i++)
            {
                SynergyDefinition synergy = synergies[i];

                if (synergy == null || build.HasSynergy(synergy))
                    continue;

                if (synergy.IsSatisfiedBy(build))
                    _buffer.Add(synergy);
            }

            return _buffer;
        }

        /// <summary>Добавление кандидата активирует хотя бы одну новую синергию.</summary>
        public bool WouldActivate(BuildState build, string candidateId)
        {
            IReadOnlyList<SynergyDefinition> synergies = _catalog.Synergies;

            for (int i = 0; i < synergies.Count; i++)
            {
                SynergyDefinition synergy = synergies[i];

                if (synergy == null || build.HasSynergy(synergy))
                    continue;

                if (synergy.WouldBeSatisfiedWith(build, candidateId))
                    return true;
            }

            return false;
        }
    }
}
