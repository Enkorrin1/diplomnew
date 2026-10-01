using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    public sealed class GaragePortableContainer : MonoBehaviour
    {
        [System.Serializable] private class Entry { public GameObject item; public Vector3 scale; }
        [SerializeField] private int capacity = 4;
        [SerializeField] private List<Entry> contents = new List<Entry>();
        public int Count => contents.Count;
        public int Capacity => capacity;
        public IEnumerable<GameObject> CheckpointContents() { foreach (var entry in contents) if (entry.item != null) yield return entry.item; }
        public void RestoreCheckpointContents(IEnumerable<GameObject> items)
        {
            contents.Clear();
            foreach (var item in items)
            {
                var scale = item.transform.lossyScale;
                item.GetComponent<PhysicsProp>()?.OnPickedUp(); item.GetComponent<CarPartItem>()?.OnPickedUp();
                item.transform.SetParent(transform, true); item.SetActive(false);
                contents.Add(new Entry { item = item, scale = scale });
            }
        }
        public string NextItemName
        {
            get { if(Count==0 || contents[Count-1].item==null)return "пусто";var item=contents[Count-1].item;
                var prop=item.GetComponent<PhysicsProp>();var part=item.GetComponent<CarPartItem>();
                return prop!=null?prop.PropName:part!=null?part.GetDisplayName():item.name; }
        }
        public string Prompt => PlayerHandsInventory.Instance != null && PlayerHandsInventory.Instance.HasItem
            ? $"[E] Положить в ящик ({Count}/{capacity})"
            : $"[E] Взять ящик ({Count}/{capacity})  •  [Shift+E] Убрать из слота  •  [F] Достать: {NextItemName}";

        public bool StorePocket(PlayerPocketInventory pocket)
        {
            if(Count>=capacity){Notify("Ящик заполнен.");return false;}
            if(pocket==null || !pocket.TransferActiveTo(transform,out var item,out var scale)){Notify("Выберите мелкий предмет в слоте.");return false;}
            contents.Add(new Entry{item=item,scale=scale});Notify($"Предмет убран в ящик: {Count}/{capacity}.");return true;
        }

        public bool Store(PlayerHandsInventory hands)
        {
            var item = hands.HeldGameObject;
            if (item == null || item == gameObject || item.GetComponent<GaragePortableContainer>() != null)
            { Notify("Ящики нельзя вкладывать друг в друга."); return false; }
            if (Count >= capacity) { Notify("Ящик заполнен."); return false; }
            hands.DropItem(forStorage: true);
            if (hands.HasItem) return false;
            var entry = new Entry { item = item, scale = item.transform.lossyScale };
            item.SetActive(false);
            item.transform.SetParent(transform, true);
            contents.Add(entry);
            Notify($"Предмет убран в ящик: {Count}/{capacity}.");
            return true;
        }

        public bool Take(PlayerHandsInventory hands)
        {
            if (hands.HasItem) { Notify("Положите ящик или освободите руки."); return false; }
            if (Count == 0) { Notify("Ящик пуст."); return false; }
            var entry = contents[Count - 1];
            if (entry.item == null) { contents.RemoveAt(Count - 1); return false; }
            var item = entry.item;
            item.transform.SetParent(null, false);
            item.transform.localScale = entry.scale;
            item.transform.position = transform.position + Vector3.up;
            item.SetActive(true);
            var part = item.GetComponent<CarPartItem>();
            var prop = item.GetComponent<PhysicsProp>();
            bool taken = part != null ? hands.HoldAssemblyItem(part) :
                prop != null && prop.PocketSized ? PlayerPocketInventory.Instance != null && PlayerPocketInventory.Instance.TryStorePhysical(prop) : hands.HoldPhysicsProp(prop);
            if (taken) contents.RemoveAt(Count - 1);
            else { item.SetActive(false); item.transform.SetParent(transform, true); Notify("Нет свободного слота. Предмет остался в ящике."); }
            return taken;
        }
        static void Notify(string text) => GarageInteractionUI.Instance?.ShowNotification(text, 3f);
    }
}
