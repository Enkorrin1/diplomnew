using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using RogueDrive.Gameplay.Hub;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class IsolatedGarageVerification {
 static IsolatedGarageVerification(){EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=FinishExit;} static void FinishExit(){if(!SessionState.GetBool("CohesionExitVerify",false)||!File.Exists("Temp/GarageExit/validation.txt"))return;SessionState.SetBool("CohesionExitVerify",false);Directory.CreateDirectory("VerificationResults/Exit");foreach(var f in Directory.GetFiles("Temp/GarageExit"))File.Copy(f,"VerificationResults/Exit/"+Path.GetFileName(f),true);EditorApplication.Exit(File.ReadAllText("Temp/GarageExit/validation.txt").Contains("PASS=True")?0:1);}
 public static void Start(){
  Directory.CreateDirectory("Temp/GarageCohesion");Directory.CreateDirectory("Temp/GaragePhysics");
  PlayerSettings.productName="RogueDrive Garage Isolated Verification";
  EditorSceneManager.OpenScene("Assets/Scenes/GarageScene.unity");
  GarageCohesionAuthoring.Refine();GarageCohesionAuthoring.Capture();
  SessionState.SetBool("CohesionVerify",true);
  EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/GarageScene.unity");
  EditorApplication.isPlaying=true;
 }
 static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("CohesionVerify",false)){
   SessionState.SetBool("CohesionVerify",false);EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<BunkerPrologueCutscene>().StartCoroutine(Check());
 }}
 static List<string> report=new List<string>();
 static void Check(bool value,string label){report.Add((value?"PASS ":"FAIL ")+label); Debug.Log(report.Last());File.WriteAllLines("Temp/GarageCohesion/validation.txt",report);}
 static IEnumerator Check(){
  Application.runInBackground=true;
  var intro=Object.FindFirstObjectByType<BunkerPrologueCutscene>();
  while(intro.IsRunning)yield return null;
  yield return new WaitForSeconds(8);
  var player=Object.FindFirstObjectByType<GaragePlayerController>();player.SetMovementLocked(true); var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=new Vector3(-3,.1f,-4);cc.enabled=true;player.PlayerCamera.transform.position=player.transform.position+Vector3.up*1.6f;player.PlayerCamera.transform.rotation=Quaternion.identity;Physics.SyncTransforms();
  var stage=UnityEngine.SceneManagement.SceneManager.GetSceneByName("Stage1_Outskirts");Check(stage.isLoaded,"Stage1 preloaded");Check(stage.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>()).All(r=>!r.enabled || !r.gameObject.activeInHierarchy),"Preloaded Stage1 has no active geometry in garage");Check(stage.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Collider>()).All(c=>!c.enabled || !c.gameObject.activeInHierarchy),"Preloaded Stage1 has no active collisions in garage"); var canon=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Canonical_")).ToArray();
  Check(canon.Length>=80,"80+ canonical visual instances survive Play Mode");
  foreach(var group in canon.GroupBy(t=>t.name)){
   var signatures=group.Select(t=>string.Join(";",t.GetComponentsInChildren<MeshFilter>().Select(m=>AssetDatabase.GetAssetPath(m.sharedMesh)+":"+m.sharedMesh.name).OrderBy(s=>s))).Distinct().Count();
   Check(signatures==1,"One mesh set for "+group.Key+" ("+group.Count()+")");
  }
  var below=canon.Where(t=>GarageCohesionAuthoring.BoundsOf(t.gameObject).max.y<-.05f).Select(t=>t.parent.name).ToArray();
  Check(below.Length==0,"No unified objects fell through floor: "+string.Join(",",below));
  var hands=PlayerHandsInventory.Instance;
  foreach(var kind in new[]{BunkerAssemblyItemType.Battery,BunkerAssemblyItemType.FuelCanister,BunkerAssemblyItemType.WaterCanister,BunkerAssemblyItemType.OilCanister}){
   var item=Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None).First(p=>p.ItemType==kind);
   var position=item.transform.position;var rotation=item.transform.rotation;var scale=item.transform.lossyScale;
   var fluid=item.GetComponent<FluidContainer>();float liters=fluid!=null?fluid.CurrentLiters:0;
   hands.HoldAssemblyItem(item);yield return null;
   Check(hands.HeldGameObject==item.gameObject,"Pick up "+kind);
   hands.DropItem(false,true);yield return new WaitForSeconds(.1f);
   Check(!hands.HasItem && !item.GetComponent<Rigidbody>().isKinematic,"Storage release restores physics "+kind);
   Check(item.GetComponentsInChildren<Collider>().Count(c=>c.enabled && !c.isTrigger)==1,"Single collider after dropping "+kind);
   Check(Vector3.Distance(scale,item.transform.lossyScale)<.001f,"World scale preserved "+kind);
   if(fluid!=null)Check(Mathf.Approximately(fluid.CurrentLiters,liters),"Fluid retained "+kind);
   item.transform.SetPositionAndRotation(position,rotation);var rb=item.GetComponent<Rigidbody>();rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
  }
  var wrench=Object.FindObjectsByType<GarageItemFunction>(FindObjectsSortMode.None).First(f=>f.Kind==GarageItemFunction.ItemKind.Wrench);
  var wp=wrench.GetComponent<PhysicsProp>();Check(PlayerPocketInventory.Instance.TryStorePhysical(wp),"Canonical wrench can be stored in pocket");
  Object.FindFirstObjectByType<GarageGeneratorSwitch>().Interact(player);
  yield return new WaitForSeconds(6);
  GarageCohesionAuthoring.Capture();
  ScreenCapture.CaptureScreenshot("Temp/GarageCohesion/play-screen.png");
  yield return new WaitForSeconds(1);
  report.Add("COMPLETE");File.WriteAllLines("Temp/GarageCohesion/validation.txt",report);
  Directory.CreateDirectory("VerificationResults"); foreach(var file in Directory.GetFiles("Temp/GarageDressing","*.png"))File.Copy(file,"VerificationResults/"+Path.GetFileName(file),true); File.Copy("Temp/GarageCohesion/validation.txt","VerificationResults/validation.txt",true); if(File.Exists("Temp/GarageCohesion/play-screen.png"))File.Copy("Temp/GarageCohesion/play-screen.png","VerificationResults/play-screen.png",true); SessionState.SetBool("CohesionExitVerify",true);GarageSceneExitValidation.Run();
 }
}




