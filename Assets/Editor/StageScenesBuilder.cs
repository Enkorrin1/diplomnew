using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    public static class StageScenesBuilder
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("RogueDrive/Build All Stage Scenes")]
        public static void BuildAllStages()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[StageScenesBuilder] Остановите Play mode перед генерацией сцен!");
                return;
            }

            const string templatePath = "Assets/Scenes/Stage1_Outskirts.unity";
            if (!File.Exists(templatePath))
            {
                Debug.LogError($"[StageScenesBuilder] Шаблонная сцена не найдена: {templatePath}");
                return;
            }

            EditorSceneManager.SaveOpenScenes();

            string[] stageSceneNames =
            {
                "Stage1_Outskirts",
                "Stage2_Wasteland",
                "Stage3_Industrial",
                "Stage4_Citadel"
            };

            var settings = AssetDatabase.LoadAssetAtPath<CampaignSceneSettings>("Assets/Content/CampaignSceneSettings.asset");
            BiomeConfig[] defaultBiomes = settings != null ? settings.Biomes : BiomeConfig.GetDefaultBiomes();

            var scenesList = new List<EditorBuildSettingsScene>();

            // Сначала добавляем MainMenu и Garage
            AddSceneIfExists(scenesList, "Assets/Scenes/MainMenuScene.unity");
            AddSceneIfExists(scenesList, "Assets/Scenes/GarageScene.unity");

            for (int i = 0; i < stageSceneNames.Length; i++)
            {
                int stageNum = i + 1;
                string scenePath = $"Assets/Scenes/{stageSceneNames[i]}.unity";

                // Копируем базовую сцену с UI, машиной, камерой и аудио
                File.Copy(templatePath, scenePath, true);
                AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var generator = Object.FindFirstObjectByType<ProceduralTrackGenerator>();

                if (generator != null)
                {
                    // В отдельных сценах удаляем монолитную общую кампанию,
                    // чтобы каждый этап генерировался на 3000м со своим чистым биомом и финишным форпостом
                    var authoredProp = new SerializedObject(generator).FindProperty("authoredCampaign");
                    if (authoredProp != null && authoredProp.objectReferenceValue != null)
                    {
                        var authoredObj = authoredProp.objectReferenceValue as Transform;
                        authoredProp.objectReferenceValue = null;
                        if (authoredObj != null)
                        {
                            Object.DestroyImmediate(authoredObj.gameObject);
                        }
                    }

                    var bakedFirst = generator.transform.Find("BakedFirstMap");
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
                Debug.Log($"[StageScenesBuilder] Сцена этапа {stageNum} успешно создана: {scenePath}");

                scenesList.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            // Обновляем EditorBuildSettings
            EditorBuildSettings.scenes = scenesList.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[StageScenesBuilder] Все {stageSceneNames.Length} сцен этапов добавлены в EditorBuildSettings!");
        }

        static void AddSceneIfExists(List<EditorBuildSettingsScene> list, string path)
        {
            if (File.Exists(path))
            {
                list.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        /// <summary>
        /// Converts the runtime-only stage track into ordinary scene objects.
        /// The resulting roots can be edited directly in the Hierarchy and are
        /// also used by ProceduralTrackGenerator at runtime.
        /// </summary>
        [MenuItem("RogueDrive/Scene Authoring/Bake All Stage Worlds For Editing")]
        public static void BakeAllStageWorldsForEditing()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[StageScenesBuilder] Остановите Play mode перед запеканием этапов.");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            CampaignSceneSettings settings = AssetDatabase.LoadAssetAtPath<CampaignSceneSettings>("Assets/Content/CampaignSceneSettings.asset");
            BiomeConfig[] biomes = settings != null ? settings.Biomes : BiomeConfig.GetDefaultBiomes();
            string[] scenes =
            {
                "Assets/Scenes/Stage1_Outskirts.unity",
                "Assets/Scenes/Stage2_Wasteland.unity",
                "Assets/Scenes/Stage3_Industrial.unity",
                "Assets/Scenes/Stage4_Citadel.unity"
            };

            for (int index = 0; index < scenes.Length; index++)
                BakeStageWorld(scenes[index], index, biomes[Mathf.Min(index, biomes.Length - 1)]);

            AssetDatabase.SaveAssets();
            Debug.Log("[StageScenesBuilder] Все этапы сохранены как редактируемые объекты сцен.");
        }

        static void BakeStageWorld(string scenePath, int biomeIndex, BiomeConfig biome)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ProceduralTrackGenerator generator = Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            if (generator == null)
            {
                Debug.LogError($"[StageScenesBuilder] Не найден ProceduralTrackGenerator: {scenePath}");
                return;
            }

            SerializedObject serializedGenerator = new SerializedObject(generator);
            SerializedProperty authoredCampaign = serializedGenerator.FindProperty("authoredCampaign");
            if (authoredCampaign == null)
                throw new InvalidOperationException("Не найдено поле authoredCampaign.");

            Transform previous = authoredCampaign.objectReferenceValue as Transform;
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);

            GameObject root = new GameObject("AuthoredStageWorld");
            root.transform.SetParent(generator.transform, false);

            Vector3 position = new Vector3(0f, 0f, 150f);
            Quaternion rotation = Quaternion.identity;
            var chunks = new List<TrackChunk>();

            // The layout mirrors the runtime chunk cadence, but is now persisted
            // in the .unity file so artists can move, replace, or decorate every piece.
            for (int chunkIndex = 0; chunkIndex < 30; chunkIndex++)
            {
                TrackChunk chunk;
                if (chunkIndex == 5 || chunkIndex == 18)
                    chunk = CreateChunk(generator, "CreateCurvedChunk", position, rotation, biome, chunkIndex == 5 ? 22f : -22f);
                else if (chunkIndex == 10 || chunkIndex == 23)
                    chunk = CreateChunk(generator, "CreateBottleneckChunk", position, rotation, biome);
                else if (chunkIndex == 15)
                    chunk = CreateChunk(generator, "CreateForkChunk", position, rotation, biome);
                else
                    chunk = CreateChunk(generator, "CreateStraightChunk", position, rotation, biome, 100f, 24f);

                chunk.transform.SetParent(root.transform, true);
                chunk.name = $"Stage_{biomeIndex + 1:00}_Chunk_{chunkIndex + 1:00}_{chunk.Type}";
                chunk.gameObject.AddComponent<BakedMapChunk>().Configure(chunkIndex);
                chunk.ApplyBiome(biome);
                chunks.Add(chunk);
                position = chunk.EndPosition;
                rotation = chunk.EndRotation;
            }

            GameObject outpostObject = new GameObject("StageFinishOutpost");
            outpostObject.transform.SetParent(root.transform, false);
            outpostObject.transform.SetPositionAndRotation(position, rotation);
            StageFinishOutpost outpost = outpostObject.AddComponent<StageFinishOutpost>();
            outpost.Configure(biomeIndex + 1, $"Форпост этапа {biomeIndex + 1}");
            outpost.BuildOutpostStructure();

            serializedGenerator.Update();
            authoredCampaign.objectReferenceValue = root.transform;
            serializedGenerator.ApplyModifiedPropertiesWithoutUndo();

            SceneAuthoringMigration.PersistGeneratedAssets(scene.GetRootGameObjects());
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static TrackChunk CreateChunk(ProceduralTrackGenerator generator, string methodName, params object[] suppliedArguments)
        {
            MethodInfo method = typeof(ProceduralTrackGenerator).GetMethod(methodName, PrivateInstance);
            if (method == null)
                throw new MissingMethodException(typeof(ProceduralTrackGenerator).Name, methodName);

            ParameterInfo[] parameters = method.GetParameters();
            object[] arguments = new object[parameters.Length];
            for (int i = 0; i < arguments.Length; i++)
                arguments[i] = i < suppliedArguments.Length ? suppliedArguments[i] : parameters[i].DefaultValue;

            return (TrackChunk)method.Invoke(generator, arguments);
        }
    }
}
