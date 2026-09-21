using RogueDrive.Gameplay.Track;
using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Контроллер пост-обработки камеры (URP & Screen Post-Processing).
    /// Динамически управляет эффектами:
    /// — Кинематографическая виньетка с пульсацией при критическом запасе прочности (Low HP Heartbeat);
    /// — Хроматическая аберрация и радиальные линии скорости (Speed Warp) при нитро и разгоне > 18 м/с;
    /// — Вспышка красного при получении сильного урона (Damage Flash);
    /// — Электромагнитные помехи развертки (Glitch/Static) во время аномалий.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode]
    public sealed class CameraPostProcessEffects : MonoBehaviour
    {
        public static CameraPostProcessEffects Instance { get; private set; }

        [Header("Shader & Material")]
        [SerializeField] private Shader postProcessShader;
        private Material postProcessMaterial;

        [Header("Vignette Settings")]
        [SerializeField, Range(0.1f, 1.5f)] private float baseVignetteIntensity = 0.55f;
        [SerializeField, Range(0.1f, 1.0f)] private float vignetteSmoothness = 0.45f;
        [SerializeField] private Color normalVignetteColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private Color criticalVignetteColor = new Color(0.75f, 0.05f, 0.05f, 1f);

        [Header("Speed & Nitro Distortion")]
        [SerializeField, Range(0f, 0.03f)] private float maxChromaticAberration = 0.022f;
        [SerializeField, Range(0f, 0.05f)] private float maxRadialBlur = 0.032f;
        [SerializeField, Min(5f)] private float speedThreshold = 16f;

        [Header("Dynamic State")]
        [SerializeField, Range(0f, 1f)] private float damageFlash = 0f;
        [SerializeField, Range(0f, 0.08f)] private float currentGlitch = 0f;

        private ArcadeCarController car;
        private GameRunController run;

        private float currentChromaticAberration;
        private float currentRadialBlur;
        private float currentVignetteIntensity;
        private Color currentVignetteColor;
        private float lastHp;

        private static readonly int VignetteColorProp = Shader.PropertyToID("_VignetteColor");
        private static readonly int VignetteIntensityProp = Shader.PropertyToID("_VignetteIntensity");
        private static readonly int VignetteSmoothnessProp = Shader.PropertyToID("_VignetteSmoothness");
        private static readonly int ChromaticAberrationProp = Shader.PropertyToID("_ChromaticAberration");
        private static readonly int RadialBlurProp = Shader.PropertyToID("_RadialBlurStrength");
        private static readonly int GlitchProp = Shader.PropertyToID("_GlitchIntensity");
        private static readonly int DamageFlashProp = Shader.PropertyToID("_DamageFlash");

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this && Application.isPlaying)
            {
                Destroy(this);
                return;
            }

            EnsureMaterial();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (postProcessMaterial != null)
            {
                DestroyImmediate(postProcessMaterial);
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                car = FindFirstObjectByType<ArcadeCarController>();
                run = FindFirstObjectByType<GameRunController>();
                if (run != null)
                {
                    lastHp = run.Health;
                    run.HealthChanged += OnHealthChanged;
                }
            }
        }

        private void EnsureMaterial()
        {
            if (postProcessMaterial == null)
            {
                if (postProcessShader == null)
                {
                    postProcessShader = Shader.Find("Hidden/RogueDrive/PostProcessScreenShader");
                }

                if (postProcessShader != null)
                {
                    postProcessMaterial = new Material(postProcessShader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current < lastHp)
            {
                float lost = lastHp - current;
                TriggerDamageFlash(Mathf.Clamp01(lost / 25f));
            }
            lastHp = current;
        }

        public void TriggerDamageFlash(float intensity = 0.8f)
        {
            damageFlash = Mathf.Max(damageFlash, intensity);
        }

        private void Update()
        {
            EnsureMaterial();

            float dt = Time.deltaTime;

            // 1. Плавное затухание вспышки урона
            if (damageFlash > 0f)
            {
                damageFlash = Mathf.MoveTowards(damageFlash, 0f, dt * 3.2f);
            }

            if (!Application.isPlaying) return;

            if (car == null)
                car = FindFirstObjectByType<ArcadeCarController>();
            if (run == null)
                run = FindFirstObjectByType<GameRunController>();

            // 2. Расчет эффекта скорости и нитро
            float targetCA = 0f;
            float targetBlur = 0f;

            if (car != null)
            {
                float speed = Mathf.Abs(car.SpeedMps);
                bool isNitro = car.IsNitroActive;

                if (isNitro)
                {
                    targetCA = maxChromaticAberration;
                    targetBlur = maxRadialBlur;
                }
                else if (speed > speedThreshold)
                {
                    float speedFactor = Mathf.Clamp01((speed - speedThreshold) / 10f);
                    targetCA = maxChromaticAberration * 0.65f * speedFactor;
                    targetBlur = maxRadialBlur * 0.55f * speedFactor;
                }
            }

            currentChromaticAberration = Mathf.Lerp(currentChromaticAberration, targetCA, dt * 6f);
            currentRadialBlur = Mathf.Lerp(currentRadialBlur, targetBlur, dt * 6f);

            // 3. Расчет критической виньетки при низком HP
            float hpRatio = run != null && run.MaxHealth > 0f ? (run.Health / run.MaxHealth) : 1f;
            float targetIntensity = baseVignetteIntensity;
            Color targetColor = normalVignetteColor;

            if (hpRatio < 0.35f)
            {
                // Пульсация тревожным красным (Heartbeat)
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6.5f);
                float crisisSeverity = (0.35f - hpRatio) / 0.35f; // 0..1
                targetIntensity = Mathf.Lerp(baseVignetteIntensity, 0.95f, crisisSeverity + pulse * 0.2f);
                targetColor = Color.Lerp(normalVignetteColor, criticalVignetteColor, crisisSeverity * 0.85f + pulse * 0.15f);
            }

            currentVignetteIntensity = Mathf.Lerp(currentVignetteIntensity, targetIntensity, dt * 4f);
            currentVignetteColor = Color.Lerp(currentVignetteColor, targetColor, dt * 4f);

            // 4. Расчет глитча от EMP и аномалий
            float targetGlitch = 0f;
            if (TrackWeatherHazardManager.Instance != null && TrackWeatherHazardManager.Instance.IsEMPActive)
            {
                // Случайные импульсы помех
                if (Random.value < 0.18f)
                    targetGlitch = Random.Range(0.015f, 0.055f);
            }
            currentGlitch = Mathf.Lerp(currentGlitch, targetGlitch, dt * 10f);
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (postProcessMaterial == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            postProcessMaterial.SetColor(VignetteColorProp, currentVignetteColor);
            postProcessMaterial.SetFloat(VignetteIntensityProp, currentVignetteIntensity);
            postProcessMaterial.SetFloat(VignetteSmoothnessProp, vignetteSmoothness);
            postProcessMaterial.SetFloat(ChromaticAberrationProp, currentChromaticAberration);
            postProcessMaterial.SetFloat(RadialBlurProp, currentRadialBlur);
            postProcessMaterial.SetFloat(GlitchProp, currentGlitch);
            postProcessMaterial.SetFloat(DamageFlashProp, damageFlash);

            Graphics.Blit(src, dest, postProcessMaterial);
        }
    }
}
