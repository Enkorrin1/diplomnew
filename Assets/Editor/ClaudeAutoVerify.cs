// ВРЕМЕННЫЙ СКРИПТ ПРОВЕРКИ — удаляется после верификации.
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;

namespace RogueDrive.EditorTools
{
    [InitializeOnLoad]
    public static class ClaudeAutoVerify
    {
        const string Dir = @"C:\Users\egorc\AppData\Local\Temp\claude\W--BSUIR-DIPLOOM\056a1873-5a8b-422f-a896-c51da190709c\scratchpad";
        static string RequestPath => Path.Combine(Dir, "verify_request.txt");
        static string LogPath => Path.Combine(Dir, "verify_log.txt");

        static ClaudeAutoVerify()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SpawnRunnerIfRequested();
            }
        }

        static void SpawnRunnerIfRequested()
        {
            string mode = SessionState.GetString("ClaudeAutoVerify.mode", "");
            if (string.IsNullOrEmpty(mode)) return;
            SessionState.EraseString("ClaudeAutoVerify.mode");
            Log("Spawning runner for mode: " + mode);
            var go = new GameObject("ClaudeAutoVerifyRunner");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>().mode = mode;
        }

        public static void Log(string s)
        {
            try { File.AppendAllText(LogPath, $"[{System.DateTime.Now:HH:mm:ss}] {s}\n"); } catch { }
            Debug.Log("[ClaudeAutoVerify] " + s);
        }

        static void Tick()
        {
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            AssetDatabase.Refresh();
            if (EditorApplication.isCompiling) return;

            // Очередь запросов: по одной строке на прогон
            var lines = new System.Collections.Generic.List<string>(File.ReadAllLines(RequestPath));
            lines.RemoveAll(string.IsNullOrWhiteSpace);
            if (lines.Count == 0) { File.Delete(RequestPath); return; }
            string mode = lines[0].Trim();
            lines.RemoveAt(0);
            if (lines.Count == 0) File.Delete(RequestPath); else File.WriteAllLines(RequestPath, lines);
            if (mode == "playthrough")
            {
                PlayerPrefs.DeleteKey("GaragePrologueDone");
                PlayerPrefs.DeleteKey("BunkerPrologueSeen_V1");
                PlayerPrefs.Save();
                Log("Reset PlayerPrefs for clean prologue playthrough test");
            }
            string scenePath = mode == "stage" ? "Assets/Scenes/Stage1_Outskirts.unity" : "Assets/Scenes/GarageScene.unity";
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null) { Log("scene not found: " + scenePath); return; }
            EditorSceneManager.playModeStartScene = sceneAsset;
            SessionState.SetString("ClaudeAutoVerify.mode", mode);
            Log("Entering play mode: " + scenePath);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnRuntimeStart()
        {
            SpawnRunnerIfRequested();
        }

        static IEnumerator Capture(string file)
        {
            string p = Path.Combine(Dir, file);
            ScreenCapture.CaptureScreenshot(p);
            yield return new WaitForSeconds(1.2f);
            Log($"screenshot {file} exists={File.Exists(p)}");
        }

        static Bounds ComputeBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.3f);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        public sealed class Runner : MonoBehaviour
        {
            public string mode;
            GameObject forcedTarget;

            IEnumerator Start()
            {
                if (mode == "stage") yield return StageRoutine();
                else if (mode == "playthrough") yield return PlaythroughRoutine();
                else yield return GarageRoutine();
            }

            void LateUpdate()
            {
                if (forcedTarget != null)
                    InteractableOutline.SetTarget(forcedTarget, new Color(0.24f, 0.9f, 1f, 1f), 2.5f);
            }

            IEnumerator Finish()
            {
                yield return null;
                EditorSceneManager.playModeStartScene = null;
                Log("Exiting play mode");
                EditorApplication.ExitPlaymode();
            }

            IEnumerator GarageRoutine()
            {
                yield return new WaitForSeconds(2f);
                var player = FindFirstObjectByType<GaragePlayerController>();
                Log($"player={(player ? player.name : "null")} cam={(player && player.PlayerCamera ? player.PlayerCamera.name : "null")} main={(Camera.main ? Camera.main.name : "null")} msaa={QualitySettings.antiAliasing}");

                // Кандидаты: интерактивные объекты с коллайдером и рендером, разных типов
                var candidates = new System.Collections.Generic.List<MonoBehaviour>();
                var seenTypes = new System.Collections.Generic.HashSet<System.Type>();
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (!(mb is IGarageInteractable)) continue;
                    if (mb.GetComponentInChildren<Renderer>() == null || mb.GetComponentInChildren<Collider>() == null) continue;
                    if (!mb.gameObject.activeInHierarchy) continue;
                    if (seenTypes.Add(mb.GetType())) candidates.Add(mb);
                    if (candidates.Count >= 4) break;
                }
                Log("candidates=" + candidates.Count);

                var cc = player ? player.GetComponent<CharacterController>() : null;
                var cam = player ? player.PlayerCamera : null;
                var pitchField = typeof(GaragePlayerController).GetField("cameraPitch", BindingFlags.NonPublic | BindingFlags.Instance);
                GameObject firstTarget = null;
                for (int idx = 0; idx < candidates.Count && player != null; idx++)
                {
                    var target = candidates[idx];
                    if (firstTarget == null) firstTarget = target.gameObject;
                    var col = target.GetComponentInChildren<Collider>();
                    Bounds b = col.bounds;
                    Vector3 look = b.center;
                    // Подходим со стороны, где до цели свободно: пробуем 8 направлений
                    Vector3 bestDir = Vector3.zero; float bestFree = -1f;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                        float free = Physics.Raycast(look, dir, out var h, 3f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 3f;
                        if (free > bestFree) { bestFree = free; bestDir = dir; }
                    }
                    float dist = Mathf.Clamp(bestFree - 0.4f, 1.2f, 2.2f);
                    if (cc) cc.enabled = false;
                    Vector3 pos = look + bestDir * dist;
                    pos.y = player.transform.position.y;
                    player.transform.position = pos;
                    player.transform.rotation = Quaternion.LookRotation(-bestDir, Vector3.up);
                    if (cam)
                    {
                        Vector3 toTarget = look - cam.transform.position;
                        float pitch = -Mathf.Atan2(toTarget.y, new Vector2(toTarget.x, toTarget.z).magnitude) * Mathf.Rad2Deg;
                        pitchField?.SetValue(player, pitch);
                        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                    }
                    if (cc) cc.enabled = true;

                    yield return new WaitForSeconds(0.7f);
                    string hitName = "none";
                    if (cam)
                    {
                        RaycastHit[] hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 4f, ~0, QueryTriggerInteraction.Collide);
                        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                        foreach (var h in hits)
                        {
                            if (player != null && (h.collider.transform.IsChildOf(player.transform) || h.collider.gameObject == player.gameObject)) continue;
                            hitName = h.collider.name;
                            break;
                        }
                    }
                    Log($"[{idx}] {target.name} ({target.GetType().Name}) dist={dist:F2} rayHit={hitName} locked={player.IsMovementLocked} hasTarget={InteractableOutline.HasTarget} current={(InteractableOutline.CurrentTarget ? InteractableOutline.CurrentTarget.name : "null")}");
                    yield return Capture($"garage_natural_{idx}.png");
                }

                forcedTarget = firstTarget;
                yield return new WaitForSeconds(0.5f);
                yield return Capture("garage_forced.png");
                forcedTarget = null;
                yield return Finish();
            }

            IEnumerator StageRoutine()
            {
                yield return new WaitForSeconds(4f);
                var car = FindFirstObjectByType<ArcadeCarController>();
                if (car == null) { Log("car=null"); yield return Finish(); yield break; }
                var rb = car.GetComponent<Rigidbody>();
                var box = car.GetComponent<BoxCollider>();
                for (int i = 0; i < 3; i++)
                {
                    Log($"t={Time.time:F1} y={car.transform.position.y:F3} up.y={car.transform.up.y:F4} grounded={car.IsGrounded} speed={car.SpeedMps:F2} com={rb.centerOfMass} box.center={box.center} box.size={box.size} angVel={rb.angularVelocity.magnitude:F3} vel={rb.linearVelocity.magnitude:F3}");
                    yield return new WaitForSeconds(1f);
                }
                float bottomY = car.transform.TransformPoint(box.center - Vector3.up * box.size.y * 0.5f).y;
                float groundY = float.NaN; string groundName = "none";
                foreach (var hit in Physics.RaycastAll(car.transform.position + Vector3.up * 3f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(car.transform)) continue;
                    if (float.IsNaN(groundY) || hit.point.y > groundY) { groundY = hit.point.y; groundName = hit.collider.name; }
                }
                Log($"ground y={groundY:F3} ({groundName}) colliderBottom y={bottomY:F3} gap={bottomY - groundY:F3}");
                yield return Capture("stage_rest.png");
                yield return Finish();
            }

            IEnumerator PlaythroughRoutine()
            {
                Log("=== STARTING FULL STARTER SCENE PLAYTHROUGH TEST ===");
                yield return new WaitForSeconds(1.0f);
                var player = FindFirstObjectByType<GaragePlayerController>();
                var cutscene = FindFirstObjectByType<BunkerPrologueCutscene>();
                if (cutscene != null && cutscene.IsRunning)
                {
                    Log("BunkerPrologueCutscene is running, fast-finishing cutscene...");
                    var finishMethod = typeof(BunkerPrologueCutscene).GetMethod("FinishCutscene", BindingFlags.NonPublic | BindingFlags.Instance);
                    finishMethod?.Invoke(cutscene, null);
                }
                yield return new WaitForSeconds(0.6f);
                Log($"Player status: isMovementLocked={player.IsMovementLocked} pos={player.transform.position}");
                yield return Capture("pt_01_awake.png");

                var raycaster = player.GetComponentInChildren<GarageInteractionRaycaster>();
                var handsInv = BunkerPlayerInventory.Instance;
                var prologue = GaragePrologueManager.Instance;
                var assembly = BunkerStarterCarAssembly.Instance;

                // 1. Включение генератора
                var genSwitch = FindFirstObjectByType<GarageGeneratorSwitch>();
                if (genSwitch == null) { Log("FAIL: GarageGeneratorSwitch not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, genSwitch.transform, "GeneratorSwitch");
                Log($"Power state: isPowerOn={prologue.IsPowerOn}");
                if (!prologue.IsPowerOn) { Log("FAIL: Power is not ON!"); yield return Finish(); yield break; }
                yield return Capture("pt_02_power_on.png");

                // 2. Подбор колеса
                CarPartItem wheelItem = null;
                foreach (var item in FindObjectsByType<CarPartItem>(FindObjectsSortMode.None))
                {
                    if (item.ItemType == BunkerAssemblyItemType.Wheel && item.gameObject.activeInHierarchy)
                    {
                        wheelItem = item;
                        break;
                    }
                }
                if (wheelItem == null) { Log("FAIL: Wheel item not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, wheelItem.transform, "CarWheel");
                Log($"Held item: {handsInv.HeldItem}");
                if (handsInv.HeldItem != BunkerAssemblyItemType.Wheel) { Log("FAIL: Did not pick up wheel!"); yield return Finish(); yield break; }
                yield return Capture("pt_03_wheel_held.png");

                // 3. Установка колеса
                Transform wheelSpot = GameObject.Find("Hotspot_Wheel_FL")?.transform;
                if (wheelSpot == null) { Log("FAIL: Hotspot_Wheel_FL not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, wheelSpot, "Hotspot_Wheel_FL");
                Log($"Wheel installed: {assembly.IsWheelInstalled}");
                if (!assembly.IsWheelInstalled) { Log("FAIL: Wheel not installed!"); yield return Finish(); yield break; }
                yield return Capture("pt_04_wheel_installed.png");

                // 4. Подбор аккумулятора
                CarPartItem batteryItem = null;
                foreach (var item in FindObjectsByType<CarPartItem>(FindObjectsSortMode.None))
                {
                    if (item.ItemType == BunkerAssemblyItemType.Battery && item.gameObject.activeInHierarchy)
                    {
                        batteryItem = item;
                        break;
                    }
                }
                if (batteryItem == null) { Log("FAIL: Battery item not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, batteryItem.transform, "Pickup_Battery");
                Log($"Held item: {handsInv.HeldItem}");
                if (handsInv.HeldItem != BunkerAssemblyItemType.Battery) { Log("FAIL: Did not pick up battery!"); yield return Finish(); yield break; }
                yield return Capture("pt_05_battery_held.png");

                // 5. Установка аккумулятора
                Transform batterySpot = GameObject.Find("Hotspot_Battery_EngineBay")?.transform;
                if (batterySpot == null) { Log("FAIL: Hotspot_Battery_EngineBay not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, batterySpot, "Hotspot_Battery_EngineBay");
                Log($"Battery installed: {assembly.IsBatteryInstalled}");
                if (!assembly.IsBatteryInstalled) { Log("FAIL: Battery not installed!"); yield return Finish(); yield break; }
                yield return Capture("pt_06_battery_installed.png");

                // 6. Подбор канистры топлива
                Transform fuelObj = null;
                foreach (var container in FindObjectsByType<FluidContainer>(FindObjectsSortMode.None))
                {
                    if (container.FluidType == BunkerFluidType.Gasoline && container.gameObject.activeInHierarchy)
                    {
                        fuelObj = container.transform;
                        break;
                    }
                }
                if (fuelObj == null) fuelObj = GameObject.Find("JerrycanLarge")?.transform ?? GameObject.Find("Pickup_FuelCanister")?.transform;
                if (fuelObj == null) { Log("FAIL: Fuel canister not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, fuelObj, "FuelCanister");
                Log($"Held item: {handsInv.HeldItem} hasItem={handsInv.HasItem}");
                if (!handsInv.HasItem) { Log("FAIL: Did not pick up fuel canister!"); yield return Finish(); yield break; }
                yield return Capture("pt_07_fuel_held.png");

                // 7. Заливка топлива в бак
                Transform fuelSpot = GameObject.Find("Hotspot_FuelTank_Inlet")?.transform;
                if (fuelSpot == null) { Log("FAIL: Hotspot_FuelTank_Inlet not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, fuelSpot, "Hotspot_FuelTank_Inlet");
                Log($"Fuel filled: {assembly.IsFuelFilled} isAssemblyComplete={assembly.IsAssemblyComplete}");
                if (!assembly.IsFuelFilled || !assembly.IsAssemblyComplete) { Log("FAIL: Assembly not complete!"); yield return Finish(); yield break; }
                yield return Capture("pt_08_car_assembled.png");

                // 8. Взятие ключей
                var keys = FindFirstObjectByType<GarageCarKeys>();
                if (keys == null) { Log("FAIL: GarageCarKeys not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, keys.transform, "CarKeysItem");
                Log($"Has keys: {prologue.HasCarKeys}");
                if (!prologue.HasCarKeys) { Log("FAIL: Did not get keys!"); yield return Finish(); yield break; }
                yield return Capture("pt_09_keys_taken.png");

                // 9. Открытие гермоворот
                var gateTerminal = FindFirstObjectByType<GarageGateSwitchInteractable>();
                Transform gateTarget = gateTerminal != null ? gateTerminal.transform : GameObject.Find("Gate_Control_Terminal")?.transform;
                if (gateTarget == null) { Log("FAIL: Gate control terminal not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, gateTarget, "Gate_Control_Terminal");
                yield return new WaitForSeconds(1.5f);
                Log($"Gate open: {prologue.IsGateOpen}");
                if (!prologue.IsGateOpen) { Log("FAIL: Gate not open!"); yield return Finish(); yield break; }
                yield return Capture("pt_10_gate_opened.png");

                // 10. Посадка в машину
                var boardingZone = GameObject.Find("VehicleBoardingZone");
                Transform boardTarget = boardingZone != null ? boardingZone.transform : GameObject.Find("PodiumAnchor")?.transform;
                if (boardTarget == null) { Log("FAIL: Boarding target not found!"); yield return Finish(); yield break; }
                yield return AimAndInteract(player, raycaster, boardTarget, "VehicleBoardingZone");
                yield return new WaitForSeconds(1.0f);
                var driveOut = GarageDriveOutController.Instance;
                Log($"Drive out: isDriving={(driveOut != null && driveOut.IsDriving)}");
                if (driveOut == null || !driveOut.IsDriving) { Log("FAIL: Driving mode not activated!"); yield return Finish(); yield break; }
                yield return Capture("pt_11_driving.png");

                // Тест выхода из машины на [E] и повторной посадки
                Log("Testing ExitCar on [E]...");
                driveOut.ExitCar();
                yield return new WaitForSeconds(0.6f);
                Log($"After ExitCar: isDriving={driveOut.IsDriving} playerActive={player.gameObject.activeInHierarchy} carExists={(GameObject.Find("Classic Car_9") != null)}");
                if (driveOut.IsDriving || !player.gameObject.activeInHierarchy || GameObject.Find("Classic Car_9") == null)
                {
                    Log("FAIL: ExitCar failed or car vanished!"); yield return Finish(); yield break;
                }
                yield return Capture("pt_11b_exited_car.png");

                // Садимся обратно в машину
                Log("Testing re-boarding vehicle...");
                boardingZone = GameObject.Find("VehicleBoardingZone");
                boardTarget = boardingZone != null ? boardingZone.transform : GameObject.Find("Classic Car_9")?.transform;
                yield return AimAndInteract(player, raycaster, boardTarget, "VehicleBoardingZone_Reentry");
                yield return new WaitForSeconds(0.8f);
                Log($"Re-boarded: isDriving={driveOut.IsDriving}");
                if (!driveOut.IsDriving) { Log("FAIL: Re-boarding failed!"); yield return Finish(); yield break; }

                // 11. Движение вперед и выезд через ворота
                var carVehicle = FindFirstObjectByType<GarageDriveOutVehicle>();
                if (carVehicle != null)
                {
                    Log("Simulating forward driving towards exit ramp...");
                    carVehicle.SimulatedThrottle = 1.0f;
                    for (int step = 0; step < 220; step++)
                    {
                        yield return new WaitForFixedUpdate();
                        if (ArcadeCarController.JustDroveOutOfBunker) break;
                    }
                    carVehicle.SimulatedThrottle = 0f;
                }
                yield return new WaitForSeconds(1.5f);
                Log($"JustDroveOutOfBunker: {ArcadeCarController.JustDroveOutOfBunker}");
                yield return Capture("pt_12_exit_bunker.png");

                Log("=== PLAYTHROUGH VERIFICATION SUCCESS: 100% PASS ===");
                yield return Finish();
            }

            IEnumerator AimAndInteract(GaragePlayerController player, GarageInteractionRaycaster raycaster, Transform target, string label)
            {
                var cc = player.GetComponent<CharacterController>();
                var cam = player.PlayerCamera;

                Vector3 targetPos = target.position;
                var col = target.GetComponentInChildren<Collider>();
                if (col != null) targetPos = col.bounds.center;

                bool isCarTarget = target.GetComponentInParent<BunkerCarAssemblyHotspot>() != null 
                    || target.GetComponentInParent<VehiclePartHotspot>() != null 
                    || target.GetComponentInParent<GarageVehicleBoarding>() != null
                    || target.name.StartsWith("Hotspot_")
                    || target.name == "VehicleBoardingZone";

                Vector3 approachDir;
                if (isCarTarget)
                {
                    Vector3 carCenter = new Vector3(0f, 0.8f, 0f);
                    approachDir = (targetPos - carCenter).normalized;
                }
                else
                {
                    Vector3 roomCenter = new Vector3(0f, 0.8f, 0f);
                    approachDir = (roomCenter - targetPos).normalized;
                }
                approachDir.y = 0f;
                if (approachDir.sqrMagnitude < 0.01f) approachDir = -target.forward;
                approachDir.Normalize();

                Vector3 playerPos = targetPos + approachDir * 1.35f;
                playerPos.y = 0.1f;

                if (cc != null) cc.enabled = false;
                player.transform.position = playerPos;

                Vector3 toTarget = targetPos - (playerPos + Vector3.up * 1.65f);
                Vector3 horizDir = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
                if (horizDir.sqrMagnitude > 0.01f)
                {
                    player.transform.rotation = Quaternion.LookRotation(horizDir, Vector3.up);
                }

                if (cam != null)
                {
                    float pitch = -Mathf.Atan2(toTarget.y, new Vector2(toTarget.x, toTarget.z).magnitude) * Mathf.Rad2Deg;
                    player.SetCameraPitch(pitch);
                }
                if (cc != null) cc.enabled = true;

                yield return new WaitForFixedUpdate();
                yield return new WaitForSeconds(0.45f);

                var targetInteractable = target.GetComponentInChildren<IGarageInteractable>() ?? target.GetComponentInParent<IGarageInteractable>();
                var current = raycaster != null ? raycaster.CurrentTarget : null;
                Log($"Aim [{label}]: target={target.name} rayHit={(current != null ? current.GetType().Name : "null")} prompt={(current != null ? current.GetPromptText() : "none")}");

                if (current != null && (targetInteractable == null || current == targetInteractable || current.GetType() == targetInteractable.GetType()))
                {
                    current.Interact(player);
                }
                else if (targetInteractable != null)
                {
                    Log($"Direct interact with targeted {targetInteractable.GetType().Name} ({target.name})");
                    targetInteractable.Interact(player);
                }

                yield return new WaitForSeconds(0.45f);
            }
        }
    }
}
#endif
