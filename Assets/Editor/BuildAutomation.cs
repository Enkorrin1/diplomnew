using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RogueDrive.Editor
{
    /// <summary>
    /// Автоматизация сборки релизных версий игры (Windows Standalone и Android APK)
    /// для демонстрации и защиты дипломного проекта БГУИР.
    /// </summary>
    public static class BuildAutomation
    {
        private const string PackageName = "com.bsuir.roguedrive";
        private const string GameTitle = "Rogue Drive: Survival";
        private const string Company = "BSUIR";

        private static readonly string[] RequiredScenes = new[]
        {
            "Assets/Scenes/RogueDrivePrototype.unity",
            "Assets/Scenes/GarageScene.unity",
            "Assets/Scenes/BossEncounter.unity"
        };

        [MenuItem("RogueDrive/Build/Configure Android Project Settings", priority = 100)]
        public static void ConfigureAndroidSettings()
        {
            Debug.Log("[BuildAutomation] Настройка параметров Android для дипломной сборки...");

            PlayerSettings.companyName = Company;
            PlayerSettings.productName = GameTitle;
            PlayerSettings.bundleVersion = "1.0.0";

            // Ориентация экрана: фиксированный альбомный режим (Landscape)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Настройки Android
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EnsureBuildScenes();

            Debug.Log($"[BuildAutomation] ✅ Настройки Android применены: Package={PackageName}, Orientation=Landscape, MinSDK=24.");
        }

        [MenuItem("RogueDrive/Build/Build Windows Standalone (Release)", priority = 110)]
        public static void BuildWindowsStandalone()
        {
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
            Directory.CreateDirectory(outputDir);
            string exePath = Path.Combine(outputDir, "RogueDrive_Survival.exe");

            Debug.Log($"[BuildAutomation] 🚀 Запуск сборки Windows Standalone в: {exePath}...");

            EnsureBuildScenes();
            string[] scenePaths = GetEnabledScenes();

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildAutomation] 🏆 Сборка Windows завершена успешно! Размер: {report.summary.totalSize / (1024 * 1024)} МБ.");
                EditorUtility.RevealInFinder(exePath);
            }
            else
            {
                Debug.LogError($"[BuildAutomation] ❌ Ошибка сборки Windows: {report.summary.result}");
            }
        }

        [MenuItem("RogueDrive/Build/Build Android APK (Release)", priority = 120)]
        public static void BuildAndroidApk()
        {
            ConfigureAndroidSettings();

            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Android");
            Directory.CreateDirectory(outputDir);
            string apkPath = Path.Combine(outputDir, "RogueDrive_Survival_v1.0.apk");

            Debug.Log($"[BuildAutomation] 🤖 Запуск сборки Android APK в: {apkPath}...");

            string[] scenePaths = GetEnabledScenes();

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildAutomation] 🏆 Сборка Android APK завершена успешно! Файл: {apkPath}, Размер: {report.summary.totalSize / (1024 * 1024)} МБ.");
                EditorUtility.RevealInFinder(apkPath);
            }
            else
            {
                Debug.LogWarning($"[BuildAutomation] Результат сборки Android: {report.summary.result}. Убедитесь, что Android Build Support и NDK/SDK установлены.");
            }
        }

        private static void EnsureBuildScenes()
        {
            var existingScenes = EditorBuildSettings.scenes.ToList();
            bool changed = false;

            foreach (var sc in RequiredScenes)
            {
                if (File.Exists(sc) && !existingScenes.Any(s => s.path == sc))
                {
                    existingScenes.Add(new EditorBuildSettingsScene(sc, true));
                    changed = true;
                }
            }

            if (changed)
            {
                EditorBuildSettings.scenes = existingScenes.ToArray();
                Debug.Log("[BuildAutomation] Обновлен список сцен в EditorBuildSettings.");
            }
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                return RequiredScenes.Where(File.Exists).ToArray();
            }

            return scenes;
        }
    }
}
