var ui = RogueDrive.UI.VehicleDashboardPanelsUI.Instance;
var trunk = RogueDrive.Gameplay.VehicleCargoTrunk.Instance;
var hands = RogueDrive.Gameplay.Hub.BunkerPlayerInventory.Instance;
var state = RogueDrive.Gameplay.VehicleModularState.Instance;
if (ui == null || trunk == null || hands == null || state == null) throw new System.Exception("Missing live systems");
System.Action<string> click = name => {
    foreach (var button in ui.GetComponentsInChildren<UnityEngine.UI.Button>())
        if (button.name == name) { button.onClick.Invoke(); return; }
    throw new System.Exception("Missing button: " + name);
};
hands.ConsumeHeldItem();
trunk.ClearCargo();
trunk.TryStoreItem(RogueDrive.Gameplay.Hub.BunkerAssemblyItemType.FuelCanister, out _);
trunk.TryStoreItem(RogueDrive.Gameplay.Hub.BunkerAssemblyItemType.WaterCanister, out _);
trunk.TryStoreItem(RogueDrive.Gameplay.Hub.BunkerAssemblyItemType.Wheel, out _);
ui.ShowTrunkPanel();
click("TakeItem");
if(trunk.ItemCount!=2 || !hands.HasItem) throw new System.Exception("Take failed");
var held = hands.HeldGameObject;
click("StoreItem");
if(trunk.ItemCount!=3 || hands.HasItem || (held!=null && held.activeSelf)) throw new System.Exception("Store duplicated item");
// Full cargo must not consume the player's item.
hands.TryHoldItem(RogueDrive.Gameplay.Hub.BunkerAssemblyItemType.Wheel);
while(trunk.ItemCount<trunk.MaxSlots) trunk.TryStoreItem(RogueDrive.Gameplay.Hub.BunkerAssemblyItemType.Wheel,out _);
ui.ShowTrunkPanel();click("StoreItem");
if(!hands.HasItem || trunk.ItemCount!=trunk.MaxSlots)throw new System.Exception("Full cargo lost item");
hands.ConsumeHeldItem();trunk.ClearCargo();
ui.ShowFuelInletPanel();
float before=state.FuelLiters;
click("Refuel");
if(UnityEngine.Mathf.Abs(state.FuelLiters-before)>.01f)throw new System.Exception("Fuel appeared without a canister");
// Partial pour must preserve remaining volume in the physical canister.
state.AddFuel(state.MaxFuelLiters-state.FuelLiters-3f);
hands.TryHoldItem(RogueDrive.Gameplay.Hub.BunkerAssemblyItemType.FuelCanister,out var can);
var fluid=can.GetComponent<RogueDrive.Gameplay.Hub.FluidContainer>()??can.AddComponent<RogueDrive.Gameplay.Hub.FluidContainer>();
fluid.Configure(RogueDrive.Gameplay.Hub.BunkerFluidType.Gasoline,15,15);
ui.ShowFuelInletPanel();click("Refuel");
if(UnityEngine.Mathf.Abs(state.FuelLiters-state.MaxFuelLiters)>.01f || UnityEngine.Mathf.Abs(fluid.CurrentLiters-12)>.01f)throw new System.Exception("Partial pour lost fuel");
click("Refuel");
if(UnityEngine.Mathf.Abs(fluid.CurrentLiters-12)>.01f)throw new System.Exception("Full tank consumed canister");
ui.ClosePanel();
if(ui.IsAnyPanelOpen)throw new System.Exception("Close failed");
hands.ConsumeHeldItem();
trunk.TryStoreItem(RogueDrive.Gameplay.Hub.BunkerAssemblyItemType.FuelCanister,out _);
trunk.TryStoreItem(RogueDrive.Gameplay.Hub.BunkerAssemblyItemType.WaterCanister,out _);
trunk.TryStoreItem(RogueDrive.Gameplay.Hub.BunkerAssemblyItemType.Wheel,out _);
ui.ShowTrunkPanel();
return "PASS: take, store/no duplicate, full cargo, missing resource, partial pour, full tank, close (7 checks)";
