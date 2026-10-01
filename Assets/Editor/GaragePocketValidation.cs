using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using Object=UnityEngine.Object;
public static class GaragePocketValidation
{
    static List<string> report;
    static void Check(bool ok,string title){report.Add((ok?"PASS ":"FAIL ")+title);File.WriteAllLines("Temp/GaragePhysics/pockets.txt",report);if(!ok)throw new Exception(title);}
    public static void Run(){report=new List<string>();Object.FindFirstObjectByType<BunkerPrologueCutscene>().StartCoroutine(Routine());}
    static IEnumerator Routine()
    {
        var intro=Object.FindFirstObjectByType<BunkerPrologueCutscene>();while(intro.IsRunning)yield return null;
        var player=Object.FindFirstObjectByType<GaragePlayerController>();var inv=PlayerPocketInventory.Instance;var hands=PlayerHandsInventory.Instance;
        var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=new Vector3(4,.1f,3);cc.enabled=true;
        player.transform.rotation=Quaternion.identity;player.SetCameraPitch(0);Physics.SyncTransforms();
        var all=Object.FindObjectsByType<PhysicsProp>(FindObjectsSortMode.None).Where(p=>p.PocketSized).ToArray();
        Check(all.Length==60,"60 small props routed to slots");
        var lamp=all.First(p=>p.GetComponent<GarageItemUse>()!=null && p.GetComponent<GarageItemUse>().Kind==GarageItemUse.UseKind.Flashlight);
        var scale=lamp.transform.lossyScale;lamp.Interact(player);yield return null;
        Check(inv.ActivePhysical==lamp && !hands.HasItem,"Small pickup occupies slot and keeps large carry free");
        Check(inv.UseActivePhysical() && lamp.GetComponent<GarageItemUse>().LightOn,"Selected pocket flashlight functions");
        inv.SelectSlot(1);yield return null;
        Check(!lamp.gameObject.activeSelf && inv.GetSlot(0).worldItem==lamp,"Switching slot hides model but retains object");
        inv.SelectSlot(0);yield return null;
        Check(lamp.gameObject.activeSelf && lamp.GetComponent<GarageItemUse>().LightOn,"Reselect restores same flashlight state");
        foreach(var prop in all.Where(p=>p!=lamp).Take(4))prop.Interact(player);
        yield return null;
        var extra=all.First(p=>!Enumerable.Range(0,5).Any(i=>inv.GetSlot(i).worldItem==p));
        extra.Interact(player);
        Check(extra.gameObject.activeSelf && extra.GetComponent<Collider>().enabled && !extra.IsHeld,"Full slots leave extra item physical in world");
        Check(Enumerable.Range(0,5).All(i=>!inv.GetSlot(i).IsEmpty),"All five slots represented");
        inv.SelectSlot(0);yield return null;
        ScreenCapture.CaptureScreenshot("Temp/GaragePhysics/pocket-hud.png");yield return null;
        Check(inv.DropActivePhysical(false),"Drop selected pocket object");yield return new WaitForSeconds(.3f);
        Check(inv.GetSlot(0).IsEmpty && lamp.GetComponent<Collider>().enabled && !lamp.GetComponent<Rigidbody>().isKinematic,"Drop frees slot and restores physics");
        Check(Vector3.Distance(lamp.transform.lossyScale,scale)<.001f,"Drop preserves original scale");
        var box=Object.FindObjectsByType<PhysicsProp>(FindObjectsSortMode.None).First(p=>!p.PocketSized && p.GetComponent<GaragePortableContainer>()!=null);
        box.Interact(player);yield return null;lamp.Interact(player);yield return null;
        Check(hands.HeldGameObject==box.gameObject && inv.GetSlot(0).worldItem==lamp,"Small pickup works while carrying a large object");
        Check(!lamp.gameObject.activeSelf,"Large carry hides selected small model");
        hands.DropItem(forStorage:true);box.gameObject.SetActive(false);yield return null;
        Check(lamp.gameObject.activeSelf && inv.ActivePhysical==lamp,"Small selected item returns after large carry ends");
        yield return new WaitForSeconds(.6f);
        Check(inv.UseActivePhysical() && !lamp.GetComponent<GarageItemUse>().LightOn,"Pocket flashlight toggles after drop and pickup");
        Check(inv.DropActivePhysical(true),"Throw from selected slot");yield return null;
        Check(lamp.GetComponent<Rigidbody>().linearVelocity.magnitude>3 && inv.GetSlot(0).IsEmpty,"Throw frees slot with physical velocity");
        File.WriteAllLines("Temp/GaragePhysics/pockets.txt",report);
    }
}
