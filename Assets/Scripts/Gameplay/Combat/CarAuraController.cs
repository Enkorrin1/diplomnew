using System.Collections.Generic;
using UnityEngine;
using RogueDrive.Modifiers;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Контроллер активных аур и постоянных поведений вокруг автомобиля игрока.
    /// Реализует эффекты огненного следа (fire_trail), боковых пил (side_saws),
    /// импульсной ударной волны (shockwave), цепной молнии (chain_lightning)
    /// и защитного энергощита (energy_shield) на базе BehaviourRegistry.
    /// </summary>
    public sealed class CarAuraController : MonoBehaviour
    {
        BehaviourRegistry registry;
        readonly Dictionary<string, float> cooldownTimers = new Dictionary<string, float>();

        public void BindBehaviours(BehaviourRegistry behaviourRegistry)
        {
            registry = behaviourRegistry;
            if (registry != null)
            {
                registry.Changed += HandleBehaviourChanged;
            }
        }

        private void OnDestroy()
        {
            if (registry != null)
            {
                registry.Changed -= HandleBehaviourChanged;
            }
        }

        void HandleBehaviourChanged(string behaviourId, BehaviourState state)
        {
            if (!cooldownTimers.ContainsKey(behaviourId))
            {
                cooldownTimers[behaviourId] = 0f;
            }
        }

        private void Update()
        {
            if (registry == null)
                return;

            float dt = Time.deltaTime;

            foreach (var kvp in registry.States)
            {
                string id = kvp.Key;
                BehaviourState state = kvp.Value;

                switch (id)
                {
                    case "fire_trail":
                        TickFireTrail(state, dt);
                        break;
                    case "side_saws":
                        TickSideSaws(state, dt);
                        break;
                    case "shockwave":
                        TickShockwave(state, dt);
                        break;
                    case "chain_lightning":
                        TickChainLightning(state, dt);
                        break;
                    case "energy_shield":
                        TickEnergyShield(state, dt);
                        break;
                }
            }
        }

        void TickFireTrail(BehaviourState state, float dt)
        {
            // Огненный след позади машины
            Vector3 trailOrigin = transform.position - transform.forward * 2.2f;
            float radius = Mathf.Max(2f, state.Radius);
            float dmg = Mathf.Max(5f, state.TickDamage) * dt;

            Collider[] hits = Physics.OverlapSphere(trailOrigin, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                IDamageable damageable = hits[i].GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(dmg, 0f, dmg * 2.5f);
                }
            }
        }

        void TickSideSaws(BehaviourState state, float dt)
        {
            // Орбитальные пилы по бортам машины
            float radius = Mathf.Max(2.5f, state.Radius);
            float dmg = Mathf.Max(8f, state.TickDamage) * dt;

            Vector3 leftPoint = transform.position - transform.right * 1.5f;
            Vector3 rightPoint = transform.position + transform.right * 1.5f;

            DamageArea(leftPoint, radius, dmg);
            DamageArea(rightPoint, radius, dmg);
        }

        void TickShockwave(BehaviourState state, float dt)
        {
            float interval = state.Interval > 0.1f ? state.Interval : 3.0f;
            if (!cooldownTimers.ContainsKey("shockwave"))
                cooldownTimers["shockwave"] = interval;

            cooldownTimers["shockwave"] -= dt;
            if (cooldownTimers["shockwave"] <= 0f)
            {
                cooldownTimers["shockwave"] = interval;
                TriggerShockwave(state);
            }
        }

        void TriggerShockwave(BehaviourState state)
        {
            float radius = Mathf.Max(6f, state.Radius);
            float dmg = Mathf.Max(25f, state.TickDamage);

            ArcadeCameraFollow.Instance?.TriggerShake(0.6f, 0.25f);

            Collider[] hits = Physics.OverlapSphere(transform.position, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                IDamageable damageable = hits[i].GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(dmg, 0.6f, 0f);

                    Rigidbody enemyRb = hits[i].GetComponentInParent<Rigidbody>();
                    if (enemyRb != null)
                    {
                        Vector3 pushDir = (hits[i].transform.position - transform.position).normalized;
                        pushDir.y = 0.2f;
                        enemyRb.AddForce(pushDir * 12f, ForceMode.Impulse);
                    }
                }
            }
        }

        void TickChainLightning(BehaviourState state, float dt)
        {
            float interval = state.Interval > 0.1f ? state.Interval : 2.2f;
            if (!cooldownTimers.ContainsKey("chain_lightning"))
                cooldownTimers["chain_lightning"] = interval;

            cooldownTimers["chain_lightning"] -= dt;
            if (cooldownTimers["chain_lightning"] <= 0f)
            {
                cooldownTimers["chain_lightning"] = interval;
                TriggerChainLightning(state);
            }
        }

        void TriggerChainLightning(BehaviourState state)
        {
            float radius = Mathf.Max(12f, state.Radius);
            float dmg = Mathf.Max(30f, state.TickDamage);

            Collider[] hits = Physics.OverlapSphere(transform.position, radius);
            int zapped = 0;
            const int maxTargets = 3;

            for (int i = 0; i < hits.Length; i++)
            {
                IDamageable damageable = hits[i].GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(dmg, 0.3f, 0f);
                    zapped++;
                    if (zapped >= maxTargets)
                        break;
                }
            }
        }

        void TickEnergyShield(BehaviourState state, float dt)
        {
            float radius = Mathf.Max(3f, state.Radius);
            Collider[] hits = Physics.OverlapSphere(transform.position, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                EnemyBase enemy = hits[i].GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    Vector3 repulse = (enemy.transform.position - transform.position).normalized;
                    repulse.y = 0f;
                    enemy.transform.position += repulse * (4f * dt);
                }
            }
        }

        void DamageArea(Vector3 origin, float radius, float dmg)
        {
            Collider[] hits = Physics.OverlapSphere(origin, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                IDamageable damageable = hits[i].GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(dmg, 0.2f, 0f);
                }
            }
        }
    }
}
