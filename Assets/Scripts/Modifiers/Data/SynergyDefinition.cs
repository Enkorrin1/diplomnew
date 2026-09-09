using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Синергия: набор модификаторов, совместное присутствие которых даёт
    /// дополнительный эффект. Активируется автоматически при выполнении условия.
    /// </summary>
    [CreateAssetMenu(fileName = "Synergy", menuName = "RogueDrive/Synergy")]
    public class SynergyDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;

        [Tooltip("Все перечисленные модификаторы должны присутствовать в билде.")]
        public string[] RequiredModifierIds = new string[0];

        [SerializeReference] public List<ModifierEffect> Effects = new List<ModifierEffect>();

        /// <summary>Условие выполнено текущим составом билда.</summary>
        public bool IsSatisfiedBy(BuildState build)
        {
            if (build == null || RequiredModifierIds == null || RequiredModifierIds.Length == 0)
                return false;

            for (int i = 0; i < RequiredModifierIds.Length; i++)
                if (!build.Has(RequiredModifierIds[i]))
                    return false;

            return true;
        }

        /// <summary>
        /// Условие будет выполнено, если к билду добавить кандидата. Используется
        /// генератором предложений для расчёта коэффициента k_syn.
        /// </summary>
        public bool WouldBeSatisfiedWith(BuildState build, string candidateId)
        {
            if (build == null || RequiredModifierIds == null || RequiredModifierIds.Length == 0)
                return false;

            bool usesCandidate = false;

            for (int i = 0; i < RequiredModifierIds.Length; i++)
            {
                string required = RequiredModifierIds[i];

                if (required == candidateId)
                {
                    usesCandidate = true;
                    continue;
                }

                if (!build.Has(required))
                    return false;
            }

            return usesCandidate;
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
    }
}
