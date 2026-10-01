using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;
public static class VehicleServiceValidation
{
    static readonly List<string> results=new List<string>();
    static void Check(bool value,string label)
    {
        results.Add((value?"PASS ":"FAIL ")+label);
        Directory.CreateDirectory("Logs/VehicleService");File.WriteAllLines("Logs/VehicleService/validation.txt",results);
        if(!value)throw new InvalidOperationException(label);
    }
    static bool Near(float a,float b)=>Mathf.Abs(a-b)<.001f;
    public static void TestLegacyCheckpoint()
    {
        var trunk=VehicleCargoTrunk.Instance;
        Check(trunk.TryStoreItem(BunkerAssemblyItemType.WaterCanister,out _),"Legacy canister materialized");
        int index=Enumerable.Range(0,trunk.MaxSlots).First(i=>trunk.GetPhysicalItem(i)!=null&&trunk.GetPhysicalItem(i).GetComponent<GarageCheckpointItem>()?.id.StartsWith("service_")==true);
        var obj=trunk.GetPhysicalItem(index);obj.GetComponent<FluidContainer>().PourOut(3.25f);
        string id=obj.GetComponent<GarageCheckpointItem>().id;
        var checkpoint=GarageDepartureCheckpoint.Capture();
        // Simulate losing all runtime-generated objects on a scene reload.
        Object.DestroyImmediate(obj);
        GarageDepartureCheckpoint.Apply(checkpoint);
        var restored=trunk.GetPhysicalItem(index);
        Check(restored!=null&&restored.GetComponent<GarageCheckpointItem>().id==id&&Near(restored.GetComponent<FluidContainer>().CurrentLiters,6.75f),"Generated canister restored with exact 6.75 litre remainder");
    }
    public static void RunFluids()
    {
        results.Clear();
        var car=VehicleModularState.Instance;if(car==null)throw new InvalidOperationException("Play GarageScene.");
        var json=JsonUtility.ToJson(car);bool enabled=car.enabled;car.enabled=false;
        var obj=new GameObject("FluidValidation");var fluid=obj.AddComponent<FluidContainer>();
        try
        {
            JsonUtility.FromJsonOverwrite("{\"currentRadiatorWater\":6.4,\"currentEngineOil\":2.5}",car);
            fluid.Configure(BunkerFluidType.Water,10,7);
            Check(VehicleServiceInventory.Pour(car,fluid,BunkerFluidType.Water,out _) && Near(car.RadiatorWater,10)&&Near(fluid.CurrentLiters,3.4f),"Partial water: 3.6 litres transferred; 3.4 retained");
            Check(!VehicleServiceInventory.Pour(car,fluid,BunkerFluidType.Water,out _)&&Near(fluid.CurrentLiters,3.4f),"Full radiator consumes nothing");
            Check(!VehicleServiceInventory.Pour(car,fluid,BunkerFluidType.EngineOil,out _)&&Near(car.EngineOil,2.5f),"Water cannot enter oil system");
            fluid.Configure(BunkerFluidType.EngineOil,5,3);
            Check(VehicleServiceInventory.Pour(car,fluid,BunkerFluidType.EngineOil,out _)&&Near(car.EngineOil,5)&&Near(fluid.CurrentLiters,.5f),"Oil fills independently and preserves remainder");
            JsonUtility.FromJsonOverwrite("{\"currentRadiatorWater\":6.4}",car);
            fluid.Configure(BunkerFluidType.Water,10,.02f);
            Check(VehicleServiceInventory.Pour(car,fluid,BunkerFluidType.Water,out _)&&Near(car.RadiatorWater,6.42f)&&fluid.IsEmpty,"Last 20 ml are transferred without loss");
            Check(!VehicleServiceInventory.Pour(car,fluid,BunkerFluidType.Water,out _),"Empty container rejected");
            JsonUtility.FromJsonOverwrite("{\"currentEngineOil\":1.25}",car);
            var checkpoint=GarageDepartureCheckpoint.Capture();
            Check(checkpoint.car.Contains("currentEngineOil") && checkpoint.car.Contains("1.25"),"Oil level included in departure checkpoint");
            var state=VehicleServiceState.For(car);
            Check(!state.AtServiceStation,"Garage is not a service station");
        }
        finally{JsonUtility.FromJsonOverwrite(json,car);car.enabled=enabled;Object.Destroy(obj);}
    }
    public static void PrepareVisual()
    {
        var car=VehicleModularState.Instance;
        JsonUtility.FromJsonOverwrite("{\"currentRadiatorWater\":6.4,\"currentEngineOil\":2.5}",car);
        var trunk=VehicleCargoTrunk.Instance;
        var water=GameObject.Find("Departure_WaterReserve");
        var oil=GameObject.Find("Departure_OilReserve");
        trunk.SetGridSlot(0,InventoryStackOps.FromObject(water,water.transform.lossyScale));
        trunk.SetGridSlot(1,InventoryStackOps.FromObject(oil,oil.transform.lossyScale));
        var ui=VehicleDashboardPanelsUI.Instance;
        if(ui==null)ui=new GameObject("VehicleDashboardPanelsUI").AddComponent<VehicleDashboardPanelsUI>();
        ui.ShowEnginePanel();
        ui.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="Fluids").onClick.Invoke();
        ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="Source_1").onClick.Invoke();
    }
    public static void TestCargoDrop()
    {
        var ui=VehicleDashboardPanelsUI.Instance;var car=VehicleModularState.Instance;
        var source=ui.GetComponentsInChildren<VehicleServiceCellUI>().First(c=>c.IsSource&&c.Address.Area==1&&c.Address.Index==0);
        var target=ui.GetComponentsInChildren<VehicleServiceCellUI>().First(c=>!c.IsSource&&c.Slot==VehicleServiceSlot.Water);
        float before=source.Address.Fluid.CurrentLiters;float gap=car.MaxRadiatorWater-car.RadiatorWater;
        var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerDrag=source.gameObject};
        ExecuteEvents.Execute(source.gameObject,e,ExecuteEvents.beginDragHandler);
        ExecuteEvents.Execute(target.gameObject,e,ExecuteEvents.dropHandler);
        ExecuteEvents.Execute(source.gameObject,e,ExecuteEvents.endDragHandler);
        Check(Near(car.RadiatorWater,10)&&Near(source.Address.Fluid.CurrentLiters,before-gap),"Real UI drag from cargo partially tops up radiator");
        Check(VehicleCargoTrunk.Instance.GetPhysicalItem(0)!=null,"Cargo retains the physical canister after pouring");
    }
}
