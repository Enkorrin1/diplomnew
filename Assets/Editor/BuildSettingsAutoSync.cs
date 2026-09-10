#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Автоматически синхронизирует официальные сцены PC-релиза в Build Settings
    /// при каждом запуске редактора или перекомпиляции скриптов.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildSettingsAutoSync
    {
        private static readonly string[] OfficialScenes = new[]
        {
            "Assets/Scenes/MainMenuScene.unity",
            "Assets/Scenes/GarageScene.unity",
            "Assets/Scenes/Stage1_Outskirts.unity",
            "Assets/Scenes/Stage2_Wasteland.unity",
            "Assets/Scenes/Stage3_Industrial.unity",
            "Assets/Scenes/Stage4_Citadel.unity"
        };

        static BuildSettingsAutoSync()
        {
            EditorApplication.delayCall += SyncScenes;
        }

        [InitializeOnLoadMethod]
        public static void SyncScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();

            foreach (var path in OfficialScenes)
            {
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            if (scenes.Count > 0)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
#endif
