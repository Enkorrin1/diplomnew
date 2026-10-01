using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RogueDrive.UI
{
    public sealed class WorkshopSlotUI : MonoBehaviour,IDropHandler,IPointerClickHandler
    {
        public VehicleDashboardPanelsUI owner;
        public WorkshopSlot slot;
        public void OnDrop(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)owner.PreviewWorkshopSlot(slot);}
        public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)owner.PreviewWorkshopSlot(slot);}
    }
}
