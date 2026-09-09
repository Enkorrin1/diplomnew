using System.Collections.Generic;
using RogueDrive.Modifiers;
using UnityEngine;

namespace RogueDrive.Meta
{
    /// <summary>
    /// Каталог автомобилей и веток прокачки гаража.
    /// Позволяет централизованно управлять списком доступного автопарка и улучшений.
    /// </summary>
    [CreateAssetMenu(fileName = "GarageCatalog", menuName = "RogueDrive/Garage Catalog")]
    public class GarageCatalog : ScriptableObject
    {
        [SerializeField] private List<CarDefinition> _cars = new List<CarDefinition>();
        [SerializeField] private List<UpgradeTrack> _upgrades = new List<UpgradeTrack>();

        public IReadOnlyList<CarDefinition> Cars => _cars;
        public IReadOnlyList<UpgradeTrack> Upgrades => _upgrades;

        public CarDefinition GetCar(string id)
        {
            if (string.IsNullOrEmpty(id)) return _cars.Count > 0 ? _cars[0] : null;
            for (int i = 0; i < _cars.Count; i++)
            {
                if (_cars[i] != null && _cars[i].Id == id)
                    return _cars[i];
            }
            return _cars.Count > 0 ? _cars[0] : null;
        }

        public UpgradeTrack GetUpgrade(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _upgrades.Count; i++)
            {
                if (_upgrades[i] != null && _upgrades[i].Id == id)
                    return _upgrades[i];
            }
            return null;
        }

#if UNITY_EDITOR
        public void EditorSetData(List<CarDefinition> cars, List<UpgradeTrack> upgrades)
        {
            _cars = cars ?? new List<CarDefinition>();
            _upgrades = upgrades ?? new List<UpgradeTrack>();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
