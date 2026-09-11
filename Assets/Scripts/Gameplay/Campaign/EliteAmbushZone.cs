using System.Collections.Generic;
using UnityEngine;
using RogueDrive.Gameplay.Narrative;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Элитная засада на подступах к казино: пик напряжения перед передышкой.
    /// Элитные противники — обычные объекты сцены, размещённые редакторской
    /// командой («RogueDrive/Казино/Разместить элитные засады») в выключенном
    /// состоянии. Зона лишь включает их при въезде машины и следит, когда отряд
    /// перебит, чтобы выдать награду. В рантайме ничего не создаётся.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class EliteAmbushZone : MonoBehaviour
    {
        [Header("Ambush")]
        [SerializeField, Min(1)] private int ambushIndex = 1;
        [SerializeField] private string ambushName = "Охрана казино";

        [Tooltip("Элитные противники сцены (выключенные объекты), которых зона включает при въезде машины.")]
        [SerializeField] private EnemyBase[] elites = new EnemyBase[0];

        [Header("Reward for clearing")]
        [SerializeField, Min(0)] private int bonusCoins = 12;
        [SerializeField, Min(0f)] private float bonusNitro = 25f;

        readonly HashSet<EnemyBase> alive = new HashSet<EnemyBase>();
        bool triggered;
        bool cleared;

        public int AmbushIndex => ambushIndex;
        public bool IsTriggered => triggered;
        public bool IsCleared => cleared;

        public void Configure(int index, string name)
        {
            ambushIndex = index;
            ambushName = name;
        }

        /// <summary>Привязка размещённых в сцене элитных противников (вызывается редакторской командой).</summary>
        public void BindElites(EnemyBase[] sceneElites)
        {
            elites = sceneElites ?? new EnemyBase[0];
        }

        private void Awake()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            if (col.size == Vector3.one)
            {
                col.size = new Vector3(26f, 8f, 4f);
                col.center = new Vector3(0f, 4f, 0f);
            }

            // Отряд ждёт выключенным до въезда машины
            for (int i = 0; i < elites.Length; i++)
                if (elites[i] != null && elites[i].gameObject.activeSelf)
                    elites[i].gameObject.SetActive(false);
        }

        private void OnEnable() => EnemyBase.AnyEnemyKilled += HandleEnemyKilled;
        private void OnDisable() => EnemyBase.AnyEnemyKilled -= HandleEnemyKilled;

        private void OnTriggerEnter(Collider other)
        {
            if (triggered) return;

            ArcadeCarController car = other.GetComponentInParent<ArcadeCarController>();
            if (car == null) return;

            Trigger(car);
        }

        void Trigger(ArcadeCarController car)
        {
            triggered = true;
            alive.Clear();

            for (int i = 0; i < elites.Length; i++)
            {
                EnemyBase elite = elites[i];
                if (elite == null) continue;

                elite.gameObject.SetActive(true);
                elite.SetTarget(car);
                alive.Add(elite);
            }

            if (alive.Count == 0)
            {
                cleared = true;
                return;
            }

            RogueDrive.Audio.AudioManager.Instance?.PlayMineBeep(0.8f);
            ArcadeCameraFollow.Instance?.TriggerShake(0.45f, 0.3f);

            PrototypeHud.Instance?.ShowBiomeNotification(
                "⚠ ЭЛИТНАЯ ЗАСАДА",
                $"{ambushName.ToUpperInvariant()} — ПЕРЕБЕЙТЕ ОТРЯД ({alive.Count}) ДО ВЪЕЗДА В КАЗИНО",
                new Color(1f, 0.35f, 0.2f));

            RadioTransmissionSystem.Instance?.EnqueueTransmission(
                "МАЯК // ЦИТАДЕЛЬ",
                "Сканер фиксирует элитную группу у придорожного казино. Они охраняют подъезд — пробивайтесь с ходу, не сбрасывайте скорость.",
                5f);
        }

        void HandleEnemyKilled(EnemyBase enemy)
        {
            if (!triggered || cleared || enemy == null || !alive.Remove(enemy))
                return;

            if (alive.Count > 0)
                return;

            cleared = true;

            GameRunController run = FindFirstObjectByType<GameRunController>();
            if (run != null)
            {
                if (bonusCoins > 0) run.AddCoins(bonusCoins);
                if (bonusNitro > 0f) run.AddNitro(bonusNitro);
            }

            RogueDrive.Audio.AudioManager.Instance?.PlayFanfare();
            Combat.ComboScoreSystem.Instance?.TriggerBanner("🛡 ЗАСАДА ОТБИТА!", new Color(0.3f, 1f, 0.5f));

            PrototypeHud.Instance?.ShowBiomeNotification(
                "ЗАСАДА ОТБИТА",
                $"ДОРОГА К КАЗИНО СВОБОДНА (+{bonusCoins} МОНЕТ, +{Mathf.RoundToInt(bonusNitro)} НИТРО)",
                new Color(0.3f, 1f, 0.5f));
        }
    }
}
