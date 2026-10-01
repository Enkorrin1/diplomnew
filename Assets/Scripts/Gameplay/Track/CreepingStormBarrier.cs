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
        private StageRoute route;
        private Hub.GaragePlayerController pedestrian;
        private float routeDistance;

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
        public float RouteDistance => routeDistance;
        public void BeginNextWave(float departureDistance)
        {
            routeDistance=departureDistance-(route!=null?route.StormLead:900);
            distanceToCar=departureDistance-routeDistance;isEngulfed=false;beepTimer=0;
            if(stormVisualObject!=null)stormVisualObject.SetActive(true);
        }

        public void RebindContinuousRoute(StageRoute continuous, float distanceOffset)
        {
            if (route == continuous) return;
            routeDistance += distanceOffset;
            route = continuous;
        }

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
            if (stormRadarRoot != null) Destroy(stormRadarRoot);
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            playerCar = FindFirstObjectByType<ArcadeCarController>();
            run = FindFirstObjectByType<GameRunController>();
            pedestrian=FindFirstObjectByType<Hub.GaragePlayerController>(FindObjectsInactive.Include);

            float startZ = playerCar != null ? playerCar.transform.position.z : 0f;
            stormZ = startZ - startOffsetBehind;
            route = StageRoute.Instance;
            if (route != null)
                routeDistance = (playerCar != null ? route.ProjectDistance(playerCar.transform.position) : 0f) - route.StormLead;

            if (stormVisualObject != null)
            {
                stormVisualObject.transform.position = new Vector3(0f, 6f, stormZ);
            }
        }

        private void Update()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenuScene")
            {
                if (stormRadarRoot != null) stormRadarRoot.SetActive(false);
                return;
            }
            if (Coop.CoopSession.Instance?.Busy == true)
            {
                UpdateCrewStorm();
                return;
            }
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

            if (run.IsGameOver)
            {
                if (stormRadarRoot != null) stormRadarRoot.SetActive(false);
                return;
            }

            float dt = Time.deltaTime;
            if(playerCar.GetComponent<VehicleModularState>()?.Workshop?.Sheltered??false)
            {
                if(stormRadarRoot!=null)stormRadarRoot.SetActive(false);
                isEngulfed=false;
                if(stormVisualObject!=null)stormVisualObject.SetActive(false);
                if(pedestrian!=null&&pedestrian.gameObject.activeInHierarchy&&route!=null
                    &&route.ProjectDistance(pedestrian.transform.position)<=routeDistance)
                    Hub.PlayerFieldNeeds.For(pedestrian).TakeDamage(stormDamagePerSec*dt);
                return;
            }
            if(stormVisualObject!=null)stormVisualObject.SetActive(true);
            if (route != null)
            {
                routeDistance += route.StormSpeed * dt;
                distanceToCar = route.ProjectDistance(playerCar.transform.position) - routeDistance;
                route.Evaluate(routeDistance, out Vector3 front, out Quaternion heading);
                stormZ = front.z;
                if (stormVisualObject != null)
                    stormVisualObject.transform.SetPositionAndRotation(front + Vector3.up * 6f, heading);
            }
            else
            {
                stormZ += baseChaseSpeed * dt;
                distanceToCar = playerCar.transform.position.z - stormZ;
                if (stormVisualObject != null)
                    stormVisualObject.transform.position = new Vector3(playerCar.transform.position.x, 6f, stormZ);
            }

            // Обработка критических зон
            isEngulfed = distanceToCar <= 0f;

            if (isEngulfed)
            {
                // Машина внутри смертоносного фронта бури!
                if(pedestrian!=null)
                    Hub.PlayerFieldNeeds.For(pedestrian).TakeDamage(stormDamagePerSec*dt*(pedestrian.gameObject.activeInHierarchy?1:.35f));
                else run.TakeDamage(stormDamagePerSec * dt);
                ArcadeCameraFollow.Instance?.TriggerShake(0.35f, 0.1f);
                CameraPostProcessEffects.Instance?.TriggerDamageFlash(0.4f);
            }

            UpdateRadarUI(dt);
        }

        void UpdateCrewStorm()
        {
            var car = Coop.CoopVehicle.Instance;
            if (car == null || !car.IsSpawned || car.Transitioning.Value) return;
            if (route == null) route = StageRoute.Instance;
            if (route == null) return;
            float dt = Time.deltaTime;
            if (car.IsServer)
            {
                routeDistance += route.StormSpeed * dt;
                distanceToCar = route.ProjectDistance(car.transform.position) - routeDistance;
                car.StormFront.Value = routeDistance;
                car.StormDistance.Value = distanceToCar;
                // Never damage the dormant offline expedition. Only the server resolves hits.
                foreach (var person in FindObjectsByType<Coop.CoopPlayer>(FindObjectsSortMode.None))
                    if (route.ProjectDistance(person.transform.position) <= routeDistance)
                        person.ServerTakeDamage(stormDamagePerSec * dt);
                if (distanceToCar <= 0) car.Hull.Value = Mathf.Max(0, car.Hull.Value - stormDamagePerSec * .35f * dt);
            }
            else
            {
                routeDistance = car.StormFront.Value;
                distanceToCar = car.StormDistance.Value;
            }
            isEngulfed = distanceToCar <= 0;
            route.Evaluate(routeDistance, out Vector3 front, out Quaternion heading);
            stormZ = front.z;
            if (stormVisualObject != null)
            { stormVisualObject.SetActive(true); stormVisualObject.transform.SetPositionAndRotation(front + Vector3.up * 6, heading); }
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
