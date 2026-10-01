using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивный мелкий предмет в мире (на столе, верстаке, полу, стеллаже),
    /// который при нажатии [E] поднимается напрямую в карманный инвентарь (Hotbar 1..5).
    /// Используется для ключей, гаечных ключей, отверток, фонарика, расходников и документов.
    /// </summary>
    public sealed class PocketItemPickup : MonoBehaviour, IGarageInteractable
    {
        [Header("Item Data")]
        [SerializeField] private string itemId = "small_item";
        [SerializeField] private string itemDisplayName = "Мелкий предмет";
        [SerializeField] private int count = 1;
        [SerializeField] private Sprite icon;

        [Header("Visuals & Audio")]
        [SerializeField] private GameObject visualModel;
        [SerializeField] private AudioClip pickupSound;

        private void Start()
        {
            if (visualModel == null) visualModel = gameObject;
        }

        public void Configure(string id, string displayName, int amount = 1, Sprite itemIcon = null)
        {
            itemId = id;
            itemDisplayName = displayName;
            count = amount;
            icon = itemIcon;
        }

        public string GetPromptText()
        {
            return $"[E] Взять: {itemDisplayName} в карман";
        }

        public bool CanInteract()
        {
            return true;
        }

        public void Interact(GaragePlayerController player)
        {
            var pocketInv = PlayerPocketInventory.Instance;
            if (pocketInv == null)
            {
                var p = player != null ? player.GetComponent<PlayerPocketInventory>() : FindFirstObjectByType<PlayerPocketInventory>();
                if (p != null) pocketInv = p;
            }

            if (pocketInv != null)
            {
                bool added = pocketInv.TryAddItem(itemId, itemDisplayName, count, icon);
                if (added)
                {
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayKeysJingle();
                    }

                    if (GaragePrologueManager.Instance != null)
                    {
                        GaragePrologueManager.Instance.ShowNotification($"В карман убрано: {itemDisplayName}", 2.0f);
                    }

                    Destroy(gameObject);
                }
                else
                {
                    if (GaragePrologueManager.Instance != null)
                    {
                        GaragePrologueManager.Instance.ShowNotification("Карманы переполнены! (Все 5 слотов заняты)", 2.5f);
                    }
                }
            }
        }
    }
}
