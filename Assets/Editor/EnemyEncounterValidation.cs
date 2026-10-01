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
    /// <summary>Integration checks against the real Stage1 scene. Run in a fresh Play session.</summary>
    public static class EnemyEncounterValidation
    {
        static IEnumerator<float> checks;
        static double next;
        static readonly List<string> report = new List<string>();
        static EncounterZombie zombie;
        static GaragePlayerController player;
        static ArcadeCarController car;
        static PlayerFieldNeeds health;
        static Vector3 home;

        [MenuItem("RogueDrive/Enemies/Run Play Mode Checks")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Start Stage1 Play Mode first.");
            if (checks != null) throw new InvalidOperationException("Checks already running.");
            report.Clear();
            checks = CheckEncounter().GetEnumerator();
            next = 0;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!Application.isPlaying) { Finish("CANCELLED: Play Mode stopped"); return; }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (!checks.MoveNext()) { Finish("PASS: all encounter checks"); return; }
                next = EditorApplication.timeSinceStartup + checks.Current;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            checks?.Dispose(); checks = null;
            report.Add(result);
            Directory.CreateDirectory("Temp/EnemyValidation");
            File.WriteAllLines("Temp/EnemyValidation/report.txt", report);
            Debug.Log("[EnemyValidation] " + result);
        }

        static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            report.Add("PASS: " + description);
        }

        static IEnumerable<float> Until(Func<bool> predicate, string description, float timeout = 12f)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (!predicate() && EditorApplication.timeSinceStartup < deadline) yield return .02f;
            Check(predicate(), description);
        }

        static void MovePlayer(Vector3 point)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = point;
            cc.enabled = true;
            player.SetMovementLocked(true);
            Physics.SyncTransforms();
        }

        static void MoveCar(Vector3 point)
        {
            var rb = car.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            car.transform.position = point;
            car.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
        }

        static IEnumerable<float> CheckEncounter()
        {
            var enemies = UnityEngine.Object.FindObjectsByType<EncounterZombie>(FindObjectsSortMode.None);
            Check(enemies.Length == 3, "three authored walkers");
            zombie = enemies.First(z => z.name == "RoadsideWalker_1");
            foreach (var other in enemies) if (other != zombie) other.gameObject.SetActive(false);
            home = zombie.HomePosition;
            var drive = GarageDriveOutController.Instance;
            drive.ExitCar();
            player = UnityEngine.Object.FindFirstObjectByType<GaragePlayerController>();
            car = UnityEngine.Object.FindFirstObjectByType<ArcadeCarController>();
            health = PlayerFieldNeeds.For(player);
            var run = UnityEngine.Object.FindFirstObjectByType<GameRunController>();
            var storm = UnityEngine.Object.FindFirstObjectByType<Gameplay.Track.CreepingStormBarrier>();
            if (storm != null) storm.enabled = false;
            MoveCar(home + Vector3.right * 80f);
            car.GetComponent<GarageDriveOutVehicle>().enabled = false;
            MovePlayer(home + Vector3.forward * 8f);
            zombie.transform.rotation = Quaternion.identity;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Verification_SightBlocker";
            wall.transform.position = home + new Vector3(0f, 1.8f, 4f);
            wall.transform.localScale = new Vector3(6f, 3.6f, .5f);
            var obstacle = wall.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            Physics.SyncTransforms();
            yield return 1f;
            Check(zombie.CurrentTarget == null && zombie.SuccessfulHits == 0, "wall blocks initial visual detection");
            UnityEngine.Object.Destroy(wall);
            foreach (float wait in Until(() => zombie.State == EncounterZombie.BehaviourState.Windup, "visible player is approached and attack telegraphed")) yield return wait;
            float before = health.Health;
            float hullBefore = run.Health;
            yield return .3f;
            Check(health.Health == before, "windup does not cause immediate contact damage");
            MovePlayer(zombie.transform.position + Vector3.forward * 7f);
            foreach (float wait in Until(() => zombie.State == EncounterZombie.BehaviourState.Recovery, "dodged swing reaches recovery")) yield return wait;
            Check(health.Health == before, "moving out of reach during windup avoids damage");
            MovePlayer(zombie.transform.position + zombie.transform.forward * 1.05f);
            foreach (float wait in Until(() => health.Health < before, "next melee swing damages pedestrian")) yield return wait;
            Check(Mathf.Approximately(health.Health, before - 12f), "one swing applies exactly 12 character damage");
            Check(run.Health == hullBefore, "pedestrian hit leaves vehicle health unchanged");
            yield return .3f;
            Check(Mathf.Approximately(health.Health, before - 12f), "recovery prevents repeated per-frame hits");

            // A solid obstacle inserted between an already known target and the walker must
            // force loss of sight and a real detour rather than a straight transform move.
            MovePlayer(home + new Vector3(0f, 0f, 15f));
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Verification_PathBlocker";
            wall.transform.position = (zombie.transform.position + player.transform.position) * .5f + Vector3.up * 1.8f;
            wall.transform.localScale = new Vector3(5f, 3.6f, .7f);
            obstacle = wall.AddComponent<NavMeshObstacle>(); obstacle.carving = true;
            Physics.SyncTransforms();
            yield return 1.5f;
            MovePlayer(home + new Vector3(0f, 0f, 13f));
            var agent = zombie.GetComponent<NavMeshAgent>();
            var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(zombie.transform.position, player.transform.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 3,
                "navigation routes around a carved physical blocker");
            UnityEngine.Object.Destroy(wall);

            player.gameObject.SetActive(false);
            foreach (float wait in Until(() => zombie.State == EncounterZombie.BehaviourState.Search, "lost target enters search")) yield return wait;
            foreach (float wait in Until(() => zombie.State == EncounterZombie.BehaviourState.Return || zombie.State == EncounterZombie.BehaviourState.Idle,
                "search expires without omniscient pursuit")) yield return wait;
            zombie.TakeDamage(7f);
            float wounded = zombie.CurrentHealth;
            zombie.gameObject.SetActive(false); zombie.gameObject.SetActive(true);
            Check(zombie.CurrentHealth == wounded, "deactivation preserves wounded health");
            foreach (float wait in Until(() => agent.enabled && agent.isOnNavMesh,
                "reactivated walker waits for and rejoins navigation")) yield return wait;

            agent.Warp(home);
            zombie.transform.rotation = Quaternion.identity;
            MoveCar(home + new Vector3(0f, -.3f, 4.3f));
            float passengerHealth = health.Health;
            hullBefore = run.Health;
            float walkerHealth = zombie.CurrentHealth;
            foreach (float wait in Until(() => run.Health < hullBefore, "walker approaches and attacks parked vehicle", 18f)) yield return wait;
            Check(health.Health == passengerHealth, "vehicle hit does not damage inactive passenger");
            Check(zombie.CurrentHealth == walkerHealth, "parked vehicle contact does not kill or damage walker");

            agent.Warp(home);
            // Keep the hull above the ground. Spawning it below the asphalt makes
            // the solver eject it upward before it can contact the walker.
            MoveCar(home + new Vector3(0f, .2f, -4.5f));
            var ramBody = car.GetComponent<Rigidbody>();
            ramBody.isKinematic = false;
            ramBody.useGravity = false;
            ramBody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            ramBody.linearVelocity = Vector3.forward * 14f;
            foreach (float wait in Until(() => zombie.IsDead, "high-speed physical ram kills walker using pre-impact speed", 6f)) yield return wait;
            ramBody.linearVelocity = Vector3.zero;
            ramBody.angularVelocity = Vector3.zero;
            MoveCar(home + Vector3.right * 80f);
            player.gameObject.SetActive(true);
            MovePlayer(home + Vector3.forward * 45f);
            float injured = health.Health;
            Check(health.Heal(10f) && Mathf.Approximately(health.Health, injured + 10f), "healing restores character health");
            float food = health.Food, water = health.Water;
            health.Advance(36000f);
            Check(health.Food == food && health.Water == water, "elapsed time no longer drains hunger or thirst");
            int deaths = 0, ends = 0;
            health.Died += () => deaths++;
            run.RunEnded += reason => ends++;
            health.TakeDamage(1000f); health.TakeDamage(1000f);
            Check(health.IsDead && health.Health == 0f && deaths == 1 && ends == 1 && run.IsGameOver
                && run.EndReason == "Персонаж погиб", "lethal damage ends the attempt exactly once");
            Check(!health.Heal(100f), "medkit cannot resurrect a dead character");
            yield return 2.7f;
            Check(!zombie.gameObject.activeSelf, "dead walker leaves combat after death presentation");
            zombie.gameObject.SetActive(true);
            Check(zombie.IsDead && !zombie.gameObject.activeSelf, "dead walker cannot respawn on site reactivation");
        }
    }
}
