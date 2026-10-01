using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Gameplay.Hub
{
    // A first-stage checkpoint restores authored identities, not transient Unity instance IDs.
    public sealed class GarageDepartureCheckpoint : MonoBehaviour
    {
        const string PreparedKey = "GarageDeparture.Prepared.v1", ArrivalKey = "GarageDeparture.Arrival.v1";
        static string preparedPreview, arrivalPreview;
        static Snapshot pending;
        static bool autoDepart;
        static bool activeDeparture;
        public static bool HasPendingRestore => pending != null;
        public static bool CanContinue => JourneyCheckpoint.CanContinue || ReadSnapshot(true) != null || ReadSnapshot(false) != null;
        public static bool CanPrepare => JourneyCheckpoint.CanContinue || ReadSnapshot(false) != null;

        static Snapshot ReadSnapshot(bool prepared)
        {
            string json = prepared ? preparedPreview : arrivalPreview;
            if (string.IsNullOrEmpty(json)) json = PlayerPrefs.GetString(prepared ? PreparedKey : ArrivalKey, "");
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var state = JsonUtility.FromJson<Snapshot>(json);
                return IsValidSnapshot(state) ? state : null;
            }
            catch (Exception) { return null; }
        }

        public static bool ContinueJourney() => JourneyCheckpoint.CanContinue ? JourneyCheckpoint.RequestRestore(true) : ReadSnapshot(true) != null ? RequestRestore(true) : RequestRestore(false);

        public static void ClearJourney()
        {
            JourneyCheckpoint.Clear();
            ResetSession();
            autoDepart = false;
            PlayerPrefs.DeleteKey(PreparedKey);
            PlayerPrefs.DeleteKey(ArrivalKey);
            PlayerPrefs.Save();
        }
        public static bool HasPrepared => !string.IsNullOrEmpty(preparedPreview) || PlayerPrefs.HasKey(PreparedKey);
        public static bool OwnsCurrentRun => activeDeparture &&
            (SceneManager.GetActiveScene().name == "Stage1_Outskirts" || SeamlessJourneyStream.Instance != null) && HasPrepared;
        public static bool Restoring { get; private set; }
        public static string LastError { get; private set; }

        [Serializable] public sealed class Snapshot
        {
            public int version = 1;
            public bool briefing;
            public bool power, keys, gate;
            public Vector3 playerPosition;
            public Quaternion playerRotation;
            public string car, needs, workshop;
            public List<ItemState> items = new List<ItemState>();
            public SlotState[] cargo, pockets;
            public bool journey;
        }
        [Serializable] public sealed class ItemState
        {
            public string id, fluid, function, workshopPart, template, journeyId;
            public bool hasFirearm;
            public int firearmAmmo;
            public Vector3 position, scale;
            public Quaternion rotation;
            public bool active;
            public string[] contents;
        }
        [Serializable] public sealed class SlotState
        {
            public string id, title;
            public int count;
            public BunkerAssemblyItemType legacy;
            public string[] objects;
            public Vector3 scale;
            public float viewScale;
        }

        public static bool IsValidSnapshot(Snapshot state)
        {
            if (state == null || state.version != 1 || string.IsNullOrEmpty(state.car)
                || state.items == null || state.cargo == null || state.pockets == null
                || state.pockets.Length != PlayerPocketInventory.SlotCount || state.cargo.Length > 64)
                return false;
            var ids = new HashSet<string>();
            foreach (var item in state.items)
                if (item == null || string.IsNullOrEmpty(item.id) || !ids.Add(item.id)) return false;
            foreach (var slot in state.cargo.Concat(state.pockets))
            {
                if (slot == null || slot.count < 0 || slot.objects == null) return false;
                if (slot.objects.Any(id => string.IsNullOrEmpty(id) || !ids.Contains(id))) return false;
            }
            return true;
        }
        // Deliberately omit Unity object references from component JSON.
        [Serializable] class CarData
        {
            public float currentEngineOil = 5f;
            public float oilPanIntegrity = 1f;
            public float currentFuelLiters, currentRadiatorWater, engineTemperature, bumperIntegrity;
            public float[] tireIntegrity;
            public bool hasCowcatcher;
        }
        [Serializable] class FunctionData
        {
            public float charge, waterLiters, heatSeconds;
            public int portions;
            public bool boiled;
        }
        [Serializable] class FluidData
        {
            public BunkerFluidType fluidType;
            public float maxCapacityLiters, currentLiters;
        }
        [Serializable] class NeedsData { public float health, food, water; }
        static string Data<T>(MonoBehaviour component) => component == null ? null : JsonUtility.ToJson(JsonUtility.FromJson<T>(JsonUtility.ToJson(component)));
        static string Id(GameObject item)
        {
            if (item == null) return null;
            var identity = item.GetComponent<GarageCheckpointItem>();
            if (identity != null && !string.IsNullOrEmpty(identity.id)) return identity.id;
            if (identity == null) identity = item.AddComponent<GarageCheckpointItem>();
            if (item.GetComponent<MeleeWeapon>() != null) identity.id = "combat_crowbar";
            else identity.id = "cargo_" + item.name.Replace("(Clone)", "").Trim().Replace(" ", "_").ToLowerInvariant() + "_" + Guid.NewGuid().ToString("N");
            return identity.id;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { preparedPreview = arrivalPreview = null; pending = null; Restoring = false; LastError = null; activeDeparture = false; }

        public static Snapshot Capture()
        {
            var player = FindFirstObjectByType<GaragePlayerController>(FindObjectsInactive.Include);
            var manager = GaragePrologueManager.Instance;
            var trunk = VehicleCargoTrunk.Instance;
            var pocket = PlayerPocketInventory.Instance;
            bool journey=SeamlessJourneyStream.Instance!=null;
            if (player == null || (!journey&&manager == null) || trunk == null || pocket == null) throw new InvalidOperationException("Departure actors are not ready.");
            var state = new Snapshot {
                briefing = GarageDeparturePreparation.Instance != null && GarageDeparturePreparation.Instance.BriefingSeen,
                journey=journey,power = manager!=null&&manager.IsPowerOn, keys = manager!=null&&manager.HasCarKeys, gate = manager!=null&&manager.IsGateOpen,
                playerPosition = player.transform.position, playerRotation = player.transform.rotation,
                car = Data<CarData>(VehicleModularState.Instance), needs = Data<NeedsData>(PlayerFieldNeeds.For(player)),
                workshop = VehicleWorkshop.For(VehicleModularState.Instance)?.Capture(),
                cargo = Enumerable.Range(0, trunk.MaxSlots).Select(i => CaptureSlot(trunk.GetSlot(i))).ToArray(),
                pockets = Enumerable.Range(0, PlayerPocketInventory.SlotCount).Select(i => CaptureSlot(pocket.GetSlot(i))).ToArray()
            };
            foreach (var item in FindObjectsByType<GarageCheckpointItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!journey&&item.gameObject.scene.name != "GarageScene") continue;
                if(item.GetComponent<WorkshopPartItem>()?.Retired??false)continue;
                state.items.Add(new ItemState { id = item.id, active = item.gameObject.activeSelf,
                    template=item.GetComponent<JourneyItemTemplate>()?.ResourceKey,
                    journeyId=item.GetComponent<JourneyPersistentObject>()?.Id,
                    position = item.transform.position, rotation = item.transform.rotation, scale = item.transform.lossyScale,
                    fluid = Data<FluidData>(item.GetComponent<FluidContainer>()), function = Data<FunctionData>(item.GetComponent<GarageItemFunction>()),
                    workshopPart = item.GetComponent<WorkshopPartItem>()!=null?JsonUtility.ToJson(item.GetComponent<WorkshopPartItem>().data):null,
                    hasFirearm = item.GetComponent<FirearmWeapon>() != null,
                    firearmAmmo = item.GetComponent<FirearmWeapon>()?.CurrentAmmo ?? 0,
                    contents = item.GetComponent<GaragePortableContainer>()?.CheckpointContents().Select(Id).ToArray() });
            }
            return state;
        }
        static SlotState CaptureSlot(PocketSlotData slot)
        {
            var ids = new List<string>();
            if (slot.WorldObject != null) { var id = Id(slot.WorldObject); if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Unregistered cargo: " + slot.displayName); ids.Add(id); }
            if (slot.reserves != null) foreach (var entry in slot.reserves) { var id = Id(entry.item != null ? entry.item.gameObject : null); if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Unregistered stacked item"); ids.Add(id); }
            return new SlotState { id = slot.id, title = slot.displayName, count = slot.count, legacy = slot.legacyType, objects = ids.ToArray(), scale = slot.worldScale, viewScale = slot.viewScale };
        }
        public static void CaptureArrival()
        {
            Save(Capture(), false);
        }
        public static bool CapturePrepared()
        {
            if(VehicleWorkshop.For(VehicleModularState.Instance)?.Busy??false){LastError="Завершите обслуживание машины.";return false;}
            if (Restoring) return true;
            try
            {
                if (PlayerHandsInventory.Instance != null && PlayerHandsInventory.Instance.HasItem) PlayerHandsInventory.Instance.DropItem();
                Save(Capture(), true); activeDeparture = true; LastError = null; return true;
            }
            catch (Exception e) { LastError = e.Message; Debug.LogError("[DepartureCheckpoint] " + e.Message); return false; }
        }
        static void Save(Snapshot snapshot, bool prepared)
        {
            string json = JsonUtility.ToJson(snapshot);
            if (prepared) preparedPreview = json; else arrivalPreview = json;
            // Play Mode validation never writes over the player's expedition.
            if (GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsPreviewRun) return;
            PlayerPrefs.SetString(prepared ? PreparedKey : ArrivalKey, json); PlayerPrefs.Save();
        }
        public static bool RequestRestore(bool depart)
        {
            if(JourneyCheckpoint.CanContinue&&(JourneyCheckpoint.OwnsCurrentRun||SceneManager.GetActiveScene().name=="MainMenuScene"))return JourneyCheckpoint.RequestRestore(depart);
            string json = depart ? preparedPreview : arrivalPreview;
            if (string.IsNullOrEmpty(json)) json = PlayerPrefs.GetString(depart ? PreparedKey : ArrivalKey, "");
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                var restored = ReadSnapshot(depart);
                if (restored == null) return false;
                pending = restored; autoDepart = depart; activeDeparture = depart;
                GaragePrologueManager.ForcePrologueAwakening = false;
                Time.timeScale = 1;
                SceneManager.LoadScene("GarageScene");
                return true;
            }
            catch (Exception e) { pending = null; Debug.LogError("[DepartureCheckpoint] " + e.Message); return false; }
        }
        IEnumerator Start()
        {
            if (pending == null) yield break;
            Restoring = true;
            yield return null; // All authored inventory components must finish Awake/Start first.
            var state = pending;
            bool depart = autoDepart;
            try { Apply(state); pending = null; }
            catch (Exception e) { LastError = e.Message; pending = null; Restoring = false; Debug.LogException(e); yield break; }
            var player = FindFirstObjectByType<GaragePlayerController>();
            if (depart)
            {
                var gate = FindFirstObjectByType<GarageGateSwitchInteractable>();
                gate?.Interact(player);
                yield return new WaitForSeconds(3f);
                var boarding = FindFirstObjectByType<GarageVehicleBoarding>();
                boarding?.Interact(player);
                // Place the prepared car at the ramp; no repeated assembly or awakening.
                yield return new WaitForSeconds(3f);
                var vehicle = FindFirstObjectByType<GarageDriveOutVehicle>();
                var exit = GarageSceneExitCinematic.Pending;
                if (vehicle != null && exit != null)
                {
                    float deadline = Time.realtimeSinceStartup + 20;
                    while (!exit.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
                    exit.TryBegin(vehicle);
                }
            }
            Restoring = false;
        }
        public static void Apply(Snapshot state)
        {
            if (!IsValidSnapshot(state)) throw new InvalidOperationException("Invalid departure checkpoint.");
            var identities = FindObjectsByType<GarageCheckpointItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(i => (state.journey||i.gameObject.scene.name == "GarageScene")&&!(i.GetComponent<WorkshopPartItem>()?.Retired??false)).GroupBy(i=>i.id).ToDictionary(g=>g.Key,g=>g.First().gameObject);
            foreach (var item in state.items)
                if (!identities.ContainsKey(item.id))
                {
                    var template=!string.IsNullOrEmpty(item.template)?Resources.Load<GameObject>(item.template):null;
                    var generated = template!=null?Instantiate(template):!string.IsNullOrEmpty(item.workshopPart)
                        ? WorkshopPartItem.Create(JsonUtility.FromJson<WorkshopPartData>(item.workshopPart), item.id).WorldObject
                        : (item.id == "combat_crowbar" || item.id.StartsWith("combat_")
                            ? RestoreCombatItem(item.id)
                            : VehicleServiceInventory.RestoreGeneratedItem(item.id));
                    if (generated != null)
                    {
                        (generated.GetComponent<GarageCheckpointItem>()??generated.AddComponent<GarageCheckpointItem>()).id=item.id;
                        identities.Add(item.id, generated);
                    }
                }
            foreach (var slot in state.cargo.Concat(state.pockets)) foreach (var id in slot.objects)
                if (!identities.ContainsKey(id)) throw new InvalidOperationException("Missing checkpoint item " + id);
            var pocket = PlayerPocketInventory.Instance; var trunk = VehicleCargoTrunk.Instance;
            PlayerHandsInventory.Instance?.DropItem(forStorage: true);
            for (int i = 0; i < PlayerPocketInventory.SlotCount; i++) pocket.SetGridSlot(i, PocketSlotData.Empty);
            for (int i = 0; i < trunk.MaxSlots; i++) trunk.SetGridSlot(i, PocketSlotData.Empty);
            VehicleWorkshop.For(VehicleModularState.Instance)?.Restore(state.workshop);
            foreach (var pair in identities) pair.Value.SetActive(false);
            foreach (var item in state.items)
            {
                if (!identities.TryGetValue(item.id, out var obj)) continue; // consumed assembly items need no respawn
                obj.transform.SetParent(null, false); obj.transform.SetPositionAndRotation(item.position, item.rotation); obj.transform.localScale = item.scale;
                obj.GetComponent<PhysicsProp>()?.OnDropped(Vector3.zero, Vector3.zero);
                obj.GetComponent<CarPartItem>()?.OnDropped(Vector3.zero, Vector3.zero);
                if (!string.IsNullOrEmpty(item.fluid)) JsonUtility.FromJsonOverwrite(item.fluid, obj.GetComponent<FluidContainer>());
                if (!string.IsNullOrEmpty(item.function)) JsonUtility.FromJsonOverwrite(item.function, obj.GetComponent<GarageItemFunction>());
                if (!string.IsNullOrEmpty(item.workshopPart))obj.GetComponent<WorkshopPartItem>().data=JsonUtility.FromJson<WorkshopPartData>(item.workshopPart);
                if (item.hasFirearm && obj.GetComponent<FirearmWeapon>() is FirearmWeapon firearm) firearm.SetAmmo(item.firearmAmmo);
                obj.SetActive(item.active);
            }
            foreach (var item in state.items) if (item.contents != null && identities.TryGetValue(item.id, out var container))
                container.GetComponent<GaragePortableContainer>()?.RestoreCheckpointContents(item.contents.Where(identities.ContainsKey).Select(id => identities[id]));
            for (int i = 0; i < state.cargo.Length; i++) trunk.SetGridSlot(i, RestoreSlot(state.cargo[i], identities));
            for (int i = 0; i < state.pockets.Length; i++) pocket.SetGridSlot(i, RestoreSlot(state.pockets[i], identities));
            JsonUtility.FromJsonOverwrite(state.car, VehicleModularState.Instance);
            var player = FindFirstObjectByType<GaragePlayerController>(FindObjectsInactive.Include);
            if (!string.IsNullOrEmpty(state.needs)) JsonUtility.FromJsonOverwrite(state.needs, PlayerFieldNeeds.For(player));
            var cc = player.GetComponent<CharacterController>(); bool enabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(state.playerPosition, state.playerRotation);
            if (cc != null) cc.enabled = enabled;
            player.SetMovementLocked(false);
            player.GetComponent<PlayerFirearmCombat>()?.MarkStarterLoadoutIssued();
            if (player != null && player.GetComponentInChildren<MeleeWeapon>(true) == null
                && FindFirstObjectByType<MeleeWeapon>(FindObjectsInactive.Include) == null)
            {
                player.GetComponent<PlayerMeleeCombat>()?.EnsureStarterWeapon();
            }
            if(!state.journey)
            {
                BunkerStarterCarAssembly.Instance.RestorePreparedAssembly();
                GaragePrologueManager.Instance.RestorePreparation(state.power, state.keys);
                GarageDeparturePreparation.Instance?.RestoreProgress(state.briefing);
                GaragePrologueManager.Instance.RefreshObjective();
            }
            Physics.SyncTransforms();
        }
        static GameObject RestoreCombatItem(string id)
        {
            var prefab = Resources.Load<GameObject>(id.StartsWith("combat_pistol") ? "Combat/Pistol" : "Combat/Crowbar");
            if (prefab == null) return null;
            var obj = UnityEngine.Object.Instantiate(prefab);
            var identity = obj.GetComponent<GarageCheckpointItem>() ?? obj.AddComponent<GarageCheckpointItem>();
            identity.id = id;
            return obj;
        }
        static PocketSlotData RestoreSlot(SlotState state, Dictionary<string, GameObject> objects)
        {
            if (state.count <= 0) return PocketSlotData.Empty;
            PocketSlotData slot;
            if (state.objects.Length > 0)
            {
                slot = InventoryStackOps.FromObject(objects[state.objects[0]], state.scale);
                slot.reserves = state.objects.Skip(1).Select(id => new PocketPhysicalEntry { item = objects[id].GetComponent<PhysicsProp>(), scale = objects[id].transform.lossyScale, viewScale = state.viewScale }).ToList();
            }
            else slot = new PocketSlotData { id = state.id, displayName = state.title, legacyType = state.legacy };
            slot.count = state.count; return VehicleServiceInventory.MaterializeLegacy(slot);
        }
    }
}
