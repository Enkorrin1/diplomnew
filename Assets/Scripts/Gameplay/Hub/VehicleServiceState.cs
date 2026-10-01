using System.Linq;
using UnityEngine;
namespace RogueDrive.Gameplay.Hub
{
    public enum VehicleServiceSlot { Engine, Radiator, Battery, Water, Oil }
    [DisallowMultipleComponent]
    public sealed class VehicleServiceState : MonoBehaviour
    {
        [SerializeField] PocketSlotData engine, radiator;
        public static VehicleServiceState For(VehicleModularState car)
        {
            if (car == null) return null;
            return car.GetComponent<VehicleServiceState>() ?? car.gameObject.AddComponent<VehicleServiceState>();
        }
        public VehiclePartHotspot Battery => GetComponentsInChildren<VehiclePartHotspot>(true).FirstOrDefault(s => s.RequiredItem == BunkerAssemblyItemType.Battery);
        public bool AtServiceStation => VehicleWorkshop.For(GetComponent<VehicleModularState>())?.Zone!=null;
        public bool TryApply(ServiceItemAddress source, VehicleServiceSlot slot, out string message)
        {
            var item = source.Read();
            message = "Перетащите подходящий предмет из инвентаря.";
            if (item.IsEmpty) return false;
            if (slot == VehicleServiceSlot.Water || slot == VehicleServiceSlot.Oil)
                return VehicleServiceInventory.Pour(GetComponent<VehicleModularState>(), source.Fluid,
                    slot == VehicleServiceSlot.Water ? BunkerFluidType.Water : BunkerFluidType.EngineOil, out message);
            if (slot == VehicleServiceSlot.Battery)
            {
                if (Battery == null) { message = "Гнездо аккумулятора недоступно."; return false; }
                if (Battery.HasPlacedBattery) { message = Battery.IsInstalled ? "Аккумулятор уже закреплён." : "Теперь перетащите отвёртку на аккумулятор."; return false; }
                if (item.WorldObject == null || item.WorldObject.GetComponent<CarPartItem>()?.ItemType != BunkerAssemblyItemType.Battery) return false;
                Battery.PlaceBattery(source.Extract());
                message = "Аккумулятор в гнезде. Перетащите отвёртку, затем нажмите ЛКМ на клеммы.";
                return true;
            }
            return VehicleWorkshop.For(GetComponent<VehicleModularState>()).BeginInstall(source,slot==VehicleServiceSlot.Engine?WorkshopSlot.Engine:WorkshopSlot.Radiator,out message);
        }
    }
}
