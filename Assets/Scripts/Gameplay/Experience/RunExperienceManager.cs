using System;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Управляет накоплением опыта и повышением уровня во время заезда.
    /// При заполнении шкалы инициирует событие LevelUp для паузы и выбора модификатора.
    /// </summary>
    public sealed class RunExperienceManager : MonoBehaviour
    {
        [Header("Progression Settings")]
        [SerializeField, Min(1f)] private float baseXp = 6f;
        [SerializeField, Min(1.05f)] private float xpGrowth = 1.35f;

        public event Action<float, float> ExperienceChanged;
        public event Action<int> LevelUp;

        public int CurrentLevel { get; private set; } = 1;
        public float CurrentXp { get; private set; }
        public float RequiredXp => CalculateRequiredXp(CurrentLevel);

        private void Awake()
        {
            ResetProgress();
        }

        public void AddExperience(float amount)
        {
            if (amount <= 0f)
                return;

            CurrentXp += amount;

            while (CurrentXp >= RequiredXp)
            {
                CurrentXp -= RequiredXp;
                CurrentLevel++;
                ExperienceChanged?.Invoke(CurrentXp, RequiredXp);
                LevelUp?.Invoke(CurrentLevel);
            }

            ExperienceChanged?.Invoke(CurrentXp, RequiredXp);
        }

        public void ResetProgress()
        {
            CurrentLevel = 1;
            CurrentXp = 0f;
            ExperienceChanged?.Invoke(CurrentXp, RequiredXp);
        }

        float CalculateRequiredXp(int level)
        {
            return Mathf.Round(baseXp * Mathf.Pow(xpGrowth, level - 1));
        }
    }
}
