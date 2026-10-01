using RogueDrive.UI;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивная 3D-точка вызова контекстной панели обслуживания авто (Diegetic Inspection Hotspot).
    /// Размещается на капоте (ДВС/радиатор), багажнике (слоты лута) и лючке бензобака.
    /// Реализует IGarageInteractable для взаимодействия с прицелом от 1-го лица.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class VehicleInspectionHotspot : MonoBehaviour, IGarageInteractable
    {
        public enum HotspotType
        {
            EngineHood,
            Trunk,
            FuelInlet
        }

        [Header("Type")]
        [SerializeField] private HotspotType type = HotspotType.EngineHood;

        public HotspotType Type => type;

        public void Configure(HotspotType hotspotType)
        {
            type = hotspotType;
        }

        public string GetPromptText()
        {
            switch (type)
            {
                case HotspotType.EngineHood:
                    return "[E] Обслуживание автомобиля / мастерская";
                case HotspotType.Trunk:
                    return "[E] Открыть багажник (грузовой отсек)";
                case HotspotType.FuelInlet:
                    return "[E] Открыть лючок бензобака";
                default:
                    return "[E] Осмотреть узел";
            }
        }

        public bool CanInteract()
        {
            // Доступно всегда, если не открыта другая панель
            return VehicleDashboardPanelsUI.Instance == null || !VehicleDashboardPanelsUI.Instance.IsAnyPanelOpen;
        }

        public void Interact(GaragePlayerController player)
        {
            EnsureUI();

            if (VehicleDashboardPanelsUI.Instance == null) return;

            switch (type)
            {
                case HotspotType.EngineHood:
                    VehicleDashboardPanelsUI.Instance.ShowEnginePanel();
                    break;
                case HotspotType.Trunk:
                    VehicleDashboardPanelsUI.Instance.ShowTrunkPanel();
                    break;
                case HotspotType.FuelInlet:
                    VehicleDashboardPanelsUI.Instance.ShowFuelInletPanel();
                    break;
            }
        }

        private void EnsureUI()
        {
            if (VehicleDashboardPanelsUI.Instance == null)
            {
                GameObject uiGo = new GameObject("VehicleDashboardPanelsUI", typeof(VehicleDashboardPanelsUI));
                DontDestroyOnLoad(uiGo);
            }
        }
    }
}
