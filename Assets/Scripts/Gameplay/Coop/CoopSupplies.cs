using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay.Hub;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RogueDrive.Gameplay.Coop
{
    /// <summary>Expedition-owned supplies and cache history. Requests never contain reward quantities.</summary>
    public sealed class CoopSupplies : NetworkBehaviour
    {
        public struct Reward : INetworkSerializable, IEquatable<Reward>
        {
            public FixedString128Bytes cache, item;
            public int remaining;
            public bool opened;
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            { s.SerializeValue(ref cache); s.SerializeValue(ref item); s.SerializeValue(ref remaining); s.SerializeValue(ref opened); }
            public bool Equals(Reward r) => cache.Equals(r.cache) && item.Equals(r.item) && remaining == r.remaining && opened == r.opened;
        }
        public struct Search : INetworkSerializable, IEquatable<Search>
        {
            public FixedString128Bytes cache;
            public ulong actor;
            public float progress;
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            { s.SerializeValue(ref cache); s.SerializeValue(ref actor); s.SerializeValue(ref progress); }
            public bool Equals(Search r) => cache.Equals(r.cache) && actor == r.actor && progress.Equals(r.progress);
        }
        sealed class Work { public CoopPlayer player; public RoadsideSupplyCache cache; public float elapsed; }
        public static CoopSupplies Instance { get; private set; }
        public readonly NetworkVariable<int> Ammo = new NetworkVariable<int>();
        public readonly NetworkVariable<int> Medkits = new NetworkVariable<int>();
        public readonly NetworkVariable<int> Bolts = new NetworkVariable<int>();
        public readonly NetworkVariable<int> Batteries = new NetworkVariable<int>();
        public readonly NetworkVariable<bool> Wrench = new NetworkVariable<bool>();
        public readonly NetworkVariable<FixedString128Bytes> MarkedCache = new NetworkVariable<FixedString128Bytes>();
        public readonly NetworkVariable<Vector3> MarkedPosition = new NetworkVariable<Vector3>();
        NetworkList<Reward> rewards;
        NetworkList<Search> searches;
        readonly Dictionary<string, Work> work = new Dictionary<string, Work>();
        readonly Dictionary<RoadsideSupplyCache, int> applied = new Dictionary<RoadsideSupplyCache, int>();
        RoadsideSupplyCache[] sceneCaches = Array.Empty<RoadsideSupplyCache>();
        int revision;
        float nextScan, nextPublish;
        public string InventoryText => $"ОБЩИЕ ПРИПАСЫ  ·  9мм {Ammo.Value}/168  ·  аптечки {Medkits.Value}/8  ·  болты {Bolts.Value}/32  ·  АКБ {Batteries.Value}/4  ·  ключ {(Wrench.Value ? "есть" : "нет")}";

        void Awake() { rewards = new NetworkList<Reward>(); searches = new NetworkList<Search>(); }
        public override void OnNetworkSpawn()
        {
            Instance = this;
            rewards.OnListChanged += RewardsChanged;
            searches.OnListChanged += SearchesChanged;
            revision++;
        }
        public override void OnNetworkDespawn()
        {
            rewards.OnListChanged -= RewardsChanged;
            searches.OnListChanged -= SearchesChanged;
            work.Clear(); applied.Clear();
            foreach (var c in sceneCaches) if (c != null) c.SetCrewSearch(false, 0);
            if (Instance == this) Instance = null;
        }
        void RewardsChanged(NetworkListEvent<Reward> e) => revision++;
        void SearchesChanged(NetworkListEvent<Search> e) => revision++;

        void Update()
        {
            if (!IsSpawned) return;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + .5f;
                sceneCaches = FindObjectsByType<RoadsideSupplyCache>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var dead in applied.Keys.Where(c => c == null).ToArray()) applied.Remove(dead);
                if (IsServer) foreach (var c in sceneCaches) Register(c);
            }
            if (IsServer)
            {
                foreach (var pair in work.ToArray())
                {
                    var w = pair.Value;
                    if (!CanUse(w.player, w.cache)) { Cancel(pair.Key); continue; }
                    w.elapsed += Time.deltaTime;
                    if (w.elapsed < w.cache.SearchSeconds) continue;
                    Open(pair.Key); Cancel(pair.Key); Claim(w.player, w.cache);
                }
                if (Time.unscaledTime >= nextPublish)
                {
                    nextPublish = Time.unscaledTime + .2f;
                    foreach (var pair in work)
                    {
                        var s = new Search { cache = pair.Key, actor = pair.Value.player.OwnerClientId,
                            progress = Mathf.Clamp01(pair.Value.elapsed / pair.Value.cache.SearchSeconds) };
                        int i = SearchIndex(pair.Key);
                        if (i < 0) searches.Add(s); else searches[i] = s;
                    }
                }
            }
            foreach (var c in sceneCaches)
            {
                if (c == null || (applied.TryGetValue(c, out int r) && r == revision)) continue;
                var state = State(c.Id);
                if (state == null) continue;
                c.RestoreState(state);
                int i = SearchIndex(c.Id);
                c.SetCrewSearch(i >= 0, i >= 0 ? searches[i].progress : 0);
                applied[c] = revision;
            }
        }
        void Register(RoadsideSupplyCache c)
        {
            if (c == null || string.IsNullOrEmpty(c.Id) || State(c.Id) != null) return;
            var state = c.CaptureState();
            if (!RoadsideSupplyCache.IsValidState(state) || state.id.Length > 120 ||
                state.rewards.Any(r => r.id.Length > 120)) return;
            foreach (var r in state.rewards)
                rewards.Add(new Reward { cache = state.id, item = r.id, remaining = r.remaining, opened = state.opened });
        }
        public RoadsideSupplyCache.State State(string id)
        {
            var found = new List<RoadsideSupplyCache.RewardState>(); bool opened = false;
            foreach (var r in rewards)
                if (r.cache.ToString() == id)
                { opened = r.opened; found.Add(new RoadsideSupplyCache.RewardState { id = r.item.ToString(), remaining = r.remaining }); }
            return found.Count == 0 ? null : new RoadsideSupplyCache.State { id = id, opened = opened, rewards = found.ToArray() };
        }
        int SearchIndex(string id)
        { for (int i = 0; i < searches.Count; i++) if (searches[i].cache.ToString() == id) return i; return -1; }
        void Cancel(string id)
        { work.Remove(id); int i = SearchIndex(id); if (i >= 0) searches.RemoveAt(i); }
        void Open(string id)
        {
            for (int i = 0; i < rewards.Count; i++)
            { var r = rewards[i]; if (r.cache.ToString() != id) continue; r.opened = true; rewards[i] = r; }
        }
        public string Prompt(RoadsideSupplyCache c, CoopPlayer player)
        {
            int i = SearchIndex(c.Id);
            if (i >= 0) return searches[i].actor == player.OwnerClientId
                ? $"[E] Прервать вскрытие · {Mathf.CeilToInt((1 - searches[i].progress) * c.SearchSeconds)} с"
                : "Напарник вскрывает тайник. Прикрывайте его.";
            return c.GetPromptText() + "\nПрипасы поступят в общий запас экипажа.";
        }
        bool CanUse(CoopPlayer p, RoadsideSupplyCache c)
        {
            if (!IsServer || p == null || !p.IsSpawned || p.IsDowned.Value || p.Health.Value <= 0 || p.Seat.Value >= 0 ||
                c == null || !c.isActiveAndEnabled || CoopVehicle.Instance == null || CoopVehicle.Instance.Transitioning.Value ||
                !(CoopQuestManager.Instance?.QuestBunkerDeparted.Value ?? false) ||
                !NetworkManager.ConnectedClients.TryGetValue(p.OwnerClientId, out var client) || client.PlayerObject != p.NetworkObject ||
                Vector3.Distance(p.transform.position, c.transform.position) > 2.8f) return false;
            var eye = p.transform.position + Vector3.up * 1.65f;
            var target = c.GetComponent<BoxCollider>().bounds.ClosestPoint(eye);
            var delta = target - eye;
            foreach (var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<CoopPlayer>() == null &&
                    hit.collider.GetComponentInParent<RoadsideSupplyCache>() != c) return false;
            return true;
        }
        public void ServerInteract(CoopPlayer p, string id)
        {
            if (!IsServer || string.IsNullOrEmpty(id) || id.Length > 120) return;
            var c = sceneCaches.FirstOrDefault(c => c != null && c.Id == id && c.isActiveAndEnabled);
            if (!CanUse(p, c)) { p?.ServerFeedback("Подойдите к тайнику пешком и дождитесь загрузки трассы."); return; }
            Register(c);
            var state = State(id);
            if (state == null || state.rewards.All(r => r.remaining == 0)) return;
            if (work.TryGetValue(id, out var w))
            {
                if (w.player == p) { Cancel(id); p.ServerFeedback("Вскрытие прервано. Припасы остались на месте."); }
                else p.ServerFeedback("Тайник уже вскрывает напарник. Прикрывайте его.");
                return;
            }
            if (state.opened) { Claim(p, c); return; }
            foreach (var own in work.Where(x => x.Value.player == p).Select(x => x.Key).ToArray()) Cancel(own);
            work[id] = new Work { player = p, cache = c };
            c.AlertGuards();
            p.ServerFeedback($"Вскрытие: {c.SearchSeconds:0} с. Шум привлёк заражённых!");
        }
        void Claim(CoopPlayer p, RoadsideSupplyCache c)
        {
            int received = 0;
            for (int i = 0; i < rewards.Count; i++)
            {
                var r = rewards[i]; if (r.cache.ToString() != c.Id || r.remaining <= 0) continue;
                int taken = Add(r.item.ToString(), r.remaining);
                if (taken == 0) continue;
                r.remaining -= taken; received += taken; rewards[i] = r;
            }
            bool empty = State(c.Id).rewards.All(r => r.remaining == 0);
            p.ServerFeedback((received > 0 ? "Припасы добавлены в ОБЩИЙ запас экипажа." : "Общий запас заполнен.") +
                (empty ? " Тайник пуст." : " Остаток сохранён: заберите после расходования припасов."));
            if (empty && c.Id.StartsWith("route0/supply/") && int.TryParse(c.Id.Substring("route0/supply/".Length), out int stop))
                CoopQuestManager.Instance?.ServerCompleteRoadStop(stop + 1);
        }
        int Add(string item, int count)
        {
            switch (item)
            {
                case "ammo_9mm": return Add(Ammo, 168, count);
                case "first_aid_medkit": return Add(Medkits, 8, count);
                case "bolt_repair": return Add(Bolts, 32, count);
                case "battery_small": return Add(Batteries, 4, count);
                case "wrench_tool": if (Wrench.Value) return 0; Wrench.Value = true; return Mathf.Min(1, count);
                default: return 0;
            }
        }
        static int Add(NetworkVariable<int> value, int capacity, int count)
        { int n = Mathf.Clamp(count, 0, capacity - value.Value); value.Value += n; return n; }
        public int ServerTakeAmmo(int count)
        { if (!IsServer) return 0; int n = Mathf.Clamp(count, 0, Ammo.Value); Ammo.Value -= n; return n; }

        public string MarkerText
        {
            get
            {
                if (MarkedCache.Value.IsEmpty || CoopVehicle.Instance == null) return "[Q] Отметить ближайший тайник";
                var c = sceneCaches.FirstOrDefault(x => x != null && x.Id == MarkedCache.Value.ToString());
                var delta = MarkedPosition.Value - CoopVehicle.Instance.transform.position;
                float angle = Vector3.SignedAngle(CoopVehicle.Instance.transform.forward, new Vector3(delta.x, 0, delta.z), Vector3.up);
                string direction = Mathf.Abs(angle) > 140 ? "ПОЗАДИ" : angle > 25 ? "СПРАВА" : angle < -25 ? "СЛЕВА" : "ВПЕРЕДИ";
                return $"МЕТКА ЭКИПАЖА: {delta.magnitude:0} м · {direction}" + (c != null && c.IsEmpty ? " · ПУСТО" : "");
            }
        }
        public void ServerAction(CoopPlayer p, int action)
        {
            var car = CoopVehicle.Instance;
            if (!IsServer || p == null || p.IsDowned.Value || p.Health.Value <= 0 || car == null || car.Transitioning.Value ||
                !(CoopQuestManager.Instance?.QuestBunkerDeparted.Value ?? false)) return;
            if (action == 2)
            {
                if (p.Seat.Value == 0) { p.ServerFeedback("За рулём лечение выполняет штурман."); return; }
                var target = p;
                if (p.Seat.Value == 1)
                    target = FindObjectsByType<CoopPlayer>(FindObjectsSortMode.None).Where(x => x.Seat.Value >= 0 &&
                        !x.IsDowned.Value && x.Health.Value > 0).OrderBy(x => x.Health.Value).FirstOrDefault();
                if (target == null || target.Health.Value >= 100 || Medkits.Value == 0)
                { p.ServerFeedback("Аптечка нужна при ранении. Проверьте общий запас."); return; }
                Medkits.Value--; target.Health.Value = Mathf.Min(100, target.Health.Value + 40);
                p.ServerFeedback("Аптечка использована: +40 HP самому раненому члену экипажа.");
                return;
            }
            if (p.Seat.Value != 1 || car.Navigator.Value != p.OwnerClientId)
            { p.ServerFeedback("Это действие выполняет штурман."); return; }
            if (action == 0)
            {
                var c = sceneCaches.Where(x => x != null &&
                    State(x.Id)?.rewards.Any(r => r.remaining > 0) == true)
                    .OrderBy(x => (x.transform.position - car.transform.position).sqrMagnitude).FirstOrDefault();
                if (c == null) { p.ServerFeedback("В загруженном регионе все тайники пусты."); return; }
                MarkedCache.Value = c.Id; MarkedPosition.Value = c.transform.position;
                p.ServerFeedback("Тайник отмечен для всего экипажа. Водитель видит направление и расстояние.");
            }
            else if (action == 1)
            {
                if (!car.Stopped) { p.ServerFeedback("Для ремонта полностью остановите машину."); return; }
                if (car.Hull.Value >= 100) { p.ServerFeedback("Машина исправна — материалы сохранены."); return; }
                if (!Wrench.Value || Bolts.Value < 2 || (car.Hull.Value <= 0 && Batteries.Value < 1))
                { p.ServerFeedback("Ремонт: ключ и 2 болта. При заглохшем моторе также нужна АКБ."); return; }
                if (car.Hull.Value <= 0) Batteries.Value--;
                Bolts.Value -= 2; car.Hull.Value = Mathf.Min(100, car.Hull.Value + 25);
                p.ServerFeedback("Полевой ремонт: +25 прочности. Потрачено 2 болта.");
            }
        }
    }
}
