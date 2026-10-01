using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace RogueDrive.Gameplay.Coop
{
    /// <summary>Session history for authored encounters, including sites temporarily unloaded by streaming.</summary>
    public sealed class CoopEnemies : NetworkBehaviour
    {
        public struct Snapshot : INetworkSerializable, IEquatable<Snapshot>
        {
            public FixedString64Bytes id;
            public Vector3 position;
            public Quaternion rotation;
            public float health, age, speed;
            public int state, hits;
            public bool active;
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref id); s.SerializeValue(ref position); s.SerializeValue(ref rotation);
                s.SerializeValue(ref health); s.SerializeValue(ref age); s.SerializeValue(ref speed);
                s.SerializeValue(ref state); s.SerializeValue(ref hits); s.SerializeValue(ref active);
            }
            public bool Equals(Snapshot s) => id.Equals(s.id) && position == s.position && rotation == s.rotation &&
                health.Equals(s.health) && age.Equals(s.age) && speed.Equals(s.speed) && state == s.state && hits == s.hits && active == s.active;
        }
        public static CoopEnemies Instance { get; private set; }
        NetworkList<Snapshot> history;
        readonly Dictionary<string, int> indices = new Dictionary<string, int>();
        readonly Dictionary<EncounterZombie, string> identities = new Dictionary<EncounterZombie, string>();
        EncounterZombie[] enemies = Array.Empty<EncounterZombie>();
        readonly List<EncounterZombie> stale = new List<EncounterZombie>();
        float nextScan, nextPublish;
        void Awake() => history = new NetworkList<Snapshot>();
        public override void OnNetworkSpawn() { Instance = this; history.OnListChanged += Changed; Reindex(); }
        public override void OnNetworkDespawn()
        {
            history.OnListChanged -= Changed; indices.Clear(); identities.Clear();
            if (Instance == this) Instance = null;
        }
        // IDs never change and history is append-only. Position updates do not need a new index.
        void Changed(NetworkListEvent<Snapshot> e) { if(indices.Count != history.Count) Reindex(); }
        void Reindex() { indices.Clear(); for (int i = 0; i < history.Count; i++) indices[history[i].id.ToString()] = i; }
        public bool TryState(string id, out Snapshot snapshot)
        {
            if (indices.TryGetValue(id, out int i)) { snapshot = history[i]; return true; }
            snapshot = default; return false;
        }
        public static string Identity(EncounterZombie enemy)
        {
            // Hash the authored persistent root and relative child path; scene names and runtime
            // reparenting do not change identity when the streamer adopts the encounter root.
            string path = ""; var t = enemy.transform;
            while (t != null)
            {
                var marker = t.GetComponent<JourneyPersistentObject>();
                if (marker != null && !string.IsNullOrEmpty(marker.Id)) { path = marker.Id + "/" + path; break; }
                path = t.name + "#" + t.GetSiblingIndex() + "/" + path; t = t.parent;
            }
            ulong hash = 14695981039346656037UL;
            foreach (char c in path) { hash ^= c; hash *= 1099511628211UL; }
            return hash.ToString("x16");
        }
        void Update()
        {
            if (!IsSpawned) return;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + .5f;
                enemies = FindObjectsByType<EncounterZombie>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                stale.Clear();
                foreach(var z in identities.Keys) if(z == null) stale.Add(z);
                foreach(var z in stale) identities.Remove(z);
                foreach (var z in enemies)
                {
                    if (!z.CrewInitialized) continue;
                    if (!identities.ContainsKey(z))
                    {
                        string id = z.CrewId; identities[z] = id;
                        if (IsServer && TryState(id, out var prior)) z.RestoreCrewHistory(prior);
                    }
                }
            }
            if (!IsServer || Time.unscaledTime < nextPublish) return;
            nextPublish = Time.unscaledTime + .1f;
            foreach (var z in enemies)
            {
                if (z == null || !z.CrewInitialized || !identities.ContainsKey(z)) continue;
                string id = identities[z]; var s = z.CaptureCrewState(id);
                if (!indices.TryGetValue(id, out int i)) history.Add(s);
                else if (!history[i].Equals(s)) history[i] = s;
            }
        }
    }
}
