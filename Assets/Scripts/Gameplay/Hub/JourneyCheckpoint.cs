using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using RogueDrive.Gameplay.Track;

namespace RogueDrive.Gameplay.Hub
{
    public static class JourneyCheckpoint
    {
        const string ArrivalKey="JourneyStation.Arrival.v1",PreparedKey="JourneyStation.Prepared.v1";
        static string arrival,prepared;
        static SaveData pending;
        static bool pendingDeparture;
        static SaveData restoredHistory;
        public static bool OwnsCurrentRun {get;private set;}
        public static bool HasPendingRestore=>pending!=null;
        public static bool Restoring {get;private set;}
        public static string LastError {get;private set;}
        public static bool CanContinue=>Read(true)!=null||Read(false)!=null;
        [Serializable] public sealed class SaveData
        {
            public int version=1,station,coins;
            public bool aidUsed;
            public Vector3 position;
            public Quaternion rotation;
            public float health;
            public string[] removed,removedItems;
            public GarageDepartureCheckpoint.Snapshot inventory;
            public StationState[] stations;
            public RoadsideSupplyCache.State[] caches;
        }
        [Serializable] public sealed class StationState{public int number;public bool aidUsed,departed;}
        public static void ApplyStationHistory(JourneyServiceStation station)
        {
            var saved=restoredHistory?.stations?.FirstOrDefault(s=>s.number==station.Number);
            if(saved!=null)station.RestoreVisit(saved.departed,saved.aidUsed);
        }
        public static void ApplyCacheHistory(RoadsideSupplyCache cache)
        {
            var saved=restoredHistory?.caches?.FirstOrDefault(s=>s!=null&&s.id==cache.Id);
            if(saved!=null)cache.RestoreState(saved);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){arrival=prepared=null;pending=null;restoredHistory=null;Restoring=false;OwnsCurrentRun=false;LastError=null;}
        public static void Clear(){Reset();PlayerPrefs.DeleteKey(ArrivalKey);PlayerPrefs.DeleteKey(PreparedKey);PlayerPrefs.Save();}
        static SaveData Read(bool depart)
        {
            if(depart&&string.IsNullOrEmpty(prepared)&&!string.IsNullOrEmpty(arrival))return null;
            var json=depart?prepared:arrival;if(string.IsNullOrEmpty(json))json=PlayerPrefs.GetString(depart?PreparedKey:ArrivalKey,"");
            try {var data=string.IsNullOrEmpty(json)?null:JsonUtility.FromJson<SaveData>(json);return IsValidSave(data)?data:null;}catch{return null;}
        }
        static bool IsValidSave(SaveData data)
        {
            return data != null && data.version == 1 && data.station >= 1 && data.station <= 3
                && data.coins >= 0 && !float.IsNaN(data.health) && !float.IsInfinity(data.health) && data.health > 0
                && Finite(data.position.x) && Finite(data.position.y) && Finite(data.position.z)
                && Finite(data.rotation.x) && Finite(data.rotation.y) && Finite(data.rotation.z) && Finite(data.rotation.w)
                && Quaternion.Dot(data.rotation, data.rotation) > .0001f
                && GarageDepartureCheckpoint.IsValidSnapshot(data.inventory) && data.inventory.journey
                && (data.caches==null || data.caches.Length<=128 && data.caches.All(RoadsideSupplyCache.IsValidState)
                    && data.caches.Select(c=>c.id).Distinct().Count()==data.caches.Length);
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Capture(JourneyServiceStation station,bool depart)
        {
            if(Restoring)return false;
            var car=VehicleModularState.Instance;var w=VehicleWorkshop.For(car);
            if(station==null||car==null||w==null||w.Busy||SeamlessJourneyStream.Instance==null)return false;
            try
            {
                var held=new ServiceItemAddress(2,0).Read().WorldObject;
                if(held!=null&&held.GetComponent<GarageCheckpointItem>()==null)
                    held.AddComponent<GarageCheckpointItem>().id="cargo_held_"+Guid.NewGuid().ToString("N");
                PlayerHandsInventory.Instance?.DropItem();
                var inventory=GarageDepartureCheckpoint.Capture();
                var run=car.GetComponent<ArcadeCarController>().Run;
                var data=new SaveData{station=station.Number,aidUsed=station.AidUsed,inventory=inventory,
                    position=car.transform.position,rotation=car.transform.rotation,health=run.Health,coins=run.CoinsCollected,
                    removed=SeamlessJourneyStream.Instance.RemovedIds().Concat(inventory.items.Select(i=>i.journeyId).Where(i=>!string.IsNullOrEmpty(i))).Distinct().ToArray(),
                    removedItems=SeamlessJourneyStream.Instance.RemovedItemKeys().Concat(inventory.items.Select(i=>i.template).Where(i=>!string.IsNullOrEmpty(i))).Distinct().ToArray()};
                data.stations=UnityEngine.Object.FindObjectsByType<JourneyServiceStation>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(s=>s.Visited||s==station).Select(s=>new StationState{number=s.Number,aidUsed=s.AidUsed,departed=s.Departed||(s==station&&depart)}).ToArray();
                data.caches=UnityEngine.Object.FindObjectsByType<RoadsideSupplyCache>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Select(c=>c.CaptureState()).ToArray();
                var json=JsonUtility.ToJson(data);if(depart)prepared=json;else{arrival=json;prepared=null;}
                OwnsCurrentRun=true;
                if(!(GaragePrologueManager.Instance?.IsPreviewRun??Application.isEditor))
                {
                    PlayerPrefs.SetString(depart?PreparedKey:ArrivalKey,json);if(!depart)PlayerPrefs.DeleteKey(PreparedKey);PlayerPrefs.Save();
                }
                return true;
            }
            catch(Exception e){Debug.LogError("[JourneyCheckpoint] "+e);return false;}
        }
        public static bool CaptureArrival(JourneyServiceStation station)=>Capture(station,false);
        public static bool CapturePrepared(JourneyServiceStation station)=>Capture(station,true);
        public static bool RequestRestore(bool depart)
        {
            var data=Read(depart)??(depart?Read(false):null);if(data==null)return false;
            pending=data;pendingDeparture=depart&&Read(true)!=null;OwnsCurrentRun=true;LastError=null;Time.timeScale=1;
            SceneManager.LoadScene("Stage1_Outskirts");return true;
        }
        public static IEnumerator RestorePending(SeamlessJourneyStream stream,ArcadeCarController car)
        {
            if(pending==null)yield break;
            Restoring=true;var data=pending;bool depart=pendingDeparture;
            var previousHistory=restoredHistory;
            var rb=car!=null?car.GetComponent<Rigidbody>():null;
            var driver=car!=null?car.GetComponent<GarageDriveOutVehicle>():null;
            bool wasKinematic=rb!=null&&rb.isKinematic,wasCinematic=driver!=null&&driver.CinematicControl;
            Vector3 startPosition=car!=null?car.transform.position:Vector3.zero;
            Quaternion startRotation=car!=null?car.transform.rotation:Quaternion.identity;
            bool succeeded=false;
            try
            {
                if(stream==null||car==null||rb==null||!IsValidSave(data))yield break;
                restoredHistory=data;
                if(driver!=null)driver.CinematicControl=true;
                rb.isKinematic=true;car.PlaceAtStart(data.position,data.rotation);rb.isKinematic=true;
                stream.RestoreRemovedIds(data.removed??Array.Empty<string>());
                stream.RestoreRemovedItems(data.removedItems??Array.Empty<string>());
                int segment=stream.Catalog.SegmentAt(stream.Route.ProjectDistance(data.position));
                float deadline=Time.realtimeSinceStartup+90;
                while(!stream.IsWorldLoaded(segment)&&Time.realtimeSinceStartup<deadline)yield return null;
                if(!stream.IsWorldLoaded(segment))yield break;
                yield return null;
                var station=UnityEngine.Object.FindObjectsByType<JourneyServiceStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(s=>s.Number==data.station);
                if(station==null)yield break;
                GarageDepartureCheckpoint.Apply(data.inventory);
                car.Run.RestoreJourneyResources(data.health,data.coins);
                foreach(var visited in UnityEngine.Object.FindObjectsByType<JourneyServiceStation>(FindObjectsInactive.Include,FindObjectsSortMode.None))ApplyStationHistory(visited);
                foreach(var cache in UnityEngine.Object.FindObjectsByType<RoadsideSupplyCache>(FindObjectsInactive.Include,FindObjectsSortMode.None))ApplyCacheHistory(cache);
                station.RestoreVisit(depart,data.aidUsed);
                CreepingStormBarrier.Instance?.BeginNextWave(stream.Route.ProjectDistance(data.position));
                succeeded=true;LastError=null;
            }
            finally
            {
                pending=null;Restoring=false;
                if(!succeeded)
                {
                    restoredHistory=previousHistory;
                    if(car!=null)car.PlaceAtStart(startPosition,startRotation);
                    LastError="Не удалось восстановить контрольную точку. Вернитесь в меню и повторите загрузку.";
                    Debug.LogWarning("[JourneyCheckpoint] "+LastError);
                    GarageInteractionUI.Instance?.ShowNotification(LastError,10);
                }
                if(rb!=null)rb.isKinematic=succeeded?false:wasKinematic;
                if(driver!=null)driver.CinematicControl=succeeded?false:wasCinematic;
            }
            if(!succeeded)yield break;
            if(!depart)GarageInteractionUI.Instance?.ShowNotification("Восстановлено прибытие на СТО: покупки и подготовку можно выбрать заново.",7);
        }
    }
}
