using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Лучевой интерактор от первого лица. Выпускает луч из центра экрана,
    /// выявляет интерактивные объекты и передает подсказку в современный Canvas (GarageInteractionUI).
    /// Полностью очищен от устаревшего OnGUI. Позволяет взаимодействовать по нажатию клавиши [E].
    /// </summary>
    public sealed class GarageInteractionRaycaster : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField, Min(1f)] private float maxInteractionDistance = 2.6f;
        [SerializeField] private LayerMask interactableLayers = ~0;

        [Header("References")]
        [SerializeField] private GaragePlayerController player;

        private IGarageInteractable currentTarget;
        private Camera playerCam;

        private void Awake()
        {
            if (player == null)
            {
                player = GetComponent<GaragePlayerController>();
            }

            // Гарантируем наличие UGUI интерфейса
            if (GarageInteractionUI.Instance == null)
            {
                var uiObj = new GameObject("GarageInteractionUI");
                uiObj.AddComponent<GarageInteractionUI>();
            }
        }

        private void Start()
        {
            if (player != null)
            {
                playerCam = player.PlayerCamera;
            }
            if (playerCam == null)
            {
                playerCam = Camera.main;
            }
        }

        private void Update()
        {
            if (player != null && player.IsMovementLocked)
            {
                if (currentTarget != null)
                {
                    currentTarget = null;
                    if (GarageInteractionUI.Instance != null) GarageInteractionUI.Instance.HidePrompt();
                }
                return;
            }

            PerformRaycast();

            // Обновление подсказки в UGUI
            if (currentTarget != null && currentTarget.CanInteract())
            {
                if (GarageInteractionUI.Instance != null)
                {
                    GarageInteractionUI.Instance.ShowPrompt(currentTarget.GetPromptText(), true);
                }

                // Проверка нажатия клавиши взаимодействия
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                {
                    currentTarget.Interact(player);
                }
            }
            else
            {
                if (GarageInteractionUI.Instance != null)
                {
                    GarageInteractionUI.Instance.HidePrompt();
                }
            }
        }

        private void PerformRaycast()
        {
            if (playerCam == null) return;

            Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance, interactableLayers))
            {
                IGarageInteractable interactable = hit.collider.GetComponentInParent<IGarageInteractable>();
                if (interactable != null && interactable.CanInteract())
                {
                    currentTarget = interactable;
                    return;
                }
            }

            currentTarget = null;
        }

        private void OnDisable()
        {
            if (GarageInteractionUI.Instance != null)
            {
                GarageInteractionUI.Instance.HidePrompt();
            }
        }
    }
}
