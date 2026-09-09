using System;
using System.Collections.Generic;

namespace RogueDrive.Modifiers
{
    /// <summary>
    /// Характеристики заезда. Итог складывается как (база + плоские прибавки) * произведение множителей.
    /// Пересобирается целиком при каждом изменении билда.
    /// </summary>
    public sealed class StatBlock
    {
        static readonly int StatCount = Enum.GetValues(typeof(StatId)).Length;

        readonly float[] _base = new float[StatCount];
        readonly float[] _flat = new float[StatCount];
        readonly float[] _multiplier = new float[StatCount];

        public StatBlock()
        {
            ResetToBase(null);
        }

        public void ResetToBase(IEnumerable<StatValue> baseValues)
        {
            for (int i = 0; i < StatCount; i++)
            {
                _base[i] = 0f;
                _flat[i] = 0f;
                _multiplier[i] = 1f;
            }

            if (baseValues == null)
                return;

            foreach (StatValue value in baseValues)
                _base[(int)value.Id] = value.Value;
        }

        public void AddFlat(StatId id, float value)
        {
            _flat[(int)id] += value;
        }

        public void AddMultiplier(StatId id, float value)
        {
            _multiplier[(int)id] *= value;
        }

        public float Get(StatId id)
        {
            int index = (int)id;
            return (_base[index] + _flat[index]) * _multiplier[index];
        }

        public float GetBase(StatId id) => _base[(int)id];
    }
}
