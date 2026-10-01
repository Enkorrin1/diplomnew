using System.Collections;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GarageSceneExitValidation
{
    [MenuItem("RogueDrive/Bunker/Validate Garage to Stage1 (Play Mode)")]
    public static void Run()
    {
        var flow = Object.FindFirstObjectByType<GarageSceneExitCinematic>();
        if (!EditorApplication.isPlaying || flow == null) throw new System.InvalidOperationException("Play GarageScene first.");
        flow.StartCoroutine(Probe(flow));
    }

    private static IEnumerator Probe(GarageSceneExitCinematic flow)
    {
        bool hadCompletion = PlayerPrefs.HasKey("GaragePrologueDone");
        int completion = PlayerPrefs.GetInt("GaragePrologueDone", 0);
        try
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            var intro = Object.FindFirstObjectByType<BunkerPrologueCutscene>();
            while ((!flow.IsReady || (intro != null && intro.IsRunning)) && Time.realtimeSinceStartup < deadline) yield return null;
            var car = Object.FindObjectsByType<ArcadeCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(c => c.gameObject.scene.name == "GarageScene");
            var player = Object.FindFirstObjectByType<GaragePlayerController>(FindObjectsInactive.Include);
            var assembly = new SerializedObject(BunkerStarterCarAssembly.Instance);
            foreach (string field in new[] { "isWheelInstalled", "isBatteryInstalled", "isFuelFilled", "isAssemblyComplete" })
                assembly.FindProperty(field).boolValue = true;
            assembly.ApplyModifiedPropertiesWithoutUndo();
            var wheel = car.transform.Find("Classic Car_9 FL Tire");
            if (wheel != null) wheel.gameObject.SetActive(true);
            GameObject.Find("Placeholder_JackStand")?.SetActive(false);
            GaragePrologueManager.Instance.SetPower(true);
            GaragePrologueManager.Instance.PickUpKeys();
            var gate = Object.FindFirstObjectByType<GarageSwingGateController>();
            var controller = GarageDriveOutController.Instance;
            controller.StartDriveOut(player, car.gameObject);
            var modules = car.GetComponent<VehicleModularState>();
            modules.AddFuel(37f - modules.FuelLiters);
            modules.AddRadiatorWater(9f - modules.RadiatorWater);
            var body = car.GetComponent<Rigidbody>();
            var driver = car.GetComponent<GarageDriveOutVehicle>();
            int carId = car.GetInstanceID();
            int cameraId = controller.DrivingCamera.GetInstanceID();
            int cargoId = car.GetComponent<VehicleCargoTrunk>().GetInstanceID();
            int pocketId = player.GetComponent<PlayerPocketInventory>().GetInstanceID();
            float fuelBefore = modules.FuelLiters;
            driver.SimulatedThrottle = 0.25f;
            string lastPhase = "";
            float phaseStarted = 0f;
            bool capturedMiddle = false;
            bool interior = false, exterior = false, revving = false, airborne = false, landing = false;
            deadline = Time.realtimeSinceStartup + 25f;
            Directory.CreateDirectory("Temp/GarageExit");
            while (!flow.Completed && Time.realtimeSinceStartup < deadline)
            {
                if (flow.Phase != lastPhase)
                {
                    lastPhase = flow.Phase;
                    phaseStarted = Time.time;
                    capturedMiddle = false;
                    if (lastPhase == "Interior") interior = true;
                    if (lastPhase == "Exterior") exterior = true;
                    if (lastPhase == "Revving") revving = true;
                    if (lastPhase == "Airborne") airborne = true;
                    if (lastPhase == "Landing") landing = true;
                    Capture(controller.DrivingCamera, "Temp/GarageExit/" + lastPhase + ".png");
                }
                if (!capturedMiddle && Time.time - phaseStarted > .45f)
                {
                    Capture(controller.DrivingCamera, "Temp/GarageExit/" + lastPhase + "-Middle.png");
                    capturedMiddle = true;
                }
                yield return null;
            }
            driver.SimulatedThrottle = 0f;
            yield return new WaitForSeconds(0.3f);
            bool identity = car != null && car.GetInstanceID() == carId && controller.DrivingCamera.GetInstanceID() == cameraId
                && car.GetComponent<VehicleCargoTrunk>().GetInstanceID() == cargoId && player.GetComponent<PlayerPocketInventory>().GetInstanceID() == pocketId;
            bool scenes = (SceneManager.GetActiveScene().name == "Stage1_Outskirts"
                || (SeamlessJourneyStream.Instance != null && SeamlessJourneyStream.Instance.CurrentSegmentIndex == 0))
                && !SceneManager.GetSceneByName("GarageScene").isLoaded;
            bool singleCar = Object.FindObjectsByType<ArcadeCarController>(FindObjectsSortMode.None).Length == 1;
            bool controls = !controller.CinematicControl && !driver.CinematicControl && !body.isKinematic && controller.IsDriving;
            bool resources = modules.FuelLiters > fuelBefore - 2f && modules.FuelLiters <= fuelBefore && modules.RadiatorWater > 8f;
            bool rolling = body.linearVelocity.magnitude > 1f && body.position.y > -0.1f;
            Capture(controller.DrivingCamera, "Temp/GarageExit/Complete.png");
            float distanceBefore = car.Run != null ? car.Run.Distance : -1f;
            driver.SimulatedThrottle = 0.2f;
            yield return new WaitForSeconds(0.75f);
            driver.SimulatedThrottle = 0f;
            bool gameplay = car.Run != null && car.Run.Distance > distanceBefore && modules.FuelLiters < fuelBefore;
            controller.ExitCar();
            yield return null;
            bool exit = !controller.IsDriving && player.isActiveAndEnabled;
            controller.StartDriveOut(player, car.gameObject);
            yield return null;
            bool reboard = controller.IsDriving && driver.enabled && controller.DrivingCamera.GetInstanceID() == cameraId;
            bool pass = flow.Completed && interior && exterior && revving && airborne && landing && identity && scenes && singleCar && controls && resources && rolling && gameplay && exit && reboard;
            string report = $"PASS={pass}; phase={flow.Phase}; shots={interior}/{exterior}; revving={revving}; airborne={airborne}; landing={landing}; identity={identity}; scenes={scenes}; singleCar={singleCar}; controls={controls}; resources={resources}; rolling={rolling}; gameplay={gameplay}; exit={exit}; reboard={reboard}; position={body.position}; fuel={fuelBefore:F2}->{modules.FuelLiters:F2}";
            File.WriteAllText("Temp/GarageExit/validation.txt", report);
            if (pass) Debug.Log("[GarageExitValidation] " + report); else Debug.LogError("[GarageExitValidation] " + report);
            ScreenCapture.CaptureScreenshot("Temp/GarageExit/FinalHUD.png");
            yield return new WaitForEndOfFrame();
            yield return null;
            EditorApplication.isPaused = true;
        }
        finally
        {
            if (hadCompletion) PlayerPrefs.SetInt("GaragePrologueDone", completion); else PlayerPrefs.DeleteKey("GaragePrologueDone");
            PlayerPrefs.Save();
        }
    }

    private static void Capture(Camera camera, string path)
    {
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(texture);
        }
    }
}
