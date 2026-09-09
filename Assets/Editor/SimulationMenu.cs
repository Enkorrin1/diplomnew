using RogueDrive.Simulation;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Запуск серии прогонов из редактора. Ассет серии выделяется в окне проекта.
    /// </summary>
    public static class SimulationMenu
    {
        [MenuItem("RogueDrive/Запустить серию прогонов %#r")]
        static void RunSelectedBatch()
        {
            var batch = Selection.activeObject as SimulationBatch;

            if (batch == null)
            {
                EditorUtility.DisplayDialog(
                    "Серия не выбрана",
                    "Выделите ассет Simulation Batch в окне проекта и повторите.",
                    "Понятно");
                return;
            }

            if (!batch.Validate(out string error))
            {
                EditorUtility.DisplayDialog("Серия настроена не полностью", error, "Понятно");
                return;
            }

            try
            {
                string directory = SimulationRunner.Execute(batch, (progress, status) =>
                    EditorUtility.DisplayProgressBar("Симуляция заездов", status, progress));

                Debug.Log($"Результаты симуляции записаны в {directory}");
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

        [MenuItem("RogueDrive/Симуляция/Запустить выделенную серию %#r", true)]
        static bool ValidateRunSelectedBatch()
        {
            return Selection.activeObject is SimulationBatch;
        }

        [MenuItem("RogueDrive/Симуляция/Запустить базовую серию (12,000 заездов)")]
        public static void RunBaselineBatch()
        {
            var batch = AssetDatabase.LoadAssetAtPath<SimulationBatch>("Assets/Content/Simulation/Batch_Baseline.asset");
            if (batch == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Не найден ассет Assets/Content/Simulation/Batch_Baseline.asset", "ОК");
                return;
            }

            try
            {
                string directory = SimulationRunner.Execute(batch, (progress, status) =>
                    EditorUtility.DisplayProgressBar("Монте-Карло симуляция (12,000 заездов)", status, progress));

                Debug.Log($"[Simulation] Симуляция успешно выполнена! Результаты в: {directory}");
                EditorUtility.RevealInFinder(directory);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Ошибка симуляции", ex.Message, "ОК");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
