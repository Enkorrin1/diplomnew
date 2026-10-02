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

        // UI Radar
        private GameObject stormRadarRoot;
        private Text stormDistanceText;
        private Image stormWarningIcon;
        private float beepTimer;

        public float StormZ => stormZ;
        public float DistanceToCar => distanceToCar;
        public bool IsEngulfed => isEngulfed;
        public float RouteDistance => routeDistance;
        /// <summary>Машина укрыта в мастерской: фронт не показывается.</summary>
        public bool IsSheltered { get; private set; }

        /// <summary>Положение фронта на дороге и направление его движения (для StormFrontView).</summary>
        public bool TryGetFront(out Vector3 position, out Quaternion heading)
        {
            if (route != null)
            {
                route.Evaluate(routeDistance, out position, out heading);
                return true;
            }
            heading = Quaternion.identity;
            position = new Vector3(playerCar != null ? playerCar.transform.position.x : 0f, 0f, stormZ);
            return playerCar != null;
        }

        public void BeginNextWave(float departureDistance)
        {
            routeDistance=departureDistance-(route!=null?route.StormLead:900);
            distanceToCar=departureDistance-routeDistance;isEngulfed=false;beepTimer=0;IsSheltered=false;
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

        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            BindStormRadarUI();
            playerCar = FindFirstObjectByType<ArcadeCarController>();
            run = FindFirstObjectByType<GameRunController>();
            pedestrian=FindFirstObjectByType<Hub.GaragePlayerController>(FindObjectsInactive.Include);

            float startZ = playerCar != null ? playerCar.transform.position.z : 0f;
            stormZ = startZ - startOffsetBehind;
            route = StageRoute.Instance;
            if (route != null)
                routeDistance = (playerCar != null ? route.ProjectDistance(playerCar.transform.position) : 0f) - route.StormLead;
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
                IsSheltered=true;
                if(pedestrian!=null&&pedestrian.gameObject.activeInHierarchy&&route!=null
                    &&route.ProjectDistance(pedestrian.transform.position)<=routeDistance)
                    Hub.PlayerFieldNeeds.For(pedestrian).TakeDamage(stormDamagePerSec*dt);
                return;
            }
            IsSheltered=false;
            if (route != null)
            {
                routeDistance += route.StormSpeed * dt;
                distanceToCar = route.ProjectDistance(playerCar.transform.position) - routeDistance;
                route.Evaluate(routeDistance, out Vector3 front, out Quaternion heading);
                stormZ = front.z;
            }
            else
            {
                stormZ += baseChaseSpeed * dt;
                distanceToCar = playerCar.transform.position.z - stormZ;
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

        // Панель лежит в сцене (Storm_Front); барьер добавляется кодом и только находит её.
        private void BindStormRadarUI()
        {
            if (stormRadarRoot != null) return;
            var radar = FindFirstObjectByType<StormRadarPanel>(FindObjectsInactive.Include);
            if (radar == null) return;
            stormRadarRoot = radar.Panel;
            stormDistanceText = radar.DistanceText;
            stormRadarRoot.SetActive(false);
        }
    }
}
