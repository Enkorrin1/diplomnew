using System;
using RogueDrive.Gameplay;
using RogueDrive.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.UI
{
    /// <summary>
    /// Меню паузы с глобальными настройками (звук, чувствительность, графика),
    /// рестартом заезда и навигацией в Гараж и Главное меню.
    /// Активируется по клавише ESC или по сенсорной кнопке [⏸].
    /// </summary>
    public sealed class PauseMenuUI : MonoBehaviour
    {
        public static PauseMenuUI Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GameRunController run;
        [SerializeField] private bool useSceneUI;

        private bool isPaused = false;
        private bool showSettings = false;

        // Настройки
        private float masterVolume = 0.85f;
        private float musicVolume = 0.75f;
        private float sfxVolume = 0.9f;
        private float steerSensitivity = 1.0f;

        // Стили интерфейса
        private GUIStyle modalBoxStyle;
        private GUIStyle headerStyle;
        private GUIStyle btnResumeStyle;
        private GUIStyle btnNormalStyle;
        private GUIStyle btnDangerStyle;
        private GUIStyle pauseIconStyle;
        private GUIStyle textStyle;

        private Texture2D bgDarkTex;
        private Texture2D btnGreenTex;
        private Texture2D btnNormalTex;
        private Texture2D btnRedTex;

        public bool IsPaused => isPaused;

        private void Awake()
        {
            Instance = this;
            if (run == null) run = FindFirstObjectByType<GameRunController>();

            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.85f);
            musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
            sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 0.9f);
            steerSensitivity = PlayerPrefs.GetFloat("SteerSensitivity", 1.0f);
        }

        private void Update()
        {
            if (run != null && run.IsGameOver)
            {
                if (isPaused) ResumeGame();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            if (run != null && run.IsGameOver) return;

            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        public void PauseGame()
        {
            if (LevelUpView.Instance != null && LevelUpView.Instance.IsVisible) return;
            isPaused = true;
            showSettings = false;
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            isPaused = false;
            showSettings = false;
            Time.timeScale = 1f;
        }

        private void OnGUI()
        {
            if (useSceneUI) return;
            EnsureStyles();

            // 1. Мобильная сенсорная кнопка паузы в правом верхнем углу
            if (!isPaused && (run == null || !run.IsGameOver))
            {
                float btnSize = 45f;
                Rect pauseBtnRect = new Rect(Screen.width - btnSize - 15f, 15f, btnSize, btnSize);
                if (GUI.Button(pauseBtnRect, "⏸", pauseIconStyle))
                {
                    PauseGame();
                }
            }

            if (!isPaused) return;

            float scale = Mathf.Clamp(Screen.height / 720f, 0.85f, 1.4f);

            // Полупрозрачный темный оверлей на весь экран
            GUI.depth = -500;
            Color prevCol = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevCol;

            if (showSettings)
            {
                DrawSettingsView(scale);
            }
            else
            {
                DrawMainPauseView(scale);
            }
        }

        private void DrawMainPauseView(float scale)
        {
            float modalW = 360f * scale;
            float modalH = 430f * scale;
            Rect modalRect = new Rect((Screen.width - modalW) / 2f, (Screen.height - modalH) / 2f, modalW, modalH);

            GUI.Box(modalRect, string.Empty, modalBoxStyle);

            float y = modalRect.y + 25f * scale;
            GUI.Label(new Rect(modalRect.x, y, modalW, 40f * scale), "ПАУЗА", headerStyle);

            y += 55f * scale;
            float btnW = modalW - 60f * scale;
            float btnH = 48f * scale;
            float btnX = modalRect.x + 30f * scale;
            float spacing = 14f * scale;

            // 1. ПРОДОЛЖИТЬ
            if (GUI.Button(new Rect(btnX, y, btnW, btnH), "▶ ПРОДОЛЖИТЬ", btnResumeStyle))
            {
                ResumeGame();
            }

            // 2. РЕСТАРТ ЗАЕЗДА
            y += btnH + spacing;
            if (GUI.Button(new Rect(btnX, y, btnW, btnH), "🔄 РЕСТАРТ ЗАЕЗДА", btnNormalStyle))
            {
                ResumeGame();
                SceneTransitionManager.SwitchScene(SceneManager.GetActiveScene().name);
            }

            // 3. НАСТРОЙКИ
            y += btnH + spacing;
            if (GUI.Button(new Rect(btnX, y, btnW, btnH), "⚙ НАСТРОЙКИ", btnNormalStyle))
            {
                showSettings = true;
            }

            // 4. В ГАРАЖ
            y += btnH + spacing;
            if (GUI.Button(new Rect(btnX, y, btnW, btnH), "🛠 В ГАРАЖ", btnNormalStyle))
            {
                ResumeGame();
                SceneTransitionManager.SwitchScene("GarageScene");
            }

            // 5. В ГЛАВНОЕ МЕНЮ
            y += btnH + spacing;
            if (GUI.Button(new Rect(btnX, y, btnW, btnH), "🚪 ГЛАВНОЕ МЕНЮ", btnDangerStyle))
            {
                ResumeGame();
                SceneTransitionManager.SwitchScene("MainMenuScene");
            }
        }

        private void DrawSettingsView(float scale)
        {
            float modalW = 440f * scale;
            float modalH = 410f * scale;
            Rect modalRect = new Rect((Screen.width - modalW) / 2f, (Screen.height - modalH) / 2f, modalW, modalH);

            GUI.Box(modalRect, string.Empty, modalBoxStyle);

            float y = modalRect.y + 20f * scale;
            GUI.Label(new Rect(modalRect.x, y, modalW, 35f * scale), "НАСТРОЙКИ", headerStyle);

            y += 50f * scale;
            float startX = modalRect.x + 30f * scale;
            float labelW = 180f * scale;
            float sliderW = 190f * scale;

            // Общий звук
            GUI.Label(new Rect(startX, y, labelW, 25f * scale), $"Мастер-громкость: {Mathf.RoundToInt(masterVolume * 100)}%", textStyle);
            masterVolume = GUI.HorizontalSlider(new Rect(startX + labelW, y + 4f, sliderW, 20f), masterVolume, 0f, 1f);

            // Музыка
            y += 45f * scale;
            GUI.Label(new Rect(startX, y, labelW, 25f * scale), $"Музыка (BGM): {Mathf.RoundToInt(musicVolume * 100)}%", textStyle);
            musicVolume = GUI.HorizontalSlider(new Rect(startX + labelW, y + 4f, sliderW, 20f), musicVolume, 0f, 1f);

            // Эффекты
            y += 45f * scale;
            GUI.Label(new Rect(startX, y, labelW, 25f * scale), $"Эффекты (SFX): {Mathf.RoundToInt(sfxVolume * 100)}%", textStyle);
            sfxVolume = GUI.HorizontalSlider(new Rect(startX + labelW, y + 4f, sliderW, 20f), sfxVolume, 0f, 1f);

            // Чувствительность руля
            y += 45f * scale;
            GUI.Label(new Rect(startX, y, labelW, 25f * scale), $"Руление: {steerSensitivity:F1}x", textStyle);
            steerSensitivity = GUI.HorizontalSlider(new Rect(startX + labelW, y + 4f, sliderW, 20f), steerSensitivity, 0.5f, 2.0f);

            // Кнопки
            y += 60f * scale;
            float btnW = 175f * scale;
            float btnH = 42f * scale;

            if (GUI.Button(new Rect(startX, y, btnW, btnH), "СОХРАНИТЬ", btnResumeStyle))
            {
                PlayerPrefs.SetFloat("MasterVolume", masterVolume);
                PlayerPrefs.SetFloat("MusicVolume", musicVolume);
                PlayerPrefs.SetFloat("SfxVolume", sfxVolume);
                PlayerPrefs.SetFloat("SteerSensitivity", steerSensitivity);
                PlayerPrefs.Save();
                AudioListener.volume = masterVolume;
                showSettings = false;
            }

            if (GUI.Button(new Rect(startX + btnW + 20f * scale, y, btnW, btnH), "НАЗАД", btnNormalStyle))
            {
                showSettings = false;
            }
        }

        private void EnsureStyles()
        {
            if (bgDarkTex == null) bgDarkTex = MakeColorTex(new Color(0.06f, 0.08f, 0.12f, 0.95f));
            if (btnGreenTex == null) btnGreenTex = MakeColorTex(new Color(0.12f, 0.65f, 0.35f, 0.95f));
            if (btnNormalTex == null) btnNormalTex = MakeColorTex(new Color(0.15f, 0.20f, 0.28f, 0.90f));
            if (btnRedTex == null) btnRedTex = MakeColorTex(new Color(0.70f, 0.18f, 0.18f, 0.90f));

            if (modalBoxStyle == null)
            {
                modalBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = bgDarkTex }
                };

                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.95f, 0.80f, 0.20f) }
                };

                btnResumeStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = btnGreenTex, textColor = Color.white }
                };

                btnNormalStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = btnNormalTex, textColor = new Color(0.9f, 0.9f, 0.95f) }
                };

                btnDangerStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = btnRedTex, textColor = Color.white }
                };

                pauseIconStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = new Color(0.85f, 0.90f, 0.95f) }
                };
            }
        }

        private Texture2D MakeColorTex(Color col)
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] p = new Color[] { col, col, col, col };
            tex.SetPixels(p);
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (bgDarkTex != null) Destroy(bgDarkTex);
            if (btnGreenTex != null) Destroy(btnGreenTex);
            if (btnNormalTex != null) Destroy(btnNormalTex);
            if (btnRedTex != null) Destroy(btnRedTex);
        }
    }
}
