using System;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    [Serializable]
    public struct StatValue
    {
        public StatId Id;
        public float Value;

        public StatValue(StatId id, float value)
        {
            Id = id;
            Value = value;
        }
    }

    [Serializable]
    public struct SocketCapacity
    {
        public SocketType Type;
        [Min(0)] public int Count;
    }

    /// <summary>
    /// Архетип автомобиля: базовые характеристики и набор сокетов.
    /// Число сокетов является характеристикой машины и настраивается без кода.
    /// </summary>
    [CreateAssetMenu(fileName = "Car", menuName = "RogueDrive/Car")]
    public class CarDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public GameObject Prefab;

        [Header("Базовые характеристики")]
        public StatValue[] BaseStats =
        {
            new StatValue(StatId.Damage, 10f),
            new StatValue(StatId.FireRate, 2f),
            new StatValue(StatId.MaxHealth, 100f),
            new StatValue(StatId.FuelCapacity, 100f),
            new StatValue(StatId.FuelDrain, 1f),
            new StatValue(StatId.Speed, 20f),
            new StatValue(StatId.Mass, 1200f),
            new StatValue(StatId.PickupRadius, 4f)
        };

        [Header("Сокеты")]
        public SocketCapacity[] Sockets = new SocketCapacity[0];

        public int GetSocketCount(SocketType type)
        {
            if (Sockets == null)
                return 0;

            int total = 0;

            for (int i = 0; i < Sockets.Length; i++)
                if (Sockets[i].Type == type)
                    total += Sockets[i].Count;

            return total;
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
    }
}
