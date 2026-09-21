using System;
using System.Collections;
using RogueDrive.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Track
{
    public enum WeatherType
    {
        Clear = 0,
        Sandstorm = 1,
        AcidRain = 2,
        EMPStorm = 3
    }

    /// <summary>
    /// Менеджер погодных условий и динамических аномалий на трассе (Hazards & Events).
    /// Управляет сменой погоды по пройденной дистанции, регулирует атмосферный туман URP/Built-in,
    /// активирует частицы песчаной бури и кислотного дождя, передает предупреждения по радиосвязи
    /// и оказывает прямое влияние на геймплей (дальность турели, форсаж нитро, урон кузову, помехи приборов).
    /// </summary>
    public sealed class TrackWeatherHazardManager : MonoBehaviour
    {
        public static TrackWeatherHazardManager Instance { get; private set; }

        public static event Action<WeatherType, string> WeatherChanged;

        [Header("Weather Cycle Settings")]
        [SerializeField] private WeatherType currentWeather = WeatherType.Clear;
        [SerializeField, Min(300f)] private float weatherCheckIntervalDistance = 750f;
        [SerializeField, Range(0.1f, 0.8f)] private float hazardChance = 0.55f;

        [Header("Weather Multipliers")]
        [SerializeField] private float turretRangeMultiplier = 1.0f;
        [SerializeField] private float nitroBonusMultiplier = 1.0f;
        [SerializeField] private float acidDamagePerSec = 0f;
        [SerializeField] private bool isEMPActive = false;

        [Header("Atmospheric Presets")]
        [SerializeField] private Color clearFogColor = new Color(0.45f, 0.55f, 0.65f);
        [SerializeField] private Color sandstormFogColor = new Color(0.85f, 0.58f, 0.28f);
        [SerializeField] private Color acidFogColor = new Color(0.28f, 0.75f, 0.35f);
        [SerializeField] private Color empFogColor = new Color(0.15f, 0.18f, 0.30f);

        private ArcadeCarController playerCar;
        private GameRunController run;
        private Light directionalSun;

        private float lastDistanceChecked = 0f;
        private float targetFogDensity = 0.005f;
        private Color targetFogColor;
        private ParticleSystem weatherParticles;

        // UI Banner & Radio
        private GameObject radioBannerRoot;
        private Text radioText;
        private Coroutine radioBannerRoutine;

        public WeatherType CurrentWeather => currentWeather;
        public float TurretRangeMultiplier => turretRangeMultiplier;
        public float NitroBonusMultiplier => nitroBonusMultiplier;
        public float AcidDamagePerSec => acidDamagePerSec;
        public bool IsEMPActive => isEMPActive;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            targetFogColor = clearFogColor;
            RenderSettings.fogColor = clearFogColor;
            RenderSettings.fogDensity = 0.005f;

            EnsureWeatherParticleSystem();
            BuildRadioBannerUI();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            playerCar = FindFirstObjectByType<ArcadeCarController>();
            run = FindFirstObjectByType<GameRunController>();

            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    directionalSun = lights[i];
                    break;
                }
            }

            SetWeather(WeatherType.Clear, false);
        }

        private void Update()
        {
            if (run == null)
            {
                run = FindFirstObjectByType<GameRunController>();
                if (run == null) return;
            }

            // Плавный переход цвета и плотности тумана
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, Time.deltaTime * 1.5f);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * 1.5f);

            // Кислотный дождь разъедает корпус машины при открытом движении
            if (acidDamagePerSec > 0f && run != null && !run.IsGameOver)
            {
                run.TakeDamage(acidDamagePerSec * Time.deltaTime);
            }

            // Проверка смены погоды по пройденной дистанции
            float currentDist = run.Distance;
            if (currentDist - lastDistanceChecked >= weatherCheckIntervalDistance)
            {
                lastDistanceChecked = currentDist;
                EvaluateWeatherChange();
            }

            // Синхронизация погодных частиц с положением камеры/машины
            if (weatherParticles != null)
            {
                Transform camT = Camera.main != null ? Camera.main.transform : (playerCar != null ? playerCar.transform : null);
                if (camT != null)
                {
                    weatherParticles.transform.position = camT.position + camT.forward * 18f + Vector3.up * 6f;
                }
            }
        }

        private void EvaluateWeatherChange()
        {
            if (currentWeather != WeatherType.Clear)
            {
                // Возврат к ясной погоде после преодоления зоны бури
                SetWeather(WeatherType.Clear, true);
                return;
            }

            if (UnityEngine.Random.value <= hazardChance)
            {
                // Случайный выбор одного из опасных погодных явлений
                int roll = UnityEngine.Random.Range(1, 4);
                SetWeather((WeatherType)roll, true);
            }
        }

        public void SetWeather(WeatherType weather, bool announce = true)
        {
            currentWeather = weather;

            switch (weather)
            {
                case WeatherType.Sandstorm:
                    turretRangeMultiplier = 0.70f;
                    nitroBonusMultiplier = 1.0f;
                    acidDamagePerSec = 0f;
                    isEMPActive = false;
                    targetFogColor = sandstormFogColor;
                    targetFogDensity = 0.024f;
                    ConfigureParticles(new Color(0.85f, 0.65f, 0.35f, 0.7f), 80f, 150);
                    if (announce)
                        BroadcastRadio("МАЯК (РАДИОСВЯЗЬ): Внимание! Вход в зону песчаной бури! Видимость падает, дальность турелей -30%!");
                    break;

                case WeatherType.AcidRain:
                    turretRangeMultiplier = 0.90f;
                    nitroBonusMultiplier = 1.25f; // кислота испаряется на соплах, давая форсаж
                    acidDamagePerSec = 1.8f;       // легкий постоянный урон
                    isEMPActive = false;
                    targetFogColor = acidFogColor;
                    targetFogDensity = 0.018f;
                    ConfigureParticles(new Color(0.35f, 0.95f, 0.40f, 0.8f), 45f, 220);
                    if (announce)
                        BroadcastRadio("МАЯК (РАДИОСВЯЗЬ): Кислотный шторм! Корпус разъедает, но двигатели получают форсаж нитро (+25%)!");
                    break;

                case WeatherType.EMPStorm:
                    turretRangeMultiplier = 0.80f;
                    nitroBonusMultiplier = 1.0f;
                    acidDamagePerSec = 0f;
                    isEMPActive = true;
                    targetFogColor = empFogColor;
                    targetFogDensity = 0.015f;
                    ConfigureParticles(new Color(0.40f, 0.60f, 1.0f, 0.6f), 30f, 80);
                    if (announce)
                        BroadcastRadio("МАЯК (РАДИОСВЯЗЬ): Электромагнитная аномалия! Приборы барахлят, следите за радаром!");
                    break;

                case WeatherType.Clear:
                default:
                    turretRangeMultiplier = 1.0f;
                    nitroBonusMultiplier = 1.0f;
                    acidDamagePerSec = 0f;
                    isEMPActive = false;
                    targetFogColor = clearFogColor;
                    targetFogDensity = 0.005f;
                    if (weatherParticles != null)
                        weatherParticles.Stop();
                    if (announce)
                        BroadcastRadio("МАЯК (РАДИОСВЯЗЬ): Погодная аномалия пройдена. Системы машины работают в штатном режиме.");
                    break;
            }

            WeatherChanged?.Invoke(currentWeather, currentWeather.ToString());
        }

        private void ConfigureParticles(Color color, float speed, int emissionRate)
        {
            if (weatherParticles == null) return;

            var main = weatherParticles.main;
            main.startColor = color;
            main.startSpeed = speed;

            var emission = weatherParticles.emission;
            emission.rateOverTime = emissionRate;

            if (!weatherParticles.isPlaying)
                weatherParticles.Play();
        }

        private void EnsureWeatherParticleSystem()
        {
            if (weatherParticles != null) return;

            GameObject pObj = new GameObject("WeatherFX_ParticleSystem");
            pObj.transform.SetParent(transform);
            weatherParticles = pObj.AddComponent<ParticleSystem>();

            var main = weatherParticles.main;
            main.loop = true;
            main.startLifetime = 1.8f;
            main.startSize = 0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var shape = weatherParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(35f, 12f, 35f);

            var emission = weatherParticles.emission;
            emission.rateOverTime = 0;

            var renderer = pObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Material mat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default"));
            renderer.sharedMaterial = mat;
        }

        private void BuildRadioBannerUI()
        {
            if (radioBannerRoot != null) return;

            Canvas existingCanvas = FindFirstObjectByType<Canvas>();
            GameObject canvasObj = existingCanvas != null ? existingCanvas.gameObject : null;

            if (canvasObj == null)
            {
                canvasObj = new GameObject("WeatherRadioCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas c = canvasObj.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 95;

                CanvasScaler cs = canvasObj.GetComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
            }

            radioBannerRoot = new GameObject("RadioWeatherBanner", typeof(RectTransform), typeof(Image));
            radioBannerRoot.transform.SetParent(canvasObj.transform, false);

            RectTransform rt = radioBannerRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 0.84f);
            rt.anchorMax = new Vector2(0.85f, 0.93f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = radioBannerRoot.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.18f, 0.90f);

            GameObject textObj = new GameObject("RadioText", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(radioBannerRoot.transform, false);

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(15f, 5f);
            textRt.offsetMax = new Vector2(-15f, -5f);

            radioText = textObj.GetComponent<Text>();
            radioText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            radioText.fontSize = 22;
            radioText.alignment = TextAnchor.MiddleCenter;
            radioText.color = new Color(1f, 0.88f, 0.35f);
            radioText.fontStyle = FontStyle.Bold;

            radioBannerRoot.SetActive(false);
        }

        public void BroadcastRadio(string message, float duration = 4.5f)
        {
            if (radioBannerRoutine != null)
                StopCoroutine(radioBannerRoutine);

            radioBannerRoutine = StartCoroutine(ShowRadioBannerRoutine(message, duration));
        }

        private IEnumerator ShowRadioBannerRoutine(string message, float duration)
        {
            if (radioBannerRoot == null || radioText == null) yield break;

            radioText.text = message;
            radioBannerRoot.SetActive(true);

            // Звуковой сигнал рации / переключения канала
            AudioManager.Instance?.PlayCoin();

            yield return new WaitForSeconds(duration);

            radioBannerRoot.SetActive(false);
            radioBannerRoutine = null;
        }
    }
}
