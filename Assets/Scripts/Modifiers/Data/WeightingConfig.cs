using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Коэффициенты алгоритма взвешивания. Варьируемый параметр эксперимента:
    /// w(i) = w_base(rarity) * k_stack(level) * k_syn * k_role.
    /// </summary>
    [CreateAssetMenu(fileName = "WeightingConfig", menuName = "RogueDrive/Weighting Config")]
    public class WeightingConfig : ScriptableObject
    {
        [Header("w_base — базовый вес по редкости")]
        public float[] RarityWeights = { 1.00f, 0.45f, 0.15f };

        [Header("k_syn — множитель за замыкание синергии")]
        [Min(1f)] public float SynergyBonus = 2.5f;

        [Header("k_stack — множитель повтора в зависимости от текущего уровня")]
        public AnimationCurve StackFalloff = new AnimationCurve(
            new Keyframe(0f, 1.00f),
            new Keyframe(1f, 0.70f),
            new Keyframe(2f, 0.40f));

        [Header("k_role — компенсация слабого места билда")]
        [Min(1f)] public float RoleCompensation = 1.8f;
        [Range(0f, 1f)] public float LowResourceThreshold = 0.35f;

        public float GetRarityWeight(Rarity rarity)
        {
            if (RarityWeights == null || RarityWeights.Length == 0)
                return 1f;

            return RarityWeights[Mathf.Clamp((int)rarity, 0, RarityWeights.Length - 1)];
        }

        public float GetStackFalloff(int currentLevel)
        {
            return StackFalloff == null ? 1f : Mathf.Max(0f, StackFalloff.Evaluate(currentLevel));
        }

        /// <summary>
        /// Нейтральная конфигурация: все коэффициенты равны единице, взвешенный
        /// генератор становится эквивалентен равновероятному. Контрольная точка
        /// эксперимента и нижняя граница ряда промежуточных настроек.
        /// </summary>
        public static WeightingConfig CreateNeutral()
        {
            var config = CreateInstance<WeightingConfig>();
            config.name = "WeightingConfig (Neutral)";
            config.RarityWeights = new[] { 1f, 1f, 1f };
            config.SynergyBonus = 1f;
            config.RoleCompensation = 1f;
            config.LowResourceThreshold = 0f;
            config.StackFalloff = AnimationCurve.Constant(0f, 16f, 1f);
            return config;
        }
    }
}
