using System.Collections;
using System.IO;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Play-mode regression probe for the authored bunker/road seam.</summary>
public static class SeamlessBunkerRoadValidation
{
    [MenuItem("RogueDrive/Bunker/Validate Seamless Exit (Play Mode)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "GarageScene")
            throw new System.InvalidOperationException("Run GarageScene in Play mode first.");
        GarageDriveOutController.Instance.StartCoroutine(Probe());
    }

    private static IEnumerator Probe()
    {
        bool hadCompletion = PlayerPrefs.HasKey("GaragePrologueDone");
        int completion = PlayerPrefs.GetInt("GaragePrologueDone", 0);
        try
        {
        var intro = Object.FindFirstObjectByType<BunkerPrologueCutscene>();
        while (intro != null && intro.IsRunning) yield return null;
        var flow = Object.FindFirstObjectByType<SeamlessBunkerRoad>();
        var controller = GarageDriveOutController.Instance;
        var player = Object.FindFirstObjectByType<GaragePlayerController>(FindObjectsInactive.Include);
        var car = Object.FindFirstObjectByType<ArcadeCarController>();
        var assembly = new SerializedObject(BunkerStarterCarAssembly.Instance);
        foreach (string field in new[] { "isWheelInstalled", "isBatteryInstalled", "isFuelFilled", "isAssemblyComplete" })
            assembly.FindProperty(field).boolValue = true;
        assembly.ApplyModifiedPropertiesWithoutUndo();
        var wheel = car.transform.Find("Classic Car_9 FL Tire");
        if (wheel != null) wheel.gameObject.SetActive(true);
        var jack = GameObject.Find("Placeholder_JackStand");
        if (jack != null) jack.SetActive(false);
        GaragePrologueManager.Instance.SetPower(true);
        GaragePrologueManager.Instance.PickUpKeys();
        Object.FindFirstObjectByType<GarageSwingGateController>().OpenGate();
        var modules = car.GetComponent<VehicleModularState>();
        modules.AddFuel(37f - modules.FuelLiters);
        modules.AddRadiatorWater(9f - modules.RadiatorWater);
        controller.StartDriveOut(player, car.gameObject);
        yield return null;
        var body = car.GetComponent<Rigidbody>();
        var driver = car.GetComponent<GarageDriveOutVehicle>();
        body.position = new Vector3(0f, 2.4f, 27f);
        body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        yield return new WaitForSeconds(0.5f);
        int carId = car.GetInstanceID();
        int cameraId = Camera.main.GetInstanceID();
        int cargoId = car.GetComponent<VehicleCargoTrunk>().GetInstanceID();
        int inventoryId = player.GetComponent<PlayerPocketInventory>().GetInstanceID();
        int sceneHandle = SceneManager.GetActiveScene().handle;
        float fuelBefore = modules.FuelLiters;
        float previousSpeed = body.linearVelocity.magnitude;
        float exitSpeedBefore = 0f, exitSpeedAfter = 0f;
        bool wasStarted = flow.HasStarted;
        float start = Time.realtimeSinceStartup;
        float minHeight = body.position.y;
        driver.SimulatedThrottle = 0.35f;
        while (body.position.z < 53f && Time.realtimeSinceStartup - start < 15f)
        {
            yield return new WaitForFixedUpdate();
            minHeight = Mathf.Min(minHeight, body.position.y);
            if (!wasStarted && flow.HasStarted)
            {
                exitSpeedBefore = previousSpeed;
                exitSpeedAfter = body.linearVelocity.magnitude;
            }
            wasStarted = flow.HasStarted;
            previousSpeed = body.linearVelocity.magnitude;
        }
        driver.SimulatedThrottle = 0f;
        bool crossed = body.position.z >= 53f && flow.HasStarted && minHeight > 1.8f;
        bool identity = car.GetInstanceID() == carId && Camera.main.GetInstanceID() == cameraId
            && car.GetComponent<VehicleCargoTrunk>().GetInstanceID() == cargoId
            && player.GetComponent<PlayerPocketInventory>().GetInstanceID() == inventoryId
            && SceneManager.GetActiveScene().handle == sceneHandle;
        bool resources = modules.FuelLiters <= fuelBefore && modules.FuelLiters > fuelBefore - 2f;
        bool momentum = exitSpeedBefore > 0f && Mathf.Abs(exitSpeedAfter - exitSpeedBefore) < 2f;
        float distance = car.Run.Distance;
        controller.ExitCar();
        yield return null;
        controller.StartDriveOut(player, car.gameObject);
        yield return null;
        bool reboarded = controller.IsDriving && driver.enabled && Camera.main.GetInstanceID() == cameraId;
        yield return new WaitForSeconds(0.5f);
        string report = $"crossed={crossed}; identity={identity}; resources={resources}; momentum={momentum}; reboarded={reboarded}; "
            + $"exitSpeed={exitSpeedBefore:F3}->{exitSpeedAfter:F3}; minHeight={minHeight:F3}; distance={distance:F2}; fuel={fuelBefore:F3}->{modules.FuelLiters:F3}";
        File.WriteAllText("Temp/seamless-exit-validation.txt", report);
        ScreenCapture.CaptureScreenshot("Temp/seamless-exit-validated.png");
        Time.timeScale = 0f; // Keep the captured result available for visual inspection; stop Play to reset.
        if (crossed && identity && resources && momentum && reboarded) Debug.Log("[SeamlessExitValidation] PASS " + report);
        else Debug.LogError("[SeamlessExitValidation] FAIL " + report);
        }
        finally
        {
        if (hadCompletion) PlayerPrefs.SetInt("GaragePrologueDone", completion);
        else PlayerPrefs.DeleteKey("GaragePrologueDone");
        PlayerPrefs.Save();
        }
    }
}
