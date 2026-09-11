using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    [Serializable]
    public struct SocketSlot
    {
        public SocketType Type;
        public Transform Anchor;
    }

    /// <summary>
    /// Сокеты на префабе автомобиля. Монтирует модель оружия при первом выборе
    /// модификатора и заменяет её при повышении уровня.
    /// </summary>
    public sealed class SocketRegistry : MonoBehaviour, ISocketProvider
    {
        [SerializeField] SocketSlot[] _slots = new SocketSlot[0];

        readonly Dictionary<string, int> _slotByModifier = new Dictionary<string, int>();
        readonly Dictionary<string, GameObject> _instanceByModifier = new Dictionary<string, GameObject>();
        bool[] _occupied;

        void Awake()
        {
            _occupied = new bool[_slots.Length];
        }

        public bool HasFree(SocketType type)
        {
            if (type == SocketType.None)
                return true;

            EnsureOccupancy();

            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Type == type && !_occupied[i])
                    return true;

            return false;
        }

        public bool AllOccupied
        {
            get
            {
                if (_slots.Length == 0)
                    return false;

                EnsureOccupancy();

                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i].Type != SocketType.None && !_occupied[i])
                        return false;

                return true;
            }
        }

        public void Mount(ModifierDefinition definition, int level)
        {
            if (definition == null || !definition.RequiresSocket)
                return;

            EnsureOccupancy();

            if (!_slotByModifier.TryGetValue(definition.Id, out int slotIndex))
            {
                slotIndex = FindFreeSlot(definition.RequiredSocket);

                if (slotIndex < 0)
                {
                    Debug.LogWarning(
                        $"Нет свободного сокета {definition.RequiredSocket} для {definition.Id}", this);
                    return;
                }

                _occupied[slotIndex] = true;
                _slotByModifier[definition.Id] = slotIndex;
            }

            GameObject prefab = definition.GetPrefabForLevel(level);

            if (prefab == null)
                return;

            if (_instanceByModifier.TryGetValue(definition.Id, out GameObject existing) && existing != null)
            {
                // Модель уровня не изменилась — пересоздавать нечего.
                if (existing.name.StartsWith(prefab.name, StringComparison.Ordinal))
                    return;

                Destroy(existing);
            }

            Transform anchor = _slots[slotIndex].Anchor;
            GameObject instance = Instantiate(prefab, anchor != null ? anchor : transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            _instanceByModifier[definition.Id] = instance;
        }

        public void Reset()
        {
            foreach (GameObject instance in _instanceByModifier.Values)
                if (instance != null)
                    Destroy(instance);

            _instanceByModifier.Clear();
            _slotByModifier.Clear();
            EnsureOccupancy();
            Array.Clear(_occupied, 0, _occupied.Length);
        }

        int FindFreeSlot(SocketType type)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Type == type && !_occupied[i])
                    return i;

            return -1;
        }

        void EnsureOccupancy()
        {
            if (_occupied == null || _occupied.Length != _slots.Length)
                _occupied = new bool[_slots.Length];
        }
    }
}
