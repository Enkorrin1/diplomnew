using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.EventSystems;
namespace RogueDrive.UI
{
    public sealed class VehicleServiceCellUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public VehicleDashboardPanelsUI Owner;
        public bool IsSource;
        public ServiceItemAddress Address;
        public VehicleServiceSlot Slot;
        int endedFrame = -1;
        public void OnBeginDrag(PointerEventData e) { if (IsSource && e.button == PointerEventData.InputButton.Left) Owner.SelectServiceItem(Address); }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) { endedFrame = Time.frameCount; Owner.CancelServiceDrag(); }
        public void OnDrop(PointerEventData e) { if (!IsSource && e.button == PointerEventData.InputButton.Left) Owner.DropServiceItem(Slot); }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || endedFrame == Time.frameCount) return;
            if (IsSource) Owner.SelectServiceItem(Address); else Owner.ClickServiceSlot(Slot);
        }
        public void OnPointerEnter(PointerEventData e) { if (!IsSource) GetComponent<UnityEngine.UI.Image>().color = LowPolyUi.Border; }
        public void OnPointerExit(PointerEventData e) { if (!IsSource) GetComponent<UnityEngine.UI.Image>().color = LowPolyUi.Surface; }
    }
}
