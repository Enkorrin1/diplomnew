using System;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Физическое модульное состояние автомобиля (A Long Drive + Pacific Drive):
    /// 1. Радиатор и температура двигателя (вода в радиаторе предотвращает перегрев ДВС).
    /// 2. Топливный бак (запас хода в литрах).
    /// 3. Состояние 4 шин (износ, проколы шипами рейдеров, боковое скольжение).
    /// 4. Кенгурятник / силовой бампер (поглощает удары от тарана зомби, защищая радиатор).
    /// Состояние узлов дополняет прочность кузова в GameRunController.
    /// </summary>
    public sealed partial class VehicleModularState : MonoBehaviour
    {
        private static VehicleModularState instance;
        public static VehicleModularState Instance
        {
            get
            {
                if(instance==null)instance=FindFirstObjectByType<VehicleModularState>();
                return instance;
            }
            private set=>instance=value;
        }

        public event Action<float, float> RadiatorChanged; // current, max
        public event Action<float> EngineTempChanged;       // celsius
        public event Action<float, float> FuelChanged;       // current, max
        public event Action<int, float> TireChanged;         // tireIndex (0..3), integrity (0..1)
        public event Action<float> BumperChanged;            // integrity (0..1)

        [Header("Radiator & Engine Heat")]
        [SerializeField, Min(1f)] private float maxRadiatorWater = 10f;
        [SerializeField] private float currentRadiatorWater = 10f;
        [SerializeField] private float engineTemperature = 85f; // норма ~85-90C
        [SerializeField] private float overheatThreshold = 115f;
        [SerializeField] private float maxEngineTemp = 135f;
        [SerializeField] private ParticleSystem steamVFX;

        [Header("Fuel System")]
        [SerializeField, Min(1f)] private float maxFuelLiters = 50f;
        [SerializeField] private float currentFuelLiters = 25f;
        [SerializeField] private float fuelBurnRatePerSecond = 0.45f; // л/сек на полном газу

        [Header("Tires (0: FL, 1: FR, 2: RL, 3: RR)")]
        [SerializeField] private float[] tireIntegrity = new float[4] { 1f, 1f, 1f, 1f };

        [Header("Bumper / Cowcatcher")]
        [SerializeField] private float bumperIntegrity = 1.0f; // 0..1
        [SerializeField] private bool hasCowcatcher = false;

        [SerializeField, Min(.1f)] private float maxEngineOil = 5f;
        [SerializeField] private float currentEngineOil = 5f;
        public float EngineOil => currentEngineOil;
        public float MaxEngineOil => maxEngineOil;
        public void AddEngineOil(float liters) { currentEngineOil = Mathf.Clamp(currentEngineOil + Mathf.Max(0, liters), 0, maxEngineOil); }

        public float RadiatorWater => currentRadiatorWater;
        public float MaxRadiatorWater => maxRadiatorWater;
        public float EngineTemperature => engineTemperature;
        public bool IsOverheated => engineTemperature >= overheatThreshold;
        public float FuelLiters => currentFuelLiters;
        public float MaxFuelLiters => maxFuelLiters;
        public bool HasFuel => currentFuelLiters > 0.1f;
        public float BumperIntegrity => bumperIntegrity;
        public bool HasCowcatcher => hasCowcatcher;

        public void SetFuelForGaragePreparation(float liters)
        {
            currentFuelLiters = Mathf.Clamp(liters, 0f, maxFuelLiters);
            FuelChanged?.Invoke(currentFuelLiters, maxFuelLiters);
        }

        private void Awake()
        {
            Instance = this;
        }

        public Hub.VehicleWorkshop Workshop { get; private set; }
        private void Start() { Workshop=Hub.VehicleWorkshop.For(this); }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private float engineLoadUntil, requestedLoad;
        private void Update()
        {
            var driver=GetComponent<Hub.GarageDriveOutVehicle>();
            var road=GetComponent<ArcadeCarController>();
            if(road?.Run?.IsGameOver??false)return;
            bool ignition=driver!=null?driver.IsDrivingEnabled:road!=null&&road.isActiveAndEnabled;
            bool running=ignition && CanProvidePower && !(Workshop?.EngineStopped??false);
            float load=Time.time<engineLoadUntil?requestedLoad:0;
            if(driver!=null && driver.isActiveAndEnabled)
                load=driver.ThrottleInput*driver.SpeedMps>=-.1f?Mathf.Abs(driver.ThrottleInput):0;
            SimulateSystems(Time.deltaTime,running,load,GetComponent<Rigidbody>()?.linearVelocity.magnitude??0);
            if(steamVFX!=null)
            {
                if(IsOverheated&&!steamVFX.isPlaying)steamVFX.Play();
                else if(!IsOverheated&&steamVFX.isPlaying)steamVFX.Stop();
            }
        }

        /// <summary>Расход топлива при движении.</summary>
        public void ConsumeFuel(float throttleInput, float dt)
        {
            throttleInput=Mathf.Clamp01(Mathf.Abs(throttleInput));
            if (throttleInput <= 0.01f || dt<=0 || !CanProvidePower) return;
            if(Workshop!=null&&Workshop.DriveBlocked)return;

            engineLoadUntil = Time.time + .25f;
            requestedLoad=throttleInput;
            float routePacing = StageRoute.Instance != null ? StageRoute.Instance.FuelMultiplier : 1f;
            float burn = fuelBurnRatePerSecond * routePacing * throttleInput * dt * (Workshop!=null?Mathf.Max(.3f,Workshop.Stats.fuel):1);
            currentFuelLiters = Mathf.Max(0f, currentFuelLiters - burn);
            FuelChanged?.Invoke(currentFuelLiters, maxFuelLiters);
        }

        /// <summary>Заливка бензина из канистры.</summary>
        public void AddFuel(float liters)
        {
            currentFuelLiters = Mathf.Clamp(currentFuelLiters + liters, 0f, maxFuelLiters);
            FuelChanged?.Invoke(currentFuelLiters, maxFuelLiters);

            var run = FindFirstObjectByType<GameRunController>();
            run?.AddFuel(liters);

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySwitchClick();
        }

        /// <summary>Заливка воды в радиатор.</summary>
        public void AddRadiatorWater(float liters)
        {
            currentRadiatorWater = Mathf.Clamp(currentRadiatorWater + liters, 0f, maxRadiatorWater);
            RadiatorChanged?.Invoke(currentRadiatorWater, maxRadiatorWater);

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySwitchClick();
        }

        /// <summary>Повреждение шины (например, наезд на шипы рейдеров).</summary>
        public void DamageTire(int tireIndex, float damage)
        {
            if (tireIndex < 0 || tireIndex >= tireIntegrity.Length) return;

            tireIntegrity[tireIndex] = Mathf.Clamp01(tireIntegrity[tireIndex] - Mathf.Max(0,damage));
            Workshop?.SetTireCondition(tireIndex,tireIntegrity[tireIndex]);
            TireChanged?.Invoke(tireIndex, tireIntegrity[tireIndex]);

            if (AudioManager.Instance != null) AudioManager.Instance.PlayImpact();
        }

        /// <summary>Замена шины на запасное колесо.</summary>
        public void RepairTire(int tireIndex)
        {
            if (tireIndex < 0 || tireIndex >= tireIntegrity.Length) return;

            tireIntegrity[tireIndex] = 1.0f;
            Workshop?.SetTireCondition(tireIndex,1);
            TireChanged?.Invoke(tireIndex, 1.0f);

            if (AudioManager.Instance != null) AudioManager.Instance.PlayLevelUp();
        }

        public float GetTireIntegrity(int index) => (index >= 0 && index < tireIntegrity.Length) ? tireIntegrity[index] : 1f;

        /// <summary>Расчет среднего коэффициента сцепления 4 колес.</summary>
        public float GetAverageTireGrip()
        {
            float sum = 0f;
            for (int i = 0; i < tireIntegrity.Length; i++)
            {
                sum += Mathf.Lerp(0.35f, 1.0f, tireIntegrity[i]);
            }
            return sum / tireIntegrity.Length * (Workshop!=null?Mathf.Max(.4f,Workshop.Stats.grip):1);
        }

        /// <summary>Таран зомби: кенгурятник поглощает урон.</summary>
        public void AbsorbRammingImpact(float impactForce)
        {
            ApplyComponentDamage(impactForce, Vector3.forward);
        }

        public void InstallCowcatcher()
        {
            hasCowcatcher = true;
            bumperIntegrity = 1.0f;
            BumperChanged?.Invoke(bumperIntegrity);
        }

        public void RepairBumper(float amount)
        {
            bumperIntegrity = Mathf.Clamp01(bumperIntegrity + Mathf.Max(0f, amount));
            BumperChanged?.Invoke(bumperIntegrity);
        }
    }
}
