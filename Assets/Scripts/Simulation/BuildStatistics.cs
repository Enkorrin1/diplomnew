using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace RogueDrive.Simulation
{
    /// <summary>
    /// Сводные метрики серии прогонов — материал таблиц и графиков
    /// экспериментальной главы.
    /// </summary>
    public struct SeriesMetrics
    {
        public string Configuration;
        public string Generator;
        public string Agent;
        public int Runs;

        public float CompletionRate;
        public float MedianDistance;
        public float DistanceStdDev;
        public float SynergyRate;
        public float MeanSynergies;

        public int DistinctBuilds;
        public float BuildEntropy;
        public float NormalizedEntropy;

        /// <summary>
        /// Разнообразие на уровне модификаторов. Энтропия по сигнатурам билдов
        /// насыщается около единицы, поскольку почти каждый билд уникален; концентрация
        /// выборов по отдельным модификаторам показывает вырождение пула гораздо чётче.
        /// </summary>
        public int DistinctModifiers;
        public float ModifierEntropy;
        public float NormalizedModifierEntropy;

        /// <summary>Индекс Херфиндаля по долям выборов: рост означает доминирование немногих.</summary>
        public float ModifierConcentration;

        public float TopModifierShare;

        public static string CsvHeader =>
            "configuration;generator;agent;runs;completion_rate;median_distance;distance_stddev;" +
            "synergy_rate;mean_synergies;distinct_builds;entropy;normalized_entropy;" +
            "distinct_modifiers;modifier_entropy;normalized_modifier_entropy;" +
            "modifier_concentration;top_modifier_share";

        public string ToCsvRow()
        {
            var c = CultureInfo.InvariantCulture;
            var b = new StringBuilder(160);

            b.Append(Configuration).Append(';');
            b.Append(Generator).Append(';');
            b.Append(Agent).Append(';');
            b.Append(Runs).Append(';');
            b.Append(CompletionRate.ToString("F4", c)).Append(';');
            b.Append(MedianDistance.ToString("F1", c)).Append(';');
            b.Append(DistanceStdDev.ToString("F1", c)).Append(';');
            b.Append(SynergyRate.ToString("F4", c)).Append(';');
            b.Append(MeanSynergies.ToString("F3", c)).Append(';');
            b.Append(DistinctBuilds).Append(';');
            b.Append(BuildEntropy.ToString("F4", c)).Append(';');
            b.Append(NormalizedEntropy.ToString("F4", c)).Append(';');
            b.Append(DistinctModifiers).Append(';');
            b.Append(ModifierEntropy.ToString("F4", c)).Append(';');
            b.Append(NormalizedModifierEntropy.ToString("F4", c)).Append(';');
            b.Append(ModifierConcentration.ToString("F4", c)).Append(';');
            b.Append(TopModifierShare.ToString("F4", c));

            return b.ToString();
        }
    }

    public static class BuildStatistics
    {
        /// <summary>
        /// Ключевой компромисс работы: усиление взвешивания повышает долю собранных
        /// синергий, но снижает энтропию распределения билдов. Обе величины считаются
        /// здесь, чтобы их можно было построить на одном графике.
        /// </summary>
        /// <param name="catalogSize">
        /// Размер пула модификаторов. Нужен, чтобы нормировать энтропию выборов
        /// на теоретический максимум, а не на фактически встреченное разнообразие.
        /// </param>
        public static SeriesMetrics Summarize(IReadOnlyList<RunResult> results, int catalogSize = 0)
        {
            var metrics = new SeriesMetrics { Runs = results.Count };

            if (results.Count == 0)
                return metrics;

            metrics.Configuration = results[0].Configuration;
            metrics.Generator = results[0].Generator;
            metrics.Agent = results[0].Agent;

            var distances = new List<float>(results.Count);
            var signatureCounts = new Dictionary<string, int>();

            int completed = 0;
            int withSynergy = 0;
            int synergyTotal = 0;

            for (int i = 0; i < results.Count; i++)
            {
                RunResult result = results[i];

                distances.Add(result.Distance);

                if (result.Completed)
                    completed++;

                if (result.SynergyCount > 0)
                    withSynergy++;

                synergyTotal += result.SynergyCount;

                string signature = string.IsNullOrEmpty(result.BuildSignature)
                    ? "(empty)"
                    : result.BuildSignature;

                signatureCounts.TryGetValue(signature, out int count);
                signatureCounts[signature] = count + 1;
            }

            metrics.CompletionRate = completed / (float)results.Count;
            metrics.SynergyRate = withSynergy / (float)results.Count;
            metrics.MeanSynergies = synergyTotal / (float)results.Count;

            distances.Sort();
            metrics.MedianDistance = Median(distances);
            metrics.DistanceStdDev = StandardDeviation(distances);

            metrics.DistinctBuilds = signatureCounts.Count;
            metrics.BuildEntropy = Entropy(signatureCounts, results.Count);
            metrics.NormalizedEntropy = signatureCounts.Count > 1
                ? metrics.BuildEntropy / Mathf.Log(signatureCounts.Count, 2f)
                : 0f;

            FillModifierDiversity(ref metrics, results, catalogSize);

            return metrics;
        }

        static void FillModifierDiversity(ref SeriesMetrics metrics,
                                          IReadOnlyList<RunResult> results,
                                          int catalogSize)
        {
            Dictionary<string, int> picks = ModifierFrequency(results);

            metrics.DistinctModifiers = picks.Count;

            if (picks.Count == 0)
                return;

            int total = 0;

            foreach (KeyValuePair<string, int> pair in picks)
                total += pair.Value;

            if (total <= 0)
                return;

            float entropy = 0f;
            float concentration = 0f;
            float top = 0f;

            foreach (KeyValuePair<string, int> pair in picks)
            {
                float share = pair.Value / (float)total;

                if (share > 0f)
                    entropy -= share * Mathf.Log(share, 2f);

                concentration += share * share;

                if (share > top)
                    top = share;
            }

            metrics.ModifierEntropy = entropy;
            metrics.ModifierConcentration = concentration;
            metrics.TopModifierShare = top;

            // Нормировка на весь пул: невостребованный модификатор снижает показатель,
            // тогда как нормировка на встреченные его бы скрыла.
            int reference = catalogSize > 1 ? catalogSize : picks.Count;

            metrics.NormalizedModifierEntropy = reference > 1
                ? entropy / Mathf.Log(reference, 2f)
                : 0f;
        }

        /// <summary>Частота выбора каждого модификатора — выявление доминирующих и невостребованных.</summary>
        public static Dictionary<string, int> ModifierFrequency(IReadOnlyList<RunResult> results)
        {
            var frequency = new Dictionary<string, int>();

            for (int i = 0; i < results.Count; i++)
            {
                string signature = results[i].BuildSignature;

                if (string.IsNullOrEmpty(signature))
                    continue;

                string[] entries = signature.Split('|');

                for (int e = 0; e < entries.Length; e++)
                {
                    int separator = entries[e].IndexOf(':');

                    if (separator <= 0)
                        continue;

                    string id = entries[e].Substring(0, separator);
                    frequency.TryGetValue(id, out int count);
                    frequency[id] = count + 1;
                }
            }

            return frequency;
        }

        static float Median(List<float> sorted)
        {
            if (sorted.Count == 0)
                return 0f;

            int middle = sorted.Count / 2;

            return sorted.Count % 2 == 1
                ? sorted[middle]
                : (sorted[middle - 1] + sorted[middle]) * 0.5f;
        }

        static float StandardDeviation(List<float> values)
        {
            if (values.Count < 2)
                return 0f;

            float sum = 0f;

            for (int i = 0; i < values.Count; i++)
                sum += values[i];

            float mean = sum / values.Count;
            float variance = 0f;

            for (int i = 0; i < values.Count; i++)
            {
                float delta = values[i] - mean;
                variance += delta * delta;
            }

            return Mathf.Sqrt(variance / (values.Count - 1));
        }

        /// <summary>Энтропия Шеннона распределения билдов в битах.</summary>
        static float Entropy(Dictionary<string, int> counts, int total)
        {
            if (total <= 0)
                return 0f;

            float entropy = 0f;

            foreach (KeyValuePair<string, int> pair in counts)
            {
                float probability = pair.Value / (float)total;

                if (probability > 0f)
                    entropy -= probability * Mathf.Log(probability, 2f);
            }

            return entropy;
        }
    }
}
