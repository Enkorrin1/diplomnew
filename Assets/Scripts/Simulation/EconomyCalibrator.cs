using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RogueDrive.Meta;
using UnityEngine;

namespace RogueDrive.Simulation
{
    public struct CalibrationReport
    {
        public int TargetRuns;
        public int TotalProgressionCost;

        public float AverageRewardBefore;
        public float AverageRewardAfter;
        public float ScaleFactor;

        public float EstimatedRunsBefore;
        public float EstimatedRunsAfter;

        public float CoinsPerMeter;
        public float CoinsPerKill;
        public float FinishBonus;

        public override string ToString()
        {
            var c = CultureInfo.InvariantCulture;
            var b = new StringBuilder();

            b.AppendLine("Калибровка экономики");
            b.AppendLine($"  Целевое число заездов:      {TargetRuns}");
            b.AppendLine($"  Полная стоимость прогресса: {TotalProgressionCost}");
            b.AppendLine($"  Средняя награда до:         {AverageRewardBefore.ToString("F1", c)}");
            b.AppendLine($"  Средняя награда после:      {AverageRewardAfter.ToString("F1", c)}");
            b.AppendLine($"  Масштабирующий множитель:   {ScaleFactor.ToString("F4", c)}");
            b.AppendLine($"  Заездов до калибровки:      {EstimatedRunsBefore.ToString("F1", c)}");
            b.AppendLine($"  Заездов после калибровки:   {EstimatedRunsAfter.ToString("F1", c)}");
            b.AppendLine($"  k_d = {CoinsPerMeter.ToString("F5", c)}");
            b.AppendLine($"  k_k = {CoinsPerKill.ToString("F5", c)}");
            b.AppendLine($"  бонус финиша = {FinishBonus.ToString("F1", c)}");

            return b.ToString();
        }
    }

    /// <summary>
    /// Вывод коэффициентов награды по результатам симуляции.
    ///
    /// Методика: полная стоимость прогресса считается по ассетам гаража, средняя
    /// награда за заезд — по фактическому распределению дистанций и убийств из серии
    /// прогонов. Коэффициенты масштабируются единым множителем так, чтобы отношение
    /// стоимости к средней награде совпало с выбранной длиной мета-прогресса.
    ///
    /// Единый множитель сохраняет заданное соотношение вкладов дистанции, убийств
    /// и бонуса за финиш: их баланс остаётся дизайнерским решением, а калибруется
    /// только общий масштаб.
    /// </summary>
    public static class EconomyCalibrator
    {
        public static CalibrationReport Calibrate(RewardConfig reward,
                                                  IReadOnlyList<RunResult> results,
                                                  IReadOnlyList<UpgradeTrack> tracks,
                                                  IReadOnlyList<int> carCosts,
                                                  IReadOnlyList<int> modifierUnlockCosts,
                                                  int targetRuns,
                                                  bool endless)
        {
            var report = new CalibrationReport { TargetRuns = Mathf.Max(1, targetRuns) };

            report.TotalProgressionCost = ComputeTotalCost(tracks, carCosts, modifierUnlockCosts);
            report.AverageRewardBefore = AverageReward(reward, results, endless);

            if (report.AverageRewardBefore <= 0f || report.TotalProgressionCost <= 0)
            {
                report.ScaleFactor = 1f;
                report.AverageRewardAfter = report.AverageRewardBefore;
                CopyCoefficients(reward, ref report);
                return report;
            }

            report.EstimatedRunsBefore = report.TotalProgressionCost / report.AverageRewardBefore;

            float requiredAverage = report.TotalProgressionCost / (float)report.TargetRuns;
            report.ScaleFactor = requiredAverage / report.AverageRewardBefore;

            reward.Scale(report.ScaleFactor);

            report.AverageRewardAfter = AverageReward(reward, results, endless);
            report.EstimatedRunsAfter = report.AverageRewardAfter > 0f
                ? report.TotalProgressionCost / report.AverageRewardAfter
                : 0f;

            CopyCoefficients(reward, ref report);
            return report;
        }

        public static float AverageReward(RewardConfig reward,
                                          IReadOnlyList<RunResult> results,
                                          bool endless)
        {
            if (reward == null || results == null || results.Count == 0)
                return 0f;

            long total = 0;

            for (int i = 0; i < results.Count; i++)
            {
                RunResult result = results[i];
                total += reward.Evaluate(result.Distance, result.Kills, result.Completed, endless);
            }

            return total / (float)results.Count;
        }

        public static int ComputeTotalCost(IReadOnlyList<UpgradeTrack> tracks,
                                           IReadOnlyList<int> carCosts,
                                           IReadOnlyList<int> modifierUnlockCosts)
        {
            int total = 0;

            if (tracks != null)
                for (int i = 0; i < tracks.Count; i++)
                    if (tracks[i] != null)
                        total += tracks[i].GetTotalCost();

            if (carCosts != null)
                for (int i = 0; i < carCosts.Count; i++)
                    total += Mathf.Max(0, carCosts[i]);

            if (modifierUnlockCosts != null)
                for (int i = 0; i < modifierUnlockCosts.Count; i++)
                    total += Mathf.Max(0, modifierUnlockCosts[i]);

            return total;
        }

        static void CopyCoefficients(RewardConfig reward, ref CalibrationReport report)
        {
            report.CoinsPerMeter = reward.CoinsPerMeter;
            report.CoinsPerKill = reward.CoinsPerKill;
            report.FinishBonus = reward.FinishBonus;
        }
    }
}
