using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Контроллер гермоворот гаража.
    /// Игрок открывает их через механизм на стене, открывая путь на ночную трассу.
    /// </summary>
    public sealed class GarageGateController : MonoBehaviour, IGarageInteractable
    {
        [Header("Gate Movement")]
        [SerializeField] private Transform gateDoorTransform;
        [SerializeField] private float openHeight = 5.5f;
        [SerializeField] private float openSpeed = 2.0f;

        private Vector3 closedPos;
        private Vector3 targetPos;
        private bool isOpening;

        private void Start()
        {
            if (gateDoorTransform == null) gateDoorTransform = transform;
            closedPos = gateDoorTransform.localPosition;
            targetPos = closedPos + Vector3.up * openHeight;

            if (GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsGateOpen)
            {
                gateDoorTransform.localPosition = targetPos;
            }
        }

        public string GetPromptText()
        {
            bool isOpen = GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsGateOpen;
            return isOpen
                ? "Ворота открыты (Путь на трассу свободен)"
                : "[E] Открыть гермоворота ангара";
        }

        public bool CanInteract()
        {
            return GaragePrologueManager.Instance != null && !GaragePrologueManager.Instance.IsGateOpen;
        }

        public void Interact(GaragePlayerController player)
        {
            if (GaragePrologueManager.Instance == null || GaragePrologueManager.Instance.IsGateOpen) return;

            GaragePrologueManager.Instance.OpenGate();
            isOpening = true;
        }

        private void Update()
        {
            if (isOpening && gateDoorTransform != null)
            {
                gateDoorTransform.localPosition = Vector3.MoveTowards(
                    gateDoorTransform.localPosition,
                    targetPos,
                    openSpeed * Time.deltaTime);

                if (Vector3.Distance(gateDoorTransform.localPosition, targetPos) < 0.05f)
                {
                    gateDoorTransform.localPosition = targetPos;
                    isOpening = false;
                }
            }
        }
    }
}
