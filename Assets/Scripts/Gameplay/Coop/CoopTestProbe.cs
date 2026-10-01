#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace RogueDrive.Gameplay.Coop
{
    /// <summary>Opt-in local IPC for two-process development smoke tests. Absent from release builds.</summary>
    public sealed class CoopTestProbe : MonoBehaviour
    {
        [Serializable] public sealed class Command
        {
            public int sequence;
            public string action;
            public int seat;
            public float throttle, steer, yaw;
            public bool brake = true;
            public int station;
        }
        [Serializable] public sealed class Snapshot
        {
            public int commandSequence;
            public bool connected, server;
            public int players, seat = -2;
            public string driver, navigator, localId, status, feedback;
            public Vector3 vehiclePosition, playerPosition;
            public float speed, vehicleYaw;
            public int cameraCount, listenerCount;
            public string scene;
            public bool hostReady, clientReady, clientConnected;
            public float countdown;
            public bool questSpawned, generator, wheel, battery, keys, gates, departed, transitioning;
            public float fuel, health;
            public int ammo, reserveAmmo;
            public bool reloading;
            public string vehicleId, playerId;
            public int vehicles, crewCanvases;
            public string stationDiagnostics;
            public bool suppliesSpawned, wrench, stop1, stop2, stop3, downed;
            public int sharedAmmo, medkits, bolts, batteries;
            public float hull, searchProgress;
            public float stormFront, stormDistance;
            public string marker, cacheState;
            public int activeCaches;
            public bool supplyHudVisible;
            public string enemyId;
            public Vector3 enemyPosition;
            public float enemyHealth;
            public int enemyState, enemyHits, activeEnemies;
            public bool enemyCollider, enemyAgent, enemyHistory;
            public float enemyAge, enemyVisualAngle;
        }
        private string prefix;
        private int lastSequence;
        private int selectedCache;
        private float next;
        private Command command = new Command();
        private EncounterZombie combatEnemy;
        private string combatId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-coop-probe") new GameObject("Coop validation probe").AddComponent<CoopTestProbe>().Configure(args[i + 1]);
        }
        public void Configure(string path) { prefix = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(prefix)); DontDestroyOnLoad(gameObject); }
        private void CaptureCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;
            // Hidden development windows may not present a frame for ScreenCapture.
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var overlays = Array.FindAll(FindObjectsByType<Canvas>(FindObjectsSortMode.None),
                c => c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay);
            var previousCameras = Array.ConvertAll(overlays, c => c.worldCamera);
            var previousPlanes = Array.ConvertAll(overlays, c => c.planeDistance);
            Texture2D image = null;
            try
            {
                foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(prefix + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                for (int i = 0; i < overlays.Length; i++)
                { overlays[i].renderMode = RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera = previousCameras[i]; overlays[i].planeDistance = previousPlanes[i]; }
                RenderTexture.ReleaseTemporary(target); if (image != null) Destroy(image);
            }
        }
        private void Update()
        {
            if (prefix == null || Time.unscaledTime < next) return;
            if (CoopSession.Instance == null)
            {
                var prefab = Resources.Load<GameObject>("CrewSession");
                if (prefab != null) Instantiate(prefab);
                return;
            }
            next = Time.unscaledTime + .1f;
            var session = CoopSession.Instance;
            try
            {
                if (File.Exists(prefix + ".command.json"))
                {
                    var incoming = JsonUtility.FromJson<Command>(File.ReadAllText(prefix + ".command.json"));
                    if (incoming != null && incoming.sequence != lastSequence)
                    {
                        lastSequence = incoming.sequence; command = incoming;
                        if (command.action == "cache" || command.action == "place_cache" || command.action == "inspect") selectedCache = command.station;
                        switch (command.action)
                        {
                            case "host": session.Host(); break;
                            case "join": session.Join("127.0.0.1"); break;
                            case "leave": session.Leave(); break;
                            case "ready": session.ToggleReady(); break;
                            case "place_station": PlaceAtStation(command.station, command.seat); break;
                            case "station": UseStation(command.station); break;
                            case "place_car": PlaceByCar(command.seat); break;
                            case "damage": if (session.Manager.IsServer) CoopPlayer.Local?.ServerTakeDamage(15f); break;
                            case "fire": CoopPlayer.Local?.TestFire(); break;
                            case "reload": CoopPlayer.Local?.TestReload(); break;
                            case "seat": CoopPlayer.Local?.RequestSeat(command.seat); break;
                            case "menu": session.SetMenu(true); break;
                            case "capture": CaptureCamera(); break;
                            case "cache": CoopPlayer.Local?.RequestCache(command.station == 99 ? "unknown/cache" : "route0/supply/" + command.station); break;
                            case "crew_action": CoopPlayer.Local?.RequestCrewAction(command.station); break;
                            case "place_cache": PlaceAtCache(command.station, command.seat, command.throttle); break;
                            case "fixture": SetSupplyFixture(command.station, command.seat); break;
                            case "combat": CombatFixture(command.station); break;
                            case "aim_fire":
                                var aim = FindCombatEnemy();
                                if (aim != null) CoopPlayer.Local?.TestAimFire(aim.transform.position + Vector3.up * (command.station == 1 ? 1.6f : .9f), command.station == 2);
                                break;
                            case "local_enemy_damage": FindCombatEnemy()?.TakeDamage(100); break;
                            case "burst_fire":
                                var burstTarget = FindCombatEnemy();
                                if (burstTarget != null) for(int j=0;j<4;j++) CoopPlayer.Local?.TestAimFire(burstTarget.transform.position + Vector3.up * .9f);
                                break;
                        }
                    }
                }
                var player = CoopPlayer.Local;
                if (player != null)
                {
                    player.TestControl = true;
                    player.SendInput(new Vector2(command.steer, command.throttle), command.brake, false, command.yaw);
                }
                var car = CoopVehicle.Instance;
                var quest = CoopQuestManager.Instance;
                var supplies = CoopSupplies.Instance;
                var snapshot = new Snapshot
                {
                    commandSequence = lastSequence,
                    questSpawned = quest != null && quest.IsSpawned,
                    generator = quest != null && quest.QuestGeneratorRunning.Value,
                    wheel = quest != null && quest.QuestWheelMounted.Value,
                    battery = quest != null && quest.QuestBatteryMounted.Value,
                    keys = quest != null && quest.QuestKeyCollected.Value,
                    gates = quest != null && quest.QuestGatesOpened.Value,
                    departed = quest != null && quest.QuestBunkerDeparted.Value,
                    fuel = quest != null ? quest.QuestFuelLiters.Value : -1f,
                    health = player != null ? player.Health.Value : -1f,
                    ammo = player != null ? player.CurrentAmmo.Value : -1,
                    reserveAmmo = player != null ? player.ReserveAmmo.Value : -1,
                    reloading = player != null && player.Reloading.Value,
                    transitioning = car != null && car.Transitioning.Value,
                    vehicleId = car != null ? car.NetworkObjectId.ToString() : "",
                    playerId = player != null ? player.NetworkObjectId.ToString() : "",
                    vehicles = FindObjectsByType<CoopVehicle>(FindObjectsSortMode.None).Length,
                    stationDiagnostics = session.Manager.IsServer ? StationDiagnostics(command.station) : "",
                    connected = session.Manager.IsConnectedClient, server = session.Manager.IsServer,
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    hostReady = session.HostReady, clientReady = session.ClientReady,
                    clientConnected = session.ClientConnected, countdown = session.LaunchCountdown,
                    players = UnityEngine.Object.FindObjectsByType<CoopPlayer>(FindObjectsSortMode.None).Length,
                    seat = player != null ? player.Seat.Value : -2,
                    localId = session.Manager.LocalClientId.ToString(), driver = car != null ? car.Driver.Value.ToString() : "",
                    navigator = car != null ? car.Navigator.Value.ToString() : "", status = session.Status,
                    feedback = player != null ? player.Feedback : "", vehiclePosition = car != null ? car.transform.position : Vector3.zero,
                    playerPosition = player != null ? player.transform.position : Vector3.zero,
                    speed = car != null ? car.Speed.Value : 0,
                    vehicleYaw = car != null ? Mathf.DeltaAngle(0, car.transform.eulerAngles.y) : 0,
                    cameraCount = Camera.allCamerasCount
                };
                snapshot.suppliesSpawned = supplies != null && supplies.IsSpawned;
                var enemy = FindCombatEnemy();
                if (enemy != null)
                {
                    snapshot.enemyId = enemy.CrewId; snapshot.enemyPosition = enemy.transform.position;
                    snapshot.enemyHealth = enemy.CurrentHealth; snapshot.enemyState = (int)enemy.State;
                    snapshot.enemyHits = enemy.SuccessfulHits;
                    snapshot.enemyCollider = enemy.GetComponent<CapsuleCollider>().enabled;
                    snapshot.enemyAgent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled;
                    snapshot.enemyHistory = CoopEnemies.Instance != null && CoopEnemies.Instance.TryState(enemy.CrewId, out _);
                    snapshot.enemyAge = enemy.CaptureCrewState(enemy.CrewId).age;
                    var model = enemy.transform.Find("VisualModel");
                    if(model != null) snapshot.enemyVisualAngle = Mathf.Abs(Mathf.DeltaAngle(0,model.localEulerAngles.x));
                }
                foreach(var z in FindObjectsByType<EncounterZombie>(FindObjectsSortMode.None)) if (!z.IsDead) snapshot.activeEnemies++;
                snapshot.sharedAmmo = supplies != null ? supplies.Ammo.Value : -1;
                snapshot.medkits = supplies != null ? supplies.Medkits.Value : -1;
                snapshot.bolts = supplies != null ? supplies.Bolts.Value : -1;
                snapshot.batteries = supplies != null ? supplies.Batteries.Value : -1;
                snapshot.wrench = supplies != null && supplies.Wrench.Value;
                snapshot.marker = supplies != null ? supplies.MarkedCache.Value.ToString() : "";
                snapshot.hull = car != null ? car.Hull.Value : -1;
                snapshot.stormFront = car != null ? car.StormFront.Value : -1;
                snapshot.stormDistance = car != null ? car.StormDistance.Value : -1;
                snapshot.stop1 = quest != null && quest.QuestStop1FuelScavenged.Value;
                snapshot.stop2 = quest != null && quest.QuestStop2CampExplored.Value;
                snapshot.stop3 = quest != null && quest.QuestStop3ServiceVisited.Value;
                snapshot.downed = player != null && player.IsDowned.Value;
                var caches = FindObjectsByType<RogueDrive.Gameplay.Hub.RoadsideSupplyCache>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in caches)
                {
                    if (c.isActiveAndEnabled) snapshot.activeCaches++;
                    if (c.Id != "route0/supply/" + selectedCache) continue;
                    snapshot.searchProgress = c.IsSearching ? c.Progress : 0;
                    snapshot.cacheState = JsonUtility.ToJson(supplies != null ? supplies.State(c.Id) : c.CaptureState());
                }
                foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (listener.enabled) snapshot.listenerCount++;
                foreach (var ui in FindObjectsByType<CoopSessionUI>(FindObjectsSortMode.None))
                {
                    if (ui.GetComponent<Canvas>() != null && ui.GetComponent<Canvas>().enabled) snapshot.crewCanvases++;
                    foreach (var label in ui.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                        if (label.name == "Shared supplies" && label.isActiveAndEnabled && label.text.Contains("ОБЩИЕ ПРИПАСЫ")) snapshot.supplyHudVisible = true;
                }
                File.WriteAllText(prefix + ".snapshot.tmp", JsonUtility.ToJson(snapshot, true));
                File.Copy(prefix + ".snapshot.tmp", prefix + ".snapshot.json", true);
            }
            catch (IOException) { /* A writer may be replacing a command or snapshot. Retry next tick. */ }
        }

        private CoopPlayer Target(int remote)
        {
            if (!CoopSession.Instance.Manager.IsServer) return null;
            foreach (var p in FindObjectsByType<CoopPlayer>(FindObjectsSortMode.None))
                if ((remote == 0) == p.IsOwner) return p;
            return null;
        }
        private EncounterZombie FindCombatEnemy()
        {
            if (combatEnemy != null) return combatEnemy;
            var all = FindObjectsByType<EncounterZombie>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(all, (a,b) => string.CompareOrdinal(a.CrewId,b.CrewId));
            foreach (var z in all)
                if ((combatId != null && z.CrewId == combatId) ||
                    (combatId == null && z.transform.parent != null && z.transform.parent.name == "Encounter_LastGasStation" &&
                     z.GetComponentInParent<JourneyPersistentObject>() != null))
                { combatId = z.CrewId; combatEnemy = z; return z; }
            return null;
        }
        private void CombatFixture(int fixture)
        {
            if (!CoopSession.Instance.Manager.IsServer) return;
            var z = FindCombatEnemy(); var car = CoopVehicle.Instance; var p = Target(1);
            if (z == null || car == null || p == null) return;
            if (fixture == 0)
            {
                Vector3 home = z.CrewInitialized ? z.HomePosition : z.transform.position;
                car.ServerFreeze(true); car.ServerPlaceAt(home + Vector3.right * 12 + Vector3.up, Quaternion.identity);
                car.ReleaseDisconnected(p.OwnerClientId);
                p.Health.Value = 100; p.IsDowned.Value = false; p.CurrentAmmo.Value = 7;
                p.ServerSetSeat(-1, home + Vector3.back * 4 + Vector3.up * .1f);
                foreach(var other in FindObjectsByType<EncounterZombie>(FindObjectsInactive.Include, FindObjectsSortMode.None)) other.enabled = false;
                z.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                z.TestHold = true; z.enabled = true;
            }
            if (fixture == 1) { z.TestHold = false; z.enabled = true; z.AlertToSound(p.transform.position); }
            if (fixture == 2)
            {
                var feet = z.transform.position + z.transform.forward * .9f + Vector3.up * .1f;
                car.ReleaseDisconnected(p.OwnerClientId); p.ServerSetSeat(-1, feet);
                z.TestHold = false; z.enabled = true; z.AlertToSound(p.transform.position);
            }
            if (fixture == 3) p.ServerSetSeat(-1, z.transform.position + Vector3.back * 8 + Vector3.up * .1f);
            if (fixture == 4)
            {
                car.Hull.Value = 100;
                car.ServerPlaceAt(z.transform.position + Vector3.forward * 1.5f + Vector3.up * .65f, Quaternion.identity);
                p.ServerSetSeat(-1, z.transform.position + Vector3.back * 30 + Vector3.up * .1f);
                z.TestHold = false; z.enabled = true; z.AlertToSound(car.transform.position);
            }
        }
        private void SetSupplyFixture(int fixture, int remote)
        {
            if (!CoopSession.Instance.Manager.IsServer) return;
            var p = Target(remote); var s = CoopSupplies.Instance; var car = CoopVehicle.Instance;
            if (p == null || s == null || car == null) return;
            switch (fixture)
            {
                case 0: // Deterministic capacity boundaries; no solo save data touched.
                    s.Ammo.Value = 168; s.Medkits.Value = 8; s.Bolts.Value = 32; s.Batteries.Value = 4; s.Wrench.Value = true; break;
                case 1: s.Ammo.Value = 0; s.Medkits.Value = 0; s.Bolts.Value = 0; s.Batteries.Value = 0; s.Wrench.Value = false; break;
                case 2: p.ReserveAmmo.Value = 0; p.CurrentAmmo.Value = 0; break;
                case 3: p.Health.Value = 40; p.IsDowned.Value = false; car.Hull.Value = 50; break;
                case 4: p.ServerTakeDamage(100); break;
                case 5: car.ServerFreeze(true); break;
                case 6: car.ServerFreeze(false); break;
                case 7: car.Hull.Value = 0; break;
                case 8:
                    var body = car.GetComponent<Rigidbody>(); if (!body.isKinematic) body.linearVelocity = Vector3.forward * 8;
                    break;
            }
        }
        private void PlaceAtCache(int index, int remote, float distance)
        {
            var p = Target(remote); var car = CoopVehicle.Instance;
            if (p == null || car == null) return;
            var caches = FindObjectsByType<RogueDrive.Gameplay.Hub.RoadsideSupplyCache>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in caches)
            {
                if (c.Id != "route0/supply/" + index) continue;
                car.ServerFreeze(true);
                car.ServerPlaceAt(c.transform.position + Vector3.right * 7 + Vector3.up * .65f, Quaternion.identity);
                var feet = c.transform.position + Vector3.back * (distance > 0 ? distance : 1.8f) + Vector3.up * .1f;
                car.ReleaseDisconnected(p.OwnerClientId); p.ServerSetSeat(-1, feet);
                foreach (var z in FindObjectsByType<EncounterZombie>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    z.enabled = false; // Isolates network inventory from combat for this opt-in fixture.
                return;
            }
        }

        private string StationDiagnostics(int action)
        {
            var p = Target(1);
            if (p == null) return "no remote player";
            var result = new System.Text.StringBuilder();
            foreach (var s in FindObjectsByType<CoopBunkerInteractable>(FindObjectsSortMode.None))
            {
                if ((int)s.ActionType != action) continue;
                var c = s.GetComponentInChildren<Collider>();
                var eye = p.transform.position + Vector3.up * 1.65f;
                var target = c != null ? CoopBunkerInteractable.InteractionPoint(c, eye) : s.transform.position;
                var blocked = Physics.Linecast(eye, target, out var hit, ~0, QueryTriggerInteraction.Ignore);
                result.Append(s.name).Append(" d=").Append(Vector3.Distance(eye,target)).Append(" hit=").Append(blocked?hit.collider.name:"none").Append(";");
                foreach(var h in Physics.RaycastAll(eye,(target-eye).normalized,Vector3.Distance(eye,target),~0,QueryTriggerInteraction.Ignore))
                    result.Append("[").Append(h.collider.name).Append(" player=").Append(h.collider.GetComponentInParent<CoopPlayer>()?.OwnerClientId).Append(" station=").Append(h.collider.GetComponentInParent<CoopBunkerInteractable>()?.name).Append("]");
            }
            return result.ToString();
        }

        private void PlaceByCar(int remote)
        {
            var p = Target(remote);
            var car = CoopVehicle.Instance;
            if (p != null && car != null) p.ServerSetSeat(-1, car.transform.position + Vector3.right * (remote == 0 ? -2.5f : 2.5f));
        }

        private void PlaceAtStation(int action, int remote)
        {
            var p = Target(remote);
            if (p == null) return;
            int skip = Mathf.Max(0, Mathf.RoundToInt(command.yaw));
            foreach (var station in FindObjectsByType<CoopBunkerInteractable>(FindObjectsSortMode.None))
            {
                if ((int)station.ActionType != action || !station.CanInteract()) continue;
                var collider = station.GetComponentInChildren<Collider>();
                if (collider == null) continue;
                var center = collider.bounds.center;
                foreach (var side in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                    (Vector3.forward+Vector3.left).normalized, (Vector3.forward+Vector3.right).normalized,
                    (Vector3.back+Vector3.left).normalized, (Vector3.back+Vector3.right).normalized })
                {
                    var feet = center + side * 1.8f;
                    if (Physics.Raycast(feet + Vector3.up * 3, Vector3.down, out var ground, 7f, ~0, QueryTriggerInteraction.Ignore)) feet.y = ground.point.y + .1f;
                    var eye = feet + Vector3.up * 1.65f;
                    if (!Physics.Linecast(eye, CoopBunkerInteractable.InteractionPoint(collider, eye), out var hit, ~0, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<CoopBunkerInteractable>() == station)
                    {
                        if (skip-- > 0) continue;
                        p.ServerSetSeat(-1, feet); return;
                    }
                }
            }
        }

        private void UseStation(int action)
        {
            var q = CoopQuestManager.Instance;
            if (q == null) return;
            switch ((CoopBunkerActionType)action)
            {
                case CoopBunkerActionType.Generator: q.StartGeneratorServerRpc(); break;
                case CoopBunkerActionType.WheelMount: q.MountWheelServerRpc(); break;
                case CoopBunkerActionType.BatteryMount: q.MountBatteryServerRpc(); break;
                case CoopBunkerActionType.FuelRefill: q.AddFuelServerRpc(10f); break;
                case CoopBunkerActionType.CollectKeys: q.CollectKeyServerRpc(); break;
                case CoopBunkerActionType.OpenGates: q.OpenGatesServerRpc(); break;
            }
        }
    }
}
#endif
