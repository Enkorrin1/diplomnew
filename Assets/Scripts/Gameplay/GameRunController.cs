using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Gameplay
{
    /// <summary>Owns the player-facing state of one prototype run.</summary>
    public sealed class GameRunController : MonoBehaviour
    {
        [Header("Starting resources")]
        [SerializeField, Min(1f)] private float startingHealth = 100f;
        [SerializeField, Min(1f)] private float startingFuel = 100f;

        public event Action<float, float> HealthChanged;
        public event Action<float, float> FuelChanged;
        public event Action<float> DistanceChanged;
        public event Action<string> RunEnded;

        public float Health { get; private set; }
        public float Fuel { get; private set; }
        public float Distance { get; private set; }
        public float MaxHealth => startingHealth;
        public float MaxFuel => startingFuel;
        public bool IsGameOver { get; private set; }
        public string EndReason { get; private set; }

        private void Awake()
        {
            ResetRun();
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
                EndRun("Топливо закончилось");
        }

        public void TakeDamage(float amount)
        {
            if (IsGameOver || amount <= 0f)
                return;

            Health = Mathf.Max(0f, Health - amount);
            HealthChanged?.Invoke(Health, MaxHealth);

            if (Health <= 0f)
                EndRun("Машина уничтожена");
        }

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void ResetRun()
        {
            Health = startingHealth;
            Fuel = startingFuel;
            Distance = 0f;
            IsGameOver = false;
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
