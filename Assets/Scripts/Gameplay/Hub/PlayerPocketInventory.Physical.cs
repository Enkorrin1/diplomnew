using UnityEngine;
namespace RogueDrive.Gameplay.Hub
{
    public sealed partial class PlayerPocketInventory
    {
        private bool lastHandsBusy;
        private string pocketHint;
        public PhysicsProp ActivePhysical => GetActiveItem().worldItem;
        public void SetGridSlot(int index, PocketSlotData value)
        {
            if(index<0 || index>=SlotCount)return;
            slots[index]=value;InventoryStackOps.Park(value,transform);OnSlotUpdated?.Invoke(index,value);
            pocketHint=null;
        }
        public bool TryStorePhysical(PhysicsProp prop)
        {
            if (prop == null || !prop.PocketSized) return false;
            for (int i=0;i<SlotCount;i++) if(slots[i].worldItem==prop || (slots[i].reserves!=null && slots[i].reserves.Exists(e=>e.item==prop)))return false;
            int free=-1;
            if(!string.IsNullOrEmpty(prop.StackKey))for(int i=0;i<SlotCount;i++)
                if(slots[i].worldItem!=null && slots[i].worldItem.StackKey==prop.StackKey && slots[i].count<prop.StackLimit){free=i;break;}
            if(free<0)for(int i=0;i<SlotCount;i++)if(slots[i].IsEmpty){free=i;break;}
            if(free<0)return false;
            var scale=prop.transform.lossyScale;
            float viewScale=1;
            var renderers=prop.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);viewScale=Mathf.Min(1,.42f/Mathf.Max(b.size.x,b.size.y,b.size.z));}
            prop.OnPickedUp();
            prop.transform.SetParent(transform,true);
            prop.gameObject.SetActive(false);
            if(slots[free].IsEmpty)
                slots[free]=new PocketSlotData { id="physical_"+prop.GetInstanceID(), displayName=prop.PropName, count=1, worldItem=prop, worldScale=scale, viewScale=viewScale, icon=prop.InventoryIcon };
            else
            {
                if(slots[free].reserves==null)slots[free].reserves=new System.Collections.Generic.List<PocketPhysicalEntry>();
                slots[free].reserves.Add(new PocketPhysicalEntry{item=prop,scale=scale,viewScale=viewScale});slots[free].count++;
            }
            OnSlotUpdated?.Invoke(free,slots[free]);
            if(GetActiveItem().IsEmpty)SelectSlot(free);
            RefreshPocketVisual();
            return true;
        }
        private void LateUpdate()
        {
            bool busy=PlayerHandsInventory.Instance!=null && PlayerHandsInventory.Instance.HasItem;
            if(busy!=lastHandsBusy){lastHandsBusy=busy;UpdateWeaponVisual();}
            RefreshPocketVisual();
        }
        public void HideVisuals()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var item = slots[i].worldItem;
                if (item != null && item.gameObject != null)
                {
                    item.gameObject.SetActive(false);
                    item.transform.SetParent(transform, false);
                }
            }
            if (weaponSocket != null)
            {
                weaponSocket.gameObject.SetActive(false);
            }
            pocketHint = null;
            if (GarageInteractionUI.Instance != null)
            {
                GarageInteractionUI.Instance.HideHeldHint();
            }
        }
        private void RefreshPocketVisual()
        {
            EnsureWeaponSocket();if(weaponSocket==null)return;
            var player=GetComponent<GaragePlayerController>();
            bool busy=PlayerHandsInventory.Instance!=null && PlayerHandsInventory.Instance.HasItem;
            bool driving=GarageDriveOutController.Instance!=null && GarageDriveOutController.Instance.IsDriving;
            bool visible=!busy && !driving && (player==null || !player.IsMovementLocked);
            for(int i=0;i<SlotCount;i++)
            {
                var item=slots[i].worldItem;if(item==null)continue;
                bool show=visible && i==activeSlotIndex;
                if(item.gameObject.activeSelf!=show)item.gameObject.SetActive(show);
                if(!show)
                {
                    if(item.transform.parent==weaponSocket)item.transform.SetParent(transform,false);
                    continue;
                }
                item.transform.SetParent(weaponSocket,false);
                bool isFirearm = item.GetComponent<FirearmWeapon>() != null;
                if (!isFirearm)
                {
                    GetPocketHoldPose(item, out Vector3 holdPos, out Quaternion holdRot);
                    item.transform.localPosition = holdPos;
                    item.transform.localRotation = holdRot;
                }
                item.transform.localScale=slots[i].worldScale*slots[i].viewScale;
                var rb=item.GetComponent<Rigidbody>();
                if(rb!=null){rb.position=item.transform.position;rb.rotation=item.transform.rotation;}
            }
            if(busy || !visible){pocketHint=null;GarageInteractionUI.Instance?.HideHeldHint();return;}
            var active=ActivePhysical;
            string hint=active!=null ? active.PropName+"  |  [Q/G] Положить / бросить"+
                (active.GetComponent<MeleeWeapon>()!=null?"  |  [ЛКМ] Удар · [V] Толчок":"")+
                (active.GetComponent<GarageItemUse>()!=null && active.GetComponent<GarageItemUse>().HasPocketAction?"  |  [F] "+active.GetComponent<GarageItemUse>().ActionLabel:"") : "[V] Толчок";
            if(hint==pocketHint)return;
            pocketHint=hint;
            if(GarageInteractionUI.Instance!=null)
            {if(string.IsNullOrEmpty(hint))GarageInteractionUI.Instance.HideHeldHint();else GarageInteractionUI.Instance.ShowHeldHint(hint);}
        }
        public bool UseActivePhysical()
        {
            if(ActivePhysical==null)return false;
            var use=ActivePhysical.GetComponent<GarageItemUse>();
            return use!=null && use.Use(GetComponent<GaragePlayerController>());
        }
        public bool DropActivePhysical(bool throwForward)
        {
            var slot=GetActiveItem();var item=slot.worldItem;var hands=PlayerHandsInventory.Instance;
            if(item==null || hands==null || hands.HasItem)return false;
            // Reuse the same collision-safe release as large items; remove the slot only after success.
            item.gameObject.SetActive(true);item.transform.SetParent(null,true);item.transform.localScale=slot.worldScale;
            if(!hands.HoldPhysicsProp(item)){item.transform.SetParent(transform,true);RefreshPocketVisual();return false;}
            hands.DropItem(throwForward);
            if(hands.HasItem)
            {hands.DropItem(forStorage:true);item.OnPickedUp();item.transform.SetParent(transform,true);pocketHint=null;RefreshPocketVisual();return false;}
            RemovePhysicalHead(activeSlotIndex);
            pocketHint=null;RefreshPocketVisual();return true;
        }
        private void RemovePhysicalHead(int index)
        {
            var slot=slots[index];
            if(slot.reserves!=null && slot.reserves.Count>0)
            {
                var next=slot.reserves[0];slot.reserves.RemoveAt(0);
                slot.worldItem=next.item;slot.worldScale=next.scale;slot.viewScale=next.viewScale;slot.count--;
                slots[index]=slot;
            }
            else slots[index]=PocketSlotData.Empty;
            OnSlotUpdated?.Invoke(index,slots[index]);pocketHint=null;RefreshPocketVisual();
        }
        public bool TransferActiveTo(Transform destination,out GameObject item,out Vector3 scale)
        {
            item=null;scale=Vector3.one;var slot=GetActiveItem();
            if(slot.worldItem==null || destination==null)return false;
            item=slot.worldItem.gameObject;scale=slot.worldScale;
            item.SetActive(false);item.transform.SetParent(destination,false);item.transform.localScale=scale;
            RemovePhysicalHead(activeSlotIndex);return true;
        }
        public bool MoveSlot(int from,int to)
        {
            if(from<0||to<0||from>=SlotCount||to>=SlotCount||from==to||slots[from].IsEmpty)return false;
            var source=slots[from].worldItem;var target=slots[to].worldItem;
            if(source!=null && target!=null && !string.IsNullOrEmpty(source.StackKey) && source.StackKey==target.StackKey)
            {
                int amount=Mathf.Min(slots[from].count,target.StackLimit-slots[to].count);if(amount<=0)return false;
                if(slots[to].reserves==null)slots[to].reserves=new System.Collections.Generic.List<PocketPhysicalEntry>();
                for(int n=0;n<amount;n++)
                {
                    var head=slots[from];head.worldItem.gameObject.SetActive(false);head.worldItem.transform.SetParent(transform,true);
                    slots[to].reserves.Add(new PocketPhysicalEntry{item=head.worldItem,scale=head.worldScale,viewScale=head.viewScale});slots[to].count++;
                    RemovePhysicalHead(from);
                }
                OnSlotUpdated?.Invoke(to,slots[to]);if(to<HotbarCount)activeSlotIndex=to;OnActiveSlotChanged?.Invoke(activeSlotIndex);pocketHint=null;RefreshPocketVisual();return true;
            }
            var swap=slots[to];slots[to]=slots[from];slots[from]=swap;
            OnSlotUpdated?.Invoke(from,slots[from]);OnSlotUpdated?.Invoke(to,slots[to]);
            if(to<HotbarCount)activeSlotIndex=to;OnActiveSlotChanged?.Invoke(activeSlotIndex);UpdateWeaponVisual();pocketHint=null;RefreshPocketVisual();return true;
        }

        public static void GetPocketHoldPose(PhysicsProp prop, out Vector3 localPos, out Quaternion localRot)
        {
            if (prop == null)
            {
                localPos = new Vector3(-0.04f, 0.04f, -0.04f);
                localRot = Quaternion.Euler(15f, -15f, 5f);
                return;
            }

            if (prop.HoldOffset != Vector3.zero || prop.HoldEuler != Vector3.zero)
            {
                localPos = prop.HoldOffset;
                localRot = Quaternion.Euler(prop.HoldEuler);
                return;
            }

            string name = prop.gameObject.name;
            string propTitle = prop.PropName ?? "";

            // 1. Огнестрельное оружие (если не под управлением FirearmCombat)
            if (prop.GetComponent<FirearmWeapon>() != null || name.Contains("Pistol") || propTitle.Contains("Пистолет"))
            {
                localPos = new Vector3(-0.06f, 0.09f, -0.04f);
                localRot = Quaternion.Euler(-2f, 87f, 4f);
                return;
            }

            // 2. Монтировка / ближний бой
            if (prop.GetComponent<MeleeWeapon>() != null || name.Contains("Crowbar") || propTitle.Contains("Монтировка"))
            {
                localPos = new Vector3(-0.04f, 0.05f, -0.05f);
                localRot = Quaternion.Euler(28f, -22f, 15f);
                return;
            }

            // 3. Гаечные ключи и инструменты
            if (name.Contains("Wrench") || propTitle.Contains("Ключ"))
            {
                localPos = new Vector3(-0.04f, 0.04f, -0.04f);
                localRot = Quaternion.Euler(24f, -20f, 12f);
                return;
            }

            // 4. Фонарик
            if (name.Contains("Flashlight") || propTitle.Contains("Фонар"))
            {
                localPos = new Vector3(-0.06f, 0.08f, -0.05f);
                localRot = Quaternion.Euler(5f, -5f, 0f);
                return;
            }

            // 5. Канистры, бутылки, кружки, жидкости
            if (name.Contains("Canister") || name.Contains("Jerry") || name.Contains("Mug") || name.Contains("Bottle") || propTitle.Contains("Канистр") || propTitle.Contains("Кружк"))
            {
                localPos = new Vector3(-0.04f, -0.03f, -0.02f);
                localRot = Quaternion.Euler(12f, -14f, 4f);
                return;
            }

            // 6. Аккумуляторы
            if (name.Contains("Battery") || propTitle.Contains("Батар") || propTitle.Contains("Аккумул"))
            {
                localPos = new Vector3(-0.04f, 0.03f, -0.03f);
                localRot = Quaternion.Euler(8f, -10f, 2f);
                return;
            }

            // 7. По умолчанию для любого другого физического предмета
            localPos = new Vector3(-0.04f, 0.02f, -0.04f);
            localRot = Quaternion.Euler(15f, -15f, 5f);
        }
    }
}
