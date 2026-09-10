using UnityEngine;

namespace RogueDrive.Gameplay.UI
{
    /// <summary>
    /// Адаптивный оверлей сенсорного управления для смартфонов и планшетов (Android/iOS).
    /// Поддерживает полноценный мультитач (одновременное руление и газ/нитро),
    /// эмуляцию мышью в редакторе Unity, визуальную индикацию нажатий и авто-масштабирование.
    /// </summary>
    public sealed class TouchControlsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ArcadeCarController car;
        [SerializeField] private GameRunController run;
        [SerializeField] private bool useSceneUI;
        [SerializeField] private RogueDrive.UI.SceneTouchButton leftButton, rightButton, gasButton, brakeButton, nitroButton;

        [Header("Configuration")]
        [SerializeField] private bool autoDetectMobile = true;
        [SerializeField] private bool enableOnPC = false;
        [SerializeField] private bool showToggleOnScreen = true;

        bool isControlsEnabled;
        bool leftPressed;
        bool rightPressed;
        bool gasPressed;
        bool brakePressed;
        bool nitroPressed;

        float steerSmooth;

        // Кэшированные стили
        GUIStyle leftStyle;
        GUIStyle rightStyle;
        GUIStyle gasStyle;
        GUIStyle brakeStyle;
        GUIStyle nitroStyle;
        GUIStyle activeStyle;
        GUIStyle toggleStyle;

        Texture2D normalBgTex;
        Texture2D activeBgTex;
        Texture2D nitroBgTex;
        Texture2D brakeBgTex;

        private void Awake()
        {
            if (car == null) car = FindFirstObjectByType<ArcadeCarController>();
            if (run == null) run = FindFirstObjectByType<GameRunController>();

            // На мобильных устройствах включаем автоматически при активном флаге
            isControlsEnabled = (autoDetectMobile && Application.isMobilePlatform) || enableOnPC;
        }

        private void Update()
        {
            if (useSceneUI)
            {
                if (car == null) car = FindFirstObjectByType<ArcadeCarController>();
                bool allowed = Time.timeScale > 0f && (run == null || !run.IsGameOver);
                float steer = allowed ? (rightButton != null && rightButton.Pressed ? 1 : 0) - (leftButton != null && leftButton.Pressed ? 1 : 0) : 0;
                float throttle = allowed ? (gasButton != null && gasButton.Pressed ? 1 : 0) - (brakeButton != null && brakeButton.Pressed ? 1 : 0) : 0;
                if (car != null) car.SetVirtualInput(throttle, steer, allowed && nitroButton != null && nitroButton.Pressed);
                if (Input.GetKeyDown(KeyCode.F1)) PlayerPrefs.SetInt("ShowTouchControls", 1 - PlayerPrefs.GetInt("ShowTouchControls", 0));
                return;
            }
            if (car == null)
            {
                car = FindFirstObjectByType<ArcadeCarController>();
                if (car == null) return;
            }

            // Быстрое переключение по F1 для тестирования в редакторе
            if (Input.GetKeyDown(KeyCode.F1))
            {
                isControlsEnabled = !isControlsEnabled;
            }

            if (!isControlsEnabled)
            {
                return;
            }

            // Плавный расчет руления
            float targetSteer = 0f;
            if (leftPressed) targetSteer -= 1f;
            if (rightPressed) targetSteer += 1f;

            steerSmooth = Mathf.MoveTowards(steerSmooth, targetSteer, Time.deltaTime * 8f);

            float targetThrottle = 0f;
            if (gasPressed) targetThrottle += 1f;
            if (brakePressed) targetThrottle -= 1f;

            car.SetVirtualInput(targetThrottle, steerSmooth, nitroPressed);
        }

