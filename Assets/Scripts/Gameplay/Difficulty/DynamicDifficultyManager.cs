using System;
using System.Collections.Generic;
using RogueDrive.Gameplay.Combat;
using UnityEngine;

namespace RogueDrive.Gameplay.Difficulty
{
    /// <summary>
    /// Интеллектуальный модуль динамической адаптации сложности (Dynamic Difficulty Adjustment, DDA).
    /// В реальном времени оценивает индекс стресса и мастерства игрока (Stress & Performance Index, SPI)
    /// на основе текущего запаса прочности, расхода топлива, комбо-серий и темпа получения урона.
    /// Адаптивно корректирует плотность врагов на трассе, вероятность элитных мутантов и веса драфта модификаторов.
    /// </summary>
    public sealed class DynamicDifficultyManager : MonoBehaviour
    {
        public static DynamicDifficultyManager Instance { get; private set; }

        public event Action<float, string> OnDifficultyAdjusted;

        [Header("Tuning & Sensitivity")]
        [SerializeField, Range(0.1f, 1.0f)] private float updateInterval = 0.5f;
        [SerializeField, Range(3f, 20f)] private float damageTrackingWindow = 8f;
        [SerializeField] private bool enableDDA = true;

        [Header("State Readout (0.0 = Кризис, 0.5 = Баланс, 1.0 = Доминирование)")]
        [SerializeField, Range(0f, 1f)] private float currentSPI = 0.5f;
        [SerializeField] private string difficultyTierName = "Номинальный поток (Normal Flow)";

        [Header("Current Adaptive Multipliers")]
        [SerializeField] private float enemyDensityMultiplier = 1.0f;
        [SerializeField] private float eliteChanceMultiplier = 1.0f;
        [SerializeField] private float defenseDraftWeightMultiplier = 1.0f;
        [SerializeField] private float supplyCrateBonusChance = 0f;

        private ArcadeCarController car;
        private GameRunController run;
        private ComboScoreSystem combo;

        private float timer;
        private float lastHp;
        private readonly Queue<DamageSample> damageHistory = new Queue<DamageSample>();

        private struct DamageSample
        {
            public float timestamp;
            public float amount;
        }

        public bool IsDDAEnabled => enableDDA;
        public float CurrentSPI => currentSPI;
        public float EnemyDensityMultiplier => enableDDA ? enemyDensityMultiplier : 1.0f;
        public float EliteChanceMultiplier => enableDDA ? eliteChanceMultiplier : 1.0f;
        public float DefenseDraftWeightMultiplier => enableDDA ? defenseDraftWeightMultiplier : 1.0f;
        public float SupplyCrateBonusChance => enableDDA ? supplyCrateBonusChance : 0f;
        public string DifficultyTierName => difficultyTierName;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            ResetMultipliers();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            car = FindFirstObjectByType<ArcadeCarController>();
            run = FindFirstObjectByType<GameRunController>();
            combo = FindFirstObjectByType<ComboScoreSystem>();

            if (run != null)
                lastHp = run.Health;
        }

