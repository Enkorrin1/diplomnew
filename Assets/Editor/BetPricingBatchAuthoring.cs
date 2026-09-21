using System.Collections.Generic;
using RogueDrive.Modifiers;
using RogueDrive.Simulation;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Серия подбора цен ставок казино. Одна и та же полная конфигурация алгоритма,
    /// меняются только стоимость ставок «Редкий+» и «Синергия» и возврат за успешную
    /// синергию. Сравнение агента со ставками с агентом без ставок показывает, при каких
    /// ценах ставки перестают быть ловушкой, но не вытесняют обычный спин.
    /// </summary>
    public static class BetPricingBatchAuthoring
    {
        const string Folder = "Assets/Content/Simulation/BetPricing";
        const string BasePath = "Assets/Content/Simulation/Weighting_Moderate.asset";
        const string BatchPath = Folder + "/Batch_BetPricing.asset";
        const string BaselineBatchPath = "Assets/Content/Simulation/Batch_Baseline.asset";

        // Имя = редкий/синергия/возврат/скидка при полных сокетах/шаг банка
        static readonly (string Name, int Rare, int Synergy, int Refund, int Discount, int BankPer)[] Variants =
        {
            ("r2_s3_ref0", 2, 3, 0, 0, 0),
            ("r2_s2_ref0", 2, 2, 0, 0, 0),
            ("r2_s3_ref1", 2, 3, 1, 0, 0),
            ("r2_s2_ref1", 2, 2, 1, 0, 0),
            ("r1_s2_ref0", 1, 2, 0, 0, 0),
            ("r1_s2_ref1", 1, 2, 1, 0, 0),
            ("r1_s1_ref0", 1, 1, 0, 0, 0),

            // Динамическая цена: ставки дешевеют, когда свободных сокетов не осталось
            ("r3_s3_disc1", 3, 3, 1, 1, 0),
            ("r3_s4_disc2", 3, 4, 1, 2, 0),

            // Банк: остаток жетонов, пронесённый мимо пункта, приносит надбавку
            ("r2_s2_bank2", 2, 2, 1, 0, 2),
            ("r3_s3_disc1_bank2", 3, 3, 1, 1, 2),
            ("r3_s3_disc1_bank3", 3, 3, 1, 1, 3),
        };

        [MenuItem("RogueDrive/Симуляция баланса/Цены ставок/Создать серию", priority = 40)]
        public static void CreateBatch()
        {
            SimulationBatch batch = CreateOrUpdate();
            if (batch != null)
            {
                Selection.activeObject = batch;
                Debug.Log($"[BetPricing] Серия готова: {BatchPath} ({batch.Variants.Length} вариантов цен).");
            }
        }

        [MenuItem("RogueDrive/Симуляция баланса/Цены ставок/Запустить серию", priority = 41)]
        public static void RunBatch()
        {
            SimulationBatch batch = CreateOrUpdate();
            if (batch == null) return;

            try
            {
                string directory = SimulationRunner.Execute(batch, (progress, status) =>
                    EditorUtility.DisplayProgressBar("Подбор цен ставок", status, progress));
                Debug.Log($"[BetPricing] Результаты записаны в {directory}");
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

        /// <summary>Пакетный запуск без участия редактора.</summary>
        public static void CreateAndRun()
        {
            SimulationBatch batch = CreateOrUpdate();
            if (batch == null)
            {
                EditorApplication.Exit(2);
                return;
            }

            try
            {
                string directory = SimulationRunner.Execute(batch);
                Debug.Log($"[BetPricing] Результаты записаны в {directory}");
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
                Debug.LogError("[BetPricing] Не найдены Weighting_Moderate.asset или Batch_Baseline.asset.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Content/Simulation", "BetPricing");

            var variants = new List<WeightingVariant>();

            foreach (var variant in Variants)
            {
                string path = $"{Folder}/Weighting_bets_{variant.Name}.asset";
                var config = AssetDatabase.LoadAssetAtPath<WeightingConfig>(path);

                if (config == null)
                {
                    config = Object.Instantiate(baseConfig);
                    config.name = $"Weighting_bets_{variant.Name}";
                    AssetDatabase.CreateAsset(config, path);
                }

                // Цены всегда перезаписываются из таблицы — это и есть варьируемый параметр
                config.BetPricing = new CasinoBetPricing
                {
                    RareBetCost = variant.Rare,
                    SynergyBetCost = variant.Synergy,
                    SynergyBetRefund = variant.Refund,
                    FullCarDiscount = variant.Discount,
                    CarryOverBonusPer = variant.BankPer
                };
                EditorUtility.SetDirty(config);

                variants.Add(new WeightingVariant { Name = variant.Name, Config = config });
            }

            var batch = AssetDatabase.LoadAssetAtPath<SimulationBatch>(BatchPath);
            if (batch == null)
            {
                batch = ScriptableObject.CreateInstance<SimulationBatch>();
                batch.Catalog = baseline.Catalog;
                batch.Car = baseline.Car;
                batch.Difficulty = baseline.Difficulty;
                batch.Agents = new[] { AgentKind.Casino, AgentKind.CasinoBettor, AgentKind.CasinoBanker };
                batch.RunsPerVariant = baseline.RunsPerVariant;
                batch.BaseSeed = baseline.BaseSeed;
                batch.StepSeconds = baseline.StepSeconds;
                batch.MaxRunSeconds = baseline.MaxRunSeconds;
                batch.OffersPerLevel = baseline.OffersPerLevel;
                batch.OutputDirectory = "Simulation/Output/BetPricing";
                AssetDatabase.CreateAsset(batch, BatchPath);
            }

            batch.Variants = variants.ToArray();
            // Набор агентов перезаписывается: серия сравнивает казино без ставок, со ставками и с банком
            batch.Agents = new[] { AgentKind.Casino, AgentKind.CasinoBettor, AgentKind.CasinoBanker };
            EditorUtility.SetDirty(batch);
            AssetDatabase.SaveAssets();
            return batch;
        }
    }
}