        private void OnGUI()
        {
            if (useSceneUI) return;
            EnsureStyles();

            // Кнопка включения/выключения тач-интерфейса в углу экрана для отладки
            if (showToggleOnScreen)
            {
                string toggleText = isControlsEnabled ? "📱 Сенсор: ВКЛ [F1]" : "📱 Сенсор: ВЫКЛ [F1]";
                if (GUI.Button(new Rect(10, Screen.height - 40, 160, 30), toggleText, toggleStyle))
                {
                    isControlsEnabled = !isControlsEnabled;
                }
            }

            if (!isControlsEnabled) return;
            if (Time.timeScale == 0f || (run != null && run.IsGameOver)) return;

            // Расчет адаптивных размеров под разрешение экрана
            float scale = Mathf.Clamp(Screen.height / 720f, 0.8f, 1.8f);
            float btnW = 100f * scale;
            float btnH = 95f * scale;
            float margin = 20f * scale;

            // Сенсорные зоны
            Rect rectLeft = new Rect(margin, Screen.height - btnH - margin - 35f, btnW, btnH);
            Rect rectRight = new Rect(margin + btnW + 15f * scale, Screen.height - btnH - margin - 35f, btnW, btnH);

            float pedalW = 110f * scale;
            float pedalH = 120f * scale;
            Rect rectGas = new Rect(Screen.width - pedalW - margin, Screen.height - pedalH - margin - 15f, pedalW, pedalH);
            Rect rectBrake = new Rect(Screen.width - (pedalW * 2f) - margin - 15f * scale, Screen.height - pedalH - margin - 15f, pedalW, pedalH);
            Rect rectNitro = new Rect(Screen.width - pedalW - margin, Screen.height - pedalH - margin - 15f - (75f * scale), pedalW, 65f * scale);

            // Сброс состояний ввода перед проверкой тачей
            leftPressed = false;
            rightPressed = false;
            gasPressed = false;
            brakePressed = false;
            nitroPressed = false;

            // 1. Проверка реального мультитача (смартфоны)
            int touchCount = Input.touchCount;
            for (int i = 0; i < touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    Vector2 pos = new Vector2(t.position.x, Screen.height - t.position.y);
                    CheckInputHit(pos, rectLeft, rectRight, rectGas, rectBrake, rectNitro);
                }
            }

            // 2. Проверка мыши (для тестирования в редакторе Unity)
            if (Input.GetMouseButton(0))
            {
                Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                CheckInputHit(mousePos, rectLeft, rectRight, rectGas, rectBrake, rectNitro);
            }

            // Отрисовка кнопок с подсветкой активного состояния
            GUI.Box(rectLeft, "◀ ВЛЕВО", leftPressed ? activeStyle : leftStyle);
            GUI.Box(rectRight, "ВПРАВО ▶", rightPressed ? activeStyle : rightStyle);
            GUI.Box(rectGas, "ГАЗ\n▲", gasPressed ? activeStyle : gasStyle);
            GUI.Box(rectBrake, "ТОРМОЗ\n▼", brakePressed ? brakeBgTexStyle() : brakeStyle);
            GUI.Box(rectNitro, "🔥 НИТРО", nitroPressed ? activeStyle : nitroStyle);
        }

        void CheckInputHit(Vector2 pos, Rect left, Rect right, Rect gas, Rect brake, Rect nitro)
        {
            if (left.Contains(pos)) leftPressed = true;
            if (right.Contains(pos)) rightPressed = true;
            if (gas.Contains(pos)) gasPressed = true;
            if (brake.Contains(pos)) brakePressed = true;
            if (nitro.Contains(pos)) nitroPressed = true;
        }

        GUIStyle brakeBgTexStyle()
        {
            if (brakeBgTex == null) brakeBgTex = MakeColorTex(new Color(0.85f, 0.15f, 0.15f, 0.85f));
            var st = new GUIStyle(activeStyle);
            st.normal.background = brakeBgTex;
            return st;
        }

        void EnsureStyles()
        {
            if (normalBgTex == null) normalBgTex = MakeColorTex(new Color(0.08f, 0.10f, 0.14f, 0.65f));
            if (activeBgTex == null) activeBgTex = MakeColorTex(new Color(0.18f, 0.65f, 0.35f, 0.85f));
            if (nitroBgTex == null) nitroBgTex = MakeColorTex(new Color(0.85f, 0.45f, 0.10f, 0.75f));

            if (leftStyle == null)
            {
                leftStyle = CreateBaseStyle(normalBgTex, new Color(0.35f, 0.85f, 1f));
                rightStyle = CreateBaseStyle(normalBgTex, new Color(0.35f, 0.85f, 1f));
                gasStyle = CreateBaseStyle(normalBgTex, new Color(0.35f, 1f, 0.45f));
                brakeStyle = CreateBaseStyle(normalBgTex, new Color(1f, 0.4f, 0.4f));
                nitroStyle = CreateBaseStyle(nitroBgTex, Color.yellow);
                activeStyle = CreateBaseStyle(activeBgTex, Color.white);

                toggleStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        GUIStyle CreateBaseStyle(Texture2D bg, Color textColor)
        {
            return new GUIStyle(GUI.skin.box)
            {
                normal = { background = bg, textColor = textColor },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
        }

        Texture2D MakeColorTex(Color col)
        {
            var tex = new Texture2D(2, 2);
            var pixels = new Color[] { col, col, col, col };
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (normalBgTex != null) Destroy(normalBgTex);
            if (activeBgTex != null) Destroy(activeBgTex);
            if (nitroBgTex != null) Destroy(nitroBgTex);
            if (brakeBgTex != null) Destroy(brakeBgTex);
        }
    }
}
