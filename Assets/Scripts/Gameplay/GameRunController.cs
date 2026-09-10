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

            // TouchControlsUI теперь создаётся только SceneUIView на мобилках;
            // на PC — не нужен, курсор заблокирован во время заезда.

            if (FindFirstObjectByType<RogueDrive.UI.PauseMenuUI>() == null)
            {
                gameObject.AddComponent<RogueDrive.UI.PauseMenuUI>();
            }

            if (FindFirstObjectByType<RogueDrive.Gameplay.Combat.ComboScoreSystem>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.Combat.ComboScoreSystem>();
            }

            // Визуальные эффекты скорости и критического здоровья
            if (FindFirstObjectByType<RogueDrive.Gameplay.VFX.SpeedLinesOverlay>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.VFX.SpeedLinesOverlay>();
            }
            if (FindFirstObjectByType<RogueDrive.Gameplay.VFX.CriticalHealthOverlay>() == null)
            {
                gameObject.AddComponent<RogueDrive.Gameplay.VFX.CriticalHealthOverlay>();
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
            if (IsGameOver || distance <= 0f)
                return;

            Distance += distance;
            DistanceChanged?.Invoke(Distance);
        }

        public void ConsumeFuel(float amount)
        {
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

        /// <summary>Вызывается контроллером машины, когда скорость при 0 топлива упала до полной остановки.</summary>
        public void ReportCarStopped()
        {
            if (IsGameOver)
                return;

            if (IsOutOfFuel)
            {
                EndRun("Топливо закончилось");
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
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadGarage()
        {
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
        public float StageTargetDistance => 3000f;

        public void ReportStageCompleted(int stageIndex)
        {
            if (IsGameOver || IsStageVictory)
                return;

            IsStageVictory = true;
            CurrentStageIndex = stageIndex;
            AddCoins(50);
            EndRun($"Этап {stageIndex} пройден!");
        }

        public void ReportBossDefeated()
        {
            if (IsGameOver || IsCampaignVictory)
                return;

            IsCampaignVictory = true;
            IsStageVictory = true;
            CurrentStageIndex = 4;
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
                if (meta != null)
                {
                    if (CoinsCollected > 0)
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
