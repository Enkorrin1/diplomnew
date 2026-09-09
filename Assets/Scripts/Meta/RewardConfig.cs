using UnityEngine;

namespace RogueDrive.Meta
{
    /// <summary>
    /// Формула награды за заезд:
    ///
    ///     монеты = дистанция * k_d + убийства * k_k + бонус_финиша
    ///
    /// Коэффициенты не назначаются экспертно, а подбираются по результатам
    /// симуляции под выбранную длину мета-прогресса (см. EconomyCalibrator).
    /// </summary>
    [CreateAssetMenu(fileName = "RewardConfig", menuName = "RogueDrive/Reward Config")]
    public class RewardConfig : ScriptableObject
    {
        [Tooltip("k_d — монет за метр дистанции.")]
        public float CoinsPerMeter = 0.05f;

        [Tooltip("k_k — монет за одно убийство.")]
        public float CoinsPerKill = 0.5f;

        [Tooltip("Бонус за достижение финиша уровня.")]
        public float FinishBonus = 200f;

        [Tooltip("Множитель награды в режиме непрерывного заезда.")]
        [Range(0f, 2f)] public float EndlessMultiplier = 0.8f;

        public int Evaluate(float distance, int kills, bool completed, bool endless)
        {
            float coins = distance * CoinsPerMeter + kills * CoinsPerKill;

            if (completed)
                coins += FinishBonus;

            if (endless)
                coins *= EndlessMultiplier;

            return Mathf.Max(0, Mathf.RoundToInt(coins));
        }

        /// <summary>Пропорциональное масштабирование всех слагаемых при калибровке.</summary>
        public void Scale(float factor)
        {
            CoinsPerMeter *= factor;
            CoinsPerKill *= factor;
            FinishBonus *= factor;
        }
    }
}
