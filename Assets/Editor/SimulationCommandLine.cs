using System;
using RogueDrive.Simulation;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Запуск серии прогонов в пакетном режиме:
    ///
    ///   Unity.exe -projectPath &lt;путь&gt; -batchmode -nographics -quit
    ///             -executeMethod RogueDrive.EditorTools.SimulationCommandLine.Run
    ///             -batchAsset &lt;путь к ассету&gt; -runs &lt;число&gt;
    ///
    /// Нужен для воспроизводимого прогона серий без участия редактора —
    /// эксперимент запускается одной командой и может повторяться сколько угодно раз.
    /// </summary>
    public static class SimulationCommandLine
    {
        const string DefaultBatchPath = "Assets/Content/Simulation/Batch_Baseline.asset";

        public static void Run()
        {
            string path = GetArgument("-batchAsset") ?? DefaultBatchPath;
            var batch = AssetDatabase.LoadAssetAtPath<SimulationBatch>(path);

            if (batch == null)
            {
                Debug.LogError($"Не найден ассет серии: {path}");
                EditorApplication.Exit(2);
                return;
            }

            string runsArgument = GetArgument("-runs");

            if (int.TryParse(runsArgument, out int runs) && runs > 0)
            {
                batch.RunsPerVariant = runs;
                Debug.Log($"Число прогонов на конфигурацию переопределено: {runs}");
            }

            try
            {
                DateTime started = DateTime.Now;
                string directory = SimulationRunner.Execute(batch);
                TimeSpan elapsed = DateTime.Now - started;

                Debug.Log($"Симуляция завершена за {elapsed.TotalSeconds:F1} с. Результаты: {directory}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"Симуляция прервана: {exception.Message}");
                Debug.LogException(exception);
                EditorApplication.Exit(3);
            }
        }

        static string GetArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();

            for (int i = 0; i < arguments.Length - 1; i++)
                if (arguments[i] == name)
                    return arguments[i + 1];

            return null;
        }
    }
}
