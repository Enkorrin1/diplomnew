using System.Collections.Generic;
using RogueDrive.Modifiers;
using RogueDrive.Simulation;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Серия абляции: от полной конфигурации алгоритма по очереди отключается
    /// один элемент (w_base, k_stack, k_syn, k_role, гарантия синергии, правило
    /// укомплектованной машины). Разница с полной конфигурацией показывает вклад
    /// каждого элемента. Ассеты создаются один раз и дальше правятся руками.
    /// </summary>
    public static class AblationBatchAuthoring
    {
        const string Folder = "Assets/Content/Simulation/Ablation";
        const string BasePath = "Assets/Content/Simulation/Weighting_Moderate.asset";
        const string BatchPath = Folder + "/Batch_Ablation.asset";
        const string BaselineBatchPath = "Assets/Content/Simulation/Batch_Baseline.asset";

        sealed class Variant
        {
            public string Name;
            public System.Action<WeightingConfig> Mutate;
        }

        static readonly Variant[] Variants =
        {
            new Variant { Name = "full", Mutate = _ => { } },
            new Variant { Name = "no_w_base", Mutate = c => c.RarityWeights = new[] { 1f, 1f, 1f } },
            new Variant { Name = "no_k_stack", Mutate = c => c.StackFalloff = AnimationCurve.Constant(0f, 16f, 1f) },
            new Variant { Name = "no_k_syn", Mutate = c => c.SynergyBonus = 1f },
            new Variant { Name = "no_k_role", Mutate = c => { c.RoleCompensation = 1f; c.LowResourceThreshold = 0f; } },
            new Variant { Name = "no_pity", Mutate = c => c.PityThreshold = 0 },
            new Variant { Name = "no_lock", Mutate = c => c.LockNewModulesWhenSocketsFull = false },
        };

        [MenuItem("RogueDrive/Симуляция баланса/Абляция/Создать серию абляции", priority = 30)]
        public static void CreateAblationBatch()
        {
            SimulationBatch batch = CreateOrUpdate();
            if (batch != null)
            {
                Selection.activeObject = batch;
                Debug.Log($"[Ablation] Серия абляции готова: {BatchPath} ({batch.Variants.Length} конфигураций).");
            }
        }

        [MenuItem("RogueDrive/Симуляция баланса/Абляция/Запустить серию абляции", priority = 31)]
        public static void RunAblationBatch()
        {
            SimulationBatch batch = CreateOrUpdate();
            if (batch == null) return;

            try
            {
                string directory = SimulationRunner.Execute(batch, (progress, status) =>
                    EditorUtility.DisplayProgressBar("Абляция алгоритма", status, progress));
                Debug.Log($"[Ablation] Результаты записаны в {directory}");
                EditorUtility.RevealInFinder(directory);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Ошибка симуляции", exception.Message, "Понятно");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Пакетный запуск: создать серию абляции и прогнать её без участия редактора.</summary>
        public static void CreateAndRun()
        {
            SimulationBatch batch = CreateOrUpdate();
            if (batch == null)
            {
                Debug.LogError("[Ablation] Серия абляции не создана.");
                EditorApplication.Exit(2);
                return;
            }

            string runsArgument = GetArgument("-runs");
            if (int.TryParse(runsArgument, out int runs) && runs > 0)
                batch.RunsPerVariant = runs;

            try
            {
                string directory = SimulationRunner.Execute(batch);
                Debug.Log($"[Ablation] Результаты записаны в {directory}");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(3);
            }
        }

        static SimulationBatch CreateOrUpdate()
        {
            var baseConfig = AssetDatabase.LoadAssetAtPath<WeightingConfig>(BasePath);
            var baseline = AssetDatabase.LoadAssetAtPath<SimulationBatch>(BaselineBatchPath);

            if (baseConfig == null || baseline == null)
            {
                Debug.LogError("[Ablation] Не найдены Weighting_Moderate.asset или Batch_Baseline.asset.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Content/Simulation", "Ablation");

            var variants = new List<WeightingVariant>();

            foreach (Variant variant in Variants)
            {
                string path = $"{Folder}/Weighting_{variant.Name}.asset";
                var config = AssetDatabase.LoadAssetAtPath<WeightingConfig>(path);

                if (config == null)
                {
                    config = Object.Instantiate(baseConfig);
                    config.name = $"Weighting_{variant.Name}";
                    variant.Mutate(config);
                    AssetDatabase.CreateAsset(config, path);
                }

                variants.Add(new WeightingVariant { Name = variant.Name, Config = config });
            }

            variants.Add(new WeightingVariant { Name = "uniform", Config = null });

            var batch = AssetDatabase.LoadAssetAtPath<SimulationBatch>(BatchPath);
            if (batch == null)
            {
                batch = ScriptableObject.CreateInstance<SimulationBatch>();
                batch.Catalog = baseline.Catalog;
                batch.Car = baseline.Car;
                batch.Difficulty = baseline.Difficulty;
                batch.Agents = new[] { AgentKind.Casino, AgentKind.CasinoBettor, AgentKind.Priority };
                batch.RunsPerVariant = baseline.RunsPerVariant;
                batch.BaseSeed = baseline.BaseSeed;
                batch.StepSeconds = baseline.StepSeconds;
                batch.MaxRunSeconds = baseline.MaxRunSeconds;
                batch.OffersPerLevel = baseline.OffersPerLevel;
                batch.OutputDirectory = "Simulation/Output/Ablation";
                AssetDatabase.CreateAsset(batch, BatchPath);
            }

            batch.Variants = variants.ToArray();
            EditorUtility.SetDirty(batch);
            AssetDatabase.SaveAssets();
            return batch;
        }

        static string GetArgument(string name)
        {
            string[] arguments = System.Environment.GetCommandLineArgs();

            for (int i = 0; i < arguments.Length - 1; i++)
                if (arguments[i] == name)
                    return arguments[i + 1];

            return null;
        }
    }
}
