using UnityEngine;
using RogueDrive.Gameplay.Narrative;

namespace RogueDrive.Gameplay.Hub
{
    public sealed class GarageItemUse : MonoBehaviour
    {
        public enum UseKind { Flashlight, Radio, Repair, Fluid, SpareWheel, FirstAid, Container }
        [SerializeField] private UseKind kind;
        [SerializeField] private Light beam;
        private float nextUse;
        public UseKind Kind => kind;
        public bool LightOn => GetComponent<GarageItemFunction>() != null ? GetComponent<GarageItemFunction>().IsRunning : beam != null && beam.enabled;
        public bool HasPocketAction => GetComponent<GarageItemFunction>() != null || kind != UseKind.FirstAid;
        public string ActionLabel => GetComponent<GarageItemFunction>() != null ? GetComponent<GarageItemFunction>().ActionLabel : kind switch
        {
            UseKind.Flashlight => LightOn ? "Выключить свет" : "Включить свет", UseKind.Radio => "Слушать рацию",
            UseKind.Repair => "Обслужить крепления", UseKind.Fluid => "Перелить",
            UseKind.SpareWheel => "Заменить шину", UseKind.FirstAid => "Убрать аптечку",
            _ => "Содержимое"
        };
        public void Configure(UseKind value, Light lamp = null) { kind = value; beam = lamp; }
        private void LateUpdate()
        {
            var hands = PlayerHandsInventory.Instance;
            if (beam != null && hands != null && (hands.HeldGameObject == gameObject ||
                (PlayerPocketInventory.Instance != null && PlayerPocketInventory.Instance.ActivePhysical == GetComponent<PhysicsProp>() && !hands.HasItem)))
            {
                var camera = hands.GetComponent<GaragePlayerController>()?.PlayerCamera;
                if (camera != null) beam.transform.rotation = camera.transform.rotation;
            }
        }
        public bool Use(GaragePlayerController player)
        {
            if (Time.unscaledTime < nextUse) return false;
            nextUse = Time.unscaledTime + .5f;
            var hands = PlayerHandsInventory.Instance;
            bool fromPocket = PlayerPocketInventory.Instance != null && PlayerPocketInventory.Instance.ActivePhysical != null &&
                PlayerPocketInventory.Instance.ActivePhysical.gameObject == gameObject;
            if (hands == null || (hands.HeldGameObject != gameObject && (!fromPocket || hands.HasItem))) return false;
            var functional = GetComponent<GarageItemFunction>();
            var aimed = player != null ? player.GetComponentInChildren<GarageInteractionRaycaster>()?.CurrentTarget as Component : null;
            if (functional != null) return functional.Use(player, aimed != null ? aimed.GetComponent<GarageItemFunction>() : null);
            var carriedFluid=GetComponent<FluidContainer>();
            var receiver=aimed!=null?aimed.GetComponent<GarageItemFunction>():null;
            if(carriedFluid!=null && receiver!=null) return receiver.ReceiveWater(carriedFluid);
            if (kind == UseKind.Flashlight) { if (beam == null) return false; beam.enabled = !beam.enabled; return true; }
            if (kind == UseKind.Radio)
            {
                nextUse = Time.unscaledTime + 12f;
                var radio = RadioTransmissionSystem.Instance;
                if (radio == null) radio = new GameObject("PortableRadioBroadcast").AddComponent<RadioTransmissionSystem>();
                radio.PlayBunkerWakeup(); return true;
            }
            if (kind == UseKind.Container)
            { Notify($"В ящике {GetComponent<GaragePortableContainer>().Count} предметов. Положите его и нажмите F, чтобы достать."); return true; }
            if (kind == UseKind.FirstAid)
            {
                if (fromPocket) { Notify("Аптечка находится в выбранном слоте."); return false; }
                if (PlayerPocketInventory.Instance != null && PlayerPocketInventory.Instance.TryAddItem("medkit", "Аптечка", 1))
                { hands.ConsumeHeldItem(); return true; }
                Notify("Нет места в карманах."); return false;
            }
            var target = player != null ? player.GetComponentInChildren<GarageInteractionRaycaster>()?.CurrentTarget as Component : null;
            var car = target != null ? target.GetComponentInParent<VehicleModularState>() : null;
            // Allow direct body aiming even where there is no inspection trigger.
            if (car == null && player != null && player.PlayerCamera != null)
            {
                var camera = player.PlayerCamera;
                var hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, 3f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a,b)=>a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                    if (!hit.transform.IsChildOf(player.transform))
                    { car = hit.transform.GetComponentInParent<VehicleModularState>(); break; }
            }
            if (car == null) { Notify("Подойдите к машине и наведитесь на нужный узел."); return false; }
            if (kind == UseKind.Repair)
            {
                if (car.BumperIntegrity >= .999f) { Notify("Крепления и бампер исправны."); return false; }
                car.RepairBumper(.15f); Notify($"Крепления восстановлены: {car.BumperIntegrity:P0}."); return true;
            }
            if (kind == UseKind.SpareWheel)
            {
                if (!(PlayerPocketInventory.Instance?.HasTool(GarageItemFunction.ItemKind.Wrench) ?? false)) { Notify("Для замены колеса нужен гаечный ключ в инвентаре."); return false; }
                int tire = 0; for (int i=1;i<4;i++) if(car.GetTireIntegrity(i)<car.GetTireIntegrity(tire)) tire=i;
                if (car.GetTireIntegrity(tire) >= .999f) { Notify("Замена не нужна — шины исправны."); return false; }
                car.RepairTire(tire); hands.ConsumeHeldItem(); Notify("Повреждённая шина заменена запасной."); return true;
            }
            var fluid = GetComponent<FluidContainer>();
            if (fluid == null || fluid.IsEmpty) { Notify("Ёмкость пуста."); return false; }
            var spot = target as VehiclePartHotspot;
            if (fluid.FluidType == BunkerFluidType.Gasoline && spot != null && !spot.IsInstalled && spot.RequiredItem == BunkerAssemblyItemType.FuelCanister)
            { spot.Interact(player); return spot.IsInstalled; }
            var inspection = target as VehicleInspectionHotspot;
            bool fuelTarget = (spot != null && spot.RequiredItem == BunkerAssemblyItemType.FuelCanister) || (inspection != null && inspection.Type == VehicleInspectionHotspot.HotspotType.FuelInlet);
            bool waterTarget = (spot != null && spot.RequiredItem == BunkerAssemblyItemType.Battery) || (inspection != null && inspection.Type == VehicleInspectionHotspot.HotspotType.EngineHood);
            if (fluid.FluidType == BunkerFluidType.Gasoline && fuelTarget)
            { float amount = fluid.PourOut(Mathf.Min(fluid.CurrentLiters, car.MaxFuelLiters-car.FuelLiters)); car.AddFuel(amount); Notify($"Заправлено {amount:0.#} л бензина."); return amount>0; }
            if (fluid.FluidType == BunkerFluidType.Water && waterTarget)
            { bool result = VehicleServiceInventory.Pour(car, fluid, BunkerFluidType.Water, out var message); Notify(message); return result; }
            if (fluid.FluidType == BunkerFluidType.EngineOil && waterTarget)
            { bool result = VehicleServiceInventory.Pour(car, fluid, BunkerFluidType.EngineOil, out var message); Notify(message); return result; }
            Notify("Бензин — в бак, воду и масло — под капот."); return false;
        }
        static void Notify(string text) => GarageInteractionUI.Instance?.ShowNotification(text, 3f);
    }
}

