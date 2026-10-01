using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueDrive.Gameplay.Track;

namespace RogueDrive.Gameplay
{
    /// <summary>One car/session/global route. Only scenery is streamed; crossing never teleports or resets actors.</summary>
    public sealed class SeamlessJourneyStream : MonoBehaviour
    {
        public static SeamlessJourneyStream Instance { get; private set; }
        [SerializeField, Min(1800)] float preloadDistance = 2600;
        [SerializeField, Min(2800)] float unloadDistance = 3400;
        readonly Dictionary<int, Scene> loaded = new Dictionary<int, Scene>();
        readonly Dictionary<string, JourneyPersistentObject> persistent = new Dictionary<string, JourneyPersistentObject>();
        readonly HashSet<string> suspended = new HashSet<string>();
        readonly Dictionary<string, Hub.JourneyItemTemplate> worldItems=new Dictionary<string, Hub.JourneyItemTemplate>();
        readonly HashSet<string> removedItemKeys=new HashSet<string>();
        public string[] RemovedItemKeys()=>removedItemKeys.Concat(worldItems.Where(p=>p.Value==null).Select(p=>p.Key)).Distinct().ToArray();
        public void RestoreRemovedItems(IEnumerable<string> keys)
        {
            foreach(var key in keys)
            {
                if(string.IsNullOrEmpty(key))continue;removedItemKeys.Add(key);
                if(worldItems.TryGetValue(key,out var item)&&item!=null){item.gameObject.SetActive(false);Destroy(item.gameObject);}
            }
        }
        JourneyStreamCatalog catalog;
        StageRoute route;
        ArcadeCarController car;
        GameRunController run;
        Scene sessionScene;
        bool busy;
        float nextCheck, nextRetry;
        int currentIndex = -1;
        Transform stateRoot;
        public int CurrentSegmentIndex => currentIndex;
        public bool IsStreaming => busy;
        public string LastError { get; private set; }
        public JourneyStreamCatalog Catalog => catalog;
        public StageRoute Route => route;
        public string EntryScene { get; private set; }
        public int LoadedWorldCount => loaded.Count;
        public bool IsWorldLoaded(int index) => loaded.TryGetValue(index, out var scene) && scene.isLoaded;
        public string[] RemovedIds()=>persistent.Where(p=>p.Value==null).Select(p=>p.Key).ToArray();
        public void RestoreRemovedIds(IEnumerable<string> ids)
        {
            foreach(var id in ids)
            {
                if(string.IsNullOrEmpty(id))continue;
                if(persistent.TryGetValue(id,out var item)&&item!=null){item.gameObject.SetActive(false);Destroy(item.gameObject);}
                persistent[id]=null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        public static void Begin(JourneyStreamEntry entry, JourneyStreamCatalog data, int index,
            GameObject world, StageRoute localRoute, ArcadeCarController car)
        {
            if (Instance != null) return;
            var sourceScene = entry.gameObject.scene;
            // NGO reconnects through a build scene. Keep the network entry scene as the
            // session owner instead of creating an unregistered runtime scene.
            bool networkSession = Coop.CoopSession.Instance?.Busy ?? false;
            var session = networkSession ? sourceScene : SceneManager.CreateScene("JourneySession");
            var host = new GameObject("Continuous_Journey_Session");
            if (host.scene != session) SceneManager.MoveGameObjectToScene(host, session);
            var manager = host.AddComponent<SeamlessJourneyStream>();
            Instance = manager;
            manager.sessionScene = session; manager.catalog = data; manager.car = car;
            manager.EntryScene = data.segments[index].entryScene;
            manager.loaded.Add(index, sourceScene);
            manager.stateRoot = new GameObject("Persistent_World_State").transform;
            manager.stateRoot.SetParent(host.transform, false);

            var placement = data.segments[index];
            if (index > 0)
            {
                world.transform.SetPositionAndRotation(placement.position, placement.rotation);
                car.PlaceAtStart(placement.position + placement.rotation * car.transform.position,
                    placement.rotation * car.transform.rotation);
            }
            localRoute.enabled = false;
            manager.route = host.AddComponent<StageRoute>();
            manager.route.Configure(data.points);
            manager.route.CopyPacingFrom(localRoute);
            manager.route.SetPreserveAuthoredEnvironment(true);

            // Move the same runtime roots, including dormant inventory objects and all UI owners.
            // Nothing is recreated, serialized or copied at a sector boundary.
            foreach (var root in sourceScene.GetRootGameObjects())
                if (root != world && root != entry.gameObject && root.name != "Legacy_Route_Backup" && root.scene != session)
                    SceneManager.MoveGameObjectToScene(root, session);
            if (!networkSession && car.gameObject.scene != session)
            {
                car.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(car.gameObject, session);
            }
            foreach (var sun in world.GetComponentsInChildren<Light>(true))
                if (sun.type == LightType.Directional)
                { sun.transform.SetParent(null, true); if (sun.gameObject.scene != session) SceneManager.MoveGameObjectToScene(sun.gameObject, session); }
            foreach (var presentation in world.GetComponentsInChildren<JourneyPresentation>(true)) presentation.enabled = false;
            foreach (var sound in world.GetComponentsInChildren<JourneySoundscape>(true)) sound.enabled = false;
            var sky = host.AddComponent<JourneyPresentation>(); sky.ConfigureSky(data.sky);
            host.AddComponent<JourneySoundscape>().Configure(data.forest, data.field, data.rain, data.dust);
            manager.AdoptState(world);
            foreach (var finish in world.GetComponentsInChildren<StageFinishOutpost>(true))
                if (finish.StageIndex < 4) finish.enabled = false;
            manager.currentIndex = index;
            manager.StartCoroutine(manager.FinishEntry());
        }

        IEnumerator FinishEntry()
        {
            // Allow the existing entry's Awake/Start and garage hand-off to complete first.
            yield return null;
            run = FindFirstObjectByType<GameRunController>();
            run?.SetJourneyStage(currentIndex + 1);
            CreepingStormBarrier.Instance?.RebindContinuousRoute(route, catalog.segments[currentIndex].startDistance);
            SceneManager.SetActiveScene(sessionScene);
            RefreshStream();
            if(Hub.JourneyCheckpoint.HasPendingRestore && !(Coop.CoopSession.Instance?.Busy ?? false))
                yield return Hub.JourneyCheckpoint.RestorePending(this,car);
        }

        void Update()
        {
            if (Coop.CoopSession.Instance?.Busy == true && Coop.CoopVehicle.Instance != null)
                car = Coop.CoopVehicle.Instance.GetComponent<ArcadeCarController>();
            if (car == null || route == null || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .2f;
            float progress = route.ProjectDistance(car.transform.position);
            int index = catalog.SegmentAt(progress);
            if (index != currentIndex)
            {
                currentIndex = index;
                if (run == null) run = FindFirstObjectByType<GameRunController>();
                run?.SetJourneyStage(index + 1);
                Debug.Log("[JourneyStream] Entered " + catalog.segments[index].title + " at " + progress.ToString("F1") + " m.");
            }
            RefreshStream();
            RefreshStateVisibility();
        }

        void RefreshStream()
        {
            if (busy || car == null || Time.unscaledTime < nextRetry) return;
            float progress = route.ProjectDistance(car.transform.position);
            // Load before unloading: reversing while a load is pending never removes the current road.
            for (int i = 0; i < catalog.segments.Length; i++)
            {
                var part = catalog.segments[i];
                if (!IsWorldLoaded(i) && progress >= part.startDistance - preloadDistance && progress <= part.endDistance + preloadDistance)
                { StartCoroutine(LoadWorld(i)); return; }
            }
            foreach (var pair in loaded.ToArray())
            {
                // The entry scene owns network synchronization and persistent globals.
                if (pair.Value == sessionScene) continue;
                var part = catalog.segments[pair.Key];
                if (pair.Key != currentIndex && (progress > part.endDistance + unloadDistance || progress < part.startDistance - unloadDistance))
                { StartCoroutine(UnloadWorld(pair.Key, pair.Value)); return; }
            }
        }

        IEnumerator LoadWorld(int index)
        {
            busy = true;
            string path = catalog.segments[index].scenePath;
            AsyncOperation operation = null;
            var scene = SceneManager.GetSceneByPath(path);
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(path)) throw new InvalidOperationException("Scene missing from Build Settings: " + path);
                // NGO may already have supplied this world during a late-join scene sync.
                if (!scene.isLoaded) operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            }
            catch (Exception error) { LastError = error.Message; Debug.LogError("[JourneyStream] " + LastError); }
            if (operation == null && !scene.isLoaded) { busy = false; nextRetry = Time.unscaledTime + 5; yield break; }
            if (operation != null)
            {
                operation.completed += _ =>
                {
                    if (this != null) return;
                    var abandoned = SceneManager.GetSceneByPath(path);
                    if (abandoned.isLoaded) SceneManager.UnloadSceneAsync(abandoned);
                };
                yield return operation;
                scene = SceneManager.GetSceneByPath(path);
            }
            var marker = scene.GetRootGameObjects().Select(g => g.GetComponent<JourneyStreamWorld>()).FirstOrDefault(x => x != null);
            if (marker == null || marker.World == null)
            {
                LastError = "Invalid world scene: " + path; Debug.LogError("[JourneyStream] " + LastError);
                yield return SceneManager.UnloadSceneAsync(scene); busy = false; nextRetry = Time.unscaledTime + 5; yield break;
            }
            AdoptState(marker.World);
            marker.World.SetActive(true);
            loaded[index] = scene;
            Physics.SyncTransforms();
            LastError = null; busy = false;
            Debug.Log("[JourneyStream] Ready " + (index + 1));
        }

        IEnumerator UnloadWorld(int index, Scene scene)
        {
            busy = true;
            // New runtime objects (e.g. dropped loot) also belong to the session, not a disposable world.
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<Rigidbody>() != null || root.GetComponent<JourneyPersistentObject>() != null)
                    SceneManager.MoveGameObjectToScene(root, sessionScene);
            loaded.Remove(index);
            yield return SceneManager.UnloadSceneAsync(scene);
            busy = false;
            Debug.Log("[JourneyStream] Released " + (index + 1));
        }

