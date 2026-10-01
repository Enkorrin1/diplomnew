using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
public static class GaragePhysicsValidation
{
    static List<string> report;
    static void Check(bool ok,string label) { report.Add((ok?"PASS ":"FAIL ")+label); File.WriteAllLines("Temp/GaragePhysics/validation.txt",report); if(!ok)throw new Exception(label); }
    public static void Run() { report=new List<string>(); Object.FindFirstObjectByType<BunkerPrologueCutscene>().StartCoroutine(Routine()); }
    static void Move(GaragePlayerController p,Vector3 pos,Vector3 aim)
    {
        var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.position=pos;cc.enabled=true;
        p.PlayerCamera.transform.position=pos+Vector3.up*1.6f;p.PlayerCamera.transform.LookAt(aim);
        Physics.SyncTransforms();
    }
    static IEnumerator Routine()
    {
        var intro=Object.FindFirstObjectByType<BunkerPrologueCutscene>();while(intro.IsRunning)yield return null;
        var player=Object.FindFirstObjectByType<GaragePlayerController>();player.SetMovementLocked(true);
        var hands=PlayerHandsInventory.Instance;
        var sofa=GameObject.Find("sofa").GetComponentInChildren<MeshCollider>();
        Check(sofa!=null && sofa.enabled && !sofa.isTrigger,"Sofa has solid mesh collision");
        var eye=GameObject.Find("Eyes_Lying").transform;var stand=GameObject.Find("Standing_Clearance").transform;
        Check(Vector3.Distance(player.transform.position,stand.position)<.5f,"Awakening ends beside red sofa");
        Check(sofa.Raycast(new Ray(sofa.bounds.center+Vector3.up*2,Vector3.down),out var seat,5),"Sofa seat supports collision from above");
        var bodies=GameObject.Find("Garage_PhysicalItems").GetComponentsInChildren<Rigidbody>();
        yield return new WaitForSeconds(5);
        Check(bodies.Length>=120,"At least 120 physical props");
        Check(bodies.All(b=>b.position.y>-.2f),"No props fell through floor after settling");
        Check(bodies.Count(b=>b.linearVelocity.magnitude>.3f)<5,"Props settle without perpetual motion");
        Move(player,new Vector3(4,.1f,3),new Vector3(4,1,6));
        var lamp=Object.FindObjectsByType<GarageItemUse>(FindObjectsSortMode.None).First(u=>u.Kind==GarageItemUse.UseKind.Flashlight);
        var originalScale=lamp.transform.lossyScale;
        hands.HoldPhysicsProp(lamp.GetComponent<PhysicsProp>());yield return null;
        Check(hands.HeldGameObject==lamp.gameObject && !lamp.GetComponent<Collider>().enabled,"Pickup disables physical collision while held");
        Check(lamp.Use(player) && lamp.LightOn,"Flashlight switches on");
        ScreenCapture.CaptureScreenshot("Temp/GaragePhysics/flashlight.png");
        yield return new WaitForSeconds(.6f);
        Check(lamp.Use(player) && !lamp.LightOn,"Flashlight switches off");
        hands.DropItem();yield return new WaitForSeconds(.2f);
        Check(!hands.HasItem && !lamp.GetComponent<Rigidbody>().isKinematic && lamp.GetComponent<Collider>().enabled,"Drop restores physics");
        Check(Vector3.Distance(lamp.transform.lossyScale,originalScale)<.001f,"Drop restores original world scale");
        hands.HoldPhysicsProp(lamp.GetComponent<PhysicsProp>());yield return null;
        hands.DropItem(true);yield return null;
        Check(!hands.HasItem && lamp.GetComponent<Rigidbody>().linearVelocity.magnitude>3,"Throw imparts velocity");
        var box=Object.FindFirstObjectByType<GaragePortableContainer>();
        hands.HoldPhysicsProp(lamp.GetComponent<PhysicsProp>());yield return null;
        Check(box.Store(hands) && !hands.HasItem && box.Count==1 && !lamp.gameObject.activeSelf,"Box stores same real object");
        hands.HoldPhysicsProp(box.GetComponent<PhysicsProp>());yield return null;
        Check(lamp.transform.IsChildOf(box.transform),"Contents travel with carried box");
        hands.DropItem();yield return null;
        Check(!hands.HasItem && box.Take(hands) && PlayerPocketInventory.Instance.ActivePhysical==lamp.GetComponent<PhysicsProp>() && box.Count==0,"Retrieve preserves object identity in pocket slot");
        Move(player,new Vector3(5,.1f,6),new Vector3(5,1,9));yield return null;
        PlayerPocketInventory.Instance.DropActivePhysical(false);yield return null;
        Check(!hands.HasItem && Vector3.Distance(lamp.transform.lossyScale,originalScale)<.001f,"Storage and repeated pickup do not shrink item");
        var car=Object.FindFirstObjectByType<VehicleModularState>();
        var carPos=car.GetComponentsInChildren<Collider>().First(c=>!c.isTrigger).bounds.center;
        Move(player,carPos+new Vector3(0,.1f,-2.5f),carPos);
        var serialized=new SerializedObject(car);serialized.FindProperty("bumperIntegrity").floatValue=.4f;serialized.ApplyModifiedPropertiesWithoutUndo();
        var repair=Object.FindObjectsByType<GarageItemUse>(FindObjectsSortMode.None).First(u=>u.Kind==GarageItemUse.UseKind.Repair);
        hands.HoldPhysicsProp(repair.GetComponent<PhysicsProp>());yield return null;
        Check(repair.Use(player) && car.BumperIntegrity>.4f,"Tool repairs damaged bumper");
        Move(player,new Vector3(6,.1f,4),new Vector3(6,1,7));yield return null;hands.DropItem();yield return null;
        Check(!hands.HasItem,"Tool can be put down after use");
        var ray=player.GetComponentInChildren<GarageInteractionRaycaster>();
        var spot=Object.FindObjectsByType<VehiclePartHotspot>(FindObjectsSortMode.None).First(s=>s.RequiredItem==BunkerAssemblyItemType.FuelCanister);
        spot.RestoreInstalledState(true);car.SetFuelForGaragePreparation(47);
        var fuel=Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None).First(i=>i.ItemType==BunkerAssemblyItemType.FuelCanister && i.GetComponent<GarageItemUse>()!=null);
        var liquid=fuel.GetComponent<FluidContainer>();liquid.Configure(BunkerFluidType.Gasoline,20,10);
        hands.HoldAssemblyItem(fuel);yield return null;
        Aim(ray,spot);
        Check(fuel.GetComponent<GarageItemUse>().Use(player) && Mathf.Abs(car.FuelLiters-50)<.01f && Mathf.Abs(liquid.CurrentLiters-7)<.01f,"Refill respects capacity and conserves gasoline");
        yield return new WaitForSeconds(.6f);
        Aim(ray,spot);
        Check(!fuel.GetComponent<GarageItemUse>().Use(player) && Mathf.Abs(liquid.CurrentLiters-7)<.01f,"Full tank does not consume gasoline");
        hands.DropItem(forStorage:true);fuel.gameObject.SetActive(false);
        var water=Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None).First(i=>i.ItemType==BunkerAssemblyItemType.WaterCanister);
        hands.HoldAssemblyItem(water);yield return null;
        var waterFluid=water.GetComponent<FluidContainer>();float before=waterFluid.CurrentLiters;
        Aim(ray,spot);
        Check(!water.GetComponent<GarageItemUse>().Use(player) && waterFluid.CurrentLiters==before,"Water rejected at fuel inlet without loss");
        yield return new WaitForSeconds(.6f);
        var engine=Object.FindObjectsByType<VehiclePartHotspot>(FindObjectsSortMode.None).First(s=>s.RequiredItem==BunkerAssemblyItemType.Battery);
        serialized.Update();serialized.FindProperty("currentRadiatorWater").floatValue=7;serialized.ApplyModifiedPropertiesWithoutUndo();
        Aim(ray,engine);
        Check(water.GetComponent<GarageItemUse>().Use(player) && Mathf.Abs(car.RadiatorWater-10)<.01f && Mathf.Abs(waterFluid.CurrentLiters-(before-3))<.01f,"Water refills radiator and conserves remaining liquid");
        hands.DropItem(forStorage:true);water.gameObject.SetActive(false);
        var wheel=Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None).First(i=>i.ItemType==BunkerAssemblyItemType.Wheel);
        hands.HoldAssemblyItem(wheel);yield return null;car.DamageTire(2,.7f);Aim(ray,engine);
        Check(wheel.GetComponent<GarageItemUse>().Use(player) && car.GetTireIntegrity(2)==1 && !hands.HasItem,"Spare wheel repairs damaged tire and is consumed");
        player.SetMovementLocked(false);
        File.WriteAllLines("Temp/GaragePhysics/validation.txt",report);
        ScreenCapture.CaptureScreenshot("Temp/GaragePhysics/complete.png");
    }
    static void Aim(GarageInteractionRaycaster ray,IGarageInteractable target)
    {
        typeof(GarageInteractionRaycaster).GetField("currentTarget",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ray,target);
        typeof(GarageInteractionRaycaster).GetField("targetConfirmed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ray,true);
    }
}


