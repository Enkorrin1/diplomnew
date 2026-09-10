using RogueDrive.Meta;
using RogueDrive.UI;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивный верстак модернизации автомобиля в убежище.
    /// При взаимодействии открывает интерфейс прокачки 6 веток машины
    /// и освобождает курсор для кликов. Выход по ESC возвращает в режим ходьбы.
    /// </summary>
    public sealed class GarageWorkbenchInteractable : MonoBehaviour, IGarageInteractable
    {
        [Header("References")]
        [SerializeField] private GarageUIController garageUI;
        [SerializeField] private SceneUIView sceneUI;

        private bool isOpen;
        private GaragePlayerController activePlayer;

        public bool IsOpen => isOpen;

        private void Start()
        {
            if (garageUI == null) garageUI = FindFirstObjectByType<GarageUIController>();
            if (sceneUI == null) sceneUI = FindFirstObjectByType<SceneUIView>();
        }

        public string GetPromptText()
        {
            return "[E] Верстак: модернизация узлов машины";
        }

        public bool CanInteract() => !isOpen;

        public void Interact(GaragePlayerController player)
        {
            activePlayer = player;
            OpenWorkbench();
        }

        public void OpenWorkbench()
        {
            isOpen = true;
            if (activePlayer != null)
            {
                activePlayer.SetMovementLocked(true);
            }

            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.ShowNotification("ВЕРСТАК: Прокачайте узлы машины. [ESC] — отойти от верстака", 6f);
            }
        }

        public void CloseWorkbench()
        {
            isOpen = false;
            if (activePlayer != null)
            {
                activePlayer.SetMovementLocked(false);
            }
        }

        private void Update()
        {
            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseWorkbench();
            }
        }
    }
}
