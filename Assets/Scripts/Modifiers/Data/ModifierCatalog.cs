using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Полный перечень модификаторов и синергий проекта с индексом по идентификатору.
    /// </summary>
    [CreateAssetMenu(fileName = "ModifierCatalog", menuName = "RogueDrive/Modifier Catalog")]
    public class ModifierCatalog : ScriptableObject
    {
        [SerializeField] List<ModifierDefinition> _modifiers = new List<ModifierDefinition>();
        [SerializeField] List<SynergyDefinition> _synergies = new List<SynergyDefinition>();

        Dictionary<string, ModifierDefinition> _index;

        public IReadOnlyList<ModifierDefinition> All => _modifiers;
        public IReadOnlyList<SynergyDefinition> Synergies => _synergies;

        public ModifierDefinition this[string id]
        {
            get
            {
                TryGet(id, out ModifierDefinition definition);
                return definition;
            }
        }

        public bool TryGet(string id, out ModifierDefinition definition)
        {
            definition = null;

            if (string.IsNullOrEmpty(id))
                return false;

            EnsureIndex();
            return _index.TryGetValue(id, out definition);
        }

        void EnsureIndex()
        {
            if (_index != null)
                return;

            _index = new Dictionary<string, ModifierDefinition>(_modifiers.Count);

            for (int i = 0; i < _modifiers.Count; i++)
            {
                ModifierDefinition definition = _modifiers[i];

                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;

                if (_index.ContainsKey(definition.Id))
                {
                    Debug.LogError($"Дублирующийся идентификатор модификатора: {definition.Id}", definition);
                    continue;
                }

                _index[definition.Id] = definition;
            }
        }

        void OnEnable()
        {
            _index = null;
        }

#if UNITY_EDITOR
        /// <summary>Наполнение каталога из редакторского генератора контента.</summary>
        public void EditorPopulate(IEnumerable<ModifierDefinition> modifiers,
                                   IEnumerable<SynergyDefinition> synergies)
        {
            _modifiers.Clear();
            _synergies.Clear();

            if (modifiers != null)
                _modifiers.AddRange(modifiers);

            if (synergies != null)
                _synergies.AddRange(synergies);

            _index = null;
        }
#endif

        void OnValidate()
        {
            _index = null;
        }
    }
}
