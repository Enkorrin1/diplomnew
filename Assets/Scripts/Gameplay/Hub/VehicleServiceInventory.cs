using System.Collections.Generic;
using UnityEngine;
namespace RogueDrive.Gameplay.Hub
{
    // Stable addresses: actions re-read their source rather than trusting a drag snapshot.
    public readonly struct ServiceItemAddress
    {
        public readonly int Area, Index; // 0 pockets, 1 cargo, 2 hands
        public ServiceItemAddress(int area, int index) { Area = area; Index = index; }
        public PocketSlotData Read()
        {
            if (Area == 0) return PlayerPocketInventory.Instance != null ? PlayerPocketInventory.Instance.GetSlot(Index) : PocketSlotData.Empty;
            if (Area == 1) return VehicleCargoTrunk.Instance != null ? VehicleCargoTrunk.Instance.GetSlot(Index) : PocketSlotData.Empty;
            var hands = PlayerHandsInventory.Instance;
            return hands != null && hands.HasItem ? InventoryStackOps.FromObject(hands.HeldGameObject, hands.HeldWorldScale) : PocketSlotData.Empty;
        }
        public void Write(PocketSlotData value)
        {
            if (Area == 0) PlayerPocketInventory.Instance.SetGridSlot(Index, value);
            else if (Area == 1) VehicleCargoTrunk.Instance.SetGridSlot(Index, value);
        }
        public PocketSlotData Extract()
        {
            var source = Read();
            if (Area == 2) { PlayerHandsInventory.Instance.DropItem(forStorage: true); return source; }
            var result = InventoryStackOps.Extract(ref source, 1); Write(source); return result;
        }
        public FluidContainer Fluid => Read().WorldObject != null ? Read().WorldObject.GetComponentInChildren<FluidContainer>(true) : null;
    }
    public static class VehicleServiceInventory
    {
        // Old saves stored a canister only as an enum. Convert once, never create liquid while pouring.
        public static PocketSlotData MaterializeLegacy(PocketSlotData item)
        {
            if (item.IsEmpty || item.WorldObject != null) return item;
            var prefab = Resources.Load<GameObject>("VehicleService/" + item.legacyType);
            if (prefab == null) return item;
            var obj = Object.Instantiate(prefab);
            var identity = obj.AddComponent<GarageCheckpointItem>();
            identity.id = "service_" + item.legacyType + "_" + System.Guid.NewGuid().ToString("N");
            var fluid = obj.GetComponent<FluidContainer>();
            if (item.legacyType == BunkerAssemblyItemType.WaterCanister) fluid.Configure(BunkerFluidType.Water,20,10);
            else if (item.legacyType == BunkerAssemblyItemType.FuelCanister) fluid.Configure(BunkerFluidType.Gasoline,20,15);
            return InventoryStackOps.FromObject(obj,obj.transform.localScale);
        }
        public static GameObject RestoreGeneratedItem(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("service_")) return null;
            var parts = id.Split('_');
            if (parts.Length != 3 || !System.Enum.TryParse(parts[1], out BunkerAssemblyItemType type)) return null;
            var prefab = Resources.Load<GameObject>("VehicleService/" + type);
            if (prefab == null) return null;
            var obj = Object.Instantiate(prefab);
            obj.AddComponent<GarageCheckpointItem>().id = id;
            return obj;
        }
        public static IEnumerable<ServiceItemAddress> Sources()
        {
            yield return new ServiceItemAddress(2, 0);
            for (int i = 0; i < PlayerPocketInventory.SlotCount; i++) yield return new ServiceItemAddress(0, i);
            var trunk = VehicleCargoTrunk.Instance;
            if (trunk != null) for (int i = 0; i < trunk.MaxSlots; i++) yield return new ServiceItemAddress(1, i);
        }
        public static bool FindFluid(BunkerFluidType type, out ServiceItemAddress address)
        {
            foreach (var candidate in Sources())
                if (candidate.Fluid != null && !candidate.Fluid.IsEmpty && candidate.Fluid.FluidType == type) { address = candidate; return true; }
            address = default; return false;
        }
        public static float Space(VehicleModularState car, BunkerFluidType type)
        {
            if (car == null) return 0;
            switch (type)
            {
                case BunkerFluidType.Water: return car.MaxRadiatorWater - car.RadiatorWater;
                case BunkerFluidType.EngineOil: return car.MaxEngineOil - car.EngineOil;
                case BunkerFluidType.Gasoline: return car.MaxFuelLiters - car.FuelLiters;
                default: return 0;
            }
        }
        public static bool Pour(VehicleModularState car, FluidContainer source, BunkerFluidType type, out string message)
        {
            message = "Нужна канистра подходящей жидкости.";
            if (source == null || source.FluidType != type) return false;
            if (source.IsEmpty) { message = "Канистра пуста."; return false; }
            if(car==null){message="Автомобиль недоступен.";return false;}
            if((type==BunkerFluidType.Water||type==BunkerFluidType.EngineOil)
                && !car.CanService(type==BunkerFluidType.Water,out message))return false;
            float space = Space(car, type);
            if (space <= .001f) { message = "Ёмкость заполнена."; return false; }
            float amount = source.PourOut(space);
            switch (type)
            {
                case BunkerFluidType.Water: car.AddRadiatorWater(amount); break;
                case BunkerFluidType.EngineOil: car.AddEngineOil(amount); break;
                case BunkerFluidType.Gasoline: car.AddFuel(amount); break;
            }
            message = $"Долито {amount:0.##} л. В канистре осталось {source.CurrentLiters:0.##} л.";
            return amount > 0;
        }
    }
}
