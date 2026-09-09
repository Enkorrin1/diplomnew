using RogueDrive.Modifiers;
using UnityEngine;

namespace RogueDrive.Meta
{
    /// <summary>
    /// Направление прокачки в гараже: одна характеристика, несколько ступеней.
    ///
    /// Стоимость растёт геометрически — принятая в жанре практика, дающая
    /// замедление прогресса без ручной настройки каждой ступени.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeTrack", menuName = "RogueDrive/Upgrade Track")]
    public class UpgradeTrack : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;

        [Header("Эффект")]
        public StatId Target;

        [Tooltip("Прибавка к базовой характеристике за одну ступень.")]
        public float ValuePerLevel = 10f;

        [Min(1)] public int MaxLevel = 5;

        [Header("Стоимость")]
        [Min(1)] public int BaseCost = 300;

        [Tooltip("Множитель стоимости следующей ступени.")]
        [Range(1.05f, 2.5f)] public float CostGrowth = 1.45f;

        /// <summary>Стоимость перехода с уровня level на level + 1.</summary>
        public int GetCost(int currentLevel)
        {
            if (currentLevel >= MaxLevel)
                return 0;

            return Mathf.RoundToInt(BaseCost * Mathf.Pow(CostGrowth, currentLevel));
        }

        /// <summary>Суммарная стоимость полной прокачки направления.</summary>
        public int GetTotalCost()
        {
            int total = 0;

            for (int level = 0; level < MaxLevel; level++)
                total += GetCost(level);

            return total;
        }

        public float GetBonus(int currentLevel)
        {
            return ValuePerLevel * Mathf.Clamp(currentLevel, 0, MaxLevel);
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
    }
}
