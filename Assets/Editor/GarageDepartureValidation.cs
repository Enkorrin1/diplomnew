using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class GarageDepartureValidation
{
    static GarageDepartureValidation()
    {
        EditorApplication.playModeStateChanged += mode =>
        {
            if (mode == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("DepartureQA.Batch", false))
                EditorApplication.delayCall += Run;
        };
    }
    public static void Batch()
    {
        if (Application.dataPath.Contains(".codex-departure-qa")) { PlayerSettings.companyName = "RogueDriveValidation"; PlayerSettings.productName = "DepartureQA"; }
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GarageScene.unity");
        if (GameObject.Find("Garage_Departure") == null) GarageDepartureAuthoring.Author();
        SessionState.SetBool("DepartureQA.Batch", true);
        EditorApplication.isPlaying = true;
    }
    public static void Run()
    {
        if (!Application.isPlaying || GarageDeparturePreparation.Instance == null) throw new InvalidOperationException("Play the authored GarageScene.");
        var runner = new GameObject("Departure_Validation").AddComponent<DepartureValidationRunner>();
        Object.DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(Verify(runner));
    }
    public static void RunPresentation()
    {
        if (!Application.isPlaying || GarageDeparturePreparation.Instance == null) throw new InvalidOperationException("Play GarageScene first.");
        var runner = new GameObject("Presentation_Validation").AddComponent<DepartureValidationRunner>();
        Object.DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(Verify(runner, true));
    }
    public static void BuildWindows()
    {
        var scenes = new[] { "Assets/Scenes/GarageScene.unity" }.Concat(EditorBuildSettings.scenes.Where(s=>s.enabled && s.path!="Assets/Scenes/GarageScene.unity").Select(s=>s.path)).ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=scenes, locationPathName="Builds/Departure/RogueDrive.exe", target=BuildTarget.StandaloneWindows64, options=BuildOptions.Development });
        Directory.CreateDirectory("Logs/DepartureQA");
        File.WriteAllText("Logs/DepartureQA/build.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; warnings="+report.summary.totalWarnings);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Departure Windows build failed.");
    }
    static readonly List<string> results = new List<string>();
    static void Check(bool condition, string name)
    {
        results.Add((condition ? "PASS " : "FAIL ") + name);
        Debug.Log("[DepartureQA] " + results[results.Count-1]);
        Directory.CreateDirectory("Logs/DepartureQA"); File.WriteAllLines("Logs/DepartureQA/validation.txt", results);
        if (!condition) throw new InvalidOperationException("Departure validation: " + name);
    }
    static IEnumerator Verify(DepartureValidationRunner runner, bool presentationOnly = false)
    {
        results.Clear();
        yield return null;
        var intro = Object.FindFirstObjectByType<BunkerPrologueCutscene>();
        float deadline = Time.realtimeSinceStartup + 30;
        while (intro != null && intro.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
        var player = Object.FindFirstObjectByType<GaragePlayerController>();
        var pocket = PlayerPocketInventory.Instance;
        var hands = PlayerHandsInventory.Instance;
        var trunk = VehicleCargoTrunk.Instance;
        var prep = GarageDeparturePreparation.Instance;
        var tank = VehicleModularState.Instance;
        Check(!prep.CanInteract(), "Briefing cannot interrupt assembly");
        Check(trunk.ItemCount == 0, "Fresh trunk is empty; no invisible free supplies");
        var generator = Object.FindFirstObjectByType<GarageGeneratorSwitch>();
        generator.Interact(player);
        Check(generator.IsStarting && !generator.CanInteract(), "Generator prevents overlapping starts");
        generator.Interact(player);
        Check(!GaragePrologueManager.Instance.IsPowerOn,"Power waits for diesel startup");
        yield return new WaitForSeconds(6f);
        Check(GaragePrologueManager.Instance.IsPowerOn && !generator.IsStarting,"Diesel reaches powered state");
        var presentation=GaragePresentationDirector.Instance;
        if(presentation != null) Check(presentation.generatorLoop.isPlaying,"Diesel idle loop starts");
        Object.FindFirstObjectByType<GarageCarKeys>().Interact(player);
        foreach (var kind in new[] {GarageItemFunction.ItemKind.Wrench, GarageItemFunction.ItemKind.Screwdriver})
        {
            var tool = Object.FindObjectsByType<GarageItemFunction>(FindObjectsSortMode.None).First(f => f.Kind == kind && f.GetComponent<PhysicsProp>() != null);
            tool.GetComponent<PhysicsProp>().Interact(player);
            Check(pocket.HasTool(kind), "Tool acquired: " + kind);
        }
        foreach(var type in new[]{BunkerAssemblyItemType.Wheel,BunkerAssemblyItemType.Battery,BunkerAssemblyItemType.FuelCanister})
        {
            var item=Object.FindObjectsByType<CarPartItem>(FindObjectsSortMode.None).First(i=>i.ItemType==type && !i.name.StartsWith("Departure_") &&
                (type!=BunkerAssemblyItemType.FuelCanister || (i.GetComponent<FluidContainer>()!=null && i.GetComponent<FluidContainer>().FluidType==BunkerFluidType.Gasoline && !i.GetComponent<FluidContainer>().IsEmpty)));
            item.Interact(player);
            var spot=Object.FindObjectsByType<VehiclePartHotspot>(FindObjectsSortMode.None).First(s=>s.RequiredItem==type && !s.IsInstalled);
            if (type == BunkerAssemblyItemType.Battery)
            {
                spot.Interact(player);
                var ui = RogueDrive.UI.VehicleDashboardPanelsUI.Instance;
                ui.SelectServiceItem(new ServiceItemAddress(2,0)); ui.DropServiceItem(VehicleServiceSlot.Battery);
                Check(spot.HasPlacedBattery && !spot.IsInstalled && !BunkerStarterCarAssembly.Instance.IsBatteryInstalled, "Battery must be secured after placement");
                var toolAddress = VehicleServiceInventory.Sources().First(a=>a.Read().WorldObject!=null && a.Read().WorldObject.GetComponent<GarageItemFunction>()?.Kind==GarageItemFunction.ItemKind.Screwdriver);
                ui.SelectServiceItem(toolAddress);ui.DropServiceItem(VehicleServiceSlot.Battery);
                Check(!spot.IsInstalled,"Dragging screwdriver alone does not fasten terminals");
                ui.ClickServiceSlot(VehicleServiceSlot.Battery);ui.ClosePanel();
            }
            else spot.Interact(player);
            Check(spot.IsInstalled,"Assembly through interaction: "+type);
            yield return null;
        }
        hands.DropItem();
        Check(prep.CanInteract(), "Briefing available after preparation");
        Check(!Object.FindFirstObjectByType<GarageVehicleBoarding>().CanInteract(),"Departure blocked before supplies");
        prep.Interact(player);
        yield return new WaitForSecondsRealtime(1f);
        Check(prep.IsRunning && player.IsMovementLocked,"Cinematic locks player and runs");
        Capture(player.PlayerCamera,"Logs/DepartureQA/01-route.png");
        yield return new WaitForSecondsRealtime(5f);
        Capture(player.PlayerCamera,"Logs/DepartureQA/02-storm.png");
        yield return new WaitForSecondsRealtime(5f);
        Capture(player.PlayerCamera,"Logs/DepartureQA/03-supplies.png");
        while(prep.IsRunning)yield return null;
        Check(prep.BriefingSeen && !player.IsMovementLocked,"Cinematic returns view and controls");
        prep.Interact(player);yield return null;prep.SkipBriefing();
        Check(prep.BriefingSeen && !prep.IsRunning && !player.IsMovementLocked,"Repeated briefing can be skipped safely");
        var fuel=GameObject.Find("Departure_FuelReserve").GetComponent<CarPartItem>();
        fuel.Interact(player);
        Check(trunk.StoreFromPlayer(hands,pocket,out _),"Physical reserve loaded into trunk");
        var repair=GameObject.Find("Departure_RepairKit").GetComponent<PhysicsProp>();
        repair.Interact(player);
        int repairSlot=Enumerable.Range(0,PlayerPocketInventory.SlotCount).First(i=>pocket.GetSlot(i).worldItem==repair);
        var repairStack=pocket.GetSlot(repairSlot);var destination=trunk.GetSlot(1);
        Check(InventoryStackOps.Move(ref repairStack,ref destination,1)==1,"Repair kit moves through inventory stack operation");
        pocket.SetGridSlot(repairSlot,repairStack);trunk.SetGridSlot(1,destination);
        prep.RefreshCargo();Check(prep.Ready,"Both required supplies unlock readiness");
        var savedFuel=trunk.GetSlot(0);trunk.SetGridSlot(0,PocketSlotData.Empty);prep.RefreshCargo();
        Check(!prep.Ready,"Removing mandatory cargo blocks readiness again");
        trunk.SetGridSlot(0,savedFuel);prep.RefreshCargo();
        var med=GameObject.Find("Departure_Medkit").GetComponent<PhysicsProp>();med.Interact(player);
        Check(pocket.HasTool(GarageItemFunction.ItemKind.Medkit),"Optional medical supply selected");
        Check(GameObject.Find("Departure_WaterReserve")!=null,"Unchosen water remains on table");
        Object.FindFirstObjectByType<GarageGateSwitchInteractable>().Interact(player);
        yield return new WaitForSeconds(3f);
        Check(GaragePrologueManager.Instance.IsGateOpen,"Ready departure opens gates");
        // Visual HUD check from a useful walking position.
        var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=new Vector3(0,.1f,-7.5f);player.transform.rotation=Quaternion.identity;player.SetCameraPitch(0);cc.enabled=true;
        yield return new WaitForSeconds(.2f);Capture(player.PlayerCamera,"Logs/DepartureQA/04-garage.png");yield return new WaitForSeconds(.3f);
        float fuelAtDeparture=tank.FuelLiters;
        Object.FindFirstObjectByType<GarageVehicleBoarding>().Interact(player);
        yield return new WaitForSeconds(3f);
        Check(GarageDepartureCheckpoint.HasPrepared,"Prepared snapshot captured at boarding");
        var flow=GarageSceneExitCinematic.Pending;
        deadline=Time.realtimeSinceStartup+30;while(!flow.IsReady && Time.realtimeSinceStartup<deadline)yield return null;
        Check(flow.TryBegin(Object.FindFirstObjectByType<GarageDriveOutVehicle>()),"Existing exit cinematic accepts prepared vehicle");
        deadline=Time.realtimeSinceStartup+35;while(!flow.Completed && Time.realtimeSinceStartup<deadline)yield return null;
        Check(flow.Completed && SceneManager.GetActiveScene().name=="Stage1_Outskirts","Prepared car arrives in first stage");
        Check(VehicleCargoTrunk.Instance.GetSlot(0).WorldObject.GetComponent<FluidContainer>().CurrentLiters==10,"Reserve volume survives scene transfer");
        if(presentationOnly)
        {
            results.Add("COMPLETE — presentation and departure");
            File.WriteAllLines("Logs/DepartureQA/presentation-validation.txt",results);
            Object.Destroy(runner.gameObject);
            yield break;
        }
        tank.SetFuelForGaragePreparation(1);tank.DamageTire(0,.8f);
        VehicleCargoTrunk.Instance.ClearCargo();
        var run=Object.FindFirstObjectByType<GameRunController>();
        int coinsBefore=RogueDrive.Meta.SaveService.GetActiveProgress().Coins;
        run.AddCoins(31);run.TakeDamage(100000);
        Check(RogueDrive.Meta.SaveService.GetActiveProgress().Coins==coinsBefore,"Failed attempt does not bank rewards");
        run.Restart();
        deadline=Time.realtimeSinceStartup+50;
        while(Time.realtimeSinceStartup<deadline)
        {
            if(SceneManager.GetActiveScene().name=="Stage1_Outskirts" && !GarageDepartureCheckpoint.Restoring && !GarageDepartureCheckpoint.HasPendingRestore && GarageSceneExitCinematic.Pending==null)break;
            yield return null;
        }
        Check(SceneManager.GetActiveScene().name=="Stage1_Outskirts" && !GarageDepartureCheckpoint.Restoring && GarageSceneExitCinematic.Pending==null,"Retry returns to road without replaying preparation");
        Check(string.IsNullOrEmpty(GarageDepartureCheckpoint.LastError),"Checkpoint reload has no missing identities");
        Check(Mathf.Abs(VehicleModularState.Instance.FuelLiters-fuelAtDeparture)<2,"Retry restores fuel");
        Check(VehicleModularState.Instance.GetTireIntegrity(0)>.99f,"Retry restores damaged tire");
        Check(VehicleCargoTrunk.Instance.ItemCount==2,"Retry restores exact cargo count without duplication");
        Check(VehicleCargoTrunk.Instance.GetSlot(0).WorldObject.GetComponent<FluidContainer>().CurrentLiters==10,"Retry restores remaining liquid volume");
        Check(PlayerPocketInventory.Instance.HasTool(GarageItemFunction.ItemKind.Medkit),"Retry restores selected optional supply");
        Object.FindFirstObjectByType<GameRunController>().LoadGarage();
        yield return new WaitForSeconds(2);
        Check(SceneManager.GetActiveScene().name=="GarageScene" && BunkerStarterCarAssembly.Instance.IsAssemblyComplete,"Change preparation preserves assembled car");
        Check(VehicleCargoTrunk.Instance.ItemCount==0,"Change preparation restores pre-choice cargo");
        Check(GameObject.Find("Departure_Medkit")!=null && GameObject.Find("Departure_FuelReserve")!=null,"Choice supplies return to the table");
        Check(!Object.FindFirstObjectByType<BunkerPrologueCutscene>().IsRunning,"Checkpoint skips awakening");
        results.Add("COMPLETE");File.WriteAllLines("Logs/DepartureQA/validation.txt",results);
        if (Application.isBatchMode) EditorApplication.Exit(0);
        else EditorApplication.isPaused=true;
    }
    static void Capture(Camera camera,string path)
    {
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.enabled && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1920,1080,24);var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.4f;}
            camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(texture);
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
        }
    }
}
public sealed class DepartureValidationRunner : MonoBehaviour { }
