#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Автоматически синхронизирует сцены GarageScene и RogueDrivePrototype
    /// в настройках сборки (Build Settings / Shared Scene List) при каждом запуске редактора
    /// или перекомпиляции скриптов.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildSettingsAutoSync
    {
        const string GaragePath = "Assets/Scenes/GarageScene.unity";
        const string PrototypePath = "Assets/Scenes/RogueDrivePrototype.unity";

        static BuildSettingsAutoSync()
        {
            EditorApplication.delayCall += SyncScenes;
        }

        [InitializeOnLoadMethod]
        public static void SyncScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            const string mainPath = "Assets/Scenes/MainMenuScene.unity";
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(mainPath)))
                scenes.Add(new EditorBuildSettingsScene(mainPath, true));

            // Проверяем существование файлов перед добавлением
            string garageGuid = AssetDatabase.AssetPathToGUID(GaragePath);
            string protoGuid = AssetDatabase.AssetPathToGUID(PrototypePath);

            if (!string.IsNullOrEmpty(garageGuid))
            {
                scenes.Add(new EditorBuildSettingsScene(GaragePath, true));
            }

            if (!string.IsNullOrEmpty(protoGuid))
            {
                scenes.Add(new EditorBuildSettingsScene(PrototypePath, true));
            }

            if (scenes.Count > 0)
            {
                foreach (var existing in EditorBuildSettings.scenes)
                    if (existing.path != mainPath && existing.path != GaragePath && existing.path != PrototypePath) scenes.Add(existing);
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
#endif
