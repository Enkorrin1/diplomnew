using UnityEngine;

namespace RogueDrive.Simulation
{
    /// <summary>
    /// Профиль сложности уровня. Поскольку движение полосное, сложность выражается
    /// числами и потому воспроизводима в симуляции: плотность врагов, их прочность
    /// и доля перекрытых полос как функции пройденной дистанции.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyProfile", menuName = "RogueDrive/Difficulty Profile")]
    public class DifficultyProfile : ScriptableObject
    {
        [Header("Уровень")]
        [Tooltip("Длина уровня в метрах. Ноль означает режим непрерывного заезда.")]
        public float LevelLength = 4000f;

        [Header("Противники")]
        [Tooltip("Появлений в секунду. По оси X — пройденная дистанция в метрах.")]
        public AnimationCurve EnemiesPerSecond = AnimationCurve.Linear(0f, 3f, 5000f, 14f);

        [Tooltip("Прочность одного противника. По оси X — дистанция.")]
        public AnimationCurve EnemyHealth = AnimationCurve.Linear(0f, 12f, 5000f, 60f);

        [Tooltip("Доля перекрытых полос от нуля до единицы. По оси X — дистанция.")]
        public AnimationCurve BlockedLaneFraction = AnimationCurve.Linear(0f, 0.05f, 5000f, 0.5f);

        [Header("Урон и ресурсы")]
        [Tooltip("Урон от одного прорвавшегося противника.")]
        public float ContactDamage = 6f;

        [Tooltip("Штраф к топливу за столкновение с тяжёлым препятствием.")]
        public float RamFuelPenalty = 3f;

        [Tooltip("Вероятность столкновения в секунду при полностью перекрытой трассе.")]
        [Range(0f, 1f)] public float RamChanceAtFullBlock = 0.35f;

        [Header("Опыт")]
        public float ExperiencePerKill = 1f;

        [Tooltip("Порог опыта до следующего уровня. По оси X — номер текущего уровня.")]
        public AnimationCurve ExperienceToNextLevel = AnimationCurve.Linear(0f, 12f, 12f, 90f);

        public float Sample(AnimationCurve curve, float distance, float fallback)
        {
            return curve == null ? fallback : curve.Evaluate(distance);
        }
    }
}
