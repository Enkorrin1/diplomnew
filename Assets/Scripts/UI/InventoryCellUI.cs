using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace RogueDrive.UI
{
    public sealed class InventoryCellUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler
    {
        public int Area {get;private set;}
        public int Index {get;private set;}
        InventoryWindowUI owner;
        Image background,icon;
        Text label,count;
        int dragFrame=-1;
        public void Initialize(InventoryWindowUI window,int area,int index,float size)
        {
            owner=window;Area=area;Index=index;background=GetComponent<Image>();
            icon=LowPolyUi.Icon(transform,"icon_scrap",new Vector2(20,-9),new Vector2(size-40,size-44));
            label=LowPolyUi.Label(transform,"Name","",new Vector2(5,-size+31),new Vector2(size-10,29),12);label.alignment=TextAnchor.MiddleCenter;
            count=LowPolyUi.Label(transform,"Count","",new Vector2(size-42,-5),new Vector2(36,24),18);count.alignment=TextAnchor.UpperRight;
            if(area==0&&index<5)LowPolyUi.Label(transform,"Key",(index+1).ToString(),new Vector2(7,-5),new Vector2(20,24),15,LowPolyUi.Muted);
        }
        public void Render(PocketSlotData value,bool selected)
        {
            background.color=selected?LowPolyUi.Border:LowPolyUi.Surface;
            icon.sprite = value.IsEmpty ? null : LowPolyUi.ItemSprite(value);
            icon.enabled = !value.IsEmpty && icon.sprite != null;
            var fluid=value.WorldObject!=null?value.WorldObject.GetComponentInChildren<FluidContainer>(true):null;
            label.text=value.IsEmpty?"":fluid!=null?fluid.GetDefaultDisplayName():value.displayName;
            var function=value.WorldObject!=null?value.WorldObject.GetComponent<GarageItemFunction>():null;
            count.fontSize=function!=null&&(function.UsesBattery||function.Kind==GarageItemFunction.ItemKind.Burner)?14:18;
            count.text=value.count>1?value.count.ToString():function!=null&&(function.UsesBattery||function.Kind==GarageItemFunction.ItemKind.Burner)?$"{function.Charge:P0}":"";
        }
        public void OnPointerClick(PointerEventData e) {if(dragFrame==Time.frameCount)return;owner.Click(this,e.button==PointerEventData.InputButton.Right,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));}
        public void OnBeginDrag(PointerEventData e) {if(e.button==PointerEventData.InputButton.Left)owner.BeginDrag(this);}
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) {dragFrame=Time.frameCount;owner.EndDrag();}
        public void OnDrop(PointerEventData e) {if(e.button==PointerEventData.InputButton.Left)owner.DropOn(this);}
        public void OnPointerEnter(PointerEventData e) {owner.Hover(this);}
    }
}

