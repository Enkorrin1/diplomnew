using System;
using System.Collections.Generic;
using RogueDrive.Meta;
using RogueDrive.Modifiers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.UI
{
    /// <summary>
    /// Контроллер главного меню: витрина лучшего автомобиля на 3D-подиуме,
    /// баланс монет, рекорд дистанции, переход в Гараж, Заезд, Настройки и информацию о дипломе.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("3D Showcase")]
        [SerializeField] private Transform podiumAnchor;
        [SerializeField] private float rotationSpeed = 20f;
        [SerializeField] private GarageCatalog catalog;
        [SerializeField] private bool useSceneUI = true;
        [SerializeField] private GameObject[] showcaseModels;

        private MetaProgress metaProgress;
        private GameObject currentCarModel;
        private float currentAngle;

        private bool showAboutModal = false;
        private bool showSettingsModal = false;

        // Settings variables
        private float masterVolume = 0.85f;
        private float musicVolume = 0.75f;
        private float sfxVolume = 0.9f;

        // GUI Styles
        private GUIStyle titleStyle;
        private GUIStyle subTitleStyle;
        private GUIStyle btnMainStyle;
        private GUIStyle btnSecondaryStyle;
        private GUIStyle statsStyle;
        private GUIStyle modalBoxStyle;
        private GUIStyle modalHeaderStyle;
        private GUIStyle modalTextStyle;

        private Texture2D panelBgTex;
        private Texture2D btnMainTex;
        private Texture2D btnSecondaryTex;
        private Texture2D modalBgTex;

        private void Awake()
        {
            // Защита: MainMenuController должен работать ТОЛЬКО на сцене главного меню
            if (!SceneManager.GetActiveScene().name.Contains("MainMenu"))
            {
                Destroy(gameObject);
                return;
            }

            Time.timeScale = 1f;
            useSceneUI = true;
            if (!useSceneUI) EnsureEnvironment();

            if (catalog == null)
            {
                catalog = Resources.Load<GarageCatalog>("GarageCatalog");
#if UNITY_EDITOR
                if (catalog == null)
                    catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<GarageCatalog>("Assets/Content/GarageCatalog.asset");
#endif
            }

            IReadOnlyList<UpgradeTrack> tracks = catalog != null ? catalog.Upgrades : Array.Empty<UpgradeTrack>();
            IReadOnlyList<CarDefinition> cars = catalog != null ? catalog.Cars : Array.Empty<CarDefinition>();
            metaProgress = SaveService.GetActiveProgress(tracks, cars);

            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.85f);
            musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
            sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 0.9f);

            SetupShowcaseCar();
        }

        private void SetupShowcaseCar()
        {
            if (showcaseModels != null && showcaseModels.Length > 0 && catalog != null)
            {
                for (int i = 0; i < showcaseModels.Length; i++)
                    if (showcaseModels[i] != null) showcaseModels[i].SetActive(i < catalog.Cars.Count && catalog.Cars[i] == metaProgress.SelectedCar);
                return;
            }
            if (podiumAnchor == null)
            {
                GameObject podium = new GameObject("PodiumAnchor");
                podium.transform.position = new Vector3(0f, 0.5f, 0f);
                podiumAnchor = podium.transform;
            }

            if (catalog != null && catalog.Cars != null && catalog.Cars.Count > 0)
            {
                int selectedIndex = 0;
                string savedId = metaProgress != null && metaProgress.SelectedCar != null
                    ? metaProgress.SelectedCar.Id
                    : catalog.Cars[0].Id;

                for (int i = 0; i < catalog.Cars.Count; i++)
                {
                    if (catalog.Cars[i] != null && catalog.Cars[i].Id == savedId)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                CarDefinition def = catalog.Cars[selectedIndex];
                GameObject carPrefab = def != null ? def.EffectivePrefab : null;
                if (carPrefab != null)
                {
                    currentCarModel = Instantiate(carPrefab, podiumAnchor);
                    currentCarModel.transform.localPosition = Vector3.zero;
                    currentCarModel.transform.localRotation = Quaternion.identity;

                    // Отключаем физику на подиуме
                    Rigidbody rb = currentCarModel.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;

                    // Очищаем лишние скрипты управления
                    MonoBehaviour[] scripts = currentCarModel.GetComponentsInChildren<MonoBehaviour>();
                    for (int i = 0; i < scripts.Length; i++)
                    {
                        if (scripts[i] != this) scripts[i].enabled = false;
                    }
                }
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.Contains("MainMenu"))
            {
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void Update()
        {
            if (podiumAnchor != null)
            {
                currentAngle += rotationSpeed * Time.deltaTime;
                podiumAnchor.rotation = Quaternion.Euler(0f, currentAngle, 0f);
            }
        }

        private void OnGUI()
        {
            if (useSceneUI) return;
            if (!SceneManager.GetActiveScene().name.Contains("MainMenu"))
            {
                return;
            }

            EnsureStyles();

            float scale = Mathf.Clamp(Screen.height / 720f, 0.85f, 1.5f);

            // Верхняя плашка профиля (Монеты и рекорд)
            DrawTopBar(scale);

            // Главный заголовок слева
            DrawTitle(scale);

            // Меню кнопок слева
            DrawMenuButtons(scale);

            // Модальное окно "Об игре / БГУИР"
            if (showAboutModal)
            {
                DrawAboutModal(scale);
            }

            // Модальное окно "Настройки"
            if (showSettingsModal)
            {
                DrawSettingsModal(scale);
            }
        }

        private void DrawTopBar(float scale)
        {
            int coins = metaProgress != null ? metaProgress.Coins : 0;
            float maxDist = PlayerPrefs.GetFloat("BestDistanceRecord", 0f);

            float barW = 380f * scale;
            float barH = 50f * scale;
            Rect topBarRect = new Rect(Screen.width - barW - 20f, 20f, barW, barH);

            GUI.Box(topBarRect, string.Empty, modalBoxStyle);
            GUI.Label(new Rect(topBarRect.x + 15f, topBarRect.y + 12f, 180f * scale, 30f * scale),
                $"💰 Монеты: <color=#FFD700><b>{coins}</b></color>", statsStyle);
            GUI.Label(new Rect(topBarRect.x + 190f * scale, topBarRect.y + 12f, 180f * scale, 30f * scale),
                $"🏁 Рекорд: <color=#00E5FF><b>{maxDist:F0} м</b></color>", statsStyle);
        }

        private void DrawTitle(float scale)
        {
            float startX = 40f * scale;
            float startY = 40f * scale;

            GUI.Label(new Rect(startX, startY, 500f * scale, 55f * scale), "ROGUE DRIVE", titleStyle);
            GUI.Label(new Rect(startX + 2f, startY + 52f * scale, 500f * scale, 30f * scale), "SURVIVAL: DIPLOMA EDITION", subTitleStyle);
        }

        private void DrawMenuButtons(float scale)
        {
            float btnW = 280f * scale;
            float btnH = 55f * scale;
            float startX = 40f * scale;
            float startY = 160f * scale;
            float spacing = 15f * scale;

            // 1. В ЗАЕЗД
            if (GUI.Button(new Rect(startX, startY, btnW, btnH), "⚔ В ЗАЕЗД", btnMainStyle))
            {
                SceneTransitionManager.SwitchScene("RogueDrivePrototype");
            }

            // 2. ГАРАЖ
            startY += btnH + spacing;
            if (GUI.Button(new Rect(startX, startY, btnW, btnH), "🛠 ГАРАЖ И АВТОПАРК", btnSecondaryStyle))
            {
                SceneTransitionManager.SwitchScene("GarageScene");
            }

            // 3. НАСТРОЙКИ
            startY += btnH + spacing;
            if (GUI.Button(new Rect(startX, startY, btnW, btnH), "⚙ НАСТРОЙКИ", btnSecondaryStyle))
            {
                showSettingsModal = true;
                showAboutModal = false;
            }

            // 4. ОБ ИГРЕ / БГУИР ДИПЛОМ
            startY += btnH + spacing;
            if (GUI.Button(new Rect(startX, startY, btnW, btnH), "🎓 ОБ ИГРЕ (БГУИР)", btnSecondaryStyle))
            {
                showAboutModal = true;
                showSettingsModal = false;
            }

            // 5. ВЫХОД
            startY += btnH + spacing;
            if (GUI.Button(new Rect(startX, startY, btnW, btnH * 0.8f), "ВЫХОД", btnSecondaryStyle))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        private void DrawAboutModal(float scale)
        {
            float w = 550f * scale;
            float h = 420f * scale;
            Rect modalRect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            GUI.Box(modalRect, string.Empty, modalBoxStyle);

            float y = modalRect.y + 20f * scale;
            GUI.Label(new Rect(modalRect.x, y, w, 35f * scale), "ДИПЛОМНЫЙ ПРОЕКТ БГУИР", modalHeaderStyle);

            y += 45f * scale;
            string text =
                "<b>Тема:</b> Разработка кроссплатформенной игры жанра Action Survival Roguelite с процедурной генерацией и балансировкой методом Монте-Карло\n\n" +
                "<b>Учреждение:</b> Белорусский государственный университет информатики и радиоэлектроники (БГУИР, Минск 2026)\n" +
                "<b>Стек:</b> Unity 6 LTS (URP), C# 9.0, PhysX, Python Matplotlib\n" +
                "<b>Особенности:</b>\n" +
                " • Аркадная физика 4 типов автомобилей и процедурные трассы\n" +
                " • 360° авто-турель, боевые ауры и 3-фазный босс «Джаггернаут»\n" +
                " • Автономная симуляция баланса Монте-Карло (6000+ заездов)\n" +
                " • Процедурный синтезатор звука и адаптивный мультитач Android";

            GUI.Label(new Rect(modalRect.x + 25f * scale, y, w - 50f * scale, 260f * scale), text, modalTextStyle);

            if (GUI.Button(new Rect(modalRect.x + (w - 180f * scale) / 2f, modalRect.yMax - 55f * scale, 180f * scale, 40f * scale), "ЗАКРЫТЬ", btnSecondaryStyle))
            {
                showAboutModal = false;
            }
        }

        private void DrawSettingsModal(float scale)
        {
            float w = 500f * scale;
            float h = 380f * scale;
            Rect modalRect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            GUI.Box(modalRect, string.Empty, modalBoxStyle);

            float y = modalRect.y + 20f * scale;
            GUI.Label(new Rect(modalRect.x, y, w, 35f * scale), "НАСТРОЙКИ ИГРЫ", modalHeaderStyle);

            y += 50f * scale;
            float labelW = 180f * scale;
            float sliderW = 200f * scale;
            float startX = modalRect.x + 35f * scale;

            // Общая громкость
            GUI.Label(new Rect(startX, y, labelW, 25f * scale), $"Общая громкость: {Mathf.RoundToInt(masterVolume * 100)}%", modalTextStyle);
            masterVolume = GUI.HorizontalSlider(new Rect(startX + labelW, y + 5f, sliderW, 20f), masterVolume, 0f, 1f);

            // Музыка
            y += 45f * scale;
            GUI.Label(new Rect(startX, y, labelW, 25f * scale), $"Музыка (BGM): {Mathf.RoundToInt(musicVolume * 100)}%", modalTextStyle);
            musicVolume = GUI.HorizontalSlider(new Rect(startX + labelW, y + 5f, sliderW, 20f), musicVolume, 0f, 1f);

            // Звуковые эффекты
            y += 45f * scale;
            GUI.Label(new Rect(startX, y, labelW, 25f * scale), $"Эффекты (SFX): {Mathf.RoundToInt(sfxVolume * 100)}%", modalTextStyle);
            sfxVolume = GUI.HorizontalSlider(new Rect(startX + labelW, y + 5f, sliderW, 20f), sfxVolume, 0f, 1f);

            // Графика / дисплей
            y += 45f * scale;
            string fpsLabel = Application.isMobilePlatform ? "Целевой FPS: 60" : "FPS: без ограничений";
            string screenLabel = Application.isMobilePlatform
                ? "<color=#00E5FF>Mobile</color>"
                : $"<color=#00E5FF>{(Screen.fullScreen ? "Полный экран" : "Окно")}  F11 — переключить</color>";
            GUI.Label(new Rect(startX,          y, labelW, 25f * scale), fpsLabel,    modalTextStyle);
            GUI.Label(new Rect(startX + labelW, y, sliderW, 25f * scale), screenLabel, modalTextStyle);

            // Кнопка сохранения
            if (GUI.Button(new Rect(modalRect.x + (w - 200f * scale) / 2f, modalRect.yMax - 55f * scale, 200f * scale, 40f * scale), "СОХРАНИТЬ", btnMainStyle))
            {
                PlayerPrefs.SetFloat("MasterVolume", masterVolume);
                PlayerPrefs.SetFloat("MusicVolume", musicVolume);
                PlayerPrefs.SetFloat("SfxVolume", sfxVolume);
                PlayerPrefs.Save();
                AudioListener.volume = masterVolume;
                showSettingsModal = false;
            }
        }

        private void EnsureStyles()
        {
            if (panelBgTex == null) panelBgTex = MakeColorTex(new Color(0.06f, 0.08f, 0.12f, 0.85f));
            if (btnMainTex == null) btnMainTex = MakeColorTex(new Color(0.12f, 0.65f, 0.38f, 0.95f));
            if (btnSecondaryTex == null) btnSecondaryTex = MakeColorTex(new Color(0.14f, 0.18f, 0.26f, 0.9f));
            if (modalBgTex == null) modalBgTex = MakeColorTex(new Color(0.04f, 0.05f, 0.08f, 0.95f));

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 44,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.95f, 0.75f, 0.15f) }
                };

                subTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.35f, 0.85f, 1f) }
                };

                btnMainStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = btnMainTex, textColor = Color.white }
                };

                btnSecondaryStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { background = btnSecondaryTex, textColor = new Color(0.85f, 0.90f, 0.95f) }
                };

                statsStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleLeft,
                    richText = true,
                    normal = { textColor = Color.white }
                };

                modalBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = modalBgTex }
                };

                modalHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.8f, 0.2f) }
                };

                modalTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    wordWrap = true,
                    richText = true,
                    normal = { textColor = new Color(0.9f, 0.9f, 0.95f) }
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

        private void EnsureEnvironment()
        {
            if (Camera.main == null && FindFirstObjectByType<Camera>() == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                Camera cam = camObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
                cam.fieldOfView = 50f;
                camObj.transform.position = new Vector3(0f, 2.2f, -5.5f);
                camObj.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }

            if (FindFirstObjectByType<Light>() == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                Light dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.color = new Color(0.85f, 0.9f, 1f);
                dirLight.intensity = 0.8f;
                lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

                GameObject spotObj = new GameObject("Podium Spotlight");
                Light spotLight = spotObj.AddComponent<Light>();
                spotLight.type = LightType.Spot;
                spotLight.color = new Color(0.2f, 0.9f, 1f);
                spotLight.intensity = 2.5f;
                spotLight.range = 15f;
                spotLight.spotAngle = 65f;
                spotObj.transform.position = new Vector3(0f, 6f, 0f);
                spotObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            RenderSettings.ambientLight = new Color(0.12f, 0.15f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.12f);
            RenderSettings.fogDensity = 0.035f;

            if (GameObject.Find("PodiumPedestal") == null)
            {
                GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pedestal.name = "PodiumPedestal";
                pedestal.transform.position = new Vector3(0f, -0.1f, 0f);
                pedestal.transform.localScale = new Vector3(4.5f, 0.2f, 4.5f);
                Renderer pedR = pedestal.GetComponent<Renderer>();
                if (pedR != null)
                {
                    Material m = new Material(Shader.Find("Standard"));
                    m.color = new Color(0.1f, 0.12f, 0.16f);
                    pedR.sharedMaterial = m;
                }

                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "NeonRing";
                ring.transform.position = new Vector3(0f, -0.05f, 0f);
                ring.transform.localScale = new Vector3(4.7f, 0.05f, 4.7f);
                Renderer ringR = ring.GetComponent<Renderer>();
                if (ringR != null)
                {
                    Material rm = new Material(Shader.Find("Standard"));
                    rm.color = new Color(0f, 0.85f, 1f);
                    rm.EnableKeyword("_EMISSION");
                    rm.SetColor("_EmissionColor", new Color(0f, 0.85f, 1f) * 1.5f);
                    ringR.sharedMaterial = rm;
                }
            }
        }

        private void OnDestroy()
        {
            if (currentCarModel != null)
            {
                Destroy(currentCarModel);
            }
            if (panelBgTex != null) Destroy(panelBgTex);
            if (btnMainTex != null) Destroy(btnMainTex);
            if (btnSecondaryTex != null) Destroy(btnSecondaryTex);
            if (modalBgTex != null) Destroy(modalBgTex);
        }
    }
}
