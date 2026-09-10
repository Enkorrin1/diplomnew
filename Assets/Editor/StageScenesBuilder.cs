using System;
using System.Collections.Generic;
using System.IO;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    public static class StageScenesBuilder
    {
        [MenuItem(RogueDrive/Build All Stage Scenes)]
        public static void BuildAllStages()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError([StageScenesBuilder] Остановите Play mode перед генерацией сцен!);
                return;
            }

            const string templatePath = Assets/Scenes/RogueDrivePrototype.unity;
            if (!File.Exists(templatePath))
            {
                Debug.LogError($[StageScenesBuilder] Шаблонная сцена не найдена: {templatePath});
                return;
            }

            EditorSceneManager.SaveOpenScenes();

            string[] stageSceneNames =
            {
                Stage1_Outskirts,
                Stage2_Wasteland,
                Stage3_Industrial,
                Stage4_Citadel
            };

            var settings = AssetDatabase.LoadAssetAtPath<CampaignSceneSettings>(Assets/Content/CampaignSceneSettings.asset);
            BiomeConfig[] defaultBiomes = settings != null ? settings.Biomes : BiomeConfig.GetDefaultBiomes();

            var scenesList = new List<EditorBuildSettingsScene>();

            // Сначала добавляем MainMenu и Garage
            AddSceneIfExists(scenesList, Assets/Scenes/MainMenuScene.unity);
            AddSceneIfExists(scenesList, Assets/Scenes/GarageScene.unity);

            for (int i = 0; i < stageSceneNames.Length; i++)
            {
                int stageNum = i + 1;
                string scenePath = $Assets/Scenes/{stageSceneNames[i]}.unity;

                // Копируем базовую сцену с UI, машиной, камерой и аудио
                File.Copy(templatePath, scenePath, true);
                AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var generator = Object.FindFirstObjectByType<ProceduralTrackGenerator>();

                if (generator != null)
                {
                    // В отдельных сценах удаляем монолитную общую кампанию,
                    // чтобы каждый этап генерировался на 3000м со своим чистым биомом и финишным форпостом
                    var authoredProp = new SerializedObject(generator).FindProperty(authoredCampaign);
                    if (authoredProp != null && authoredProp.objectReferenceValue != null)
                    {
                        var authoredObj = authoredProp.objectReferenceValue as Transform;
                        authoredProp.objectReferenceValue = null;
                        if (authoredObj != null)
                        {
                            Object.DestroyImmediate(authoredObj.gameObject);
                        }
                    }

                    var bakedFirst = generator.transform.Find(BakedFirstMap);
                    if (bakedFirst != null)
                    {
                        Object.DestroyImmediate(bakedFirst.gameObject);
                    }

                    // Применяем цветовую палитру освещения биома к сцене
                    if (i < defaultBiomes.Length)
                    {
                        BiomeConfig biome = defaultBiomes[i];
                        RenderSettings.fog = true;
                        RenderSettings.fogColor = biome.fogColor;
                        RenderSettings.fogDensity = biome.fogDensity;
                        RenderSettings.ambientSkyColor = biome.fogColor * 1.15f;

                        Light sun = RenderSettings.sun ?? Object.FindFirstObjectByType<Light>();
                        if (sun != null && sun.type == LightType.Directional)
                        {
                            sun.color = biome.fogColor * 1.2f;
                            sun.intensity = 1.1f;
                        }
                    }

                    EditorUtility.SetDirty(generator);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($[StageScenesBuilder] Сцена этапа {stageNum} успешно создана: {scenePath});

                scenesList.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            // Добавляем также RogueDrivePrototype как тестовый полигон / Endless
            AddSceneIfExists(scenesList, templatePath);

            // Обновляем EditorBuildSettings
            EditorBuildSettings.scenes = scenesList.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($[StageScenesBuilder] Все {stageSceneNames.Length} сцен этапов добавлены в EditorBuildSettings!);
        }

        static void AddSceneIfExists(List<EditorBuildSettingsScene> list, string path)
        {
            if (File.Exists(path))
            {
                list.Add(new EditorBuildSettingsScene(path, true));
            }
        }
    }
}
