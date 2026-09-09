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
        public event Action<string> RunEnded;

        public float Health { get; private set; }
        public float Fuel { get; private set; }
        public float Nitro { get; private set; }
        public float Distance { get; private set; }

        public float MaxHealth => startingHealth;
        public float MaxFuel => startingFuel;
        public float MaxNitro => startingNitro;

        public bool IsGameOver { get; private set; }
        public bool IsOutOfFuel { get; private set; }
        public string EndReason { get; private set; }

        private void Awake()
        {
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

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void ResetRun()
        {
            Time.timeScale = 1f;
            Health = startingHealth;
            Fuel = startingFuel;
            Nitro = startingNitro;
            Distance = 0f;
            IsGameOver = false;
            IsOutOfFuel = false;
            EndReason = string.Empty;
        }

        void EndRun(string reason)
        {
            IsGameOver = true;
            EndReason = reason;
            RunEnded?.Invoke(reason);
        }
    }
}
