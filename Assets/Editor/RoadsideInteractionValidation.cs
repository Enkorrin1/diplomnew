using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.UI;
using UnityEditor;
using UnityEngine;

public static class RoadsideInteractionValidation
{
    [MenuItem("RogueDrive/Validate roadside interactions")]
    public static void RunMenu()=>Debug.Log(Run());

    public static string Run()
    {
        if(!Application.isPlaying)throw new Exception("Run in Play Mode on the road.");
        UnityEngine.Object.FindFirstObjectByType<GarageDriveOutController>()?.ExitCar();
        var car=UnityEngine.Object.FindFirstObjectByType<VehicleModularState>();
        var player=UnityEngine.Object.FindFirstObjectByType<GaragePlayerController>();
        var camera=player.PlayerCamera;
        var ray=camera.GetComponent<GarageInteractionRaycaster>();
        var scan=typeof(GarageInteractionRaycaster).GetMethod("PerformRaycast",BindingFlags.NonPublic|BindingFlags.Instance);
        var body=car.GetComponent<Rigidbody>();bool kinematic=body.isKinematic;
        Vector3 cameraPosition=camera.transform.position;Quaternion cameraRotation=camera.transform.rotation;
        string state=JsonUtility.ToJson(car),workshop=VehicleWorkshop.For(car).Capture();
        var trunk=car.GetComponent<VehicleCargoTrunk>();
        int free=Enumerable.Range(0,trunk.MaxSlots).First(i=>trunk.GetSlot(i).IsEmpty);
        GameObject fuel=null,wall=null;
        var checks=new List<string>();
        var fluidSnapshots=new Dictionary<FluidContainer,string>();
        void Check(bool result,string name){if(!result)throw new Exception("FAIL: "+name);checks.Add(name);}
        void Aim(Component target,Vector3 localView)
        {
            camera.transform.position=car.transform.TransformPoint(localView);
            camera.transform.LookAt(target.GetComponent<Collider>().bounds.center);
            Physics.SyncTransforms();scan.Invoke(ray,null);
            Check(ReferenceEquals(ray.CurrentTarget,target),"ray selects "+target.name);
        }
        try
        {
            body.isKinematic=true;
            InventoryWindowUI.Instance.Close();VehicleDashboardPanelsUI.Instance?.ClosePanel();
            foreach(var spot in car.GetComponentsInChildren<VehicleInspectionHotspot>())
            {
                Vector3 view=spot.Type==VehicleInspectionHotspot.HotspotType.Trunk?new Vector3(0,1.5f,-3.7f)
                    :spot.Type==VehicleInspectionHotspot.HotspotType.EngineHood?new Vector3(0,1.5f,3.7f):new Vector3(2.7f,1.4f,-1.25f);
                Aim(spot,view);ray.CurrentTarget.Interact(player);
                if(spot.Type==VehicleInspectionHotspot.HotspotType.Trunk)
                {Check(InventoryWindowUI.Instance.IsOpen&&InventoryWindowUI.Instance.Trunk==trunk,"trunk panel binds live cargo");InventoryWindowUI.Instance.Close();}
                else {Check(VehicleDashboardPanelsUI.Instance.IsAnyPanelOpen,"opens "+spot.Type);VehicleDashboardPanelsUI.Instance.ClosePanel();}
            }
            var tires=car.GetComponentsInChildren<VehicleTireHotspot>();
            Check(tires.Length==4,"four tire service points on direct road start");
            foreach(var tire in tires)Aim(tire,new Vector3(tire.TireIndex%2==0?-2.6f:2.6f,1,tire.transform.localPosition.z));
            car.DamageTire(0,.6f);
            Check(trunk.TryStoreItem(BunkerAssemblyItemType.Wheel,out _),"spare placed into cargo");
            int before=trunk.ItemCount;
            tires.First(t=>t.TireIndex==0).Interact(player);
            Check(car.GetTireIntegrity(0)==1&&trunk.ItemCount==before-1,"replacement repairs selected tire and consumes one spare");
            fuel=new GameObject("ValidationFuel");var fluid=fuel.AddComponent<FluidContainer>();fluid.Configure(BunkerFluidType.Gasoline,20,8);
            trunk.SetGridSlot(free,new PocketSlotData{id="validation_fuel",displayName="Test fuel",count=1,largeItem=fuel,worldScale=Vector3.one,legacyType=BunkerAssemblyItemType.FuelCanister});
            car.SetFuelForGaragePreparation(car.MaxFuelLiters-3);
            VehicleDashboardPanelsUI.Instance.ShowFuelInletPanel();
            var button=VehicleDashboardPanelsUI.Instance.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="Refuel");
            Check(button.interactable,"refuel button detects cargo canister");
            var sources=VehicleServiceInventory.Sources().Select(a=>a.Fluid).Where(f=>f!=null&&f.FluidType==BunkerFluidType.Gasoline).Distinct().ToArray();
            foreach(var source in sources)fluidSnapshots[source]=JsonUtility.ToJson(source);
            float available=sources.Sum(f=>f.CurrentLiters);
            float initial=car.FuelLiters;
            button.onClick.Invoke();
            float gained=car.FuelLiters-initial;
            Check(gained>0,"refuel UI callback transfers gasoline");
            Check(Mathf.Abs((available-sources.Sum(f=>f.CurrentLiters))-gained)<.001f&&gained<=3.001f,"fuel volume conserved and leftover retained");
            VehicleDashboardPanelsUI.Instance.ClosePanel();
            var hood=car.GetComponentsInChildren<VehicleInspectionHotspot>().First(h=>h.Type==VehicleInspectionHotspot.HotspotType.EngineHood);
            Aim(hood,new Vector3(0,1.5f,3.7f));
            wall=new GameObject("ValidationOccluder");var box=wall.AddComponent<BoxCollider>();box.size=new Vector3(2,2,.2f);
            wall.transform.position=(camera.transform.position+hood.transform.position)*.5f;wall.transform.rotation=car.transform.rotation;
            Physics.SyncTransforms();scan.Invoke(ray,null);
            Check(ray.CurrentTarget==null,"solid wall still blocks service and boarding");
            return "PASS "+checks.Count+"\n"+string.Join("\n",checks);
        }
        finally
        {
            InventoryWindowUI.Instance?.Close();VehicleDashboardPanelsUI.Instance?.ClosePanel();
            foreach(var pair in fluidSnapshots)if(pair.Key!=null)JsonUtility.FromJsonOverwrite(pair.Value,pair.Key);
            if(fuel!=null){trunk.SetGridSlot(free,PocketSlotData.Empty);UnityEngine.Object.DestroyImmediate(fuel);}
            if(wall!=null)UnityEngine.Object.DestroyImmediate(wall);
            JsonUtility.FromJsonOverwrite(state,car);VehicleWorkshop.For(car).Restore(workshop);
            body.isKinematic=kinematic;camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
            Physics.SyncTransforms();
        }
    }
}
