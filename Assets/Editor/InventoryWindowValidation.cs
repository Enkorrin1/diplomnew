using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class InventoryWindowValidation
{
    static readonly List<string> checks=new List<string>();
    static void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines("Temp/GaragePhysics/inventory-window.txt",checks);if(!ok)throw new Exception(message);}
    public static void Run(){checks.Clear();Application.runInBackground=true;Object.FindFirstObjectByType<BunkerPrologueCutscene>().StartCoroutine(Routine());}
    static IEnumerator Routine()
    {
        var intro=Object.FindFirstObjectByType<BunkerPrologueCutscene>();while(intro.IsRunning)yield return null;
        var inv=PlayerPocketInventory.Instance;var trunk=VehicleCargoTrunk.Instance;var ui=InventoryWindowUI.Instance;
        inv.transform.position=trunk.transform.position+Vector3.right*3;
        for(int i=0;i<20;i++)inv.ClearSlot(i);trunk.ClearCargo();
        var template=Object.FindObjectsByType<PhysicsProp>(FindObjectsSortMode.None).First(p=>p.name.Contains("Bolt"));
        var ids=new HashSet<int>();
        for(int i=0;i<12;i++){var p=Object.Instantiate(template.gameObject).GetComponent<PhysicsProp>();p.transform.SetParent(null);p.gameObject.SetActive(true);p.OnDropped(Vector3.zero,Vector3.zero);ids.Add(p.GetInstanceID());Check(inv.TryStorePhysical(p),"Collect physical bolt "+i);}
        ui.OpenForTrunk(trunk);yield return null;
        Check(ui.IsOpen&&InventoryWindowUI.BlockGameplayInput&&Cursor.visible,"Open blocks gameplay and releases cursor");
        var cells=ui.GetComponentsInChildren<InventoryCellUI>();
        InventoryCellUI Cell(int a,int i)=>cells.Single(c=>c.Area==a&&c.Index==i);
        var right=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right};
        var left=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
        ExecuteEvents.Execute(Cell(0,0).gameObject,right,ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(Cell(1,4).gameObject,left,ExecuteEvents.pointerClickHandler);
        Check(inv.GetSlot(0).count==6&&trunk.GetSlot(4).count==6,"Right click splits physical stack in half");
        ExecuteEvents.Execute(Cell(1,4).gameObject,left,ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(Cell(0,5).gameObject,right,ExecuteEvents.pointerClickHandler);
        Check(inv.GetSlot(5).count==1&&trunk.GetSlot(4).count==5,"Right click places one object");
        ui.Close();Check(trunk.GetSlot(4).count==5,"Closing with selection preserves all remaining objects");
        ui.OpenForTrunk(trunk);cells=ui.GetComponentsInChildren<InventoryCellUI>();
        ExecuteEvents.Execute(Cell(1,4).gameObject,left,ExecuteEvents.beginDragHandler);
        ExecuteEvents.Execute(Cell(0,6).gameObject,left,ExecuteEvents.dropHandler);
        ExecuteEvents.Execute(Cell(1,4).gameObject,left,ExecuteEvents.endDragHandler);
        Check(trunk.GetSlot(4).IsEmpty&&inv.GetSlot(6).count==5,"EventSystem drag moves stack to backpack");
        ui.QuickTransfer(Cell(0,0));ui.QuickTransfer(Cell(0,5));ui.QuickTransfer(Cell(0,6));
        Check(trunk.GetSlot(0).count==12,"Quick transfer merges exact stack into cargo");
        var stack=trunk.GetSlot(0);var actual=new HashSet<int>{stack.worldItem.GetInstanceID()};foreach(var entry in stack.reserves)actual.Add(entry.item.GetInstanceID());
        Check(actual.SetEquals(ids),"Every physical instance preserved exactly once");
        for(int i=0;i<20;i++)inv.SetGridSlot(i,new PocketSlotData{id="test"+i,displayName="Test",count=1});
        ui.QuickTransfer(Cell(1,0));Check(trunk.GetSlot(0).count==12,"Full backpack rejects transfer without loss");
        for(int i=0;i<20;i++)inv.ClearSlot(i);
        ui.QuickTransfer(Cell(1,0));Check(inv.GetSlot(0).count==12&&trunk.GetSlot(0).IsEmpty,"Quick take restores stack");
        trunk.TryStoreItem(BunkerAssemblyItemType.Wheel,out _);
        Check(!ui.Transfer(1,0,0,10,1)&&!trunk.GetSlot(0).IsEmpty,"Large wheel cannot enter backpack");
        Check(ui.Transfer(1,0,2,0,1)&&PlayerHandsInventory.Instance.HasItem,"Large wheel can move into hands");
        Check(ui.Transfer(2,0,1,7,1)&&!PlayerHandsInventory.Instance.HasItem,"Hands deposit into chosen stable cargo slot");
        Check(trunk.GetSlot(0).IsEmpty&&!trunk.GetSlot(7).IsEmpty,"Cargo holes do not shift other slots");
        inv.SetGridSlot(5,new PocketSlotData{id="car_keys",displayName="Ключи",count=1});
        Check(!ui.Transfer(0,5,1,0,1)&&inv.GetSlot(5).count==1,"Keys cannot be deposited or lost");
        for(int i=0;i<trunk.MaxSlots;i++)if(trunk.GetSlot(i).IsEmpty)trunk.TryStoreItem(BunkerAssemblyItemType.Wheel,out _);
        ui.QuickTransfer(Cell(0,0));Check(inv.GetSlot(0).count==12,"Full cargo preserves source stack");
        for(int i=0;i<trunk.MaxSlots;i++)if(i!=7)trunk.SetGridSlot(i,PocketSlotData.Empty);
        inv.SelectSlot(19);Check(inv.ActiveSlotIndex<5,"Backpack cannot become active hand slot");
        ui.Close();ui.OpenForTrunk(null);yield return null;
        Check(ui.GetComponentsInChildren<InventoryCellUI>().Length==21,"Inventory has 20 slots plus hands");
        ScreenCapture.CaptureScreenshot("Temp/GaragePhysics/window-player.png");yield return new WaitForSeconds(.3f);
        ui.Close();ui.OpenForTrunk(trunk);yield return null;
        ScreenCapture.CaptureScreenshot("Temp/GaragePhysics/window-cargo.png");yield return new WaitForSeconds(.3f);
        Check(ui.GetComponentsInChildren<InventoryCellUI>().Length==36,"Combined window has 20 player and 15 cargo slots plus hands");
        ui.Close();yield return null;Check(!InventoryWindowUI.BlockGameplayInput,"Closing restores gameplay input next frame");
        checks.Add("COMPLETE");File.WriteAllLines("Temp/GaragePhysics/inventory-window.txt",checks);
    }
}