        void AdoptState(GameObject world)
        {
            if (!(Coop.CoopSession.Instance?.Busy ?? false))
            {
                foreach(var station in world.GetComponentsInChildren<Hub.JourneyServiceStation>(true))Hub.JourneyCheckpoint.ApplyStationHistory(station);
                foreach(var cache in world.GetComponentsInChildren<Hub.RoadsideSupplyCache>(true))Hub.JourneyCheckpoint.ApplyCacheHistory(cache);
            }
            foreach(var item in world.GetComponentsInChildren<Hub.JourneyItemTemplate>(true))
            {
                if(removedItemKeys.Contains(item.ResourceKey)){item.gameObject.SetActive(false);Destroy(item.gameObject);}
                else if(!worldItems.ContainsKey(item.ResourceKey))worldItems.Add(item.ResourceKey,item);
            }
            foreach (var item in world.GetComponentsInChildren<JourneyPersistentObject>(true))
            {
                if (persistent.ContainsKey(item.Id))
                {
                    // Includes a null/destroyed instance: consumed loot must not respawn on a return trip.
                    item.gameObject.SetActive(false); Destroy(item.gameObject); continue;
                }
                persistent.Add(item.Id, item);
                item.transform.SetParent(null, true);
                if (item.gameObject.scene != sessionScene) SceneManager.MoveGameObjectToScene(item.gameObject, sessionScene);
                item.transform.SetParent(stateRoot, true);
            }
        }

        void RefreshStateVisibility()
        {
            foreach (var pair in persistent)
            {
                var item = pair.Value;
                if (item == null) continue;
                // Inventory/held objects retain their own parenting and active state.
                if (item.transform.parent != stateRoot && item.transform.parent != null) continue;
                float distance = (item.transform.position - car.transform.position).sqrMagnitude;
                if (distance > 1800 * 1800 && item.gameObject.activeSelf)
                { suspended.Add(pair.Key); item.gameObject.SetActive(false); }
                else if (distance < 1600 * 1600 && suspended.Remove(pair.Key)) item.gameObject.SetActive(true);
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
