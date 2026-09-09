using System;
using System.Collections.Generic;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Применение выбранного модификатора и пересчёт состояния билда.
    ///
    /// Пересчёт полный, а не инкрементальный: результат зависит только от текущего
    /// состава билда и не накапливает погрешность при стакинге и наложении синергий.
    /// Повышение уровня не требует отдельной ветки — меняется число в BuildState.
    /// </summary>
    public sealed class ModifierService
    {
        public event Action<ModifierDefinition, int> Applied;
        public event Action<SynergyDefinition> SynergyActivated;

        readonly ModifierCatalog _catalog;
        readonly BuildState _build;
        readonly EffectContext _effects;
        readonly SynergyResolver _synergies;
        readonly ISocketProvider _sockets;
        readonly IBaseStatsProvider _baseStats;

        readonly List<SynergyDefinition> _activatedBuffer = new List<SynergyDefinition>();

        public ModifierService(ModifierCatalog catalog,
                               BuildState build,
                               EffectContext effects,
                               SynergyResolver synergies,
                               ISocketProvider sockets,
                               IBaseStatsProvider baseStats)
        {
            _catalog = catalog;
            _build = build;
            _effects = effects;
            _synergies = synergies;
            _sockets = sockets;
            _baseStats = baseStats;
        }

        public void Apply(ModifierDefinition definition)
        {
            if (definition == null)
                return;

            int level = _build.Increment(definition.Id);

            if (definition.RequiresSocket)
                _sockets.Mount(definition, level);

            _activatedBuffer.Clear();
            _activatedBuffer.AddRange(_synergies.FindNewlyActivated(_build));

            for (int i = 0; i < _activatedBuffer.Count; i++)
                _build.AddSynergy(_activatedBuffer[i]);

            Recalculate();

            // События поднимаются после пересчёта, чтобы подписчики видели согласованное состояние.
            for (int i = 0; i < _activatedBuffer.Count; i++)
                SynergyActivated?.Invoke(_activatedBuffer[i]);

            Applied?.Invoke(definition, level);
        }

        /// <summary>Полный пересчёт от базовых характеристик машины. Идемпотентен.</summary>
        public void Recalculate()
        {
            _effects.Stats.ResetToBase(_baseStats?.GetBaseStats());
            _effects.Clear();

            foreach (KeyValuePair<string, int> pair in _build.Levels)
            {
                if (pair.Value <= 0 || !_catalog.TryGet(pair.Key, out ModifierDefinition definition))
                    continue;

                ApplyEffects(definition.Effects, pair.Value);
            }

            IReadOnlyList<SynergyDefinition> active = _build.ActiveSynergies;

            for (int i = 0; i < active.Count; i++)
                ApplyEffects(active[i].Effects, 1);
        }

        public void ResetRun()
        {
            _build.Reset();
            _sockets.Reset();
            Recalculate();
        }

        void ApplyEffects(List<ModifierEffect> effects, int level)
        {
            if (effects == null)
                return;

            for (int i = 0; i < effects.Count; i++)
                effects[i]?.Contribute(_effects, level);
        }
    }
}
