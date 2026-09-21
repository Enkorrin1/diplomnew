using System;
using RogueDrive.Audio;
using RogueDrive.Gameplay.VFX;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Track
{
    /// <summary>
    /// Надвигающаяся стена бури (The Creeping Storm Barrier / Death Wall).
    /// Смертоносный фронт радиоактивной аномалии, непрерывно преследующий автомобиль сзади.
    /// Задает неумолимый темп выживания в заезде:
    /// — При сближении менее 70м активирует тревожный радар на HUD;
    /// — При сближении менее 35м включает аварийную сирену и тряску экрана;
    /// — При поглощении машины наносит 18 HP урона в секунду и ускоряет расход топлива.
    /// </summary>
    public sealed class CreepingStormBarrier : MonoBehaviour
    {
        public static CreepingStormBarrier Instance { get; private set; }

        [Header("Movement & Speed")]
        [SerializeField, Min(5f)] private float baseChaseSpeed = 13.8f;      // ~50 км/ч
        [SerializeField, Min(20f)] private float startOffsetBehind = 130f;
        [SerializeField, Min(10f)] private float warningDistance = 70f;
        [SerializeField, Min(5f)] private float criticalDistance = 35f;
        [SerializeField, Min(1f)] private float stormDamagePerSec = 18f;

        [Header("Status Readout")]
        [SerializeField] private float stormZ;
        [SerializeField] private float distanceToCar = 130f;
        [SerializeField] private bool isEngulfed = false;

        private ArcadeCarController playerCar;
        private GameRunController run;

        private GameObject stormVisualObject;
        private ParticleSystem stormParticles;

        // UI Radar
        private GameObject stormRadarRoot;
        private Text stormDistanceText;
        private Image stormWarningIcon;
        private float beepTimer;

        public float StormZ => stormZ;
        public float DistanceToCar => distanceToCar;
        public bool IsEngulfed => isEngulfed;

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

            BuildStormVisualWall();
            BuildStormRadarUI();
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

            float startZ = playerCar != null ? playerCar.transform.position.z : 0f;
            stormZ = startZ - startOffsetBehind;

            if (stormVisualObject != null)
            {
                stormVisualObject.transform.position = new Vector3(0f, 6f, stormZ);
            }
        }

        private void Update()
        {
            if (run == null)
            {
                run = FindFirstObjectByType<GameRunController>();
                if (run == null) return;
            }

            if (playerCar == null)
            {
                playerCar = FindFirstObjectByType<ArcadeCarController>();
                if (playerCar == null) return;
            }

            if (run.IsGameOver) return;

            float dt = Time.deltaTime;
            float carZ = playerCar.transform.position.z;
            distanceToCar = carZ - stormZ;

            // Адаптивная скорость преследования:
            // Если игрок улетел далеко вперед (> 150м) — буря ускоряется для поддержания драйва.
            // Если игрок на грани поглощения (< 15м) — скорость чуть стабилизируется, давая шанс на нитро.
            float currentSpeed = baseChaseSpeed;
            if (distanceToCar > 150f)
            {
                currentSpeed = Mathf.Lerp(baseChaseSpeed, 18.5f, (distanceToCar - 150f) / 100f);
            }
            else if (distanceToCar < 15f && distanceToCar > 0f)
            {
                currentSpeed = Mathf.Lerp(11.5f, baseChaseSpeed, distanceToCar / 15f);
            }

            stormZ += currentSpeed * dt;

            // Синхронизация 3D-стены бури
            if (stormVisualObject != null)
            {
                float carX = playerCar.transform.position.x;
                stormVisualObject.transform.position = new Vector3(carX, 6f, stormZ);
            }

            // Обработка критических зон
            isEngulfed = distanceToCar <= 0f;

            if (isEngulfed)
            {
                // Машина внутри смертоносного фронта бури!
                run.TakeDamage(stormDamagePerSec * dt);
                ArcadeCameraFollow.Instance?.TriggerShake(0.35f, 0.1f);
                CameraPostProcessEffects.Instance?.TriggerDamageFlash(0.4f);
            }

            UpdateRadarUI(dt);
        }

        private void UpdateRadarUI(float dt)
        {
            if (stormRadarRoot == null || stormDistanceText == null) return;

            if (distanceToCar < warningDistance)
            {
                stormRadarRoot.SetActive(true);

                int meters = Mathf.Max(0, Mathf.RoundToInt(distanceToCar));

                if (isEngulfed)
                {
                    stormDistanceText.text = "⚠️ ВНУТРИ СТЕНЫ БУРИ! ГАЗ В ПОЛ! ⚠️";
                    stormDistanceText.color = Color.red;
                }
                else if (distanceToCar < criticalDistance)
                {
                    stormDistanceText.text = $"⚡ СТЕНА БУРИ: {meters}м — ЖМИ НА ГАЗ!";
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
                    stormDistanceText.color = Color.Lerp(Color.yellow, Color.red, pulse);

                    beepTimer += dt;
                    if (beepTimer >= 0.65f)
                    {
                        beepTimer = 0f;
                        AudioManager.Instance?.PlayMineBeep(1.35f);
                    }
                }
                else
                {
                    stormDistanceText.text = $"⚠️ СТЕНА БУРИ СЗАДИ: {meters}м";
                    stormDistanceText.color = new Color(1f, 0.85f, 0.25f);
                }
            }
            else
            {
                stormRadarRoot.SetActive(false);
            }
        }

        private void BuildStormVisualWall()
        {
            if (stormVisualObject != null) return;

            stormVisualObject = new GameObject("CreepingStormWall");
            stormVisualObject.transform.SetParent(transform);

            // Частицы пыли и молний фронта
            stormParticles = stormVisualObject.AddComponent<ParticleSystem>();
            var main = stormParticles.main;
            main.loop = true;
            main.startLifetime = 2.5f;
            main.startSpeed = 8f;
            main.startSize = 4.5f;
            main.startColor = new Color(0.85f, 0.35f, 0.15f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;

            var shape = stormParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(55f, 22f, 8f);

            var emission = stormParticles.emission;
            emission.rateOverTime = 160;

            var renderer = stormVisualObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Material mat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default"));
            renderer.sharedMaterial = mat;

            // Зловещий свет фронта бури
            GameObject lightObj = new GameObject("StormGlowLight");
            lightObj.transform.SetParent(stormVisualObject.transform, false);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.35f, 0.1f);
            l.range = 45f;
            l.intensity = 3.5f;
        }

        private void BuildStormRadarUI()
        {
            if (stormRadarRoot != null) return;

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            stormRadarRoot = new GameObject("StormRadarPanel", typeof(RectTransform), typeof(Image));
            stormRadarRoot.transform.SetParent(canvas.transform, false);

            RectTransform rt = stormRadarRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.30f, 0.93f);
            rt.anchorMax = new Vector2(0.70f, 0.985f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = stormRadarRoot.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.05f, 0.05f, 0.90f);

            GameObject textObj = new GameObject("DistanceText", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(stormRadarRoot.transform, false);

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 2f);
            textRt.offsetMax = new Vector2(-10f, -2f);

            stormDistanceText = textObj.GetComponent<Text>();
            stormDistanceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            stormDistanceText.fontSize = 20;
            stormDistanceText.alignment = TextAnchor.MiddleCenter;
            stormDistanceText.color = Color.yellow;
            stormDistanceText.fontStyle = FontStyle.Bold;

            stormRadarRoot.SetActive(false);
        }
    }
}
