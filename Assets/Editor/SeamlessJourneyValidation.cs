using System;
using System.Collections;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    [InitializeOnLoad]
    public static class SeamlessJourneyValidation
    {
        public const string Output = "Artifacts/SeamlessJourney";
        static SeamlessJourneyValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Seamless.Validate", false)) return;
                SessionState.SetBool("Seamless.Validate", false);
                new GameObject("Seamless_Validation_Runner").AddComponent<SeamlessJourneyValidationRunner>();
            };
        }

        public static void StartPlayValidation()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/playmode.txt", "STARTING\n");
            EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");
            SessionState.SetBool("Seamless.Validate", true); EditorApplication.isPlaying = true;
        }

        public static string ValidateGeometry()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode only.");
            var data = AssetDatabase.LoadAssetAtPath<JourneyStreamCatalog>("Assets/Content/SeamlessJourney/Campaign.asset");
            if(data==null)throw new InvalidOperationException("Bake streaming worlds first.");
            data.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            Directory.CreateDirectory(Output); string report="";
            try
            {
                for(int i=0;i<3;i++)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                    var a=EditorSceneManager.OpenScene(data.segments[i].scenePath,OpenSceneMode.Additive);
                    var b=EditorSceneManager.OpenScene(data.segments[i+1].scenePath,OpenSceneMode.Additive);
                    foreach(var scene in new[]{a,b})foreach(var root in scene.GetRootGameObjects())
                    {var marker=root.GetComponent<JourneyStreamWorld>();if(marker!=null)marker.World.SetActive(true);}
                    var probe=new GameObject("Route_Probe").AddComponent<StageRoute>();probe.Configure(data.points);Physics.SyncTransforms();
                    int rays=0,misses=0,blocked=0;
                    for(float d=data.segments[i].roadEndDistance-60;d<=data.segments[i+1].startDistance+100;d+=2)
                    {
                        probe.Evaluate(d,out var p,out var q);
                        foreach(float x in new[]{-5f,0,5f})
                        {rays++;var hits=Physics.RaycastAll(p+q*Vector3.right*x+Vector3.up*1.5f,Vector3.down,2.5f,~0,QueryTriggerInteraction.Ignore);
                         if(!hits.Any(h=>Mathf.Abs(h.point.y-p.y)<.25f))misses++;}
                        if(Physics.OverlapBox(p+Vector3.up*1.8f,new Vector3(2.5f,1.2f,1.2f),q,~0,QueryTriggerInteraction.Ignore).Length>0)blocked++;
                    }
                    string line=$"Join {i+1}->{i+2}: rays={rays}, missing={misses}, blocked={blocked}\n";report+=line;
                    if(misses>0||blocked>0)throw new InvalidOperationException(line);
                    EditorSceneManager.CloseScene(a,true);EditorSceneManager.CloseScene(b,true);Object.DestroyImmediate(probe.gameObject);
                    EditorUtility.UnloadUnusedAssetsImmediate();
                }
                report+="PASS: all three connectors have continuous collision and vehicle clearance.\n";
                return report;
            }
            finally
            {
                data.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
                File.WriteAllText(Output+"/geometry.txt",report);
                EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");
            }
        }
    }

    public sealed class SeamlessJourneyValidationRunner : MonoBehaviour
    {
        SeamlessJourneyStream stream;
        ArcadeCarController car;
        GarageDriveOutVehicle driver;
        Rigidbody body;
        GameRunController run;
        VehicleCargoTrunk cargo;
        VehicleModularState vehicle;
        Camera camera;
        int carId,runId,cameraId;
        GameObject storedItem;
        string storedId,destroyedId;
        int slot;
        bool failed;

        IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            yield return new WaitForSecondsRealtime(2);
            stream=SeamlessJourneyStream.Instance;car=Object.FindFirstObjectByType<ArcadeCarController>();
            if(!Check(stream!=null&&car!=null,"Stream and car ready"))yield break;
            driver=car.GetComponent<GarageDriveOutVehicle>();body=car.GetComponent<Rigidbody>();
            run=Object.FindFirstObjectByType<GameRunController>();vehicle=car.GetComponent<VehicleModularState>();
            cargo=car.GetComponent<VehicleCargoTrunk>();camera=Camera.main;
            carId=car.GetInstanceID();runId=run.GetInstanceID();cameraId=camera.GetInstanceID();
            if(!Check(driver!=null&&cargo!=null&&vehicle!=null,"Original garage driving/inventory rig"))yield break;
            driver.ExternalInput=true;driver.EnableDriving();
            var items=Object.FindObjectsByType<JourneyPersistentObject>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(p=>p.GetComponent<PhysicsProp>()!=null).ToArray();
            slot=Enumerable.Range(0,cargo.MaxSlots).FirstOrDefault(i=>cargo.GetSlot(i).IsEmpty);
            if(!Check(items.Length>1&&cargo.GetSlot(slot).IsEmpty,"World loot and free cargo slot"))yield break;
            storedItem=items[0].gameObject;storedId=items[0].Id;destroyedId=items[1].Id;
            cargo.SetGridSlot(slot,InventoryStackOps.FromObject(storedItem,storedItem.transform.lossyScale));
            Object.Destroy(items[1].gameObject);
            run.AddCoins(17);
            float health=run.Health;

            for(int boundary=1;boundary<=3;boundary++)
            {
                float d=stream.Catalog.segments[boundary].startDistance;
                HoldAt(d-2100);
                yield return WaitForWorld(boundary);
                if(failed)yield break;
                yield return DriveAcross(d,false,boundary);
                if(failed)yield break;
                Check(run.CurrentStageIndex==boundary+1,"Stage index advanced without victory");
                Check(!run.IsGameOver&&!run.IsStageVictory&&run.CoinsCollected==17,"Run, coins and progression preserved");
                Check(Mathf.Abs(run.Health-health)<.1f,"Health preserved");
                Check(cargo.GetPhysicalItem(slot)==storedItem,"Same physical cargo instance");
                if(boundary>=2)Check(!stream.IsWorldLoaded(0),"First world unloaded behind the car");
                if(failed)yield break;
            }
            Check(Object.FindObjectsByType<StageFinishOutpost>(FindObjectsSortMode.None).Any(f=>f.StageIndex==4&&f.enabled),"Final finish remains active");
            run.ReportStageCompleted(2);Check(!run.IsGameOver,"Intermediate completion cannot end continuous run");

            // Return after unloading: no duplicated loot, car, camera, run or consumed objects.
            float first=stream.Catalog.segments[1].startDistance;
            HoldAt(first+1900);yield return WaitForWorld(0);yield return WaitForWorld(1);
            if(failed)yield break;
            yield return DriveAcross(first,true,0);
            yield return null;
            var reloaded=Object.FindObjectsByType<JourneyPersistentObject>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Check(reloaded.Count(p=>p.Id==storedId)==1&&cargo.GetPhysicalItem(slot)==storedItem,"Reload preserves collected item without respawn");
            Check(reloaded.All(p=>p.Id!=destroyedId),"Consumed/destroyed object stays removed after reload");
            Check(run.CurrentStageIndex==1,"Reverse crossing restores sector index, without resetting run");
            Check(Object.FindObjectsByType<ArcadeCarController>(FindObjectsSortMode.None).Length==1,"One active vehicle");
            Check(Object.FindObjectsByType<GameRunController>(FindObjectsSortMode.None).Length==1,"One run controller");
            Log(failed?"FAILED":"PASS: 3 forward crossings, backward reload, same actors/resources/cargo, no intermediate finish.");
            driver.SetExternalInput(0,0,true);driver.ExternalInput=false;
            EditorApplication.isPaused=true;
        }

        void HoldAt(float distance)
        {
            driver.CinematicControl=true;body.isKinematic=true;
            stream.Route.Evaluate(distance,out var p,out var q);car.transform.SetPositionAndRotation(p+Vector3.up*.25f,q);body.position=car.transform.position;body.rotation=q;
        }

        IEnumerator WaitForWorld(int index)
        {
            float deadline=Time.realtimeSinceStartup+60;
            while(!stream.IsWorldLoaded(index)&&Time.realtimeSinceStartup<deadline&&!failed)yield return null;
            Check(stream.IsWorldLoaded(index),"World "+(index+1)+" loaded ahead");
        }

        IEnumerator DriveAcross(float boundary,bool reverse,int index)
        {
            float start=boundary+(reverse?45:-45);stream.Route.Evaluate(start,out var p,out var q);
            if(reverse)q*=Quaternion.Euler(0,180,0);
            body.isKinematic=false;driver.CinematicControl=false;car.PlaceAtStart(p+Vector3.up*.25f,q);
            body.linearVelocity=q*Vector3.forward*12;driver.SetExternalInput(.65f,0,false);
            float fuel=vehicle.FuelLiters,water=vehicle.RadiatorWater;
            float deadline=Time.realtimeSinceStartup+20;bool crossed=false;float maxFrame=0;
            var previous=car.transform.position;
            while(Time.realtimeSinceStartup<deadline&&!failed)
            {
                yield return null;
                float movement=Vector3.Distance(previous,car.transform.position);maxFrame=Mathf.Max(maxFrame,movement);previous=car.transform.position;
                if(!Check(movement<Mathf.Max(3,50*Time.deltaTime),"",false))yield break;
                float progress=stream.Route.ProjectDistance(car.transform.position);
                if(reverse?progress<boundary-20:progress>boundary+20){crossed=true;break;}
            }
            driver.SetExternalInput(0,0,true);
            Check(crossed,"Physical crossing "+(reverse?"back to 1":(index+1).ToString())+"; max frame displacement="+maxFrame.ToString("F2"));
            Check(car.GetInstanceID()==carId&&run.GetInstanceID()==runId&&camera.GetInstanceID()==cameraId,"Actor and camera identities unchanged");
            Check(vehicle.FuelLiters<=fuel+.05f&&vehicle.FuelLiters>fuel-3,"Fuel retained with normal consumption");
            Check(vehicle.RadiatorWater<=water+.05f&&vehicle.RadiatorWater>water-1,"Coolant retained");
            Check(Time.timeScale==1,"No transition pause or slow motion");
            string shot=RemainingRoutesValidation.Capture(boundary+(reverse?-10:10),"Seamless_"+(reverse?"reverse":"join"+index));
            File.Copy(shot,SeamlessJourneyValidation.Output+"/"+(reverse?"reverse":"join"+index)+".png",true);
            Log("Worlds loaded: "+stream.LoadedWorldCount+", global progress "+stream.Route.Progress.ToString("F1"));
        }

        bool Check(bool condition,string message,bool log=true)
        {
            if(log||!condition)Log((condition?"OK ":"FAIL ")+(string.IsNullOrEmpty(message)?"No spatial jump":message));
            if(!condition){failed=true;EditorApplication.isPaused=true;}return condition;
        }
        void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Exception||type==LogType.Error){failed=true;Log("RUNTIME ERROR: "+message);}
        }
        void Log(string message)=>File.AppendAllText(SeamlessJourneyValidation.Output+"/playmode.txt",message+"\n");
        void OnDestroy(){Application.logMessageReceived-=OnLog;if(driver!=null){driver.ExternalInput=false;driver.CinematicControl=false;driver.SetExternalInput(0,0,false);}}
    }
}
