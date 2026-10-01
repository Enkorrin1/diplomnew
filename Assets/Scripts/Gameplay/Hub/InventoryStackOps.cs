using System.Collections.Generic;
using UnityEngine;
namespace RogueDrive.Gameplay.Hub
{
    public static class InventoryStackOps
    {
        public static bool FitsPocket(PocketSlotData s) => s.IsEmpty || (s.legacyType==BunkerAssemblyItemType.None && s.largeItem==null && (s.worldItem==null || s.worldItem.PocketSized));
        public static int Limit(PocketSlotData s) => s.worldItem!=null && !string.IsNullOrEmpty(s.worldItem.StackKey)?s.worldItem.StackLimit:s.WorldObject==null && s.legacyType==BunkerAssemblyItemType.None?99:1;
        public static bool Matches(PocketSlotData a,PocketSlotData b)
        {
            if(a.IsEmpty||b.IsEmpty)return false;
            if(a.worldItem!=null && b.worldItem!=null)return !string.IsNullOrEmpty(a.worldItem.StackKey) && a.worldItem.StackKey==b.worldItem.StackKey;
            return a.WorldObject==null && b.WorldObject==null && a.legacyType==BunkerAssemblyItemType.None && b.legacyType==BunkerAssemblyItemType.None && a.id==b.id;
        }
        public static PocketSlotData FromObject(GameObject obj,Vector3 scale)
        {
            var prop=obj.GetComponent<PhysicsProp>();var part=obj.GetComponent<CarPartItem>();
            var result=new PocketSlotData{id="object_"+obj.GetInstanceID(),displayName=prop!=null?prop.PropName:part!=null?part.GetDisplayName():obj.name,count=1,
                worldItem=prop,largeItem=prop==null?obj:null,legacyType=part!=null?part.ItemType:BunkerAssemblyItemType.None,worldScale=scale,viewScale=1,icon=prop!=null?prop.InventoryIcon:null};
            var renderers=obj.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);result.viewScale=Mathf.Min(1,.42f/Mathf.Max(b.size.x,b.size.y,b.size.z));}
            return result;
        }
        public static PocketSlotData Extract(ref PocketSlotData source,int amount)
        {
            if(source.IsEmpty || amount<=0)return PocketSlotData.Empty;
            amount=Mathf.Min(amount,source.count);var result=source;
            if(amount==source.count){source=PocketSlotData.Empty;return result;}
            result.count=amount;source.count-=amount;
            if(source.worldItem!=null)
            {
                var entries=new List<PocketPhysicalEntry>{new PocketPhysicalEntry{item=source.worldItem,scale=source.worldScale,viewScale=source.viewScale}};
                if(source.reserves!=null)entries.AddRange(source.reserves);
                result.reserves=entries.GetRange(1,amount-1);
                var next=entries[amount];source.worldItem=next.item;source.worldScale=next.scale;source.viewScale=next.viewScale;
                source.reserves=entries.GetRange(amount+1,entries.Count-amount-1);
            }
            return result;
        }
        public static int Move(ref PocketSlotData source,ref PocketSlotData target,int requested)
        {
            if(source.IsEmpty || requested<=0)return 0;
            if(!target.IsEmpty && !Matches(source,target))return 0;
            int amount=Mathf.Min(requested,source.count,target.IsEmpty?Limit(source):Limit(target)-target.count);
            if(amount<=0)return 0;var moved=Extract(ref source,amount);
            if(target.IsEmpty){target=moved;return amount;}
            if(moved.worldItem!=null)
            {
                // Copy the list so callers can validate without mutating the original slot.
                target.reserves=target.reserves!=null?new List<PocketPhysicalEntry>(target.reserves):new List<PocketPhysicalEntry>();
                target.reserves.Add(new PocketPhysicalEntry{item=moved.worldItem,scale=moved.worldScale,viewScale=moved.viewScale});
                if(moved.reserves!=null)target.reserves.AddRange(moved.reserves);
            }
            target.count+=amount;return amount;
        }
        public static void Park(PocketSlotData s,Transform owner)
        {
            if(s.WorldObject!=null){if(s.worldItem!=null && !s.worldItem.IsHeld)s.worldItem.OnPickedUp();else if(s.largeItem!=null)s.largeItem.GetComponent<CarPartItem>()?.OnPickedUp();s.WorldObject.SetActive(false);s.WorldObject.transform.SetParent(owner,false);s.WorldObject.transform.localScale=s.worldScale;}
            if(s.reserves!=null)foreach(var e in s.reserves)if(e.item!=null){if(!e.item.IsHeld)e.item.OnPickedUp();e.item.gameObject.SetActive(false);e.item.transform.SetParent(owner,false);e.item.transform.localScale=e.scale;}
        }
        public static void DestroyObjects(PocketSlotData s)
        {
            if(s.WorldObject!=null)Object.Destroy(s.WorldObject);
            if(s.reserves!=null)foreach(var e in s.reserves)if(e.item!=null)Object.Destroy(e.item.gameObject);
        }
    }
}

