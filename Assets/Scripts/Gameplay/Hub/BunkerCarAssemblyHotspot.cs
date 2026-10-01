using System;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Универсальная точка монтажа/обслуживания автомобиля (ступица колеса, подкапотное гнездо АКБ, горловина бака, багажник).
    /// Реагирует на луч прицела игрока и проверяет наличие требуемой детали/канистры в руках.
    /// Работает в гараже, на блокпостах, заправках и при ремонте на обочине.
    /// </summary>
    [AddComponentMenu("RogueDrive/Vehicle Part Hotspot")]
    public class VehiclePartHotspot : MonoBehaviour, IGarageInteractable
    {
        public event Action<VehiclePartHotspot> Assembled;

        [Header("Hotspot Settings")]
        [SerializeField] private BunkerAssemblyItemType requiredItem = BunkerAssemblyItemType.Wheel;
        [SerializeField] private string slotName = "Передняя левая ступица";
        [SerializeField] private string hintLocation = "на стеллаже у стены";

        [Header("Visual Elements")]
        [SerializeField] private GameObject mountedVisual;   // 3D-деталь, которая появляется после установки
        [SerializeField] private GameObject unmountedVisual; // 3D-деталь до установки (например, домкрат/подпорка)
        [SerializeField] private ParticleSystem installVFX;

        private bool isInstalled = false;
        [SerializeField] GameObject installedPhysicalWheel;
        [SerializeField] Vector3 installedWheelScale=Vector3.one;
        bool HasRequiredTool => PlayerPocketInventory.Instance != null && PlayerPocketInventory.Instance.HasTool(requiredItem==BunkerAssemblyItemType.Wheel?GarageItemFunction.ItemKind.Wrench:GarageItemFunction.ItemKind.Screwdriver);

        public BunkerAssemblyItemType RequiredItem => requiredItem;
        public string SlotName => slotName;
        public bool IsInstalled => isInstalled;
        [SerializeField] private GameObject placedBattery;
        public bool HasPlacedBattery => placedBattery != null || isInstalled;
        public bool PlaceBattery(PocketSlotData item)
        {
            if (requiredItem != BunkerAssemblyItemType.Battery || HasPlacedBattery || item.WorldObject == null) return false;
            placedBattery = item.WorldObject;
            InventoryStackOps.Park(item, transform);
            UpdateVisuals();
            return true;
        }
        public bool SecureBattery(GarageItemFunction screwdriver)
        {
            if (requiredItem != BunkerAssemblyItemType.Battery || !HasPlacedBattery || isInstalled || screwdriver == null || screwdriver.Kind != GarageItemFunction.ItemKind.Screwdriver) return false;
            CompleteInstallation();
            return true;
        }
        public float LastTransferredLiters { get; private set; }

        public void RestoreInstalledState(bool installed)
        {
            isInstalled = installed;
            UpdateVisuals();
        }

        public void Configure(BunkerAssemblyItemType item, string name, string hint)
        {
            requiredItem = item;
            slotName = name;
            hintLocation = hint;
        }

        public void BindVisuals(GameObject mounted, GameObject unmounted, ParticleSystem vfx = null)
        {
            mountedVisual = mounted;
            unmountedVisual = unmounted;
            installVFX = vfx;

            UpdateVisuals();
        }

        private void Awake()
        {
            UpdateVisuals();
        }

        public string GetPromptText()
        {
            if (requiredItem == BunkerAssemblyItemType.Battery) return "[E] Под капот: установить и закрепить аккумулятор";
            if (!isInstalled && requiredItem != BunkerAssemblyItemType.FuelCanister && !HasRequiredTool)
                return requiredItem == BunkerAssemblyItemType.Wheel ? "Нужен гаечный ключ в инвентаре." : "Нужна отвёртка для клемм аккумулятора.";
            if (isInstalled)
            {
                if (requiredItem == BunkerAssemblyItemType.FuelCanister)
                {
                    return "[E] Осмотреть уровень топлива в баке";
                }
                if (requiredItem == BunkerAssemblyItemType.Battery)
                {
                    return "[E] Осмотреть двигатель и радиатор";
                }
                return HasRequiredTool ? $"[E] Снять колесо: {slotName} (освободите руки)" : "Для снятия колеса нужен гаечный ключ.";
            }

            var inv = BunkerPlayerInventory.Instance;
            if (inv != null && inv.HeldItem == requiredItem)
            {
                if (requiredItem == BunkerAssemblyItemType.FuelCanister)
                {
                    var container = inv.HeldGameObject != null ? inv.HeldGameObject.GetComponentInChildren<FluidContainer>() : null;
                    if (container != null)
                    {
                        if (container.IsEmpty)
                        {
                            return $"[!] {slotName}: Канистра пуста (0 л) — найдите заправленную!";
                        }
                        return $"[E] Залить в бак: {container.CurrentLiters:F1} л ({container.GetFluidName()})";
                    }
                    return $"[E] Залить топливо в бак: Канистра бензина";
                }

                return $"[E] Смонтировать: {BunkerPlayerInventory.GetItemDisplayName(requiredItem)}";
            }

            string expected = BunkerPlayerInventory.GetItemDisplayName(requiredItem);
            return $"[E] {slotName}: Требуется {expected} (найдите {hintLocation})";
        }

        public bool CanInteract()
        {
            if (requiredItem == BunkerAssemblyItemType.Battery) return RogueDrive.UI.VehicleDashboardPanelsUI.Instance == null || !RogueDrive.UI.VehicleDashboardPanelsUI.Instance.IsAnyPanelOpen;
            if (isInstalled)
            {
                if (requiredItem == BunkerAssemblyItemType.FuelCanister || requiredItem == BunkerAssemblyItemType.Battery)
                {
                    return RogueDrive.UI.VehicleDashboardPanelsUI.Instance == null || !RogueDrive.UI.VehicleDashboardPanelsUI.Instance.IsAnyPanelOpen;
                }
                return requiredItem == BunkerAssemblyItemType.Wheel && HasRequiredTool && PlayerHandsInventory.Instance != null && !PlayerHandsInventory.Instance.HasItem;
            }

            if (requiredItem != BunkerAssemblyItemType.FuelCanister && !HasRequiredTool) return false;
            var inv = BunkerPlayerInventory.Instance;
            if (inv == null || inv.HeldItem != requiredItem) return false;

            if (requiredItem == BunkerAssemblyItemType.FuelCanister)
            {
                var container = inv.HeldGameObject != null ? inv.HeldGameObject.GetComponentInChildren<FluidContainer>() : null;
                var tank = GetComponentInParent<VehicleModularState>();
                if (container == null || container.IsEmpty || container.FluidType != BunkerFluidType.Gasoline ||
                    tank == null || tank.MaxFuelLiters - tank.FuelLiters <= 0.05f)
                {
                    return false;
                }
            }

            return true;
        }

        public void Interact(GaragePlayerController player)
        {
            if (requiredItem == BunkerAssemblyItemType.Battery) { EnsureUI(); RogueDrive.UI.VehicleDashboardPanelsUI.Instance.ShowEnginePanel(); return; }
            if (requiredItem == BunkerAssemblyItemType.Wheel && isInstalled) { TryRemoveWheel(player); return; }
            if (!isInstalled && requiredItem != BunkerAssemblyItemType.FuelCanister && !HasRequiredTool)
            {
                GarageInteractionUI.Instance?.ShowNotification(requiredItem == BunkerAssemblyItemType.Wheel ? "Нужен гаечный ключ." : "Нужна отвёртка для клемм.", 3);
                return;
            }
            if (isInstalled)
            {
                if (requiredItem == BunkerAssemblyItemType.FuelCanister)
                {
                    EnsureUI();
                    RogueDrive.UI.VehicleDashboardPanelsUI.Instance?.ShowFuelInletPanel();
                    return;
                }
                if (requiredItem == BunkerAssemblyItemType.Battery)
                {
                    EnsureUI();
                    RogueDrive.UI.VehicleDashboardPanelsUI.Instance?.ShowEnginePanel();
                    return;
                }
                return;
            }

            var inv = BunkerPlayerInventory.Instance;
            if (inv == null || inv.HeldItem != requiredItem)
            {
                if (GaragePrologueManager.Instance != null)
                {
                    string reqName = BunkerPlayerInventory.GetItemDisplayName(requiredItem);
                    GaragePrologueManager.Instance.ShowNotification($"Сначала возьмите в руки: {reqName} ({hintLocation})", 3.0f);
                }
                return;
            }

            if (requiredItem == BunkerAssemblyItemType.FuelCanister)
            {
                var fluidCont = inv.HeldGameObject != null ? inv.HeldGameObject.GetComponentInChildren<FluidContainer>() : null;
                var tank = GetComponentInParent<VehicleModularState>();
                if (!CanInteract())
                {
                    GaragePrologueManager.Instance?.ShowNotification(
                        fluidCont == null || fluidCont.IsEmpty ? "Канистра пуста — нужен бензин." :
                        fluidCont.FluidType != BunkerFluidType.Gasoline ? "Для этого двигателя нужен бензин." : "Бак уже полон.", 3f);
                    return;
                }
                if (fluidCont != null)
                {
                    if (fluidCont.IsEmpty)
                    {
                        if (GaragePrologueManager.Instance != null)
                        {
                            GaragePrologueManager.Instance.ShowNotification("Эта канистра пуста! Найдите заправленную канистру бензина.", 3.0f);
                        }
                        return;
                    }

                    float poured = fluidCont.PourOut(Mathf.Min(fluidCont.CurrentLiters, tank.MaxFuelLiters - tank.FuelLiters));
                    LastTransferredLiters = poured;
                    var modular = GetComponentInParent<VehicleModularState>();
                    if (modular != null)
                    {
                        modular.AddFuel(poured);
                    }

                    if (fluidCont.KeepEmptyCanisterInHand || !fluidCont.IsEmpty)
                    {
                        CompleteInstallation();
                        return;
                    }
                }
            }

            if (requiredItem == BunkerAssemblyItemType.Wheel)
            {
                installedPhysicalWheel = inv.HeldGameObject;
                installedWheelScale = inv.HeldWorldScale;
                inv.DropItem(forStorage: true);
                installedPhysicalWheel.transform.SetParent(transform, false);
                installedPhysicalWheel.SetActive(false);
            }
            else inv.ConsumeHeldItem();
            CompleteInstallation();
        }

        public bool TryRemoveWheel(GaragePlayerController player)
        {
            var hands=PlayerHandsInventory.Instance;
            if(!isInstalled || requiredItem!=BunkerAssemblyItemType.Wheel)return false;
            if(!HasRequiredTool){GarageInteractionUI.Instance?.ShowNotification("Нужен гаечный ключ.",3);return false;}
            if(hands==null||hands.HasItem){GarageInteractionUI.Instance?.ShowNotification("Освободите руки для снятого колеса.",3);return false;}
            var body=GetComponentInParent<Rigidbody>();
            if(body!=null && body.linearVelocity.sqrMagnitude>.1f){GarageInteractionUI.Instance?.ShowNotification("Сначала остановите машину.",3);return false;}
            bool taken;
            if(installedPhysicalWheel!=null)
            {
                installedPhysicalWheel.transform.SetParent(null);installedPhysicalWheel.transform.localScale=installedWheelScale;installedPhysicalWheel.SetActive(true);
                taken=hands.HoldAssemblyItem(installedPhysicalWheel.GetComponent<CarPartItem>());
                if(!taken){installedPhysicalWheel.transform.SetParent(transform);installedPhysicalWheel.SetActive(false);}
            }
            else taken=hands.TryHoldItem(BunkerAssemblyItemType.Wheel);
            if(!taken)return false;
            installedPhysicalWheel=null;isInstalled=false;UpdateVisuals();
            GetComponentInParent<BunkerStarterCarAssembly>()?.WheelRemoved();
            GarageInteractionUI.Instance?.ShowNotification("Колесо откручено и взято в руки. Установите его перед поездкой.",3);
            return true;
        }
        private void CompleteInstallation()
        {
            isInstalled = true;
            UpdateVisuals();

            if (GaragePresentationDirector.Instance == null) PlayInstallSound();
            if (installVFX != null)
            {
                installVFX.Play();
            }

            Assembled?.Invoke(this);
            GaragePresentationDirector.Instance?.InstallationFeedback(this, mountedVisual);
        }

        private void EnsureUI()
        {
            if (RogueDrive.UI.VehicleDashboardPanelsUI.Instance == null)
            {
                var uiGo = new GameObject("VehicleDashboardPanelsUI", typeof(RogueDrive.UI.VehicleDashboardPanelsUI));
                DontDestroyOnLoad(uiGo);
            }
        }

        private void UpdateVisuals()
        {
            if (mountedVisual != null)
            {
                mountedVisual.SetActive(isInstalled || (requiredItem == BunkerAssemblyItemType.Battery && placedBattery != null));
            }
            if (unmountedVisual != null)
            {
                unmountedVisual.SetActive(!isInstalled);
            }
        }

        private void PlayInstallSound()
        {
            if (AudioManager.Instance != null)
            {
                switch (requiredItem)
                {
                    case BunkerAssemblyItemType.Wheel:
                        AudioManager.Instance.PlayImpact();
                        break;
                    case BunkerAssemblyItemType.Battery:
                        AudioManager.Instance.PlayLevelUp();
                        break;
                    case BunkerAssemblyItemType.FuelCanister:
                        AudioManager.Instance.PlaySwitchClick();
                        break;
                    default:
                        AudioManager.Instance.PlaySwitchClick();
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Псевдоним класса для обратной совместимости со старыми сценами бункера.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RogueDrive/Bunker Car Assembly Hotspot (Legacy)")]
    public class BunkerCarAssemblyHotspot : VehiclePartHotspot
    {
    }
}

