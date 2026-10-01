using System;
using UnityEngine;
namespace RogueDrive.Gameplay.Hub
{
    public sealed partial class PlayerPocketInventory
    {
        public bool HasTool(GarageItemFunction.ItemKind kind)
        {
            for(int i=0;i<SlotCount;i++)if(slots[i].worldItem!=null&&slots[i].worldItem.GetComponent<GarageItemFunction>()?.Kind==kind)return true;
            var held=PlayerHandsInventory.Instance?.HeldGameObject;
            return held!=null&&held.GetComponent<GarageItemFunction>()?.Kind==kind;
        }
        public bool ConsumeSupply(GarageItemFunction.ItemKind kind)
        {
            for(int i=0;i<SlotCount;i++)
            {
                var item=slots[i].worldItem;if(item==null)continue;
                var function=item.GetComponent<GarageItemFunction>();
                if(function==null||function.Kind!=kind)continue;
                RemovePhysicalHead(i);Destroy(item.gameObject);return true;
            }
            return false;
        }
        public bool ConsumePhysical(PhysicsProp prop)
        {
            for(int i=0;i<SlotCount;i++)if(slots[i].worldItem==prop){RemovePhysicalHead(i);Destroy(prop.gameObject);return true;}
            return false;
        }
        public bool ReplaceSelectedBattery()
        {
            var device=ActivePhysical!=null?ActivePhysical.GetComponent<GarageItemFunction>():null;
            return device!=null&&device.ReplaceBattery();
        }
    }
}
