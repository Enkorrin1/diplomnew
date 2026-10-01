using System;
using System.Linq;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>A timed, interruptible roadside search with independently claimable rewards.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RoadsideSupplyCache : MonoBehaviour, IGarageInteractable
    {
        [Serializable] public sealed class Supply
        {
            public string id, title;
            [Min(1)] public int count = 1;
        }
        [Serializable] public sealed class RewardState { public string id; public int remaining; }
        [Serializable] public sealed class State
        {
            public string id;
            public bool opened;
            public RewardState[] rewards;
        }

        [SerializeField] string cacheId, title = "Тайник", contents;
        [SerializeField, Min(1)] float searchSeconds = 8;
        [SerializeField, Range(1, 4)] float interactionRadius = 2.8f;
        [SerializeField] Supply[] supplies = Array.Empty<Supply>();
        [SerializeField] EncounterZombie[] guards = Array.Empty<EncounterZombie>();
        [SerializeField] TextMesh statusLabel;
        [SerializeField] Transform lidHinge;
        int[] remaining;
        bool opened;
        float elapsed, nextLabel;
        GaragePlayerController searchingPlayer;
        GameRunController run;
        bool crewSearching;

        public string Id => cacheId;
        public bool IsOpen => opened;
        public bool IsSearching => searchingPlayer != null || crewSearching;
        public bool IsEmpty { get { EnsureState(); return remaining.All(n => n == 0); } }
        public float SearchSeconds => searchSeconds;
        public float Progress => opened ? 1 : Mathf.Clamp01(elapsed / searchSeconds);
        public void ConfigureLid(Transform hinge) { lidHinge = hinge; RefreshLabel(); }

        public void Configure(string id, string name, string description, float seconds, Supply[] loot,
            EncounterZombie[] nearbyGuards, TextMesh label)
        {
            if (string.IsNullOrEmpty(id) || loot == null || loot.Length == 0 || loot.Length > 16 ||
                loot.Any(s => s == null || string.IsNullOrEmpty(s.id) || s.count <= 0) ||
                loot.Select(s => s.id).Distinct().Count() != loot.Length)
                throw new ArgumentException("Cache needs a stable identity and unique positive rewards.");
            cacheId = id; title = name; contents = description; searchSeconds = Mathf.Max(1, seconds);
            supplies = loot; guards = nearbyGuards ?? Array.Empty<EncounterZombie>(); statusLabel = label;
            remaining = null; EnsureState(); RefreshLabel();
        }

        void Awake() => EnsureState();
        void OnEnable()
        {
            RefreshLabel();
        }
        void OnDisable() => CancelSearch(false);
        void EnsureState()
        {
            if (remaining == null || remaining.Length != supplies.Length)
                remaining = supplies.Select(s => Mathf.Max(0, s.count)).ToArray();
        }
        public State CaptureState()
        {
            EnsureState();
            return new State { id = cacheId, opened = opened, rewards = supplies.Select((s, i) =>
                new RewardState { id = s.id, remaining = remaining[i] }).ToArray() };
        }
        public static bool IsValidState(State state)
        {
            return state != null && !string.IsNullOrEmpty(state.id) && state.rewards != null &&
                state.rewards.Length > 0 && state.rewards.Length <= 16 && state.rewards.All(r => r != null &&
                    !string.IsNullOrEmpty(r.id) && r.remaining >= 0 && r.remaining <= 1000) &&
                state.rewards.Select(r => r.id).Distinct().Count() == state.rewards.Length;
        }
        public void RestoreState(State state)
        {
            if (!IsValidState(state) || state.id != cacheId) return;
            CancelSearch(false); EnsureState(); opened = state.opened;
            for (int i = 0; i < supplies.Length; i++)
            {
                var saved = state.rewards.FirstOrDefault(r => r != null && r.id == supplies[i].id);
                if (saved != null) remaining[i] = Mathf.Clamp(saved.remaining, 0, supplies[i].count);
            }
            RefreshLabel();
        }

        public bool CanInteract() => isActiveAndEnabled && !IsEmpty;
        public string GetPromptText()
        {
            if (IsEmpty) return title + " — пусто";
            if (IsSearching) return $"[E] Прервать вскрытие · {Mathf.CeilToInt(searchSeconds - elapsed)} с";
            return opened ? "[E] Забрать оставшиеся припасы · " + title
                : $"[E] Вскрыть: {title} · {searchSeconds:0} с\n{contents} · Шум привлечёт заражённых";
        }
        bool CanSearch(GaragePlayerController player)
        {
            return isActiveAndEnabled && player != null && player.isActiveAndEnabled && !player.IsMovementLocked &&
                !(player.GetComponent<PlayerFieldNeeds>()?.IsDead ?? false) &&
                !(player.GetComponent<PlayerDownedState>()?.IsDowned ?? false) &&
                !(run?.IsGameOver ?? false) &&
                (player.transform.position - transform.position).sqrMagnitude <= interactionRadius * interactionRadius;
        }
        public void Interact(GaragePlayerController player)
        {
            if (Coop.CoopSession.Instance?.Busy ?? false) return;
            if (IsEmpty) return;
            if (run == null) run = FindFirstObjectByType<GameRunController>();
            if (!CanSearch(player)) return;
            if (IsSearching) { CancelSearch(true); return; }
            if (opened) { Claim(player.GetComponent<PlayerPocketInventory>()); return; }
            searchingPlayer = player; elapsed = 0;
            AlertGuards();
            AudioManager.Instance?.PlayImpact();
            Notify($"{title}: вскрытие {searchSeconds:0} с.\nМожно прервать [E] или отойти. Шторм продолжает двигаться.");
            RefreshLabel();
        }
        void Update()
        {
            if (Coop.CoopSession.Instance?.Busy ?? false) return;
            if (!IsSearching || Time.deltaTime <= 0) return;
            if (!CanSearch(searchingPlayer)) { CancelSearch(true); return; }
            elapsed = Mathf.Min(searchSeconds, elapsed + Time.deltaTime);
            if (elapsed >= searchSeconds)
            {
                var player = searchingPlayer; searchingPlayer = null; opened = true;
                Claim(player.GetComponent<PlayerPocketInventory>());
                return;
            }
            if (Time.time >= nextLabel) { nextLabel = Time.time + .2f; RefreshLabel(); }
        }
        void CancelSearch(bool notify)
        {
            bool wasSearching = IsSearching;
            searchingPlayer = null; elapsed = 0; RefreshLabel();
            if (notify && wasSearching) Notify("Вскрытие прервано. Припасы остались в тайнике.");
        }
        public void AlertGuards()
        {
            foreach (var guard in guards) if (guard != null && guard.isActiveAndEnabled)
                guard.AlertToSound(transform.position);
        }
        public void SetCrewSearch(bool busy, float progress)
        {
            crewSearching = busy;
            elapsed = busy ? Mathf.Clamp01(progress) * searchSeconds : 0;
            RefreshLabel();
        }
        void Claim(PlayerPocketInventory inventory)
        {
            EnsureState();
            var taken = new System.Collections.Generic.List<string>();
            for (int i = 0; i < supplies.Length; i++)
            {
                if (remaining[i] <= 0 || inventory == null ||
                    !inventory.TryAddItem(supplies[i].id, supplies[i].title, remaining[i])) continue;
                taken.Add(supplies[i].title + " ×" + remaining[i]); remaining[i] = 0;
            }
            if (taken.Count > 0) AudioManager.Instance?.PlayLevelUp();
            Notify((taken.Count > 0 ? "Получено: " + string.Join(", ", taken) + "." : "Карманы заполнены.") +
                (IsEmpty ? " Тайник пуст." : "\nОстаток здесь: освободите карманы и нажмите [E]."));
            RefreshLabel();
        }
        void RefreshLabel()
        {
            if (lidHinge != null) lidHinge.localRotation = Quaternion.Euler(opened ? 75 : 0, 0, 0);
            if (statusLabel == null) return;
            statusLabel.text = title.ToUpperInvariant() + "\n" + (IsEmpty ? "ПУСТО" :
                IsSearching ? $"ВСКРЫТИЕ {Mathf.CeilToInt(searchSeconds - elapsed)} С · [E] ПРЕРВАТЬ" :
                opened ? "[E] ЗАБРАТЬ ОСТАТОК" : $"[E] ВСКРЫТЬ · {searchSeconds:0} С") +
                (IsEmpty ? "" : "\n" + (opened ? string.Join("\n", supplies.Select((s, i) =>
                    remaining[i] > 0 ? s.title + " ×" + remaining[i] : null).Where(s => s != null)) : contents));
            statusLabel.color = IsEmpty ? new Color(.5f, .6f, .55f) : new Color(1, .85f, .47f);
        }
        static void Notify(string text) => GarageInteractionUI.Instance?.ShowNotification(text, 6);
    }
}
