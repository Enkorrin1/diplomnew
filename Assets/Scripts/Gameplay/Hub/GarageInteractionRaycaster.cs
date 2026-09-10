using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Лучевой интерактор от первого лица. Выпускает луч из центра экрана,
    /// выявляет интерактивные объекты и отображает контекстную подсказку на экране.
    /// Позволяет взаимодействовать по нажатию клавиши [E].
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

        private GUIStyle promptStyle;
        private GUIStyle crosshairStyle;
        private Texture2D promptBgTex;
        private Texture2D crosshairTex;

        private void Awake()
        {
            if (player == null)
            {
                player = GetComponent<GaragePlayerController>();
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
                currentTarget = null;
                return;
            }

            PerformRaycast();

            // Проверка нажатия клавиши взаимодействия
            if (currentTarget != null && currentTarget.CanInteract())
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                {
                    currentTarget.Interact(player);
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

        private void OnGUI()
        {
            if (player != null && player.IsMovementLocked) return;

            EnsureStyles();

            float screenCenterX = Screen.width * 0.5f;
            float screenCenterY = Screen.height * 0.5f;

            // 1. Точечный прицел (Crosshair)
            float dotSize = currentTarget != null ? 8f : 5f;
            Color dotColor = currentTarget != null ? new Color(0.2f, 0.9f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.6f);
            GUI.color = dotColor;
            GUI.DrawTexture(new Rect(screenCenterX - dotSize * 0.5f, screenCenterY - dotSize * 0.5f, dotSize, dotSize), crosshairTex);
            GUI.color = Color.white;

            // 2. Всплывающая подсказка взаимодействия
            if (currentTarget != null && currentTarget.CanInteract())
            {
                string prompt = currentTarget.GetPromptText();
                if (!string.IsNullOrEmpty(prompt))
                {
                    float width = 340f;
                    float height = 44f;
                    float y = screenCenterY + 36f;
                    Rect promptRect = new Rect(screenCenterX - width * 0.5f, y, width, height);

                    GUI.Box(promptRect, prompt, promptStyle);
                }
            }
        }

        private void EnsureStyles()
        {
            if (crosshairTex == null)
            {
                crosshairTex = new Texture2D(1, 1);
                crosshairTex.SetPixel(0, 0, Color.white);
                crosshairTex.Apply();
            }

            if (promptBgTex == null)
            {
                promptBgTex = new Texture2D(1, 1);
                promptBgTex.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.12f, 0.88f));
                promptBgTex.Apply();
            }

            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = promptBgTex, textColor = new Color(1f, 0.88f, 0.25f) },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private void OnDestroy()
        {
            if (crosshairTex != null) Destroy(crosshairTex);
            if (promptBgTex != null) Destroy(promptBgTex);
        }
    }
}
