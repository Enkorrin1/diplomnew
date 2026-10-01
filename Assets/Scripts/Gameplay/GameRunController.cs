using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Gameplay
{
    /// <summary>Управляет состоянием заезда: здоровье, топливо, нитро, дистанция и условия завершения.</summary>
    public sealed class GameRunController : MonoBehaviour
    {
        [Header("Starting resources")]
        [SerializeField, Min(1f)] private float startingHealth = 100f;
        [SerializeField, Min(1f)] private float startingFuel = 100f;

        [Header("Nitro")]
        [SerializeField, Min(1f)] private float startingNitro = 100f;
        [SerializeField, Min(0f)] private float nitroRechargeRate = 12f;

        public event Action<float, float> HealthChanged;
        public event Action<float, float> FuelChanged;
        public event Action<float, float> NitroChanged;
        public event Action<float> DistanceChanged;
        public event Action<int> CoinsChanged;
        public event Action<string> RunEnded;

        /// <summary>
        /// Этап или кампания пройдены, заезд ещё не закрыт: последний момент, когда можно
        /// начислить монеты (конвертация непотраченных жетонов) и сохранить билд для переноса.
        /// </summary>
        public event Action<int> StageFinishing;

        public float Health { get; private set; }
        public float Fuel { get; private set; }
        public float Nitro { get; private set; }
        public float Distance { get; private set; }
        public int CoinsCollected { get; private set; }

        public float MaxHealth => startingHealth;
        public float MaxFuel => startingFuel;
        public float MaxNitro => startingNitro;

        public bool IsGameOver { get; private set; }
        public bool IsOutOfFuel { get; private set; }
        public string EndReason { get; private set; }

        private void Awake()
        {
            // PC-first: снимаем ограничение частоты кадров (60/120/144/165+ Hz мониторы)
            Application.targetFrameRate = -1;

            // На мобильных устройствах оставляем защиту от засыпания экрана
            if (Application.isMobilePlatform)
                Screen.sleepTimeout = SleepTimeout.NeverSleep;


            if (FindFirstObjectByType<RogueDrive.UI.PauseMenuUI>() == null)
            {
                gameObject.AddComponent<RogueDrive.UI.PauseMenuUI>();
            }

            // The scene entry activates gameplay before other components' OnEnable may run.
            bool legacyArcadeRun = FindFirstObjectByType<StageRoute>(FindObjectsInactive.Include) == null;
            if (legacyArcadeRun && FindFirstObjectByType<RogueDrive.Gameplay.Combat.ComboScoreSystem>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.Combat.ComboScoreSystem>();
            }

            if (legacyArcadeRun && FindFirstObjectByType<RogueDrive.Gameplay.Combat.RaiderSpawner>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.Combat.RaiderSpawner>();
            }

            // Визуальные эффекты скорости и критического здоровья
            if (legacyArcadeRun && FindFirstObjectByType<RogueDrive.Gameplay.VFX.SpeedLinesOverlay>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.VFX.SpeedLinesOverlay>();
            }
            if (legacyArcadeRun && FindFirstObjectByType<RogueDrive.Gameplay.VFX.CriticalHealthOverlay>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.VFX.CriticalHealthOverlay>();
            }

            // Динамическая сложность (DDA), погода и пост-процессинг
            if (legacyArcadeRun && FindFirstObjectByType<RogueDrive.Gameplay.Difficulty.DynamicDifficultyManager>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.Difficulty.DynamicDifficultyManager>();
            }
            if (legacyArcadeRun && FindFirstObjectByType<RogueDrive.Gameplay.Track.TrackWeatherHazardManager>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.Track.TrackWeatherHazardManager>();
            }
            if (FindFirstObjectByType<RogueDrive.Gameplay.Track.CreepingStormBarrier>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.Track.CreepingStormBarrier>();
            }
            if (legacyArcadeRun && FindFirstObjectByType<RogueDrive.Gameplay.Campaign.RadioBountyManager>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.Campaign.RadioBountyManager>();
            }
            Camera mainCam = Camera.main;
            var existingCar = FindFirstObjectByType<ArcadeCarController>();
            bool continuousBunkerCamera = existingCar != null && existingCar.UsesGarageDriving;
            if (!continuousBunkerCamera && mainCam != null && mainCam.GetComponent<RogueDrive.Gameplay.VFX.CameraPostProcessEffects>() == null)
            {
                mainCam.gameObject.AddComponent<RogueDrive.Gameplay.VFX.CameraPostProcessEffects>();
            }

            // PC: блокируем и скрываем курсор мыши во время заезда
            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }

            ResetRun();
        }

        private void Update()
        {
            if (!IsGameOver && Nitro < MaxNitro)
            {
                AddNitro(nitroRechargeRate * Time.deltaTime);
            }
        }

        public void ReportTravelled(float distance)
        {
            if (StageRoute.Instance != null) return;
            if (IsGameOver || distance <= 0f)
                return;

            Distance += distance;
            DistanceChanged?.Invoke(Distance);
        }

        public void ConsumeFuel(float amount)
        {
            // Authored survival routes use the actual tank, not a second countdown.
            if (StageRoute.Instance != null && VehicleModularState.Instance != null) return;
            if (IsGameOver || amount <= 0f)
                return;

            Fuel = Mathf.Max(0f, Fuel - amount);
            FuelChanged?.Invoke(Fuel, MaxFuel);

            if (Fuel <= 0f)
            {
                IsOutOfFuel = true;
            }
        }

        public bool TryConsumeNitro(float amount)
        {
            if (IsGameOver || amount <= 0f || Nitro <= 0f)
                return false;

            Nitro = Mathf.Max(0f, Nitro - amount);
            NitroChanged?.Invoke(Nitro, MaxNitro);
            return true;
        }

        public void AddNitro(float amount)
        {
            if (IsGameOver || amount <= 0f)
                return;

            Nitro = Mathf.Min(MaxNitro, Nitro + amount);
            NitroChanged?.Invoke(Nitro, MaxNitro);
        }

        public void Heal(float amount)
        {
            if (IsGameOver || amount <= 0f)
                return;

            Health = Mathf.Min(MaxHealth, Health + amount);
            HealthChanged?.Invoke(Health, MaxHealth);
        }

        public void AddFuel(float amount)
        {
            if (IsGameOver || amount <= 0f)
                return;

            Fuel = Mathf.Min(MaxFuel, Fuel + amount);
            if (Fuel > 0f)
            {
                IsOutOfFuel = false;
            }
            FuelChanged?.Invoke(Fuel, MaxFuel);
        }

        public void BindStats(RogueDrive.Modifiers.StatBlock stats)
        {
            if (stats == null) return;
            float maxHp = stats.Get(RogueDrive.Modifiers.StatId.MaxHealth);
            if (maxHp > 0f)
            {
                float diff = maxHp - startingHealth;
                startingHealth = maxHp;
                if (diff > 0f) Health += diff;
                Health = Mathf.Min(startingHealth, Health);
                HealthChanged?.Invoke(Health, MaxHealth);
            }

            float maxFuel = stats.Get(RogueDrive.Modifiers.StatId.FuelCapacity);
            if (maxFuel > 0f)
            {
                float diff = maxFuel - startingFuel;
                startingFuel = maxFuel;
                if (diff > 0f) Fuel += diff;
                Fuel = Mathf.Min(startingFuel, Fuel);
                FuelChanged?.Invoke(Fuel, MaxFuel);
            }
        }

        public void TakeDamage(float amount)
        {
            if(IsGameOver||amount<=0)return;
            var vehicle=FindFirstObjectByType<ArcadeCarController>();
            vehicle?.GetComponent<VehicleModularState>()?.ApplyComponentDamage(amount,Vector3.forward);
            TakeChassisDamage(amount);
        }

        public void TakeChassisDamage(float amount)
        {
            if (IsGameOver || amount <= 0f)
                return;

            Health = Mathf.Max(0f, Health - amount);
            HealthChanged?.Invoke(Health, MaxHealth);

            if (Health <= 0f)
            {
                Time.timeScale = 0.25f;
                EndRun("Машина уничтожена");
            }
        }

        private float lastFuelWarningTime;

        /// <summary>Вызывается контроллером машины, когда скорость при 0 топлива упала до полной остановки.</summary>
        public void ReportCarStopped()
        {
            if (IsGameOver)
                return;

            if (IsOutOfFuel)
            {
                // Не завершаем заезд мгновенно! Даем игроку шанс выйти из машины [E],
                // достать канистру из багажника или слить бензин с обочины до прихода бури!
                if (Time.time - lastFuelWarningTime > 5.0f)
                {
                    lastFuelWarningTime = Time.time;
                    if (RogueDrive.Gameplay.Hub.GaragePrologueManager.Instance != null)
                    {
                        RogueDrive.Gameplay.Hub.GaragePrologueManager.Instance.ShowNotification(
                            "⚠️ ТОПЛИВО НА НУЛЕ! Выйдите из машины [E], достаньте канистру из багажника или слейте бензин с обочины до прихода шторма!", 4.5f);
                    }
                }
            }
        }

        public void AddCoins(int amount)
        {
            if (IsGameOver || amount <= 0)
                return;

            CoinsCollected += amount;
            CoinsChanged?.Invoke(CoinsCollected);
        }

        public void Restart()
        {
            if(Hub.JourneyCheckpoint.OwnsCurrentRun&&Hub.JourneyCheckpoint.RequestRestore(true))return;
            if (Hub.GarageDepartureCheckpoint.OwnsCurrentRun && Hub.GarageDepartureCheckpoint.RequestRestore(true)) return;
            Time.timeScale = 1f;
            if (SeamlessJourneyStream.Instance != null)
            {
                SceneManager.LoadScene(SeamlessJourneyStream.Instance.EntryScene);
                return;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadGarage()
        {
            if(Hub.JourneyCheckpoint.OwnsCurrentRun&&Hub.JourneyCheckpoint.RequestRestore(false))return;
            if (Hub.GarageDepartureCheckpoint.OwnsCurrentRun && Hub.GarageDepartureCheckpoint.RequestRestore(false)) return;
            Time.timeScale = 1f;
            const string garageSceneName = "GarageScene";
            const string garageScenePath = "Assets/Scenes/GarageScene.unity";

            if (Application.CanStreamedLevelBeLoaded(garageSceneName))
            {
                SceneManager.LoadScene(garageSceneName);
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(garageScenePath))
            {
                SceneManager.LoadScene(garageScenePath);
                return;
            }

#if UNITY_EDITOR
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    garageScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameRunController] Резервная загрузка через EditorSceneManager: {ex.Message}");
            }
#endif
            SceneManager.LoadScene(0);
        }

        public bool IsCampaignVictory { get; private set; }
        public bool IsStageVictory { get; private set; }
        public int CurrentStageIndex { get; private set; } = 1;
        public void SetJourneyStage(int stageIndex)
        {
            if (!IsGameOver) CurrentStageIndex = Mathf.Clamp(stageIndex, 1, 4);
        }
        public void RestoreJourneyResources(float health,int coins)
        {
            Health=Mathf.Clamp(health,1,MaxHealth);CoinsCollected=Mathf.Max(0,coins);
            HealthChanged?.Invoke(Health,MaxHealth);CoinsChanged?.Invoke(CoinsCollected);
        }
        public float StageTargetDistance => StageRoute.Instance != null ? StageRoute.Instance.Length : 3000f;

        public void ReportRoutePosition(float distance)
        {
            if (IsGameOver) return;
            Distance = Mathf.Clamp(distance, 0f, StageTargetDistance);
            DistanceChanged?.Invoke(Distance);
        }

        public void SyncPhysicalFuel(float fraction)
        {
            if (IsGameOver) return;
            Fuel = Mathf.Clamp01(fraction) * MaxFuel;
            IsOutOfFuel = fraction <= .002f;
            FuelChanged?.Invoke(Fuel, MaxFuel);
        }

        public void ReportStageCompleted(int stageIndex)
        {
            if (SeamlessJourneyStream.Instance != null && stageIndex < 4) return;
            if (IsGameOver || IsStageVictory)
                return;

            IsStageVictory = true;
            CurrentStageIndex = stageIndex;
            AddCoins(50);
            StageFinishing?.Invoke(stageIndex);
            EndRun($"Этап {stageIndex} пройден!");
        }

        public void ReportBossDefeated()
        {
            if (IsGameOver || IsCampaignVictory)
                return;

            IsCampaignVictory = true;
            IsStageVictory = true;
            CurrentStageIndex = 4;
            StageFinishing?.Invoke(4);
            EndRun("Кампания завершена");
        }

        void ResetRun()
        {
            Time.timeScale = 1f;
            Health = startingHealth;
            Fuel = startingFuel;
            Nitro = startingNitro;

            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.StartsWith("Stage"))
            {
                if (sceneName.Contains("1")) CurrentStageIndex = 1;
                else if (sceneName.Contains("2")) CurrentStageIndex = 2;
                else if (sceneName.Contains("3")) CurrentStageIndex = 3;
                else if (sceneName.Contains("4")) CurrentStageIndex = 4;
                Distance = 0f;
            }
            else
            {
                int startSector = CampaignMapModal.SelectedStartSector;
                CurrentStageIndex = Mathf.Clamp(startSector, 1, 4);
                if (startSector >= 2 && startSector <= 4)
                {
                    Distance = (startSector - 1) * 1000f;
                }
                else
                {
                    Distance = 0f;
                }
            }

            CoinsCollected = 0;
            IsGameOver = false;
            IsOutOfFuel = false;
            IsCampaignVictory = false;
            IsStageVictory = false;
            EndReason = string.Empty;
        }

        public void ReportPlayerDeath()
        {
            if (IsGameOver) return;
            EndRun("Персонаж погиб");
        }

        void EndRun(string reason)
        {
            if (IsGameOver)
                return;

            IsGameOver = true;
            EndReason = reason;

            // Навсегда сохраняем заработанные монеты в мета-прогресс
            try
            {
                var meta = RogueDrive.Meta.SaveService.GetActiveProgress();
                if (meta != null && !(Hub.GaragePrologueManager.Instance?.IsPreviewRun??Application.isEditor))
                {
                    if (CoinsCollected > 0 && ((!Hub.GarageDepartureCheckpoint.OwnsCurrentRun&&!Hub.JourneyCheckpoint.OwnsCurrentRun) || IsStageVictory || IsCampaignVictory))
                    {
                        meta.AddCoins(CoinsCollected);
                    }
                    int sectorIdx = IsCampaignVictory ? 5 : (IsStageVictory ? (CurrentStageIndex + 1) : CurrentStageIndex);
                    meta.RegisterRunResult(sectorIdx, Distance);
                    RogueDrive.Meta.SaveService.SaveActive();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameRunController] Ошибка сохранения прогресса: {ex.Message}");
            }

            RunEnded?.Invoke(reason);
        }
    }
}
