using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GaragePolishValidation
{
    public static void Run()
    {
        var flow=Object.FindFirstObjectByType<GarageSceneExitCinematic>();
        if(!Application.isPlaying || flow==null) throw new InvalidOperationException("Play GarageScene first");
        flow.StartCoroutine(Check(flow));
    }
    static IEnumerator Check(GarageSceneExitCinematic flow)
    {
        Directory.CreateDirectory("Temp/GaragePolish");
        var results=new List<string>();
        var intro=Object.FindFirstObjectByType<BunkerPrologueCutscene>();
        while(intro!=null && intro.IsRunning) yield return null;
        var player=Object.FindFirstObjectByType<GaragePlayerController>();
        var assembly=BunkerStarterCarAssembly.Instance;
        var tank=assembly.GetComponentInChildren<VehicleModularState>();
        var hands=PlayerHandsInventory.Instance;
        var boarding=Object.FindFirstObjectByType<GarageVehicleBoarding>();
        Assert(tank.FuelLiters==0,"Fresh fuel is zero",results);
        Assert(!boarding.CanInteract(),"Boarding blocked before preparation",results);
        Object.FindFirstObjectByType<GarageGeneratorSwitch>().Interact(player);
        yield return new WaitForSeconds(6f);
        Assert(GaragePrologueManager.Instance.IsPowerOn,"Generator interaction",results);
        Object.FindFirstObjectByType<GarageCarKeys>().Interact(player);
        Assert(GaragePrologueManager.Instance.HasCarKeys,"Keys can be collected early",results);
        ScreenCapture.CaptureScreenshot("Temp/GaragePolish/after-powered.png");
        yield return new WaitForSeconds(.3f);
        foreach(var type in new[]{BunkerAssemblyItemType.Wheel,BunkerAssemblyItemType.Battery,BunkerAssemblyItemType.FuelCanister})
        {
            var spot=Object.FindObjectsByType<VehiclePartHotspot>(FindObjectsSortMode.None).First(s=>s.RequiredItem==type);
            var item=Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None).First(i=>i.ItemType==type &&
                (type!=BunkerAssemblyItemType.FuelCanister || (i.GetComponent<FluidContainer>()!=null && !i.GetComponent<FluidContainer>().IsEmpty)));
            item.Interact(player);
            Assert(hands.HeldGameObject==item.gameObject,"Pickup "+type,results);
            if(type==BunkerAssemblyItemType.FuelCanister)
            {
                var fluid=item.GetComponent<FluidContainer>();
                Assert(fluid!=null,"Authored fuel container exists",results);
                float initial=fluid.CurrentLiters;
                fluid.Configure(BunkerFluidType.Water,20,initial);
                spot.Interact(player);
                Assert(!spot.IsInstalled && tank.FuelLiters==0 && fluid.CurrentLiters==initial,"Wrong fluid rejected without loss",results);
                fluid.Configure(BunkerFluidType.Gasoline,20,0);
                spot.Interact(player);
                Assert(!spot.IsInstalled,"Empty canister rejected",results);
                fluid.Configure(BunkerFluidType.Gasoline,20,initial);
                spot.Interact(player);
                Assert(Mathf.Abs(tank.FuelLiters-initial)<.01f && fluid.IsEmpty,"Exact fuel transfer",results);
            }
            else spot.Interact(player);
            Assert(spot.IsInstalled,"Install "+type,results);
            yield return null;
        }
        Assert(assembly.IsAssemblyComplete,"Assembly complete through interactions",results);
        Assert(!boarding.CanInteract(),"Closed gate blocks boarding",results);
        var terminal=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b=>b.name=="Gate_Control_Terminal" && b is IGarageInteractable) as IGarageInteractable;
        terminal.Interact(player);
        yield return new WaitForSeconds(4f);
        Assert(GaragePrologueManager.Instance.IsGateOpen,"Terminal opens gate",results);
        Assert(boarding.CanInteract(),"Boarding unlocked",results);
        boarding.Interact(player);
        yield return new WaitForSeconds(3f);
        Assert(GarageDriveOutController.Instance.IsDriving,"Boarding interaction",results);
        File.WriteAllLines("Temp/GaragePolish/validation.txt",results);
        // Continue the independently instrumented two-scene transfer regression.
        GarageSceneExitValidation.Run();
    }
    static void Assert(bool value,string label,List<string> report)
    {
        report.Add((value?"PASS ":"FAIL ")+label);
        File.WriteAllLines("Temp/GaragePolish/validation.txt",report);
        if(!value) throw new InvalidOperationException(label);
    }
}
