using System;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Универсальный тип жидкости в канистре или резервуаре.
    /// </summary>
    public enum FluidType
    {
        Gasoline,       // Бензин (АИ-92/95 для легковых авто)
        Water,          // Дистиллированная вода для системы охлаждения / радиатора
        Diesel,         // Дизельное топливо (генератор, тяжелая техника)
        EngineOil,      // Моторное масло
        Empty           // Пустая емкость
    }

    /// <summary>
    /// Псевдоним перечисления для обратной совместимости.
    /// </summary>
    public enum BunkerFluidType
    {
        Gasoline = 0,
        Water = 1,
        Diesel = 2,
        EngineOil = 3,
        Empty = 4
    }

    /// <summary>
    /// Универсальный компонент канистры / резервуара с настраиваемым объемом жидкости.
    /// Используется в бункере, на заправках, блокпостах, заброшенных автомобилях и в открытом мире.
    /// </summary>
    [AddComponentMenu("RogueDrive/Fluid Container")]
    public class FluidContainer : MonoBehaviour
    {
        public event Action<float, float> VolumeChanged;

        [Header("Параметры жидкости")]
        [Tooltip("Тип залитой жидкости в канистре")]
        [SerializeField] private BunkerFluidType fluidType = BunkerFluidType.Gasoline;

        [Tooltip("Максимальная вместимость канистры в литрах (например, 20 л)")]
        [SerializeField, Min(0.1f)] private float maxCapacityLiters = 20f;

        [Tooltip("Текущий залитый объем жидкости в литрах (выставляется в Инспекторе)")]
        [SerializeField, Min(0f)] private float currentLiters = 15f;

        [Header("Физический вес (кг)")]
        [Tooltip("Масса пустой металлической/пластиковой канистры")]
        [SerializeField, Min(0.1f)] private float emptyMassKg = 1.8f;

        [Tooltip("Плотность жидкости (кг/л): Бензин ~0.75, Дизель ~0.84, Вода ~1.0, Масло ~0.88")]
        [SerializeField, Min(0.1f)] private float fluidDensity = 0.75f;

        [Header("Поведение при заправке")]
        [Tooltip("Оставлять ли пустую канистру в руках игрока после полной выливки жидкости")]
        [SerializeField] private bool keepEmptyCanisterInHand = true;

        public BunkerFluidType FluidType => fluidType;
        public float MaxCapacityLiters => maxCapacityLiters;
        public float CurrentLiters => currentLiters;
        public float EmptyMassKg => emptyMassKg;
        public float FluidDensity => fluidDensity;
        public bool KeepEmptyCanisterInHand => keepEmptyCanisterInHand;

        public bool IsEmpty => currentLiters <= 0.0001f || fluidType == BunkerFluidType.Empty;
        public bool IsFull => currentLiters >= maxCapacityLiters - 0.05f;
        public float FillPercentage => maxCapacityLiters > 0f ? Mathf.Clamp01(currentLiters / maxCapacityLiters) : 0f;

        public float TotalMass => emptyMassKg + (currentLiters * fluidDensity);

        private void Awake()
        {
            ClampVolume();
            UpdatePhysicalMass();
        }

        private void OnValidate()
        {
            if (fluidType == BunkerFluidType.Empty)
            {
                currentLiters = 0f;
            }
            else
            {
                currentLiters = Mathf.Clamp(currentLiters, 0f, maxCapacityLiters);
                if (Mathf.Approximately(fluidDensity, 0.75f) || Mathf.Approximately(fluidDensity, 1.0f) ||
                    Mathf.Approximately(fluidDensity, 0.84f) || Mathf.Approximately(fluidDensity, 0.88f))
                {
                    fluidDensity = GetDefaultDensity(fluidType);
                }
            }

            UpdatePhysicalMass();
        }

        public void Configure(BunkerFluidType type, float capacity, float currentAmount)
        {
            fluidType = type;
            maxCapacityLiters = Mathf.Max(0.5f, capacity);
            currentLiters = Mathf.Clamp(currentAmount, 0f, maxCapacityLiters);
            fluidDensity = GetDefaultDensity(type);

            UpdatePhysicalMass();
            VolumeChanged?.Invoke(currentLiters, maxCapacityLiters);
        }

        public float PourOut(float requestedLiters)
        {
            if (IsEmpty || requestedLiters <= 0f) return 0f;

            float poured = Mathf.Min(currentLiters, requestedLiters);
            currentLiters -= poured;

            if (currentLiters <= 0.0001f)
            {
                currentLiters = 0f;
            }

            UpdatePhysicalMass();
            VolumeChanged?.Invoke(currentLiters, maxCapacityLiters);

            return poured;
        }

        public float Fill(float litersToAdd, BunkerFluidType newFluidType = BunkerFluidType.Gasoline)
        {
            if (litersToAdd <= 0f) return 0f;

            if (IsEmpty)
            {
                fluidType = newFluidType;
                fluidDensity = GetDefaultDensity(newFluidType);
            }

            float space = maxCapacityLiters - currentLiters;
            float added = Mathf.Min(space, litersToAdd);
            currentLiters += added;

            UpdatePhysicalMass();
            VolumeChanged?.Invoke(currentLiters, maxCapacityLiters);

            return added;
        }

        [ContextMenu("Заполнить канистру до 100%")]
        public void FillMax()
        {
            if (fluidType == BunkerFluidType.Empty)
            {
                fluidType = BunkerFluidType.Gasoline;
                fluidDensity = GetDefaultDensity(fluidType);
            }

            currentLiters = maxCapacityLiters;
            UpdatePhysicalMass();
            VolumeChanged?.Invoke(currentLiters, maxCapacityLiters);
        }

        [ContextMenu("Залить наполовину (50%)")]
        public void FillHalf()
        {
            if (fluidType == BunkerFluidType.Empty)
            {
                fluidType = BunkerFluidType.Gasoline;
                fluidDensity = GetDefaultDensity(fluidType);
            }

            currentLiters = maxCapacityLiters * 0.5f;
            UpdatePhysicalMass();
            VolumeChanged?.Invoke(currentLiters, maxCapacityLiters);
        }

        [ContextMenu("Опустошить канистру (0 л)")]
        public void DrainAll()
        {
            currentLiters = 0f;
            UpdatePhysicalMass();
            VolumeChanged?.Invoke(currentLiters, maxCapacityLiters);
        }

        public void UpdatePhysicalMass()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                float calculatedMass = Mathf.Max(0.5f, emptyMassKg + (currentLiters * fluidDensity));
                rb.mass = Mathf.Round(calculatedMass * 100f) / 100f;
            }
        }

        private void ClampVolume()
        {
            currentLiters = Mathf.Clamp(currentLiters, 0f, maxCapacityLiters);
        }

        public string GetFormattedVolumeString()
        {
            if (IsEmpty)
            {
                return $"Пустая (0 / {maxCapacityLiters:F0} л)";
            }

            return $"{currentLiters:F1} / {maxCapacityLiters:F0} л ({GetFluidName()})";
        }

        public string GetFluidName()
        {
            return fluidType switch
            {
                BunkerFluidType.Gasoline => "Бензин",
                BunkerFluidType.Water => "Вода",
                BunkerFluidType.Diesel => "Дизель",
                BunkerFluidType.EngineOil => "Моторное масло",
                BunkerFluidType.Empty => "Пусто",
                _ => "Жидкость"
            };
        }

        public string GetDefaultDisplayName()
        {
            if (IsEmpty)
            {
                return $"Пустая канистра ({maxCapacityLiters:F0} л)";
            }

            return fluidType switch
            {
                BunkerFluidType.Gasoline => $"Канистра бензина ({currentLiters:F1}/{maxCapacityLiters:F0} л)",
                BunkerFluidType.Water => $"Канистра с водой ({currentLiters:F1}/{maxCapacityLiters:F0} л)",
                BunkerFluidType.Diesel => $"Канистра дизеля ({currentLiters:F1}/{maxCapacityLiters:F0} л)",
                BunkerFluidType.EngineOil => $"Канистра масла ({currentLiters:F1}/{maxCapacityLiters:F0} л)",
                _ => $"Канистра ({currentLiters:F1}/{maxCapacityLiters:F0} л)"
            };
        }

        public static float GetDefaultDensity(BunkerFluidType type)
        {
            return type switch
            {
                BunkerFluidType.Gasoline => 0.75f,
                BunkerFluidType.Diesel => 0.84f,
                BunkerFluidType.Water => 1.0f,
                BunkerFluidType.EngineOil => 0.88f,
                _ => 0.75f
            };
        }
    }

    /// <summary>
    /// Псевдоним класса для обратной совместимости со старыми сценами и префабами бункера.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RogueDrive/Bunker Fluid Container (Legacy)")]
    public class BunkerFluidContainer : FluidContainer
    {
    }
}
