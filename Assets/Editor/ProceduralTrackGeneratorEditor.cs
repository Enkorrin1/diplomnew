using RogueDrive.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.Editor
{
    /// <summary>
    /// Пользовательский инспектор для ProceduralTrackGenerator.
    /// Предоставляет разработчику:
    /// 1. Режим «100% НА СЦЕНЕ» — всё находится в Hierarchy (дорога, брошенные авто для слива бензина, сейфы, шипы).
    ///    Никакой кривой процедурной генерации на лету, полный ручной контроль через Scene View.
    /// 2. Кнопка «Расставить объекты выживания по всей сцене» — в 1 клик наполняет всю 3-километровую авторскую трассу машинами, лутом и шипами.
    /// 3. Кнопка «Удалить дубликаты запекания» — мгновенно удаляет наложенные тестовые чанки BakedFirstMap.
    /// </summary>
    [CustomEditor(typeof(ProceduralTrackGenerator))]
    public sealed class ProceduralTrackGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ProceduralTrackGenerator generator = (ProceduralTrackGenerator)target;

            Transform bakedRoot = generator.transform.Find("BakedFirstMap");
            bool hasBakedMap = bakedRoot != null && bakedRoot.childCount > 0;
            Transform authoredRoot = generator.AuthoredCampaign != null ? generator.AuthoredCampaign : generator.transform.Find("AuthoredStageWorld");
            bool hasAuthored = authoredRoot != null && authoredRoot.childCount > 0;

            EditorGUILayout.Space(8);

            // ── Информационный статус-баннер текущего режима ─────────────────────────
            EditorGUILayout.HelpBox(
                " РЕЖИМ РУЧНОЙ СБОРКИ ТРАССЫ (Scene Authored Mode)\n" +
                "Все процедурные генерации полностью отключены.\n" +
                "Никакие чанки, враги, препятствия или кубы декора не спавнятся кодом на лету.\n" +
                "Вы можете собирать трассу полностью вручную, расставляя префабы дороги и окружения в 'AuthoredStageWorld' на сцене.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            if (hasAuthored)
            {
                if (GUILayout.Button(" Выделить контейнер трассы (AuthoredStageWorld) в Hierarchy", GUILayout.Height(30)))
                {
                    Selection.activeGameObject = authoredRoot.gameObject;
                    EditorGUIUtility.PingObject(authoredRoot.gameObject);
                }
            }

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1.0f, 0.40f, 0.40f);
            if (GUILayout.Button("🗑 Очистить сцену от старых треков (Чистый лист)", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog("Очистить сцену", "Удалить все объекты трека со сцены для сборки с нуля?", "Да, очистить", "Отмена"))
                {
                    RogueDrive.Editor.ClearTrackObjectsUtility.CleanCurrentScene();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            // ── Основные переключатели режимов ─────────────────────────────────────
            EditorGUILayout.LabelField("Режимы работы трассы:", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            bool sceneMode = EditorGUILayout.Toggle(new GUIContent("Все на сцене (Scene Mode)", "Если включено, трасса берется целиком со сцены. Никакой процедурки на лету."), generator.SceneAuthoredMode);
            bool endless = EditorGUILayout.Toggle(new GUIContent("Бесконечный режим (Endless)", "Если включен процедурный режим, дорога никогда не кончается на финише."), generator.EndlessMode);
            bool dynamicBiomes = EditorGUILayout.Toggle(new GUIContent("Смена биомов по дистанции", "Каждые 1000м меняется биом: Пригород -> Пустошь -> Промзона -> Цитадель."), generator.DynamicBiomesByDistance);
            float targetDist = EditorGUILayout.FloatField(new GUIContent("Целевая дистанция этапа (м)", "Дистанция этапа."), generator.StageTargetDistance);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(generator, "Change Track Generator Settings");
                generator.SceneAuthoredMode = sceneMode;
                generator.EndlessMode = endless;
                generator.DynamicBiomesByDistance = dynamicBiomes;
                generator.StageTargetDistance = targetDist;
                EditorUtility.SetDirty(generator);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Параметры спавна объектов выживания (для кнопки расстановки):", EditorStyles.boldLabel);

            // Отрисовка стандартных полей инспектора
            DrawDefaultInspector();
        }

        [MenuItem("Rogue Drive/Трасса/Расставить объекты выживания по всей сцене", false, 10)]
        public static void MenuPopulateSurvivalObjects()
        {
            var gen = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (gen != null)
            {
                gen.PopulateSurvivalObjectsIntoScene();
                EditorUtility.DisplayDialog("Готово", "Объекты выживания успешно расставлены прямо в сцене! Вы можете редактировать их в Scene View.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Ошибка", "На сцене не найден ProceduralTrackGenerator!", "OK");
            }
        }

        [MenuItem("Rogue Drive/Трасса/Удалить дубликаты запекания (BakedFirstMap)", false, 11)]
        public static void MenuRemoveBakedDuplicates()
        {
            var gen = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (gen != null)
            {
                gen.RemoveBakedDuplicates();
                EditorUtility.DisplayDialog("Готово", "Дублирующий объект BakedFirstMap удален из сцены.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Ошибка", "На сцене не найден ProceduralTrackGenerator!", "OK");
            }
        }

        [MenuItem("Rogue Drive/Трасса/Запечь 1-й километр (10 чанков) в сцену", false, 20)]
        public static void MenuBake1Km()
        {
            var gen = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (gen != null)
            {
                gen.BakeMapIntoScene(10);
                EditorUtility.DisplayDialog("Трасса запечена", "1-й километр успешно создан в сцене под 'BakedFirstMap'. Вы можете редактировать любые объекты в Scene View!", "Отлично");
            }
            else
            {
                EditorUtility.DisplayDialog("Ошибка", "На сцене не найден объект с компонентом ProceduralTrackGenerator!", "OK");
            }
        }

        [MenuItem("Rogue Drive/Трасса/Запечь весь этап (30 чанков / 3000м) в сцену", false, 21)]
        public static void MenuBake3Km()
        {
            var gen = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (gen != null)
            {
                gen.BakeMapIntoScene(30);
                EditorUtility.DisplayDialog("Этап запечен", "3000 метров трассы успешно созданы в сцене под 'BakedFirstMap'!", "Отлично");
            }
            else
            {
                EditorUtility.DisplayDialog("Ошибка", "На сцене не найден объект с компонентом ProceduralTrackGenerator!", "OK");
            }
        }

        [MenuItem("Rogue Drive/Трасса/Очистить запеченную трассу из сцены", false, 30)]
        public static void MenuClearBakedTrack()
        {
            var gen = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (gen != null)
            {
                gen.ClearBakedMap();
                EditorUtility.DisplayDialog("Очищено", "Запеченная трасса удалена. Генератор вернулся в процедурный режим.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Ошибка", "На сцене не найден объект с компонентом ProceduralTrackGenerator!", "OK");
            }
        }

        public static void PopulateStage1SceneSurvivalObjects()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");
            var gen = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (gen != null)
            {
                gen.PopulateSurvivalObjectsIntoScene();
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                Debug.Log("[PopulateStage1SceneSurvivalObjects] Успешно расставлены объекты выживания в Stage1_Outskirts.unity!");
            }
            else
            {
                Debug.LogError("[PopulateStage1SceneSurvivalObjects] Не найден ProceduralTrackGenerator в Stage1_Outskirts.unity!");
            }
        }
    }
}
