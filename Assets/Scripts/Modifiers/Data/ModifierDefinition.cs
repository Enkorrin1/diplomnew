using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Описание сессионного модификатора. Добавление нового бафа сводится к созданию
    /// ассета и настройке полей — код при этом не изменяется.
    /// </summary>
    [CreateAssetMenu(fileName = "Modifier", menuName = "RogueDrive/Modifier")]
    public class ModifierDefinition : ScriptableObject
    {
        [Header("Идентификация")]
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;

        [Header("Классификация")]
        public ModifierCategory Category;
        public Rarity Rarity = Rarity.Common;
        [Min(1)] public int MaxLevel = 3;
        public SynergyTag[] Tags = new SynergyTag[0];

        [Header("Установка на корпус")]
        public SocketType RequiredSocket = SocketType.None;

        [Tooltip("Индекс равен уровню минус один. Оставить пустым для пассивных модификаторов.")]
        public GameObject[] LevelPrefabs = new GameObject[0];

        [Header("Эффекты")]
        [Tooltip("Список разнотипных эффектов хранится внутри ассета через SerializeReference.")]
        [SerializeReference] public List<ModifierEffect> Effects = new List<ModifierEffect>();

        public bool RequiresSocket => RequiredSocket != SocketType.None;

        public GameObject GetPrefabForLevel(int level)
        {
            if (LevelPrefabs == null || LevelPrefabs.Length == 0)
                return null;

            int index = Mathf.Clamp(level - 1, 0, LevelPrefabs.Length - 1);
            return LevelPrefabs[index];
        }

        public bool HasTag(SynergyTag tag)
        {
            if (Tags == null) return false;

            for (int i = 0; i < Tags.Length; i++)
                if (Tags[i] == tag) return true;

            return false;
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
    }
}
