using System.IO;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorScripts
{
    /// <summary>
    /// Автоматический импортер и конфигуратор спрайтов для кастомного UI-кита RogueDrive:
    /// 1. Приборные шкалы (Gauges) -> Sprite (2D and UI), Pivot (0.5, 0.5).
    /// 2. Рамки слотов (Frames) -> Sprite (2D and UI), 9-slice Border (18, 18, 18, 18).
    /// 3. Иконки предметов (Icons) -> Sprite (2D and UI), Clamp, Crisp.
    /// </summary>
    public static class BunkerUIAssetImporter
    {
        [InitializeOnLoadMethod]
        private static void OnProjectLoad()
        {
            EditorApplication.delayCall += ConfigureAllUISprites;
        }

        [MenuItem("RogueDrive/UI/Настроить импорт спрайтов UI")]
        public static void ConfigureAllUISprites()
        {
            string rootPath = "Assets/Textures/UI";
            if (!Directory.Exists(rootPath)) return;

            string[] pngFiles = Directory.GetFiles(rootPath, "*.png", SearchOption.AllDirectories);
            int updatedCount = 0;

            foreach (string file in pngFiles)
            {
                string assetPath = file.Replace('\\', '/');
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                bool modified = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    modified = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    modified = true;
                }

                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    modified = true;
                }

                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    modified = true;
                }

                if (importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    modified = true;
                }

                // 9-slice borders for frames
                if (assetPath.Contains("/Frames/"))
                {
                    Vector4 targetBorder = new Vector4(18f, 18f, 18f, 18f);
                    if (importer.spriteBorder != targetBorder)
                    {
                        importer.spriteBorder = targetBorder;
                        modified = true;
                    }
                }

                // Center pivot for gauge needle and dials
                if (assetPath.Contains("/Gauges/"))
                {
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    if (settings.spriteAlignment != (int)SpriteAlignment.Center)
                    {
                        settings.spriteAlignment = (int)SpriteAlignment.Center;
                        importer.SetTextureSettings(settings);
                        modified = true;
                    }
                }

                if (modified)
                {
                    importer.SaveAndReimport();
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[BunkerUIAssetImporter] Успешно сконфигурировано {updatedCount} спрайтов UI.");
            }
        }
    }
}
