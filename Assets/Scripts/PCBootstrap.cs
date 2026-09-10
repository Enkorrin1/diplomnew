using UnityEngine;

namespace RogueDrive
{
    /// <summary>
    /// Глобальный PC-инициализатор. Создаётся автоматически при первом старте,
    /// сохраняется между сценами через DontDestroyOnLoad.
    ///
    /// Отвечает за:
    /// — снятие лимита FPS и включение VSync
    /// — установку стартового разрешения 1920×1080 Fullscreen при первом запуске
    /// — переключение Fullscreen/Windowed по F11 и Alt+Enter
    /// — сохранение предпочтений разрешения в PlayerPrefs
    /// </summary>
    public sealed class PCBootstrap : MonoBehaviour
    {
        static PCBootstrap instance;

        void Awake()
        {
            // Синглтон через сцены
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            InitDisplay();
        }

        void Update()
        {
            // F11 — стандартный PC-хоткей для полноэкранного режима
            if (Input.GetKeyDown(KeyCode.F11))
            {
                ToggleFullscreen();
                return;
            }

            // Alt+Enter — классика Windows
            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                && Input.GetKeyDown(KeyCode.Return))
            {
                ToggleFullscreen();
            }
        }

        static void InitDisplay()
        {
            // Снимаем ограничение FPS; VSync задаёт пространство кадров сам
            QualitySettings.vSyncCount     = 1;   // 1 = синхронизация с монитором
            Application.targetFrameRate    = -1;  // uncapped поверх VSync

            // Только для PC — не трогаем мобилки
            if (Application.isMobilePlatform) return;

            bool firstRun = PlayerPrefs.GetInt("PC_DisplayInit", 0) == 0;

            if (firstRun)
            {
                // Первый запуск: ставим 1920×1080 Fullscreen
                int targetW = 1920;
                int targetH = 1080;

                // Если системное разрешение ниже — берём ближайшее из списка
                foreach (var res in Screen.resolutions)
                {
                    if (res.width <= Screen.currentResolution.width
                        && res.height <= Screen.currentResolution.height)
                    {
                        targetW = res.width;
                        targetH = res.height;
                    }
                }

                Screen.SetResolution(targetW, targetH, FullScreenMode.FullScreenWindow);
                PlayerPrefs.SetInt("PC_ResW",       targetW);
                PlayerPrefs.SetInt("PC_ResH",       targetH);
                PlayerPrefs.SetInt("PC_Fullscreen",  1);
                PlayerPrefs.SetInt("PC_DisplayInit", 1);
                PlayerPrefs.Save();
            }
            else
            {
                // Последующие запуски: восстанавливаем сохранённые настройки
                int w          = PlayerPrefs.GetInt("PC_ResW",      1920);
                int h          = PlayerPrefs.GetInt("PC_ResH",      1080);
                bool fullscreen= PlayerPrefs.GetInt("PC_Fullscreen", 1) == 1;
                Screen.SetResolution(w, h, fullscreen
                    ? FullScreenMode.FullScreenWindow
                    : FullScreenMode.Windowed);
            }
        }

        static void ToggleFullscreen()
        {
            bool goFull = !Screen.fullScreen;
            if (goFull)
            {
                // Во весь экран — берём текущее системное разрешение
                Resolution sys = Screen.currentResolution;
                Screen.SetResolution(sys.width, sys.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                // В окно — 1280×720 по умолчанию (менять под нужды)
                int w = PlayerPrefs.GetInt("PC_ResW", 1280);
                int h = PlayerPrefs.GetInt("PC_ResH",  720);
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
            }

            PlayerPrefs.SetInt("PC_Fullscreen", goFull ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Устанавливает конкретное разрешение с сохранением в PlayerPrefs.
        /// Вызывается из меню настроек игры.
        /// </summary>
        public static void ApplyResolution(int width, int height, bool fullscreen)
        {
            Screen.SetResolution(width, height, fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed);
            PlayerPrefs.SetInt("PC_ResW",      width);
            PlayerPrefs.SetInt("PC_ResH",      height);
            PlayerPrefs.SetInt("PC_Fullscreen", fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Гарантирует существование PCBootstrap в сцене.
        /// Вызывается из GameRunController / MainMenuController.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (instance != null) return;
            var go = new GameObject("[PCBootstrap]");
            go.AddComponent<PCBootstrap>();
            // DontDestroyOnLoad уже вызывается в Awake
        }
    }
}
