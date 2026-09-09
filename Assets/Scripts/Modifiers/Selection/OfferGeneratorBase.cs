using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Общая часть генераторов: фильтр допустимых кандидатов и выборка без возвращения.
    /// Правило допуска одинаково для контрольной и экспериментальной групп, поэтому
    /// различия в результатах объясняются только весами.
    /// </summary>
    public abstract class OfferGeneratorBase : IOfferGenerator
    {
        protected readonly ModifierCatalog Catalog;
        protected readonly ISocketProvider Sockets;
        protected readonly IUnlockProvider Unlocks;
        protected readonly IRandomSource Random;

        readonly List<ModifierDefinition> _candidates = new List<ModifierDefinition>();
        readonly List<float> _weights = new List<float>();
        readonly List<ModifierDefinition> _result = new List<ModifierDefinition>();

        protected OfferGeneratorBase(ModifierCatalog catalog,
                                     ISocketProvider sockets,
                                     IUnlockProvider unlocks,
                                     IRandomSource random)
        {
            Catalog = catalog;
            Sockets = sockets;
            Unlocks = unlocks ?? new AllUnlocked();
            Random = random ?? new SeededRandom(0);
        }

        public IReadOnlyList<ModifierDefinition> Generate(BuildState build, RunContext context, int count)
        {
            _candidates.Clear();
            _weights.Clear();
            _result.Clear();

            IReadOnlyList<ModifierDefinition> all = Catalog.All;

            for (int i = 0; i < all.Count; i++)
            {
                ModifierDefinition definition = all[i];

                if (definition == null || !IsEligible(definition, build))
                    continue;

                float weight = GetWeight(definition, build, context);

                if (weight <= 0f)
                    continue;

                _candidates.Add(definition);
                _weights.Add(weight);
            }

            for (int picked = 0; picked < count && _candidates.Count > 0; picked++)
                TakeOne();

            return _result;
        }

        protected abstract float GetWeight(ModifierDefinition definition, BuildState build, RunContext context);

        /// <summary>
        /// Кандидат допустим, если разблокирован, не достиг предельного уровня и либо
        /// уже установлен, либо для него есть свободный сокет требуемого типа.
        /// </summary>
        protected virtual bool IsEligible(ModifierDefinition definition, BuildState build)
        {
            if (!Unlocks.IsUnlocked(definition.Id))
                return false;

            int level = build.GetLevel(definition.Id);

            if (level >= definition.MaxLevel)
                return false;

            if (level == 0 && definition.RequiresSocket && !Sockets.HasFree(definition.RequiredSocket))
                return false;

            return true;
        }

        void TakeOne()
        {
            float total = 0f;

            for (int i = 0; i < _weights.Count; i++)
                total += _weights[i];

            int chosen;

            if (total <= 0f)
            {
                chosen = Mathf.Min((int)(Random.NextFloat() * _candidates.Count), _candidates.Count - 1);
            }
            else
            {
                float roll = Random.NextFloat() * total;
                chosen = _candidates.Count - 1;

                for (int i = 0; i < _candidates.Count; i++)
                {
                    roll -= _weights[i];

                    if (roll <= 0f)
                    {
                        chosen = i;
                        break;
                    }
                }
            }

            _result.Add(_candidates[chosen]);
            _candidates.RemoveAt(chosen);
            _weights.RemoveAt(chosen);
        }
    }
}
