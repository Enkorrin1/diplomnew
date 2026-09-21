using System.Collections.Generic;
using RogueDrive.Modifiers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Перенос билда между этапами кампании. Сессия модификаторов создаётся заново
    /// в каждой сцене, поэтому состав билда (уровни модификаторов) сохраняется здесь
    /// на финише этапа и восстанавливается при старте следующего. Живёт в памяти
    /// процесса: возврат в гараж или главное меню означает новую кампанию и очищает перенос.
    /// </summary>
    public static class CampaignCarryOver
    {
        static readonly List<KeyValuePair<string, int>> levels = new List<KeyValuePair<string, int>>();
        static string carId;
        static int completedStage;

        public static bool HasBuild => levels.Count > 0;
        public static int CompletedStage => completedStage;
        public static int ModifierCount => levels.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Гараж и меню — начало новой кампании
            if (!scene.name.StartsWith("Stage"))
                Clear();
        }

        /// <summary>Запомнить билд пройденного этапа.</summary>
        public static void Store(string car, BuildState build, int stage)
        {
            levels.Clear();
            carId = car;
            completedStage = stage;

            if (build == null)
                return;

            foreach (KeyValuePair<string, int> pair in build.Levels)
                if (pair.Value > 0)
                    levels.Add(pair);
        }

        /// <summary>
        /// Восстановить билд в новой сессии, если она на той же машине и этап следует
        /// за пройденным. Возвращает число восстановленных модификаторов.
        /// </summary>
        public static int Restore(string car, int stage, ModifierSession session)
        {
            if (!HasBuild || session == null)
                return 0;

            if (!string.Equals(carId, car) || stage != completedStage + 1)
            {
                Clear();
                return 0;
            }

            int restored = 0;
            ModifierCatalog catalog = session.Catalog;

            for (int i = 0; i < levels.Count; i++)
            {
                if (catalog == null || !catalog.TryGet(levels[i].Key, out ModifierDefinition def))
                    continue;

                for (int level = 0; level < levels[i].Value; level++)
                    session.Service.Apply(def);

                restored++;
            }

            Clear();
            return restored;
        }

        public static void Clear()
        {
            levels.Clear();
            carId = null;
            completedStage = 0;
        }
    }
}
