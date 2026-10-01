using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace RogueDrive.Editor
{
    public static class PlayerMeleeValidation
    {
        static IEnumerator<float> checks;
        static double next;
        static readonly List<string> report = new List<string>();
        static GaragePlayerController player;
        static EncounterZombie zombie;
        static PlayerMeleeCombat combat;
        static PlayerPocketInventory pocket;
        static Vector3 home;
        static int slot;

        public static void Run()
        {
            if (!Application.isPlaying || checks != null) throw new InvalidOperationException("Use fresh Stage1 Play Mode.");
            report.Clear(); checks = CheckCombat().GetEnumerator(); next = 0;
            EditorApplication.update += Tick;
        }
        static void Tick()
        {
            if (!Application.isPlaying) { Finish("CANCELLED: Play Mode stopped"); return; }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (!checks.MoveNext()) { Finish("PASS: all melee checks"); return; }
                next = EditorApplication.timeSinceStartup + checks.Current;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            EditorApplication.update -= Tick; checks?.Dispose(); checks = null;
            report.Add(result); Directory.CreateDirectory("Artifacts/EnemySystem");
            File.WriteAllLines("Artifacts/EnemySystem/melee-report.txt", report);
            Debug.Log("[MeleeValidation] " + result);
        }
        static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            report.Add("PASS: " + label);
        }
        static void Aim(float distance = 1.3f)
        {
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.SetPositionAndRotation(home - Vector3.forward * distance, Quaternion.identity);
            cc.enabled = true; player.SetCameraPitch(0f); player.SetMovementLocked(false);
            zombie.GetComponent<NavMeshAgent>().Warp(home);
            Physics.SyncTransforms();
        }
        static IEnumerable<float> CheckCombat()
        {
            GarageDriveOutController.Instance.ExitCar();
            yield return .5f;
            player = UnityEngine.Object.FindFirstObjectByType<GaragePlayerController>();
            pocket = player.GetComponent<PlayerPocketInventory>(); combat = player.GetComponent<PlayerMeleeCombat>();
            var enemies = UnityEngine.Object.FindObjectsByType<EncounterZombie>(FindObjectsSortMode.None);
            zombie = enemies.First(e => e.name == "RoadsideWalker_1"); home = zombie.HomePosition;
            foreach (var enemy in enemies) if (enemy != zombie) enemy.gameObject.SetActive(false);
            var storm = UnityEngine.Object.FindFirstObjectByType<Gameplay.Track.CreepingStormBarrier>();
            if (storm != null) storm.enabled = false;
            var car = UnityEngine.Object.FindFirstObjectByType<ArcadeCarController>();
            car.GetComponent<GarageDriveOutVehicle>().enabled = false;
            car.GetComponent<Rigidbody>().isKinematic = true; car.transform.position = home + Vector3.right * 80f;
            var data = new SerializedObject(zombie); data.FindProperty("baseSpeed").floatValue = 0f;
            data.FindProperty("contactDamage").floatValue = 0f; data.ApplyModifiedPropertiesWithoutUndo();
            slot = Enumerable.Range(0, PlayerPocketInventory.HotbarCount).First(i => pocket.GetSlot(i).worldItem?.GetComponent<MeleeWeapon>() != null);
            pocket.SelectSlot(slot); Aim(); yield return .3f;
            Check(pocket.ActivePhysical.GetComponent<MeleeWeapon>() != null, "starter crowbar in hotbar");
            player.SetMovementLocked(true); Check(!combat.TryAttack(), "movement lock blocks attack"); player.SetMovementLocked(false);
            pocket.SelectSlot((slot+1)%5); Check(!combat.TryAttack(), "empty hand cannot deal crowbar damage"); pocket.SelectSlot(slot);
            Aim(5f); Check(combat.TryAttack(), "out-of-range swing starts"); yield return .9f;
            Check(zombie.CurrentHealth == 45f, "out-of-range swing misses");
            Aim(); player.transform.rotation = Quaternion.Euler(0,180,0); combat.TryAttack(); yield return .9f;
            Check(zombie.CurrentHealth == 45f, "enemy behind player is not hit");
            Aim(); var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = home + new Vector3(0,1f,-.7f); wall.transform.localScale = new Vector3(3,3,.12f);
            Physics.SyncTransforms(); combat.TryAttack(); yield return .9f;
            Check(zombie.CurrentHealth == 45f, "solid wall blocks melee"); UnityEngine.Object.Destroy(wall); yield return .1f;
            Aim(); combat.TryAttack(); pocket.SelectSlot((slot+1)%5); yield return .9f;
            Check(zombie.CurrentHealth == 45f, "switching slot during windup cancels impact"); pocket.SelectSlot(slot);
            Aim(); Check(combat.TryAttack(true), "shove starts"); Check(!combat.TryAttack(true), "cannot queue duplicate shove"); yield return .3f;
            Check(combat.SuccessfulShoves == 1 && zombie.State == EncounterZombie.BehaviourState.Stagger, "shove interrupts enemy into stagger");
            Check(zombie.CurrentHealth == 45f && Vector3.Distance(zombie.transform.position,home) > .5f, "shove displaces without damage");
            yield return .4f; Check(!combat.TryAttack(true), "shove cooldown persists after recovery"); yield return 1.4f;
            Aim(); var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.transform.position = home + new Vector3(0,1f,.65f); backWall.transform.localScale = new Vector3(3,3,.15f);
            Physics.SyncTransforms(); Check(combat.TryAttack(true), "shove recovers after cooldown"); yield return .35f;
            Check(zombie.transform.position.z < home.z + .3f, "shove cannot push walker through solid wall");
            UnityEngine.Object.Destroy(backWall); yield return 1.8f;
            Aim(); Check(combat.TryAttack(), "crowbar attack starts"); Check(!combat.TryAttack(), "cannot queue duplicate strike"); yield return .9f;
            Check(zombie.CurrentHealth == 25f && combat.SuccessfulStrikes == 1, "one swing deals exactly twenty damage");
            Aim(); combat.TryAttack(); yield return .9f; Check(zombie.CurrentHealth == 5f, "second strike leaves five health");
            Aim(); combat.TryAttack(); yield return .9f; Check(zombie.IsDead && combat.SuccessfulStrikes == 3, "third strike kills walker");
            var original = pocket.ActivePhysical;
            player.gameObject.SetActive(false); player.gameObject.SetActive(true); yield return .2f;
            Check(player.GetComponentsInChildren<MeleeWeapon>(true).Length == 1 && pocket.ActivePhysical == original, "reactivation does not duplicate starter weapon");
            player.SetMovementLocked(false); Time.timeScale = 0f;
            Check(!combat.TryAttack(), "paused game blocks combat"); Time.timeScale = 1f;
        }
    }
}
