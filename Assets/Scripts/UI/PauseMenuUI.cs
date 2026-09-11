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
        [SerializeField] private bool useSceneUI = true;
        public bool UseSceneUI => useSceneUI;

        private bool isPaused = false;
        private bool showSettings = false;
        public bool ShowSettings { get => showSettings; set => showSettings = value; }

        // Настройки
        private float masterVolume = 0.85f;
        private float musicVolume = 0.75f;
        private float sfxVolume = 0.9f;
        private float steerSensitivity = 1.0f;
        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;
        public float SteerSensitivity => steerSensitivity;

        public bool IsPaused => isPaused;

        private void Awake()
        {
            Instance = this;
            useSceneUI = true;
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
                // PC: освобождаем курсор при Game Over для кнопок результата
                if (!Application.isMobilePlatform)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible   = true;
                }
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
            if (BuffCasinoView.Instance != null && BuffCasinoView.Instance.IsVisible) return;
            isPaused = true;
            showSettings = false;
            Time.timeScale = 0f;
            // PC: освобождаем курсор чтобы можно было кликать по кнопкам меню
            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
        }

        public void ResumeGame()
        {
            isPaused = false;
            showSettings = false;
            Time.timeScale = 1f;
            // PC: снова блокируем курсор в игровом режиме
            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
        }
    }
}
