using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.UI;
using UnityEngine;
using Object=UnityEngine.Object;
public static class GarageInventoryValidation
{
    static List<string> report;
    static void Check(bool ok,string title){report.Add((ok?"PASS ":"FAIL ")+title);File.WriteAllLines("Temp/GaragePhysics/inventory.txt",report);if(!ok)throw new Exception(title);}
    public static void Run(){report=new List<string>();Object.FindFirstObjectByType<BunkerPrologueCutscene>().StartCoroutine(Routine());}
    static PhysicsProp Clone(PhysicsProp source){var p=Object.Instantiate(source.gameObject).GetComponent<PhysicsProp>();p.transform.SetParent(null);p.gameObject.SetActive(true);p.OnDropped(Vector3.zero,Vector3.zero);return p;}
    static IEnumerator Routine()
    {
        var intro=Object.FindFirstObjectByType<BunkerPrologueCutscene>();while(intro.IsRunning)yield return null;
        var inv=PlayerPocketInventory.Instance;var hands=PlayerHandsInventory.Instance;var player=Object.FindFirstObjectByType<GaragePlayerController>();
        var props=Object.FindObjectsByType<PhysicsProp>(FindObjectsSortMode.None);
        Check(props.All(p=>p.InventoryIcon!=null),"All authored physical props have runtime icon references");
        var bolt=props.First(p=>p.name.Contains("Bolt"));var bolts=Enumerable.Range(0,22).Select(i=>Clone(bolt)).ToArray();
        foreach(var p in bolts.Take(20))Check(inv.TryStorePhysical(p),"Stack bolt "+p.GetInstanceID());
        Check(inv.GetSlot(0).count==20 && Enumerable.Range(1,4).All(i=>inv.GetSlot(i).IsEmpty),"Twenty bolts occupy one slot");
        Check(inv.TryStorePhysical(bolts[20]) && inv.GetSlot(1).count==1,"Stack limit spills into next slot");
        Check(!inv.MoveSlot(1,0),"Cannot merge into capped stack");
        var box=Object.FindFirstObjectByType<GaragePortableContainer>();inv.SelectSlot(0);
        box.StorePocket(inv);box.StorePocket(inv);
        Check(inv.MoveSlot(1,0) && inv.GetSlot(0).count==19 && inv.GetSlot(1).IsEmpty,"Move merges compatible stacks with capacity");
        box.Take(hands);box.Take(hands);
        inv.ClearSlot(1);
        var battery=props.First(p=>p.name.Contains("BatterySmall"));var batteries=Enumerable.Range(0,3).Select(i=>Clone(battery)).ToArray();
        foreach(var p in batteries)inv.TryStorePhysical(p);
        Check(inv.GetSlot(1).count==3,"Identical batteries stack");
        var lamp=props.First(p=>p.GetComponent<GarageItemUse>()!=null && p.GetComponent<GarageItemUse>().Kind==GarageItemUse.UseKind.Flashlight);
        var radio=props.First(p=>p.name.Contains("Walkie"));var wrench=props.First(p=>p.name.Contains("Wrench"));
        inv.TryStorePhysical(lamp);inv.TryStorePhysical(radio);inv.TryStorePhysical(wrench);
        Check(Enumerable.Range(0,5).All(i=>!inv.GetSlot(i).IsEmpty),"Five occupied slots");
        var overflow=Clone(wrench);Check(!inv.TryStorePhysical(overflow) && overflow.gameObject.activeSelf,"Full inventory does not destroy extra tool");
        Check(inv.MoveSlot(0,4) && inv.GetSlot(4).count==20 && inv.GetSlot(0).worldItem==wrench,"Swap occupied slots preserves full stack");
        Check(box.StorePocket(inv) && box.Count==1 && inv.GetSlot(4).count==19,"Box receives one real bolt from stack");
        Check(box.Take(hands) && box.Count==0 && inv.GetSlot(4).count==20,"Box retrieval merges into full hotbar without empty slot");
        inv.SelectSlot(2);yield return null;
        Check(inv.UseActivePhysical() && lamp.GetComponent<GarageItemUse>().LightOn,"Lamp works from pocket");
        Check(box.StorePocket(inv) && inv.GetSlot(2).IsEmpty,"Pocket lamp moved into box");
        Check(box.Take(hands) && inv.GetSlot(2).worldItem==lamp && lamp.GetComponent<GarageItemUse>().LightOn,"Box preserves lamp identity and switch state");
        for(int i=0;i<box.Capacity;i++){inv.SelectSlot(4);Check(box.StorePocket(inv),"Fill box "+i);}
        int before=inv.GetSlot(4).count;Check(!box.StorePocket(inv) && inv.GetSlot(4).count==before,"Full box leaves source stack unchanged");
        inv.SelectSlot(2);var trunk=Object.FindFirstObjectByType<VehicleCargoTrunk>();trunk.ClearCargo();
        Check(trunk.StoreFromPlayer(hands,inv,out _) && trunk.GetPhysicalItem(0)==lamp.gameObject && inv.GetSlot(2).IsEmpty,"Cargo stores actual pocket lamp");
        var temp=Clone(wrench);inv.TryStorePhysical(temp);
        Check(!trunk.TakeToPlayer(0,hands,inv,out _) && trunk.ItemCount==1 && trunk.GetPhysicalItem(0)==lamp.gameObject,"Full pocket rejects cargo retrieval without loss");
        inv.ClearSlot(2);Check(trunk.TakeToPlayer(0,hands,inv,out _) && inv.GetSlot(2).worldItem==lamp && trunk.ItemCount==0,"Cargo returns exact lamp to slot");
        var can=Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None).First(p=>p.ItemType==BunkerAssemblyItemType.FuelCanister);
        var fluid=can.GetComponent<FluidContainer>();fluid.Configure(BunkerFluidType.Gasoline,20,3.25f);hands.HoldAssemblyItem(can);
        Check(trunk.StoreFromPlayer(hands,inv,out _) && !hands.HasItem,"Cargo accepts partial physical canister");
        Check(trunk.TakeToPlayer(0,hands,inv,out _) && hands.HeldGameObject==can.gameObject && fluid.CurrentLiters==3.25f,"Cargo preserves partial volume and object identity");
        hands.DropItem(forStorage:true);can.gameObject.SetActive(false);
        inv.SelectSlot(2);Check(trunk.StoreFromPlayer(hands,inv,out _),"Lamp deposited for panel preview");
        var boxProp=box.GetComponent<PhysicsProp>();hands.HoldPhysicsProp(boxProp);
        Check(trunk.StoreFromPlayer(hands,inv,out _) && box.Count==box.Capacity,"Cargo stores filled box with contents");
        Check(trunk.TakeToPlayer(1,hands,inv,out _) && hands.HeldGameObject==box.gameObject && box.Count==box.Capacity,"Filled box retrieved intact");
        hands.DropItem(forStorage:true);box.gameObject.SetActive(false);
        inv.SelectSlot(4);int stackBefore=inv.GetSlot(4).count;
        for(int i=0;i<5;i++)trunk.TryStoreItem(BunkerAssemblyItemType.Wheel,out _);
        Check(trunk.ItemCount==trunk.MaxSlots && !trunk.StoreFromPlayer(hands,inv,out _) && inv.GetSlot(4).count==stackBefore,"Full cargo preserves source stack");
        for(int i=0;i<5;i++)trunk.TakeItem(0);
        for(int i=0;i<5;i++)inv.SelectSlot(i);inv.SelectSlot(4);yield return null;
        ScreenCapture.CaptureScreenshot("Temp/GaragePhysics/inventory-hotbar.png");yield return new WaitForSeconds(.2f);
        var ui=VehicleDashboardPanelsUI.Instance;if(ui==null)ui=new GameObject("TestServiceUI").AddComponent<VehicleDashboardPanelsUI>();
        ui.ShowTrunkPanel();yield return new WaitForSeconds(.3f);ScreenCapture.CaptureScreenshot("Temp/GaragePhysics/inventory-cargo.png");
        yield return new WaitForSeconds(.5f);
        int countBefore=trunk.ItemCount;int boltsBefore=inv.GetSlot(4).count;
        ui.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="StoreItem").onClick.Invoke();
        Check(trunk.ItemCount==countBefore+1 && inv.GetSlot(4).count==boltsBefore-1,"Cargo deposit button transfers from selected pocket slot");
        ui.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="TakeItem" && b.gameObject.activeInHierarchy).onClick.Invoke();
        Check(inv.GetSlot(2).worldItem==lamp && lamp.GetComponent<GarageItemUse>().LightOn,"Cargo retrieve button restores lamp to pocket");
        ui.ClosePanel();
        File.WriteAllLines("Temp/GaragePhysics/inventory.txt",report);
    }
}

