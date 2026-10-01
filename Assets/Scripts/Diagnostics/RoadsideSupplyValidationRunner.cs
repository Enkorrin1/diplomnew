#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using RogueDrive.Gameplay.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RoadsideSupplyValidation
{
    static RoadsideSupplyValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("SupplyTest", false)) return;
            SessionState.EraseBool("SupplyTest"); new GameObject("Supply_Validation").AddComponent<RoadsideSupplyValidationRunner>();
        };
    }
    public static void Run()
    {
        Directory.CreateDirectory("Artifacts/RoadsideSupply"); File.WriteAllText("Artifacts/RoadsideSupply/playmode.txt", "START\n");
        EditorSceneManager.playModeStartScene = null; EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");
        SessionState.SetBool("SupplyTest", true); EditorApplication.isPlaying = true;
    }
}

public sealed class RoadsideSupplyValidationRunner : MonoBehaviour
{
    const string Report = "Artifacts/RoadsideSupply/playmode.txt";
    GaragePlayerController player;
    ArcadeCarController car;
    PlayerPocketInventory inventory;
    void Check(bool condition, string message)
    {
        File.AppendAllText(Report, (condition ? "OK " : "FAIL ") + message + "\n");
        if (!condition) throw new Exception(message);
    }
    IEnumerator Until(Func<bool> condition, string message, float seconds = 90)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        Check(condition(), message);
    }
    void MovePlayer(Vector3 position)
    {
        var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
        player.transform.position = position; controller.enabled = true; Physics.SyncTransforms();
    }
    void ClearInventory()
    {
        for (int i = 0; i < PlayerPocketInventory.SlotCount; i++) inventory.ClearSlot(i);
    }
    void FillInventory()
    {
        ClearInventory();
        for (int i = 0; i < PlayerPocketInventory.SlotCount; i++)
            inventory.TryAddItem("validation_full_" + i, "Заполненный слот", 1);
    }
    void LegacyLootChecks()
    {
        foreach (var type in new[] { RoadsideScavengePoint.ScavengeType.Backpack, RoadsideScavengePoint.ScavengeType.CardboardBox, RoadsideScavengePoint.ScavengeType.ToolCabinet })
        {
            var obj = new GameObject("Validation_LegacyLoot", typeof(BoxCollider)); var point = obj.AddComponent<RoadsideScavengePoint>(); point.Configure(type);
            string first = type == RoadsideScavengePoint.ScavengeType.Backpack ? "first_aid_medkit" : type == RoadsideScavengePoint.ScavengeType.CardboardBox ? "bolt_repair" : "wrench_tool";
            int count = type == RoadsideScavengePoint.ScavengeType.CardboardBox ? 4 : 1;
            FillInventory(); point.Interact(player);
            Check(!point.IsLooted && inventory.GetItemCount(first) == 0, "Legacy full inventory retains loot: " + type);
            inventory.ClearSlot(0); point.Interact(player);
            Check(!point.IsLooted && inventory.GetItemCount(first) == count, "Legacy partial collection succeeds: " + type);
            inventory.RemoveItem(first, count); point.Interact(player);
            Check(inventory.GetItemCount(first) == 0, "Legacy consumed partial reward cannot refill: " + type);
            for (int i = 1; i < PlayerPocketInventory.SlotCount; i++) inventory.ClearSlot(i);
            point.Interact(player); Check(point.IsLooted, "Legacy remainder completes: " + type);
            int ammo = inventory.GetItemCount("ammo_9mm"); point.Interact(player);
            Check(inventory.GetItemCount("ammo_9mm") == ammo, "Legacy exhausted container cannot farm ammo: " + type);
            Object.Destroy(obj);
        }
    }
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        yield return Until(() => SeamlessJourneyStream.Instance != null && !SeamlessJourneyStream.Instance.IsStreaming, "Continuous route ready");
        car = Object.FindFirstObjectByType<ArcadeCarController>();
        GarageDriveOutController.Instance.ExitCar();
        player = Object.FindFirstObjectByType<GaragePlayerController>(); inventory = player.GetComponent<PlayerPocketInventory>();
        player.SetMovementLocked(false);
        var caches = Object.FindObjectsByType<RoadsideSupplyCache>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(c => c.Id).ToArray();
        Check(caches.Length == 3 && caches.Select(c => c.Id).Distinct().Count() == 3, "Three distinct roadside caches");
        Check(caches.All(c => c.GetComponent<JourneyPersistentObject>()?.Id == c.Id), "Every cache has stable stream identity");
        Check(caches.All(c => c.GetComponent<UnityEngine.AI.NavMeshObstacle>()?.carving ?? false), "Cache bodies carve enemy navigation");
        var guards = Object.FindObjectsByType<EncounterZombie>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var enemy in guards) enemy.enabled = false;
        LegacyLootChecks();
        var cache = caches[0]; var initial = cache.CaptureState();
        yield return new WaitForSeconds(.6f);
        UnityEngine.AI.NavMeshHit blocked;
        Check(!UnityEngine.AI.NavMesh.SamplePosition(cache.transform.position, out blocked, .1f, UnityEngine.AI.NavMesh.AllAreas), "Enemy navigation excludes the solid cache footprint");
        car.GetComponent<GarageDriveOutVehicle>().CinematicControl = true; car.GetComponent<Rigidbody>().isKinematic = true;
        car.PlaceAtStart(cache.transform.position + Vector3.right * 60, Quaternion.identity);
        MovePlayer(cache.transform.position - cache.transform.forward * 8);
        cache.Interact(player); Check(!cache.IsSearching, "Cannot start a cache remotely");
        MovePlayer(cache.transform.position - cache.transform.forward * 1.8f + Vector3.up * .1f);
        var guard = new SerializedObject(cache).FindProperty("guards").GetArrayElementAtIndex(0).objectReferenceValue as EncounterZombie;
        Check(guard != null, "Cache is linked to real local encounter guards");
        guard.enabled = true; var previous = guard.State; cache.Interact(player);
        Check(cache.IsSearching && (previous != EncounterZombie.BehaviourState.Idle || guard.State == EncounterZombie.BehaviourState.Alert), "Starting search alerts local guard");
        guard.enabled = false;
        cache.Interact(player); Check(!cache.IsSearching && !cache.IsOpen, "Pressing E again cancels without rewards");
        cache.Interact(player); yield return new WaitForSeconds(.5f);
        Check(cache.Progress > 0 && !cache.IsOpen, "Search takes real gameplay time");
        MovePlayer(cache.transform.position - cache.transform.forward * 8); yield return null;
        Check(!cache.IsSearching && cache.Progress == 0 && !cache.IsOpen, "Leaving range cancels search and resets timer");
        FillInventory(); MovePlayer(cache.transform.position - cache.transform.forward * 1.8f + Vector3.up * .1f);
        cache.Interact(player); yield return new WaitForSeconds(.4f); float progress = cache.Progress;
        player.transform.rotation = cache.transform.rotation;
        typeof(GaragePlayerController).GetField("cameraPitch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(player, 32f);
        yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/RoadsideSupply/gameplay-prompt.png"); yield return new WaitForEndOfFrame(); yield return null;
        progress = cache.Progress;
        Time.timeScale = 0; yield return new WaitForSecondsRealtime(.4f);
        Check(Mathf.Approximately(cache.Progress, progress), "Pause freezes search timer"); Time.timeScale = 1;
        float front = CreepingStormBarrier.Instance.RouteDistance;
        yield return Until(() => cache.IsOpen, "Patrol cache opens after timed search", 20);
        Check(CreepingStormBarrier.Instance.RouteDistance > front, "Storm advances while searching outside shelter");
        Check(!cache.IsEmpty && cache.CaptureState().rewards.All(r => r.remaining > 0), "Full inventory preserves all cache rewards");
        inventory.ClearSlot(0); cache.Interact(player);
        Check(inventory.GetItemCount("ammo_9mm") == 28 && !cache.IsEmpty, "One free slot takes one reward; remainder stays in cache");
        inventory.RemoveItem("ammo_9mm", 28); cache.Interact(player);
        Check(inventory.GetItemCount("ammo_9mm") == 0 && inventory.GetItemCount("first_aid_medkit") == 1, "Consumed reward cannot be claimed again during partial search");
        inventory.ClearSlot(1); cache.Interact(player);
        Check(cache.IsEmpty && inventory.GetItemCount("bolt_repair") == 4, "Last reward completes cache exactly once");
        int bolts = inventory.GetItemCount("bolt_repair"); cache.Interact(player);
        Check(inventory.GetItemCount("bolt_repair") == bolts, "Repeated interaction with empty cache cannot farm loot");
        Capture(cache, "patrol-empty");
        var clone = new GameObject("Validation_CacheClone").AddComponent<RoadsideSupplyCache>();
        clone.Configure(cache.Id, "Копия", "", 1, initial.rewards.Select(r => new RoadsideSupplyCache.Supply { id = r.id, title = r.id, count = r.remaining }).ToArray(), null, null);
        clone.RestoreState(JsonUtility.FromJson<RoadsideSupplyCache.State>(JsonUtility.ToJson(cache.CaptureState())));
        Check(clone.IsOpen && clone.IsEmpty, "JSON round trip preserves opened and consumed cache");
        var invalid = cache.CaptureState(); invalid.rewards[0].remaining = -1;
        Check(!RoadsideSupplyCache.IsValidState(invalid), "Negative saved reward is rejected");
        Object.Destroy(clone.gameObject); yield return null;
        foreach (var stop in caches.Skip(1))
        {
            ClearInventory();
            car.PlaceAtStart(stop.transform.position + Vector3.right * 60, Quaternion.identity);
            yield return Until(() => stop.gameObject.activeInHierarchy, "Stream reactivates approached cache " + stop.Id, 5);
            MovePlayer(stop.transform.position - stop.transform.forward * 1.8f + Vector3.up * .1f);
            Capture(stop, "cache-" + stop.Id.Last() + "-closed");
            stop.Interact(player); yield return Until(() => stop.IsOpen, "Distinct cache " + stop.Id + " completes", 25);
            Check(stop.IsEmpty && stop.CaptureState().rewards.All(r => inventory.GetItemCount(r.id) > 0), "All authored rewards delivered: " + stop.Id);
        }
        // Prepare a partially collected cache; restoring a real station checkpoint must preserve its remainder.
        var partial = caches[1].CaptureState(); partial.rewards[1].remaining = 14;
        caches[1].RestoreState(partial);
        var stream = SeamlessJourneyStream.Instance;
        stream.Route.Evaluate(stream.Catalog.segments[0].roadEndDistance + 400, out var position, out var rotation);
        MovePlayer(position + rotation * new Vector3(46, .3f, 0)); car.PlaceAtStart(position + rotation * new Vector3(50, .25f, 0), rotation);
        var station = Object.FindObjectsByType<JourneyServiceStation>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(s => s.Number == 1);
        yield return Until(() => station.Ready, "Real station checkpoint after roadside search", 120);
        Check(JourneyCheckpoint.CaptureArrival(station), "Arrival checkpoint includes cache histories");
        Check(JourneyCheckpoint.RequestRestore(false), "Station restart requested");
        yield return Until(() => !JourneyCheckpoint.HasPendingRestore && !JourneyCheckpoint.Restoring && SeamlessJourneyStream.Instance != null, "Actual scene reload and checkpoint restore");
        caches = Object.FindObjectsByType<RoadsideSupplyCache>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(c => c.Id).ToArray();
        Check(caches.Length == 3 && caches[0].IsEmpty && caches[2].IsEmpty, "Consumed caches remain empty after actual checkpoint reload");
        var restored = caches[1].CaptureState();
        Check(restored.opened && restored.rewards.Single(r => r.id == "ammo_9mm").remaining == 14 && restored.rewards.Single(r => r.id == "first_aid_medkit").remaining == 0,
            "Partial cache preserves exact unclaimed rewards after actual reload");
        File.AppendAllText(Report, "PASS: roadside supply search, inventory pressure, storm and actual checkpoint restore.\n");
        EditorApplication.isPaused = true;
    }
    static void Capture(RoadsideSupplyCache cache, string name)
    {
        var obj = new GameObject("ValidationCamera"); var camera = obj.AddComponent<Camera>(); camera.farClipPlane = 400;
        camera.transform.position = cache.transform.TransformPoint(new Vector3(-2.8f, 2.6f, -5.5f));
        camera.transform.LookAt(cache.transform.position + Vector3.up);
        var target = RenderTexture.GetTemporary(1280, 720, 24); var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        File.WriteAllBytes("Artifacts/RoadsideSupply/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); Object.Destroy(obj);
    }
}
#endif
