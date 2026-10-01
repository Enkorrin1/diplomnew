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
public static class GarageItemLogicValidation
{
    static readonly List<string> log=new List<string>();
    static void Check(bool ok,string name){log.Add((ok?"PASS ":"FAIL ")+name);File.WriteAllLines("Temp/GaragePhysics/item-logic.txt",log);if(!ok)throw new Exception(name);}
    public static void Run(){log.Clear();Application.runInBackground=true;Object.FindFirstObjectByType<BunkerPrologueCutscene>().StartCoroutine(RunChecks());}
    static IEnumerator RunChecks()
    {
        var intro=Object.FindFirstObjectByType<BunkerPrologueCutscene>();while(intro.IsRunning)yield return null;
        var player=Object.FindFirstObjectByType<GaragePlayerController>();var inv=PlayerPocketInventory.Instance;var hands=PlayerHandsInventory.Instance;var trunk=VehicleCargoTrunk.Instance;
        var all=Object.FindObjectsByType<GarageItemFunction>(FindObjectsSortMode.None);
        GarageItemFunction Item(GarageItemFunction.ItemKind kind)=>all.First(f=>f.Kind==kind);
        for(int i=0;i<20;i++)inv.ClearSlot(i);
        var torch=Item(GarageItemFunction.ItemKind.Flashlight);var torchProp=torch.GetComponent<PhysicsProp>();
        inv.TryStorePhysical(torchProp);inv.SelectSlot(0);yield return null;
        Check(inv.UseActivePhysical()&&torch.IsRunning,"Flashlight F action switches real light on");
        torch.Advance(150);Check(torch.Charge<.51f&&torch.Charge>.48f,"Flashlight drains according to elapsed time");
        torch.Advance(300);Check(!torch.IsRunning&&torch.Charge==0,"Empty flashlight turns off");
        Check(!torch.ReplaceBattery(),"No battery cannot refill flashlight");
        var battery=Item(GarageItemFunction.ItemKind.Battery).GetComponent<PhysicsProp>();var second=Object.Instantiate(battery.gameObject).GetComponent<PhysicsProp>();
        inv.TryStorePhysical(battery);inv.TryStorePhysical(second);int batterySlot=Enumerable.Range(0,20).First(i=>inv.GetSlot(i).worldItem==battery);
        Check(inv.GetSlot(batterySlot).count==2,"Battery stack contains two physical items");
        Check(torch.ReplaceBattery()&&torch.Charge==1&&inv.GetSlot(batterySlot).count==1&&inv.GetSlot(batterySlot).worldItem==second,"Replacing battery consumes exactly one and promotes reserve");
        Check(!torch.ReplaceBattery()&&inv.GetSlot(batterySlot).count==1,"Full charge does not waste batteries");
        torch.Advance(1);yield return new WaitForSeconds(.6f);inv.UseActivePhysical();torch.Advance(90);
        float saved=torch.Charge;trunk.ClearCargo();Check(trunk.StoreFromPlayer(hands,inv,out _),"Store flashlight in cargo");
        yield return null;Check(Mathf.Abs(torch.Charge-saved)<.02f&&!torch.IsRunning,"Stowed flashlight switches off and preserves charge");
        Check(trunk.TakeToPlayer(0,hands,inv,out _)&&inv.ActivePhysical==torchProp&&Mathf.Abs(torch.Charge-saved)<.02f,"Retrieve same partially charged flashlight");
        var wheel=Object.FindObjectsByType<VehiclePartHotspot>(FindObjectsSortMode.None).First(s=>s.RequiredItem==BunkerAssemblyItemType.Wheel);
        Check(hands.TryHoldItem(BunkerAssemblyItemType.Wheel),"Hold test wheel");var original=hands.HeldGameObject;
        wheel.Interact(player);Check(!wheel.IsInstalled&&hands.HeldGameObject==original,"Wheel installation rejected without wrench");
        inv.TryStorePhysical(Item(GarageItemFunction.ItemKind.Wrench).GetComponent<PhysicsProp>());
        wheel.Interact(player);Check(wheel.IsInstalled&&!hands.HasItem&&original!=null,"Wheel installed with wrench, physical instance retained");
        var body=wheel.GetComponentInParent<Rigidbody>();if(body!=null){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
        Check(wheel.TryRemoveWheel(player)&&hands.HeldGameObject==original&&!BunkerStarterCarAssembly.Instance.IsWheelInstalled,"Unscrew returns same wheel and resets assembly readiness");
        wheel.Interact(player);Check(wheel.IsInstalled&&!hands.HasItem,"Reinstall original wheel");
        var batterySpot=Object.FindObjectsByType<VehiclePartHotspot>(FindObjectsSortMode.None).First(s=>s.RequiredItem==BunkerAssemblyItemType.Battery);
        hands.TryHoldItem(BunkerAssemblyItemType.Battery);batterySpot.Interact(player);
        Check(!batterySpot.IsInstalled&&hands.HasItem,"Battery terminals require screwdriver");inv.TryStorePhysical(Item(GarageItemFunction.ItemKind.Screwdriver).GetComponent<PhysicsProp>());batterySpot.Interact(player);Check(batterySpot.IsInstalled&&!hands.HasItem,"Screwdriver connects car battery");
        var ray=player.GetComponentInChildren<GarageInteractionRaycaster>();
        void AimAtCar(){typeof(GarageInteractionRaycaster).GetField("currentTarget",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ray,batterySpot);typeof(GarageInteractionRaycaster).GetField("targetConfirmed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ray,true);}
        var car=batterySpot.GetComponentInParent<VehicleModularState>();Check(car!=null,"Service hotspot resolves live car state");car.InstallCowcatcher();car.AbsorbRammingImpact(50);
        var wrench=Item(GarageItemFunction.ItemKind.Wrench);AimAtCar();float damage=car.BumperIntegrity;
        Check(!wrench.Use(player)&&Mathf.Approximately(car.BumperIntegrity,damage),"Wrench repair cannot generate free bolts");
        inv.TryStorePhysical(Item(GarageItemFunction.ItemKind.Bolt).GetComponent<PhysicsProp>());yield return new WaitForSeconds(.4f);AimAtCar();
        Check(wrench.Use(player)&&car.BumperIntegrity>damage&&!inv.HasTool(GarageItemFunction.ItemKind.Bolt),"Wrench repair consumes one bolt");
        var hammer=Item(GarageItemFunction.ItemKind.Hammer);damage=car.BumperIntegrity;AimAtCar();Check(hammer.Use(player)&&car.BumperIntegrity>damage,"Hammer straightens damaged bumper");
        var radio=Item(GarageItemFunction.ItemKind.Radio);float radioBefore=radio.Charge;Check(radio.Use(player)&&radio.Charge<radioBefore,"Radio transmission consumes battery charge");
        var burner=Item(GarageItemFunction.ItemKind.Burner);var pot=Item(GarageItemFunction.ItemKind.Pot);var mug=Item(GarageItemFunction.ItemKind.Mug);
        var secondBurner=Object.Instantiate(burner.gameObject).GetComponent<GarageItemFunction>();
        foreach(var item in new[]{burner,pot}){var rb=item.GetComponent<Rigidbody>();rb.isKinematic=true;item.transform.rotation=Quaternion.identity;}
        burner.transform.position=player.transform.position+Vector3.forward*1.5f+Vector3.up*.5f;pot.transform.position=burner.transform.position+Vector3.up*.4f;
        Check(burner.Use(player)&&burner.IsRunning,"Ground burner ignites");
        burner.Advance(30);Check(burner.Charge<.91f&&burner.Charge>.88f,"Burner consumes gas");
        var water=Object.FindObjectsByType<FluidContainer>(FindObjectsSortMode.None).First(f=>f.FluidType==BunkerFluidType.Water);water.Configure(BunkerFluidType.Water,20,5);
        Check(pot.ReceiveWater(water)&&Mathf.Approximately(pot.WaterLiters,2)&&Mathf.Approximately(water.CurrentLiters,3),"Canister fills pot with conserved water volume");
        pot.Advance(10);Check(!pot.IsBoiled,"Boiling takes time");pot.Advance(10);Check(pot.IsBoiled,"Lit burner boils pot");
        Check(mug.Use(player,pot)&&Mathf.Abs(mug.WaterLiters-.35f)<.001f&&Mathf.Abs(pot.WaterLiters-1.65f)<.001f,"Mug receives boiled water without duplication");
        var needs=PlayerFieldNeeds.For(player);needs.Advance(3000);float hydration=needs.Water;
        yield return new WaitForSeconds(.4f);Check(mug.Use(player)&&needs.Water>hydration&&mug.WaterLiters<.16f,"Drinking consumes a measured serving and restores hydration");
        var food=Item(GarageItemFunction.ItemKind.Food);var plate=Item(GarageItemFunction.ItemKind.Plate);
        Check(plate.Use(player,food)&&plate.Portions==1&&food.Portions==3,"Transfer one food portion to reusable plate");
        yield return new WaitForSeconds(.4f);float hunger=needs.Food;Check(plate.Use(player)&&plate.Portions==0&&needs.Food>hunger,"Eating consumes portion and restores satiety");
        var medkit=Item(GarageItemFunction.ItemKind.Medkit);inv.TryStorePhysical(medkit.GetComponent<PhysicsProp>());needs.TakeDamage(40);float health=needs.Health;
        Check(medkit.Use(player)&&needs.Health>health,"Medkit heals character and consumes item");yield return null;
        Check(medkit==null,"Used medkit removed from physical inventory");
        var barrel=Item(GarageItemFunction.ItemKind.Barrel);Check(barrel.ReceiveWater(water)&&Mathf.Approximately(barrel.WaterLiters,3)&&water.IsEmpty,"Barrel stores actual remaining canister water");
        Check(!pot.ReceiveWater(water)&&Mathf.Abs(pot.WaterLiters-1.65f)<.001f,"Empty source cannot generate water");
        Check(barrel.ReceiveWater(water)&&Mathf.Approximately(barrel.WaterLiters,0)&&Mathf.Approximately(water.CurrentLiters,3),"Empty water canister retrieves conserved water from barrel");
        burner.Advance(400);Check(!burner.IsRunning&&burner.Charge==0,"Empty gas supply extinguishes burner");
        var rb2=secondBurner.GetComponent<Rigidbody>();rb2.isKinematic=true;secondBurner.transform.rotation=Quaternion.identity;
        Check(secondBurner.Use(player),"Second burner ignites for screenshot");secondBurner.transform.position=burner.transform.position;
        burner.transform.position+=Vector3.right*3;pot.transform.position+=Vector3.right*3;inv.SelectSlot(4);
        player.SetMovementLocked(true);
        var oldCameraPosition=player.PlayerCamera.transform.localPosition;
        var oldCameraRotation=player.PlayerCamera.transform.localRotation;
        player.PlayerCamera.transform.position=secondBurner.transform.position+new Vector3(.65f,.6f,-.8f);
        player.PlayerCamera.transform.LookAt(secondBurner.transform.position+Vector3.up*.12f);
        yield return new WaitForSeconds(.3f);ScreenCapture.CaptureScreenshot("Temp/GaragePhysics/item-burner.png");yield return new WaitForSeconds(.3f);
        secondBurner.transform.rotation=Quaternion.Euler(0,0,90);secondBurner.Advance(.1f);Check(!secondBurner.IsRunning,"Tipping burner extinguishes flame");
        player.PlayerCamera.transform.localPosition=oldCameraPosition;player.PlayerCamera.transform.localRotation=oldCameraRotation;player.SetMovementLocked(false);
        player.transform.position=trunk.transform.position+Vector3.right*3;InventoryWindowUI.Instance.OpenForTrunk(trunk);yield return null;
        ScreenCapture.CaptureScreenshot("Temp/GaragePhysics/item-status.png");yield return new WaitForSeconds(.3f);InventoryWindowUI.Instance.Close();
        log.Add("COMPLETE");File.WriteAllLines("Temp/GaragePhysics/item-logic.txt",log);
    }
}
