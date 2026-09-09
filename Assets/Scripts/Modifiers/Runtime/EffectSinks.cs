using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Modifiers
{
    /// <summary>Агрегированные параметры снарядов для всех турелей машины.</summary>
    public sealed class ProjectilePipeline
    {
        public int Bounces { get; private set; }
        public float Slow { get; private set; }
        public float Burn { get; private set; }

        public void AddBounces(int value) => Bounces += Mathf.Max(0, value);
        public void AddSlow(float value) => Slow = Mathf.Clamp01(Slow + value);
        public void AddBurn(float value) => Burn += Mathf.Max(0f, value);

        public void Clear()
        {
            Bounces = 0;
            Slow = 0f;
            Burn = 0f;
        }
    }

    public struct BehaviourState
    {
        public int Level;
        public float Radius;
        public float TickDamage;
        public float Interval;
    }

    /// <summary>
    /// Активные постоянные поведения. Слой представления подписывается на Changed
    /// и синхронизирует объекты сцены; в режиме симуляции подписчиков нет.
    /// </summary>
    public sealed class BehaviourRegistry
    {
        readonly Dictionary<string, BehaviourState> _states = new Dictionary<string, BehaviourState>();

        public event Action<string, BehaviourState> Changed;
        public event Action Cleared;

        public IReadOnlyDictionary<string, BehaviourState> States => _states;

        public void Enable(string behaviourId, BehaviourState state)
        {
            if (string.IsNullOrEmpty(behaviourId))
                return;

            _states[behaviourId] = state;
            Changed?.Invoke(behaviourId, state);
        }

        public bool IsActive(string behaviourId) => _states.ContainsKey(behaviourId);

        public bool TryGet(string behaviourId, out BehaviourState state) =>
            _states.TryGetValue(behaviourId, out state);

        public void Clear()
        {
            _states.Clear();
            Cleared?.Invoke();
        }
    }

    public struct ResourceTrigger
    {
        public ResourceKind Kind;
        public int KillsPerTrigger;
        public float Amount;
    }

    /// <summary>Восстановление ресурсов по счётчику убийств.</summary>
    public sealed class ResourceRegistry
    {
        readonly List<ResourceTrigger> _triggers = new List<ResourceTrigger>();

        public IReadOnlyList<ResourceTrigger> Triggers => _triggers;

        public void RegisterKillTrigger(ResourceKind kind, int killsPerTrigger, float amount)
        {
            if (killsPerTrigger <= 0 || amount <= 0f)
                return;

            _triggers.Add(new ResourceTrigger
            {
                Kind = kind,
                KillsPerTrigger = killsPerTrigger,
                Amount = amount
            });
        }

        public void Clear() => _triggers.Clear();
    }
}
