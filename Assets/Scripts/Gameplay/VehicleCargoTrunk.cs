using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
namespace RogueDrive.Gameplay
{
    public sealed class VehicleCargoTrunk : MonoBehaviour
    {
        private static VehicleCargoTrunk instance;
        public static VehicleCargoTrunk Instance
        {
            get
            {
                if(instance==null)instance=FindFirstObjectByType<VehicleCargoTrunk>();
                return instance;
            }
            private set=>instance=value;
        }
        public event Action CargoChanged;
        [SerializeField,Range(6,30)] private int maxSlots=15;
        // Retained only for migration of existing scenes and initial expedition supplies.
        [SerializeField] private List<BunkerAssemblyItemType> items=new List<BunkerAssemblyItemType>();
        [Serializable] private class StoredObject {public GameObject item;public Vector3 scale;}
        [SerializeField] private List<StoredObject> physicalItems=new List<StoredObject>();
        [SerializeField] private PocketSlotData[] grid;
        public int MaxSlots=>maxSlots;
        int baseSlots;
        public bool CanSetWorkshopCapacity(int extra)
        {
            int target=(baseSlots>0?baseSlots:15)+Mathf.Max(0,extra);
            for(int i=target;i<MaxSlots;i++)if(!GetSlot(i).IsEmpty)return false;
            return true;
        }
        public void SetWorkshopCapacity(int extra)
        {
            if(baseSlots==0)baseSlots=maxSlots;
            int target=baseSlots+Mathf.Max(0,extra);
            if(!CanSetWorkshopCapacity(extra)||target==maxSlots)return;
            maxSlots=target;Array.Resize(ref grid,maxSlots);CargoChanged?.Invoke();
        }
        public int ItemCount=>grid==null?0:grid.Count(s=>!s.IsEmpty);
        public IReadOnlyList<BunkerAssemblyItemType> Items=>Enumerable.Range(0,MaxSlots).Select(GetItem).Where(t=>t!=BunkerAssemblyItemType.None).ToArray();
        void Awake()
        {
            Instance=this;
            baseSlots=maxSlots;
            if(grid==null || grid.Length==0)
            {
                grid=new PocketSlotData[maxSlots];
                if(items.Count==0 && physicalItems.Count==0)items.AddRange(new[]{BunkerAssemblyItemType.FuelCanister,BunkerAssemblyItemType.WaterCanister,BunkerAssemblyItemType.Wheel});
                foreach(var type in items)TryStoreItem(type,out _);
                foreach(var entry in physicalItems)if(entry.item!=null){int free=FindEmpty();if(free>=0)SetGridSlot(free,InventoryStackOps.FromObject(entry.item,entry.scale));}
                items.Clear();physicalItems.Clear();
            }
            else { maxSlots=Mathf.Max(maxSlots,grid.Length); Array.Resize(ref grid,maxSlots); for(int i=0;i<grid.Length;i++)SetGridSlot(i,VehicleServiceInventory.MaterializeLegacy(grid[i])); }
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
        int FindEmpty(){for(int i=0;i<MaxSlots;i++)if(grid[i].IsEmpty)return i;return -1;}
        public PocketSlotData GetSlot(int index)=>grid!=null&&index>=0&&index<grid.Length?grid[index]:PocketSlotData.Empty;
        public void SetGridSlot(int index,PocketSlotData value)
        {if(index<0||index>=MaxSlots)return;grid[index]=value;InventoryStackOps.Park(value,transform);CargoChanged?.Invoke();}
        public bool TryStoreItem(BunkerAssemblyItemType type,out string reason)
        {
            reason="";int free=FindEmpty();if(type==BunkerAssemblyItemType.None || free<0){reason="Багажник заполнен.";return false;}
            SetGridSlot(free,VehicleServiceInventory.MaterializeLegacy(new PocketSlotData{id="supply_"+type,displayName=PlayerHandsInventory.GetItemDisplayName(type),legacyType=type,count=1}));return true;
        }
        public BunkerAssemblyItemType TakeItem(int index)
        {var type=GetItem(index);if(type==BunkerAssemblyItemType.None)return type;InventoryStackOps.DestroyObjects(grid[index]);SetGridSlot(index,PocketSlotData.Empty);return type;}
        public BunkerAssemblyItemType GetItem(int index)
        {var s=GetSlot(index);if(s.IsEmpty)return BunkerAssemblyItemType.None;var part=s.WorldObject!=null?s.WorldObject.GetComponent<CarPartItem>():null;return part!=null?part.ItemType:s.legacyType;}
        public GameObject GetPhysicalItem(int index)=>GetSlot(index).WorldObject;
        public string GetDisplayName(int index){var s=GetSlot(index);return s.IsEmpty?"Пусто":s.WorldObject!=null && s.WorldObject.GetComponentInChildren<FluidContainer>(true)!=null?s.WorldObject.GetComponentInChildren<FluidContainer>(true).GetDefaultDisplayName():s.displayName;}
        public Sprite GetIcon(int index)=>GetSlot(index).icon;
        public bool ContainsItem(BunkerAssemblyItemType type)=>Items.Contains(type);
        public bool RemoveFirst(BunkerAssemblyItemType type)
        {for(int i=0;i<MaxSlots;i++)if(GetItem(i)==type){TakeItem(i);return true;}return false;}
        public void ClearCargo(){for(int i=0;i<MaxSlots;i++){InventoryStackOps.DestroyObjects(grid[i]);grid[i]=PocketSlotData.Empty;}CargoChanged?.Invoke();}
        public bool StoreFromPlayer(PlayerHandsInventory hands,PlayerPocketInventory pocket,out string reason)
        {
            reason="";
            var source=hands!=null&&hands.HasItem?InventoryStackOps.FromObject(hands.HeldGameObject,hands.HeldWorldScale):pocket!=null?pocket.GetActiveItem():PocketSlotData.Empty;
            if(source.IsEmpty || (source.WorldObject==null && source.legacyType==BunkerAssemblyItemType.None)){reason="Выберите переносимый предмет.";return false;}
            int target=-1;for(int i=0;i<MaxSlots;i++)if(InventoryStackOps.Matches(source,grid[i]) && grid[i].count<InventoryStackOps.Limit(grid[i])){target=i;break;}
            if(target<0)target=FindEmpty();if(target<0){reason="Багажник заполнен.";return false;}
            if(hands!=null && hands.HasItem)hands.DropItem(forStorage:true);
            else {var playerSource=pocket.GetActiveItem();source=InventoryStackOps.Extract(ref playerSource,1);pocket.SetGridSlot(pocket.ActiveSlotIndex,playerSource);}
            var destination=grid[target];InventoryStackOps.Move(ref source,ref destination,1);SetGridSlot(target,destination);return true;
        }
        public bool TakeToPlayer(int index,PlayerHandsInventory hands,PlayerPocketInventory pocket,out string reason)
        {
            reason="";var source=GetSlot(index);if(source.IsEmpty){reason="Выберите предмет.";return false;}
            if(InventoryStackOps.FitsPocket(source))
            {
                if(pocket==null){reason="Инвентарь недоступен.";return false;}
                for(int pass=0;pass<2;pass++)for(int i=0;i<PlayerPocketInventory.SlotCount;i++)
                {
                    var target=pocket.GetSlot(i);if(pass==0? !InventoryStackOps.Matches(source,target):!target.IsEmpty)continue;
                    if(InventoryStackOps.Move(ref source,ref target,1)>0){SetGridSlot(index,source);pocket.SetGridSlot(i,target);return true;}
                }
                reason="Инвентарь заполнен.";return false;
            }
            if(hands==null||hands.HasItem){reason="Освободите руки для крупного предмета.";return false;}
            bool taken;
            if(source.WorldObject==null)taken=hands.TryHoldItem(source.legacyType);
            else
            {
                var obj=source.WorldObject;obj.transform.SetParent(null,false);obj.transform.localScale=source.worldScale;obj.SetActive(true);
                var part=obj.GetComponent<CarPartItem>();taken=part!=null?hands.HoldAssemblyItem(part):hands.HoldPhysicsProp(source.worldItem);
                if(!taken)InventoryStackOps.Park(source,transform);
            }
            if(taken)SetGridSlot(index,PocketSlotData.Empty);else reason="Не удалось взять предмет.";return taken;
        }
    }
}

