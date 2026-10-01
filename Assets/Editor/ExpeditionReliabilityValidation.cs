using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RogueDrive.Gameplay.Hub;
using RogueDrive.Gameplay;
using RogueDrive.Meta;
using UnityEditor;
using UnityEngine;

// Deterministic regression checks. Save fixtures never touch the player's files.
public static class ExpeditionReliabilityValidation
{
    const string Report = "Artifacts/Upgrade/reliability-editmode.txt";
    static readonly List<string> results = new List<string>();

    static void Check(bool value, string message)
    {
        results.Add((value ? "OK " : "FAIL ") + message);
        File.WriteAllLines(Report, results);
        if (!value) throw new InvalidOperationException(message);
    }

    [MenuItem("RogueDrive/Validation/Expedition reliability")]
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit Mode only.");
        Directory.CreateDirectory("Artifacts/Upgrade");
        results.Clear();
        CheckWorkshopSerialization();
        CheckFirearmAmmo();
        CheckCheckpointValidation();
        CheckAtomicSaves();
        string summary = "PASS: " + results.Count + " expedition reliability checks.";
        File.AppendAllText(Report, summary + "\n");
        Debug.Log(summary);
        return summary;
    }

    static void CheckWorkshopSerialization()
    {
        const string legacy = "{\"instanceId\":\"engine-legacy\",\"definitionId\":\"engine_stock\",\"condition\":0.7,\"internals\":[{\"instanceId\":\"turbo-legacy\",\"definitionId\":\"turbo_1\",\"condition\":0.6,\"internals\":[]}]}";
        var engine = JsonUtility.FromJson<WorkshopPartData>(legacy);
        Check(engine.Internals.Count == 1 && engine.Internals[0].instanceId == "turbo-legacy", "Old engine JSON keeps internal component identity");
        Check(Mathf.Abs(engine.condition - .7f) < .001f && Mathf.Abs(engine.Internals[0].condition - .6f) < .001f, "Legacy component condition preserved");
        engine.Internals.Add(new WorkshopPartData { instanceId = "filter-test", definitionId = "filter_1", condition = .4f });
        var copy = engine.Copy();
        copy.Internals[0].condition = .1f;
        Check(Mathf.Abs(engine.Internals[0].condition - .6f) < .001f && !ReferenceEquals(engine.Internals, copy.Internals), "Copied engine owns independent internal components");
        var decoded = JsonUtility.FromJson<WorkshopPartData>(JsonUtility.ToJson(engine));
        Check(decoded.Internals.Count == 2 && decoded.Internals[1].instanceId == "filter-test", "New engine JSON preserves all internal components");
        string fittedLegacy = "{\"coins\":75,\"fitted\":[{\"slot\":0,\"part\":" + legacy + "}]}";
        var fitted = JsonUtility.FromJson<VehicleWorkshop.SaveData>(fittedLegacy);
        var fittedCopy = JsonUtility.FromJson<VehicleWorkshop.SaveData>(JsonUtility.ToJson(fitted));
        Check(fittedCopy.coins == 75 && fittedCopy.fitted[0].part.Internals[0].instanceId == "turbo-legacy", "Existing fitted-engine save remains compatible");
        var obj = new GameObject("SerializationFixture");
        try
        {
            var item = obj.AddComponent<WorkshopPartItem>();
            item.data = engine;
            string json = EditorJsonUtility.ToJson(item);
            item.data = new WorkshopPartData();
            EditorJsonUtility.FromJsonOverwrite(json, item);
            Check(item.data.Internals.Count == 2 && item.data.Internals[0].instanceId == "turbo-legacy", "Component serialization restores assembly without recursive depth expansion");
        }
        finally { UnityEngine.Object.DestroyImmediate(obj); }
    }

    static GarageDepartureCheckpoint.Snapshot ValidInventory()
    {
        Func<GarageDepartureCheckpoint.SlotState> empty = () => new GarageDepartureCheckpoint.SlotState { count = 0, objects = Array.Empty<string>() };
        return new GarageDepartureCheckpoint.Snapshot {
            journey = true, car = "{}", cargo = new[] { empty() },
            pockets = Enumerable.Range(0, PlayerPocketInventory.SlotCount).Select(i => empty()).ToArray()
        };
    }

    static void CheckFirearmAmmo()
    {
        var prefab=Resources.Load<GameObject>("Combat/Pistol");
        Check(prefab!=null&&GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab)==0,"Pistol prefab has valid serialized components");
        var instance=UnityEngine.Object.Instantiate(prefab);
        try
        {
            var gun=instance.GetComponent<FirearmWeapon>();
            gun.SetAmmo(2);
            Check(gun.Reload(-5)==0&&gun.CurrentAmmo==2,"Negative reserve cannot remove magazine ammo");
            Check(gun.Reload(2)==2&&gun.CurrentAmmo==4,"Reload consumes only available reserve");
            Check(gun.Reload(int.MaxValue)==3&&gun.CurrentAmmo==7,"Reload never exceeds magazine capacity");
        }
        finally {UnityEngine.Object.DestroyImmediate(instance);}
    }

    static void CheckCheckpointValidation()
    {
        var snapshot = ValidInventory();
        Check(GarageDepartureCheckpoint.IsValidSnapshot(snapshot), "Complete checkpoint accepted");
        snapshot.pockets[0] = null;
        Check(!GarageDepartureCheckpoint.IsValidSnapshot(snapshot), "Null inventory cell rejected before mutation");
        snapshot = ValidInventory();
        snapshot.cargo[0].objects = new[] { "missing-item" };
        Check(!GarageDepartureCheckpoint.IsValidSnapshot(snapshot), "Missing cargo identity rejected");
        snapshot = ValidInventory();
        snapshot.items.Add(new GarageDepartureCheckpoint.ItemState { id = "same-item" });
        snapshot.items.Add(new GarageDepartureCheckpoint.ItemState { id = "same-item" });
        Check(!GarageDepartureCheckpoint.IsValidSnapshot(snapshot), "Duplicate physical identity rejected");
        var valid = new JourneyCheckpoint.SaveData {
            station = 1, health = 90, rotation = Quaternion.identity, inventory = ValidInventory()
        };
        var validator = typeof(JourneyCheckpoint).GetMethod("IsValidSave", BindingFlags.Static | BindingFlags.NonPublic);
        Func<bool> accepted = () => (bool)validator.Invoke(null, new object[] { valid });
        Check(accepted(), "Valid station checkpoint accepted");
        valid.inventory = new GarageDepartureCheckpoint.Snapshot();
        Check(!accepted(), "Incomplete station save cannot advertise Continue");
        valid.inventory = ValidInventory(); valid.position.x = float.NaN;
        Check(!accepted(), "Non-finite car position rejected");
        valid.position = Vector3.zero; valid.station = 9;
        Check(!accepted(), "Unknown station rejected");
    }

    static void CheckAtomicSaves()
    {
        string folder = Path.GetFullPath("Artifacts/Upgrade/SaveFixtures");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json");
        string backup = path + ".bak";
        var save = typeof(SaveService).GetMethod("SaveToPaths", BindingFlags.Static | BindingFlags.NonPublic);
        var read = typeof(SaveService).GetMethod("TryRead", BindingFlags.Static | BindingFlags.NonPublic);
        Action<int> write = coins => save.Invoke(null, new object[] { new MetaProgressData { Coins = coins, SelectedCarId = "light", OwnedCarIds = new List<string> { "light" } }, path, backup });
        Func<string, MetaProgressData> load = file => (MetaProgressData)read.Invoke(null, new object[] { file });
        try
        {
            write(40);
            Check(load(path).Coins == 40 && !File.Exists(path + ".tmp"), "First save promoted from flushed temporary file");
            write(80);
            Check(load(path).Coins == 80 && load(backup).Coins == 40, "Replacement keeps previous valid progress in backup");
            string goodBackup = File.ReadAllText(backup);
            File.WriteAllText(path, "RD1:broken!");
            write(120);
            Check(load(path).Coins == 120 && File.ReadAllText(backup) == goodBackup, "Saving after primary corruption preserves valid backup");
            string goodPrimary = File.ReadAllText(path);
            bool failed = false;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { write(160); }
                catch (TargetInvocationException) { failed = true; }
            }
            Check(failed && File.ReadAllText(path) == goodPrimary && File.ReadAllText(backup) == goodBackup, "Failed replacement leaves primary and backup intact");
            File.WriteAllText(path + ".tmp", "interrupted-write");
            Check(load(path).Coins == 120, "Uncommitted temporary file cannot replace active progress");
            write(140);
            Check(load(path).Coins == 140 && load(backup).Coins == 120 && !File.Exists(path + ".tmp"), "Next save recovers after interrupted write");
        }
        finally
        {
            foreach (string file in new[] { path, backup, path + ".tmp" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    public static string CheckRestoreCleanup(ArcadeCarController car)
    {
        var body=car.GetComponent<Rigidbody>();
        var driver=car.GetComponent<GarageDriveOutVehicle>();
        bool kinematic=body.isKinematic,cinematic=driver!=null&&driver.CinematicControl;
        Vector3 position=car.transform.position;
        var pending=typeof(JourneyCheckpoint).GetField("pending",BindingFlags.Static|BindingFlags.NonPublic);
        pending.SetValue(null,new JourneyCheckpoint.SaveData());
        var iterator=JourneyCheckpoint.RestorePending(null,car);
        if(iterator.MoveNext())throw new InvalidOperationException("Invalid restore unexpectedly yielded.");
        if(JourneyCheckpoint.HasPendingRestore||JourneyCheckpoint.Restoring||body.isKinematic!=kinematic
            ||(driver!=null&&driver.CinematicControl!=cinematic)||Vector3.Distance(car.transform.position,position)>.001f)
            throw new InvalidOperationException("Failed restore retained locks or pending state.");
        return "OK Failed restore clears pending state and preserves physics/control";
    }
}
