using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace RogueDrive.Simulation
{
    /// <summary>
    /// Прогон серии и выгрузка результатов. Формирует три файла:
    /// построчные результаты, сводные метрики и частоты выбора модификаторов.
    /// </summary>
    public static class SimulationRunner
    {
        public static string Execute(SimulationBatch batch, Action<float, string> onProgress = null)
        {
            if (!batch.Validate(out string error))
                throw new InvalidOperationException(error);

            var simulator = new RunSimulator();
            var allResults = new List<RunResult>();
            var summaries = new List<SeriesMetrics>();

            var setups = new List<SimulationSetup>(batch.EnumerateSetups());
            int totalRuns = setups.Count * batch.RunsPerVariant;
            int completedRuns = 0;

            foreach (SimulationSetup setup in setups)
            {
                var seriesResults = new List<RunResult>(batch.RunsPerVariant);

                for (int i = 0; i < batch.RunsPerVariant; i++)
                {
                    int seed = unchecked(batch.BaseSeed + i);
                    RunResult result = simulator.Run(setup, seed);

                    seriesResults.Add(result);
                    allResults.Add(result);
                    completedRuns++;

                    if (onProgress != null && completedRuns % 50 == 0)
                        onProgress(completedRuns / (float)totalRuns,
                                   $"{setup.ConfigurationName}: {i + 1} из {batch.RunsPerVariant}");
                }

                summaries.Add(BuildStatistics.Summarize(seriesResults, batch.Catalog.All.Count));
            }

            string directory = ResolveDirectory(batch.OutputDirectory);
            Directory.CreateDirectory(directory);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            WriteRuns(Path.Combine(directory, $"runs_{stamp}.csv"), allResults);
            WriteSummaries(Path.Combine(directory, $"summary_{stamp}.csv"), summaries);
            WriteFrequencies(Path.Combine(directory, $"frequency_{stamp}.csv"), setups, batch, allResults);

            return directory;
        }

        static void WriteRuns(string path, IReadOnlyList<RunResult> results)
        {
            var builder = new StringBuilder(results.Count * 128);
            builder.AppendLine(RunResult.CsvHeader);

            for (int i = 0; i < results.Count; i++)
                builder.AppendLine(results[i].ToCsvRow());

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        static void WriteSummaries(string path, IReadOnlyList<SeriesMetrics> summaries)
        {
            var builder = new StringBuilder();
            builder.AppendLine(SeriesMetrics.CsvHeader);

            for (int i = 0; i < summaries.Count; i++)
                builder.AppendLine(summaries[i].ToCsvRow());

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        static void WriteFrequencies(string path,
                                     IReadOnlyList<SimulationSetup> setups,
                                     SimulationBatch batch,
                                     IReadOnlyList<RunResult> allResults)
        {
            var builder = new StringBuilder();
            builder.AppendLine("configuration;agent;modifier;runs_with_modifier;share");

            foreach (SimulationSetup setup in setups)
            {
                var subset = new List<RunResult>();

                for (int i = 0; i < allResults.Count; i++)
                    if (allResults[i].Configuration == setup.ConfigurationName)
                        subset.Add(allResults[i]);

                if (subset.Count == 0)
                    continue;

                Dictionary<string, int> frequency = BuildStatistics.ModifierFrequency(subset);

                foreach (KeyValuePair<string, int> pair in frequency)
                {
                    float share = pair.Value / (float)subset.Count;

                    builder.Append(setup.ConfigurationName).Append(';')
                           .Append(setup.Agent).Append(';')
                           .Append(pair.Key).Append(';')
                           .Append(pair.Value).Append(';')
                           .AppendLine(share.ToString("F4", CultureInfo.InvariantCulture));
                }
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        static string ResolveDirectory(string configured)
        {
            if (string.IsNullOrEmpty(configured))
                configured = "Simulation/Output";

            if (Path.IsPathRooted(configured))
                return configured;

            // Каталог проекта: на уровень выше Assets.
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, configured);
        }
    }
}
