using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.UI
{
    public static class GraphicsQualitySettings
    {
        public const int OptionCount = 10;
        private const string Key = "GraphicsV2.";
        private static readonly string[] PresetNames = { "НИЗКОЕ", "СРЕДНЕЕ", "ВЫСОКОЕ", "УЛЬТРА", "СВОЙ" };
        private static readonly string[] ShadowNames = { "ВЫКЛ", "ЖЁСТКИЕ", "МЯГКИЕ" };
        private static readonly string[] DisplayNames = { "ОКНО", "БЕЗ РАМОК", "ПОЛНЫЙ ЭКРАН" };
        private static readonly int[] AntiAliasingValues = { 0, 2, 4, 8 };
        private static readonly int[] ShadowDistances = { 25, 50, 90, 160 };
        private static readonly float[] DetailValues = { 0.5f, 1f, 1.5f, 2.5f };
        private static readonly List<Vector2Int> Resolutions = new List<Vector2Int>();

        private static bool loaded;
        private static int preset, baseTier, antiAliasing, shadows, shadowDistance, textures, detail, displayMode;
        private static bool vSync, effects;
        private static int resolutionWidth, resolutionHeight;

        public static bool PostEffectsEnabled { get { EnsureLoaded(); return effects; } }
        public static int Preset { get { EnsureLoaded(); return preset; } }
        public static bool IsUltra => Preset == 3;
        public static void Select(bool ultra)
        {
            EnsureLoaded();
            baseTier = ultra ? 3 : 0;
            preset = baseTier;
            SetPresetValues(baseTier);
            Save();
            Apply(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnLaunch()
        {
            loaded = false;
            EnsureLoaded();
            Apply(true);
        }

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            int legacy = PlayerPrefs.GetInt("GraphicsQualityPreset", Application.isMobilePlatform ? 0 : 1);
            baseTier = Mathf.Clamp(PlayerPrefs.GetInt(Key + "BaseTier", legacy == 0 ? 0 : 3), 0, 3);
            preset = Mathf.Clamp(PlayerPrefs.GetInt(Key + "Preset", baseTier), 0, 4);
            SetPresetValues(baseTier);
            antiAliasing = Mathf.Clamp(PlayerPrefs.GetInt(Key + "AA", antiAliasing), 0, 3);
            shadows = Mathf.Clamp(PlayerPrefs.GetInt(Key + "Shadows", shadows), 0, 2);
            shadowDistance = Mathf.Clamp(PlayerPrefs.GetInt(Key + "ShadowDistance", shadowDistance), 0, 3);
            textures = Mathf.Clamp(PlayerPrefs.GetInt(Key + "Textures", textures), 0, 2);
            detail = Mathf.Clamp(PlayerPrefs.GetInt(Key + "Detail", detail), 0, 3);
            vSync = PlayerPrefs.GetInt(Key + "VSync", vSync ? 1 : 0) != 0;
            effects = PlayerPrefs.GetInt(Key + "Effects", effects ? 1 : 0) != 0;
            displayMode = Mathf.Clamp(PlayerPrefs.GetInt(Key + "DisplayMode", ModeToIndex(Screen.fullScreenMode)), 0, 2);
            resolutionWidth = PlayerPrefs.GetInt(Key + "Width", Screen.width);
            resolutionHeight = PlayerPrefs.GetInt(Key + "Height", Screen.height);
            RefreshResolutions();
        }

        public static void Change(int option, int direction)
        {
            EnsureLoaded();
            switch (option)
            {
                case 0:
                    baseTier = (baseTier + direction + 4) % 4;
                    preset = baseTier;
                    SetPresetValues(baseTier);
                    break;
                case 1: displayMode = (displayMode + direction + 3) % 3; break;
                case 2:
                    int current = Resolutions.FindIndex(r => r.x == resolutionWidth && r.y == resolutionHeight);
                    Vector2Int selected = Resolutions[(current + direction + Resolutions.Count) % Resolutions.Count];
                    resolutionWidth = selected.x;
                    resolutionHeight = selected.y;
                    break;
                case 3: antiAliasing = (antiAliasing + direction + 4) % 4; preset = 4; break;
                case 4: shadows = (shadows + direction + 3) % 3; preset = 4; break;
                case 5: shadowDistance = (shadowDistance + direction + 4) % 4; preset = 4; break;
                case 6: textures = (textures + direction + 3) % 3; preset = 4; break;
                case 7: detail = (detail + direction + 4) % 4; preset = 4; break;
                case 8: vSync = !vSync; preset = 4; break;
                case 9: effects = !effects; preset = 4; break;
                default: return;
            }
            Save();
            Apply(option == 1 || option == 2);
        }

        public static string GetValue(int option)
        {
            EnsureLoaded();
            switch (option)
            {
                case 0: return PresetNames[preset];
                case 1: return DisplayNames[displayMode];
                case 2: return resolutionWidth + " × " + resolutionHeight;
                case 3: return antiAliasing == 0 ? "ВЫКЛ" : AntiAliasingValues[antiAliasing] + "× MSAA";
                case 4: return ShadowNames[shadows];
                case 5: return ShadowDistances[shadowDistance] + " м";
                case 6: return textures == 0 ? "ПОЛНЫЕ" : textures == 1 ? "ПОЛОВИНА" : "ЧЕТВЕРТЬ";
                case 7: return new[] { "НИЗКАЯ", "СРЕДНЯЯ", "ВЫСОКАЯ", "МАКСИМУМ" }[detail];
                case 8: return vSync ? "ВКЛ" : "ВЫКЛ";
                case 9: return effects ? "ВКЛ" : "ВЫКЛ";
                default: return string.Empty;
            }
        }

        private static void SetPresetValues(int tier)
        {
            // Постобработка и так сглаживает через SMAA; 8× MSAA остаётся ручным выбором.
            antiAliasing = Mathf.Min(tier, 2);
            shadows = tier == 0 ? 0 : tier == 1 ? 1 : 2;
            shadowDistance = tier;
            textures = tier == 0 ? 1 : 0;
            detail = tier;
            vSync = tier != 0;
            effects = tier != 0;
        }

        private static void Apply(bool applyDisplay)
        {
            string qualityName = new[] { "Low", "Medium", "Very High", "Ultra" }[baseTier];
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == qualityName && QualitySettings.GetQualityLevel() != i)
                {
                    QualitySettings.SetQualityLevel(i, true);
                    break;
                }

            QualitySettings.antiAliasing = AntiAliasingValues[antiAliasing];
            QualitySettings.shadows = shadows == 0 ? ShadowQuality.Disable : shadows == 1 ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowDistance = ShadowDistances[shadowDistance];
            QualitySettings.shadowResolution = baseTier >= 3 ? ShadowResolution.VeryHigh : baseTier >= 2 ? ShadowResolution.High : ShadowResolution.Medium;
            QualitySettings.shadowCascades = baseTier >= 2 ? 4 : baseTier == 1 ? 2 : 1;
            QualitySettings.globalTextureMipmapLimit = textures;
            QualitySettings.lodBias = DetailValues[detail];
            QualitySettings.anisotropicFiltering = baseTier >= 2 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Disable;
            QualitySettings.pixelLightCount = baseTier == 0 ? 0 : baseTier == 1 ? 2 : baseTier == 2 ? 4 : 8;
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            ApplyPostProcessing();

            if (!applyDisplay || Application.isEditor || Application.isMobilePlatform) return;
            FullScreenMode mode = displayMode == 0 ? FullScreenMode.Windowed : displayMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.ExclusiveFullScreen;
            Screen.SetResolution(resolutionWidth, resolutionHeight, mode);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FollowSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= ReapplyPostProcessing;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += ReapplyPostProcessing;
            ApplyPostProcessing();
        }

        private static void ReapplyPostProcessing(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => ApplyPostProcessing();

        // Постобработка подчиняется переключателю «Эффекты»; затенение углов (самый дорогой эффект)
        // остаётся только на высоком и ультра. profile — копия объёма, ассет профиля не меняется.
        private static void ApplyPostProcessing()
        {
            if (!Application.isPlaying) return;
            foreach (var layer in Object.FindObjectsByType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                layer.enabled = effects;
            bool ambientOcclusion = effects && baseTier >= 2;
            foreach (var volume in Object.FindObjectsByType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume.sharedProfile == null || !volume.sharedProfile.HasSettings<UnityEngine.Rendering.PostProcessing.AmbientOcclusion>()) continue;
                // Копию профиля создаём только когда AO нужно выключить или копия уже есть.
                if (ambientOcclusion && !volume.HasInstantiatedProfile()) continue;
                volume.profile.GetSetting<UnityEngine.Rendering.PostProcessing.AmbientOcclusion>().enabled.Override(ambientOcclusion);
            }
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(Key + "BaseTier", baseTier);
            PlayerPrefs.SetInt(Key + "Preset", preset);
            PlayerPrefs.SetInt(Key + "AA", antiAliasing);
            PlayerPrefs.SetInt(Key + "Shadows", shadows);
            PlayerPrefs.SetInt(Key + "ShadowDistance", shadowDistance);
            PlayerPrefs.SetInt(Key + "Textures", textures);
            PlayerPrefs.SetInt(Key + "Detail", detail);
            PlayerPrefs.SetInt(Key + "VSync", vSync ? 1 : 0);
            PlayerPrefs.SetInt(Key + "Effects", effects ? 1 : 0);
            PlayerPrefs.SetInt(Key + "DisplayMode", displayMode);
            PlayerPrefs.SetInt(Key + "Width", resolutionWidth);
            PlayerPrefs.SetInt(Key + "Height", resolutionHeight);
            PlayerPrefs.Save();
        }

        private static void RefreshResolutions()
        {
            Resolutions.Clear();
            foreach (Resolution resolution in Screen.resolutions)
            {
                if (resolution.width < 1024 || resolution.height < 720) continue;
                Vector2Int size = new Vector2Int(resolution.width, resolution.height);
                if (!Resolutions.Contains(size)) Resolutions.Add(size);
            }
            Vector2Int current = new Vector2Int(resolutionWidth, resolutionHeight);
            if (!Resolutions.Contains(current)) Resolutions.Add(current);
            Resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        private static int ModeToIndex(FullScreenMode mode) => mode == FullScreenMode.Windowed ? 0 : mode == FullScreenMode.ExclusiveFullScreen ? 2 : 1;
    }
}
