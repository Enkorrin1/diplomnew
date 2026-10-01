using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

public static class WorkshopValidation
{
    static readonly List<string> results=new List<string>();
    static void Check(bool ok,string name)
    {
        results.Add((ok?"PASS ":"FAIL ")+name);
        Directory.CreateDirectory("Logs/Workshop");File.WriteAllLines("Logs/Workshop/validation.txt",results);
        if(!ok)throw new InvalidOperationException(name);
    }
    static VehicleWorkshop Require()
    {
        if(!Application.isPlaying||VehicleModularState.Instance==null)throw new InvalidOperationException("Enter Play Mode in GarageScene first.");
        return VehicleWorkshop.For(VehicleModularState.Instance);
    }
    static void Funds(VehicleWorkshop w,int coins)
    {
        var save=JsonUtility.FromJson<VehicleWorkshop.SaveData>(w.Capture());save.coins=coins;w.Restore(JsonUtility.ToJson(save));
    }
    [MenuItem("RogueDrive/Workshop/Preview in current Play Mode")]
    public static void PreparePreview()
    {
        var w=Require();var car=VehicleModularState.Instance;
        var point=GameObject.Find("WorkshopPreview_Service")??new GameObject("WorkshopPreview_Service");point.transform.position=car.transform.position;
        var zone=point.GetComponent<WorkshopServiceZone>()??point.AddComponent<WorkshopServiceZone>();zone.Configure("СТО · ПРОВЕРКА В ГАРАЖЕ",true,WorkshopEquipment.All,3);
        Funds(w,1200);JsonUtility.FromJsonOverwrite("{\"engineStopped\":true}",w);
        var trunk=VehicleCargoTrunk.Instance;
        foreach(var id in new[]{"engine_torque","radiator_heavy","turbo_1","springs_offroad","rack_1"})
        {
            int free=Enumerable.Range(0,trunk.MaxSlots).Where(i=>trunk.GetSlot(i).IsEmpty).DefaultIfEmpty(-1).First();
            if(free>=0)trunk.SetGridSlot(free,WorkshopPartItem.Create(WorkshopCatalog.New(id)));
        }
        var ui=VehicleDashboardPanelsUI.Instance??new GameObject("VehicleDashboardPanelsUI").AddComponent<VehicleDashboardPanelsUI>();ui.ShowEnginePanel();
        ui.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="WorkshopSource_1").onClick.Invoke();
    }
    [MenuItem("RogueDrive/Workshop/Run transaction checks (Play Mode)")]
    public static void Run()
    {
        results.Clear();var w=Require();var car=VehicleModularState.Instance;var trunk=VehicleCargoTrunk.Instance;
        var before=w.Capture();var carBefore=JsonUtility.ToJson(car);bool wasEnabled=car.enabled;car.enabled=false;
        var slots=Enumerable.Range(0,trunk.MaxSlots).Select(trunk.GetSlot).ToArray();
        var oldObjects=new HashSet<int>(Object.FindObjectsByType<WorkshopPartItem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(p=>p.GetInstanceID()));
        var zoneObject=new GameObject("WorkshopValidationZone");zoneObject.transform.position=car.transform.position;
        var zone=zoneObject.AddComponent<WorkshopServiceZone>();zone.Configure("TEST",true,WorkshopEquipment.All,3);
        var rb=car.GetComponent<Rigidbody>();bool wasKinematic=rb!=null&&rb.isKinematic;
        if(rb!=null){if(!rb.isKinematic)rb.linearVelocity=Vector3.zero;rb.isKinematic=true;}
        string message;
        try
        {
            for(int i=0;i<trunk.MaxSlots;i++)trunk.SetGridSlot(i,PocketSlotData.Empty);
            JsonUtility.FromJsonOverwrite("{\"engineStopped\":true}",w);JsonUtility.FromJsonOverwrite("{\"engineTemperature\":85}",car);Funds(w,1000);
            Check(WorkshopCatalog.All.Count==21&&WorkshopCatalog.All.Select(d=>d.id).Distinct().Count()==21,"21 unique catalog definitions");
            var source=new ServiceItemAddress(1,0);
            trunk.SetGridSlot(0,WorkshopPartItem.Create(WorkshopCatalog.New("engine_torque")));
            Check(!w.BeginInstall(source,WorkshopSlot.Radiator,out message),"Wrong slot cannot consume a part or coins");
            Funds(w,0);Check(!w.BeginInstall(source,WorkshopSlot.Engine,out message)&&!source.Read().IsEmpty,"Insufficient funds preserve inventory");Funds(w,1000);
            zone.Configure("ROADSIDE",false,WorkshopEquipment.All,3);
            Check(!w.BeginInstall(source,WorkshopSlot.Engine,out message),"Roadside engine swap denied even with a crane");
            zone.Configure("SAFE",true,WorkshopEquipment.Tools,3);
            Check(!w.BeginInstall(source,WorkshopSlot.Engine,out message)&&message.Contains("кран"),"Missing equipment explained");
            zone.Configure("SAFE",true,WorkshopEquipment.All,3);
            JsonUtility.FromJsonOverwrite("{\"engineTemperature\":110}",car);
            Check(!w.BeginInstall(source,WorkshopSlot.Engine,out message),"Hot engine rejected");JsonUtility.FromJsonOverwrite("{\"engineTemperature\":85}",car);
            Check(w.BeginInstall(source,WorkshopSlot.Engine,out message)&&w.DriveBlocked,"Paid timed installation starts and blocks driving");
            w.Advance(1);Check(w.Coins==1000&&w.Installed(WorkshopSlot.Engine).definitionId=="engine_stock","No early debit or early replacement");
            w.CancelJob();Check(w.Coins==1000&&!source.Read().IsEmpty&&!w.Busy,"Cancellation retains coins and source");
            Check(w.BeginInstall(source,WorkshopSlot.Engine,out message),"Restart after cancel");w.Advance(100);
            Check(w.Coins==910&&w.Installed(WorkshopSlot.Engine).definitionId=="engine_torque"&&WorkshopPartItem.Read(source.Read()).definitionId=="engine_stock","One debit; old engine returned");
            Check(w.Stats.power>1&&w.Stats.fuel>1&&w.Stats.mass>0,"Engine changes power, fuel and mass");
            var engineId=w.Installed(WorkshopSlot.Engine).instanceId;
            trunk.SetGridSlot(1,WorkshopPartItem.Create(WorkshopCatalog.New("turbo_1")));
            Check(w.BeginInstall(new ServiceItemAddress(1,1),WorkshopSlot.Turbo,out message),"Turbo installation starts");w.Advance(100);
            Check(w.Installed(WorkshopSlot.Engine).Internals.Count==1,"Turbo belongs to the engine assembly");
            Check(w.BeginInstall(source,WorkshopSlot.Engine,out message),"Reinstall original engine");w.Advance(100);
            var removed=WorkshopPartItem.Read(source.Read());
            Check(removed.instanceId==engineId&&removed.Internals.Count==1&&w.Installed(WorkshopSlot.Turbo)==null,"Engine carries its internal parts when removed");
            string save=w.Capture();Funds(w,0);w.Restore(save);Check(w.Coins>0&&w.Installed(WorkshopSlot.Engine).definitionId=="engine_stock","Save round trip restores coins and equipped parts");
            int money=w.Coins;int sale=w.SellPrice(source.Read());Check(w.Sell(source,out message)&&w.Coins==money+sale&&source.Read().IsEmpty,"Sale pays once and includes engine internals");
            Check(!w.Sell(source,out message)&&w.Coins==money+sale,"Repeated sale cannot duplicate money");
            Funds(w,1000);Check(w.Buy("radiator_heavy",out message)&&w.Coins==830,"Purchase delivers physical cargo at catalog price");
            zone.Configure("ROADSIDE",false,WorkshopEquipment.Tools,2);
            Check(w.BeginInstall(source,WorkshopSlot.Radiator,out message)&&!w.Sheltered,"Roadside radiator job allowed without shelter");
            var savedSource=source.Read();source.Write(PocketSlotData.Empty);w.Advance(100);
            Check(!w.Busy&&w.Coins==830,"Moved source cancels job without debit");source.Write(savedSource);
            var damaged=WorkshopPartItem.Read(source.Read());damaged.condition=.2f;
            Check(!w.BeginInstall(source,WorkshopSlot.Radiator,out message),"Broken part requires repair");
            Check(!w.BeginRepair(source,out message),"Full repair unavailable roadside");
            zone.Configure("SAFE",true,WorkshopEquipment.All,3);
            int cost=w.RepairPrice(damaged);Check(w.BeginRepair(source,out message),"Paid repair starts");w.Advance(100);
            Check(WorkshopPartItem.Read(source.Read()).condition==1&&w.Coins==830-cost,"Repair restores the specific item once");
            for(int i=0;i<trunk.MaxSlots;i++)trunk.SetGridSlot(i,new PocketSlotData{id="test_loot_"+i,displayName="Лут",count=1});
            money=w.Coins;Check(!w.Buy("filter_1",out message)&&w.Coins==money,"Full cargo blocks buying without debit");
            trunk.SetGridSlot(0,WorkshopPartItem.Create(WorkshopCatalog.New("radiator_heavy")));
            Check(w.BeginInstall(source,WorkshopSlot.Radiator,out message),"Full cargo allows a same-cell replacement");w.Advance(100);
            Check(!source.Read().IsEmpty&&WorkshopPartItem.Read(source.Read()).definitionId=="radiator_stock","Full cargo retains removed radiator");
            Check(Time.timeScale==1,"Service transactions never pause world time");
        }
        finally
        {
            w.CancelJob();for(int i=0;i<trunk.MaxSlots;i++)trunk.SetGridSlot(i,PocketSlotData.Empty);w.Restore(before);
            for(int i=0;i<slots.Length;i++)trunk.SetGridSlot(i,slots[i]);
            foreach(var part in Object.FindObjectsByType<WorkshopPartItem>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!oldObjects.Contains(part.GetInstanceID()))Object.DestroyImmediate(part.gameObject);
            Object.DestroyImmediate(zoneObject);JsonUtility.FromJsonOverwrite(carBefore,car);car.enabled=wasEnabled;if(rb!=null)rb.isKinematic=wasKinematic;
        }
        Debug.Log("[Workshop] "+results.Count+" checks passed.");
    }
    public static void CheckpointRoundTrip()
    {
        var w=Require();var trunk=VehicleCargoTrunk.Instance;var snapshot=GarageDepartureCheckpoint.Capture();
        Check(!string.IsNullOrEmpty(snapshot.workshop),"Departure snapshot includes workshop state");
        var item=snapshot.items.First(i=>!string.IsNullOrEmpty(i.workshopPart));
        var obj=Object.FindObjectsByType<GarageCheckpointItem>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(i=>i.id==item.id);
        Object.DestroyImmediate(obj.gameObject);Funds(w,0);GarageDepartureCheckpoint.Apply(snapshot);
        Check(w.Coins==JsonUtility.FromJson<VehicleWorkshop.SaveData>(snapshot.workshop).coins,"Checkpoint restores exact currency");
        Check(Object.FindObjectsByType<GarageCheckpointItem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(i=>i.id==item.id&&i.GetComponent<WorkshopPartItem>()!=null),"Checkpoint recreates missing physical part with its identity");
    }
}
