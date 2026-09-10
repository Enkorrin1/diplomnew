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
        [SerializeField] private Light itemHighlightLight;

        private void Start()
        {
            if (visualModel == null) visualModel = gameObject;

            // Если ключи уже получены, скрываем объект
            if (GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.HasCarKeys)
            {
                visualModel.SetActive(false);
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
            if (GaragePrologueManager.Instance == null) return;

            GaragePrologueManager.Instance.PickUpKeys();

            if (visualModel != null)
            {
                visualModel.SetActive(false);
            }
            if (itemHighlightLight != null)
            {
                itemHighlightLight.enabled = false;
            }
        }

        private void Update()
        {
            // Легкое покачивание и подсветка ключей
            if (visualModel != null && visualModel.activeSelf)
            {
                transform.Rotate(Vector3.up * (45f * Time.deltaTime));
            }
        }
    }
}