        private void Update()
        {
            if (!enableDDA) return;

            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                EvaluatePlayerPerformance();
            }
        }

        private void EvaluatePlayerPerformance()
        {
            if (run == null)
            {
                run = FindFirstObjectByType<GameRunController>();
                if (run == null) return;
            }
            if (car == null)
            {
                car = FindFirstObjectByType<ArcadeCarController>();
            }
            if (combo == null)
            {
                combo = FindFirstObjectByType<ComboScoreSystem>();
            }

            float now = Time.time;

            // 1. Отслеживание полученного урона за скользящее окно
            float currentHp = run.Health;
            if (currentHp < lastHp)
            {
                damageHistory.Enqueue(new DamageSample { timestamp = now, amount = lastHp - currentHp });
            }
            lastHp = currentHp;

            while (damageHistory.Count > 0 && (now - damageHistory.Peek().timestamp) > damageTrackingWindow)
            {
                damageHistory.Dequeue();
            }

            float damageTakenInWindow = 0f;
            foreach (var sample in damageHistory)
            {
                damageTakenInWindow += sample.amount;
            }

            // 2. Расчет компонент индекса стресса и эффективности
            float hpRatio = run.MaxHealth > 0 ? Mathf.Clamp01(run.Health / run.MaxHealth) : 1f;
            float fuelRatio = run.MaxFuel > 0 ? Mathf.Clamp01(run.Fuel / run.MaxFuel) : 1f;

            // Штраф за высокий темп урона (если потеряли 50% HP за окно — сильный стресс)
            float damageStress = Mathf.Clamp01(damageTakenInWindow / Mathf.Max(20f, run.MaxHealth * 0.4f));

            // Бонус за скорость и комбо
            float speedBonus = car != null ? Mathf.Clamp01(Mathf.Abs(car.SpeedMps) / 22f) : 0.5f;
            float comboBonus = combo != null ? Mathf.Clamp01(combo.ComboCount / 10f) : 0f;

            // 3. Формула вычисления SPI (0.0 = бедствие, 1.0 = триумф)
            // SPI = 0.45 * HP + 0.20 * Fuel - 0.35 * DamageStress + 0.15 * Speed + 0.15 * Combo
            float rawSPI = (hpRatio * 0.45f) + (fuelRatio * 0.20f) - (damageStress * 0.35f) + (speedBonus * 0.15f) + (comboBonus * 0.15f);
            currentSPI = Mathf.Clamp01(Mathf.Lerp(currentSPI, rawSPI, 0.35f));

            // 4. Расчет адаптивных коэффициентов управления сложностью
            if (currentSPI < 0.35f)
            {
                // Игрок в критическом состоянии: уменьшаем давление, предлагаем спасение
                float crisisSeverity = (0.35f - currentSPI) / 0.35f; // 0..1
                enemyDensityMultiplier = Mathf.Lerp(0.90f, 0.65f, crisisSeverity);
                eliteChanceMultiplier = Mathf.Lerp(0.80f, 0.40f, crisisSeverity);
                defenseDraftWeightMultiplier = Mathf.Lerp(1.25f, 2.20f, crisisSeverity);
                supplyCrateBonusChance = Mathf.Lerp(0.10f, 0.30f, crisisSeverity);
                difficultyTierName = "Смягчение угрозы (Emergency Lifeline)";
            }
            else if (currentSPI > 0.70f)
            {
                // Игрок уверенно доминирует: повышаем вызов и азарт
                float dominanceRatio = (currentSPI - 0.70f) / 0.30f; // 0..1
                enemyDensityMultiplier = Mathf.Lerp(1.05f, 1.25f, dominanceRatio);
                eliteChanceMultiplier = Mathf.Lerp(1.15f, 1.50f, dominanceRatio);
                defenseDraftWeightMultiplier = 0.85f;
                supplyCrateBonusChance = 0.05f;
                difficultyTierName = "Повышенный вызов (Intense Escalation)";
            }
            else
            {
                // Номинальный баланс
                ResetMultipliers();
                difficultyTierName = "Номинальный поток (Normal Flow)";
            }

            OnDifficultyAdjusted?.Invoke(currentSPI, difficultyTierName);
        }

        private void ResetMultipliers()
        {
            enemyDensityMultiplier = 1.0f;
            eliteChanceMultiplier = 1.0f;
            defenseDraftWeightMultiplier = 1.0f;
            supplyCrateBonusChance = 0f;
        }

        public void SetDDAEnabled(bool state)
        {
            enableDDA = state;
            if (!enableDDA)
            {
                ResetMultipliers();
                currentSPI = 0.5f;
                difficultyTierName = "DDA отключен (Fixed Difficulty)";
            }
        }
    }
}
