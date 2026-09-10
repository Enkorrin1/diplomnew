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

        /// <summary>
        /// Возвращает назначенный префаб или автоматически разрешает 3D-модель из ассетов по Id автомобиля.
        /// </summary>
        public GameObject EffectivePrefab
        {
            get
            {
                if (Prefab != null) return Prefab;
#if UNITY_EDITOR
                string path = Id switch
                {
                    "light" => "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Classic Car_9.prefab",
                    "truck" => "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/N Van_10.prefab",
                    "suv" => "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Pick Up_11.prefab",
                    "armored" => "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Military Vehicle_3.prefab",
                    "sport" => "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Sport Car_39.prefab",
                    "police" => "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Police Car N_4.prefab",
                    _ => null
                };
                if (!string.IsNullOrEmpty(path))
                {
                    Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    return Prefab;
                }
#endif
                return null;
            }
        }

        [Min(0)] public int Price = 0;
        [SerializeField, Min(1)] public int RequiredCampaignLevel = 1;
        public Color BodyColor = new Color(0.08f, 0.55f, 0.95f);

        public int GetRequiredCampaignLevel()
        {
            if (RequiredCampaignLevel > 1) return RequiredCampaignLevel;
            return Id switch
            {
                "light" => 1,
                "truck" => 2,
                "suv" => 3,
                "armored" => 4,
                "sport" => 5,
                "police" => 5,
                _ => 1
            };
        }

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

        public float GetStat(StatId id, float defaultValue = 0f)
        {
            if (BaseStats == null) return defaultValue;
            for (int i = 0; i < BaseStats.Length; i++)
            {
                if (BaseStats[i].Id == id)
                    return BaseStats[i].Value;
            }
            return defaultValue;
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                Id = name;
        }
    }
}
