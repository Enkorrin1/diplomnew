using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивная связка ключей зажигания на верстаке.
    /// Игрок должен найти и забрать её перед выездом из гаража.
    /// </summary>
    public sealed class GarageCarKeys : MonoBehaviour, IGarageInteractable
    {
        [Header("Visuals")]
        [SerializeField] private GameObject visualModel;

        private void Start()
        {
            if (visualModel == null) visualModel = gameObject;

            // Если ключи уже получены, скрываем объект
            if (GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.HasCarKeys)
            {
                visualModel.SetActive(false);
                if (PlayerPocketInventory.Instance != null && !PlayerPocketInventory.Instance.HasItem("car_keys"))
                {
                    PlayerPocketInventory.Instance.TryAddItem("car_keys", "Ключи авто", 1);
                }
            }
        }

        public string GetPromptText()
        {
            return "[E] Взять ключи зажигания от машины";
        }

        public bool CanInteract()
        {
            return GaragePrologueManager.Instance != null && !GaragePrologueManager.Instance.HasCarKeys;
        }

        public void Interact(GaragePlayerController player)
        {
            if (!CanInteract()) return;

            if (PlayerPocketInventory.Instance != null)
            {
                if (!PlayerPocketInventory.Instance.TryAddItem("car_keys", "Ключи авто", 1))
                {
                    GaragePrologueManager.Instance.ShowNotification("Освободите место в карманах для ключей.", 3f);
                    return;
                }
            }

            GaragePrologueManager.Instance.PickUpKeys();

            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayKeysJingle();
            }

            if (visualModel != null)
            {
                visualModel.SetActive(false);
            }
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
#endif
    }
}
