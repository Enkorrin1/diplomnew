using System;
using System.Collections.Generic;
using RogueDrive.Modifiers;
using UnityEngine;

namespace RogueDrive.Simulation
{
    [Serializable]
    public struct WeightingVariant
    {
        [Tooltip("Метка варианта в выгрузке.")]
        public string Name;

        [Tooltip("Пусто означает равновероятную выборку — контрольную группу.")]
        public WeightingConfig Config;
    }

    /// <summary>
    /// Конфигурация серии прогонов. Фиксированное зерно обеспечивает
    /// воспроизводимость результатов — требование к экспериментальной части.
    /// </summary>
    [CreateAssetMenu(fileName = "SimulationBatch", menuName = "RogueDrive/Simulation Batch")]
    public class SimulationBatch : ScriptableObject
    {
        [Header("Входные данные")]
        public ModifierCatalog Catalog;
        public CarDefinition Car;
        public DifficultyProfile Difficulty;

        [Header("Сравниваемые конфигурации")]
        public WeightingVariant[] Variants = new WeightingVariant[0];

        [Header("Стратегии игрока")]
        public AgentKind[] Agents = { AgentKind.Priority };

        [Header("Параметры серии")]
        [Min(1)] public int RunsPerVariant = 1000;
        public int BaseSeed = 20260907;
        [Min(0.05f)] public float StepSeconds = 0.25f;
        [Min(10f)] public float MaxRunSeconds = 600f;
        [Min(2)] public int OffersPerLevel = 3;

        [Header("Выгрузка")]
        public string OutputDirectory = "Simulation/Output";

        public bool Validate(out string error)
        {
            if (Catalog == null) { error = "Не задан каталог модификаторов."; return false; }
            if (Car == null) { error = "Не задан автомобиль."; return false; }
            if (Difficulty == null) { error = "Не задан профиль сложности."; return false; }
            if (Variants == null || Variants.Length == 0) { error = "Не задано ни одной конфигурации."; return false; }
            if (Agents == null || Agents.Length == 0) { error = "Не задана стратегия игрока."; return false; }

            error = null;
            return true;
        }

        public IEnumerable<SimulationSetup> EnumerateSetups()
        {
            foreach (WeightingVariant variant in Variants)
            {
                foreach (AgentKind agent in Agents)
                {
                    yield return new SimulationSetup
                    {
                        Catalog = Catalog,
                        Car = Car,
                        Difficulty = Difficulty,
                        Weighting = variant.Config,
                        ConfigurationName = string.IsNullOrEmpty(variant.Name) ? "unnamed" : variant.Name,
                        Agent = agent,
                        StepSeconds = StepSeconds,
                        MaxRunSeconds = MaxRunSeconds,
                        OffersPerLevel = OffersPerLevel
                    };
                }
            }
        }
    }
}
