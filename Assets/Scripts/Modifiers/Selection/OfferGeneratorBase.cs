using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Общая часть генераторов: фильтр допустимых кандидатов и выборка без возвращения.
    /// Правило допуска одинаково для контрольной и экспериментальной групп, поэтому
    /// различия в результатах объясняются только весами и правилами пула.
    ///
    /// Помимо весов здесь реализованы два правила, общие для казино и симуляции:
    /// ограничение пула по ставке (<see cref="OfferConstraint"/>) и гарантия от
    /// невезения (<see cref="PoolRules.PityThreshold"/>): после N выборов подряд без
    /// замыкания синергии первая позиция выборки обязана замыкать синергию, если
    /// такой кандидат вообще существует.
    /// </summary>
    public abstract class OfferGeneratorBase : IOfferGenerator
    {
        protected readonly ModifierCatalog Catalog;
        protected readonly ISocketProvider Sockets;
        protected readonly IUnlockProvider Unlocks;
        protected readonly IRandomSource Random;
        protected readonly SynergyResolver Synergies;
        protected readonly PoolRules Rules;

        readonly List<ModifierDefinition> _candidates = new List<ModifierDefinition>();
        readonly List<float> _weights = new List<float>();
        readonly List<ModifierDefinition> _result = new List<ModifierDefinition>();

        // Учёт гарантии: счётчик выборов с последнего замыкания синергии
        // восстанавливается по состоянию билда, поэтому не зависит от того,
        // кто применял модификатор — казино, игрок или агент симуляции.
        int _synergiesSeen;
        int _picksAtLastSynergy;
        int _pityTriggers;

        protected OfferGeneratorBase(ModifierCatalog catalog,
                                     ISocketProvider sockets,
                                     IUnlockProvider unlocks,
                                     IRandomSource random,
                                     SynergyResolver synergies = null,
                                     PoolRules rules = default)
        {
            Catalog = catalog;
            Sockets = sockets;
            Unlocks = unlocks ?? new AllUnlocked();
            Random = random ?? new SeededRandom(0);
            Synergies = synergies;
            Rules = rules;
        }

        public int PityThreshold => Synergies != null ? Mathf.Max(0, Rules.PityThreshold) : 0;

        public int PicksSinceLastSynergy { get; private set; }

        public int PityTriggers => _pityTriggers;

        public IReadOnlyList<ModifierDefinition> Generate(BuildState build, RunContext context, int count)
        {
            return Generate(build, context, count, OfferConstraint.None);
        }

        public IReadOnlyList<ModifierDefinition> Generate(BuildState build, RunContext context, int count, OfferConstraint constraint)
        {
            _result.Clear();
            RefreshPityCounter(build);
            CollectCandidates(build, context, constraint);

            if (_candidates.Count == 0)
                return _result;

            // Гарантия от невезения: первая позиция обязана замыкать синергию.
            // Ставка «Синергия» уже сузила пул, поэтому там гарантия не нужна.
            bool pityDue = PityThreshold > 0
                        && !constraint.RequireSynergyClosing
                        && PicksSinceLastSynergy >= PityThreshold;

            if (pityDue && TakeSynergyClosingFirst(build))
                _pityTriggers++;

            for (int picked = _result.Count; picked < count && _candidates.Count > 0; picked++)
                TakeOne();

            return _result;
        }

        public bool HasCandidates(BuildState build, RunContext context, OfferConstraint constraint)
        {
            IReadOnlyList<ModifierDefinition> all = Catalog.All;

            for (int i = 0; i < all.Count; i++)
            {
                ModifierDefinition definition = all[i];

                if (definition == null || !IsEligible(definition, build) || !Satisfies(definition, build, constraint))
                    continue;

                if (GetWeight(definition, build, context) > 0f)
                    return true;
            }

            return false;
        }

        protected abstract float GetWeight(ModifierDefinition definition, BuildState build, RunContext context);

        /// <summary>
        /// Кандидат допустим, если разблокирован, не достиг предельного уровня и либо
        /// уже установлен, либо для него есть свободный сокет требуемого типа.
        /// При включённом правиле укомплектованной машины новые модификаторы не
        /// допускаются, пока все сокеты заняты.
        /// </summary>
        protected virtual bool IsEligible(ModifierDefinition definition, BuildState build)
        {
            if (!Unlocks.IsUnlocked(definition.Id))
                return false;

            int level = build.GetLevel(definition.Id);

            if (level >= definition.MaxLevel)
                return false;

            if (level == 0)
            {
                if (Rules.LockNewModulesWhenSocketsFull && Sockets != null && Sockets.AllOccupied)
                    return false;

                if (definition.RequiresSocket && !Sockets.HasFree(definition.RequiredSocket))
                    return false;
            }

            return true;
        }

        protected bool ClosesSynergy(ModifierDefinition definition, BuildState build)
        {
            return Synergies != null && Synergies.WouldActivate(build, definition.Id);
        }

        bool Satisfies(ModifierDefinition definition, BuildState build, OfferConstraint constraint)
        {
            if (definition.Rarity < constraint.MinRarity)
                return false;

            if (constraint.RequireSynergyClosing && !ClosesSynergy(definition, build))
                return false;

            return true;
        }

        void CollectCandidates(BuildState build, RunContext context, OfferConstraint constraint)
        {
            _candidates.Clear();
            _weights.Clear();

            IReadOnlyList<ModifierDefinition> all = Catalog.All;

            for (int i = 0; i < all.Count; i++)
            {
                ModifierDefinition definition = all[i];

                if (definition == null || !IsEligible(definition, build) || !Satisfies(definition, build, constraint))
                    continue;

                float weight = GetWeight(definition, build, context);

                if (weight <= 0f)
                    continue;

                _candidates.Add(definition);
                _weights.Add(weight);
            }
        }

        void RefreshPityCounter(BuildState build)
        {
            if (build == null)
                return;

            int synergies = build.ActiveSynergies.Count;

            if (synergies != _synergiesSeen)
            {
                _synergiesSeen = synergies;
                _picksAtLastSynergy = build.TotalPicks;
            }

            PicksSinceLastSynergy = Mathf.Max(0, build.TotalPicks - _picksAtLastSynergy);
        }

        /// <summary>Взвешенная выборка среди кандидатов, замыкающих синергию. Ложь, если таких нет.</summary>
        bool TakeSynergyClosingFirst(BuildState build)
        {
            float total = 0f;

            for (int i = 0; i < _candidates.Count; i++)
                if (ClosesSynergy(_candidates[i], build))
                    total += _weights[i];

            if (total <= 0f)
                return false;

            float roll = Random.NextFloat() * total;
            int chosen = -1;

            for (int i = 0; i < _candidates.Count; i++)
            {
                if (!ClosesSynergy(_candidates[i], build))
                    continue;

                chosen = i;
                roll -= _weights[i];

                if (roll <= 0f)
                    break;
            }

            if (chosen < 0)
                return false;

            _result.Add(_candidates[chosen]);
            _candidates.RemoveAt(chosen);
            _weights.RemoveAt(chosen);
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
