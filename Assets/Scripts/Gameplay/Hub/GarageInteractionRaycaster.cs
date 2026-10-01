using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Лучевой интерактор от первого лица.
    /// Использует комбинацию прямого луча и SphereCast с буфером удержания цели (hysteresis),
    /// предотвращающим мерцание и кратковременное исчезновение подсказок взаимодействия.
    /// </summary>
    public sealed class GarageInteractionRaycaster : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField, Min(1f)] private float maxInteractionDistance = 3.6f;
        [SerializeField] private float sphereCastRadius = 0.28f;
        [SerializeField] private LayerMask interactableLayers = ~0;
        [SerializeField] private float targetGracePeriod = 0.25f; // Время удержания цели при соскакивании луча

        [Header("Hover Outline Settings")]
        [SerializeField] private bool enableHoverOutline = true;
        [SerializeField] private Color outlineActiveColor = new Color(0.24f, 0.90f, 1.0f, 1.0f);
        [SerializeField] private Color outlineWarningColor = new Color(1.0f, 0.72f, 0.25f, 1.0f);

        [Header("References")]
        [SerializeField] private GaragePlayerController player;

        private IGarageInteractable currentTarget;
        public IGarageInteractable CurrentTarget => targetConfirmed ? currentTarget : null;
        private Camera playerCam;
        private float lostTargetTimer = 0f;
        private bool targetConfirmed;

        private void Awake()
        {
            if (player == null)
            {
                player = GetComponent<GaragePlayerController>() ?? GetComponentInParent<GaragePlayerController>();
            }

            if (playerCam == null)
            {
                playerCam = GetComponent<Camera>();
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
            if (player == null)
            {
                player = GetComponent<GaragePlayerController>() ?? GetComponentInParent<GaragePlayerController>();
            }
            if (playerCam == null && player != null)
            {
                playerCam = player.PlayerCamera;
            }
            if (playerCam == null)
            {
                playerCam = GetComponent<Camera>();
            }
            if (playerCam == null)
            {
                playerCam = Camera.main;
            }

            // Подсветка контура рендерится именно камерой игрока, а не первой попавшейся Camera.main
            if (enableHoverOutline && playerCam != null)
            {
                InteractableOutline.Bind(playerCam);
            }
        }

        private void Update()
        {
            if (RogueDrive.UI.InventoryWindowUI.BlockGameplayInput || (player != null && player.IsMovementLocked) || Cursor.lockState != CursorLockMode.Locked)
            {
                if (currentTarget != null)
                {
                    currentTarget = null;
                    if (GarageInteractionUI.Instance != null) GarageInteractionUI.Instance.HidePrompt();
                }
                ClearOutline();
                return;
            }

            PerformRaycast();

            // Обновление подсказки в UGUI и подсветки контура
            if (currentTarget != null)
            {
                bool canInteract = currentTarget.CanInteract();
                string prompt = currentTarget.GetPromptText();

                if (enableHoverOutline)
                {
                    UpdateOutline(currentTarget, canInteract);
                }

                if (!string.IsNullOrEmpty(prompt))
                {
                    if (GarageInteractionUI.Instance != null)
                    {
                        GarageInteractionUI.Instance.ShowPrompt(prompt, canInteract);
                    }

                    // Проверка нажатия клавиши взаимодействия (ЛКМ разрешен только при захваченном курсоре)
                    bool interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) ||
                        (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0));
                    if (interactPressed && targetConfirmed)
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
            else
            {
                ClearOutline();

                if (GarageInteractionUI.Instance != null)
                {
                    GarageInteractionUI.Instance.HidePrompt();
                }
            }
        }

        private void UpdateOutline(IGarageInteractable target, bool canInteract)
        {
            GameObject targetObj = null;
            if (target is Component comp)
            {
                targetObj = comp.gameObject;
            }

            if (targetObj != null)
            {
                Color color = canInteract ? outlineActiveColor : outlineWarningColor;
                InteractableOutline.SetTarget(targetObj, color, 2.2f);
            }
            else
            {
                ClearOutline();
            }
        }

        private void ClearOutline()
        {
            InteractableOutline.Clear();
        }

        private void PerformRaycast()
        {
            targetConfirmed = false;
            if (currentTarget is Object obj && obj == null) currentTarget = null;
            if (currentTarget is Behaviour behaviour && !behaviour.isActiveAndEnabled) currentTarget = null;
            if (playerCam == null) return;

            Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
            IGarageInteractable found = null;
            float effectiveMaxDist = maxInteractionDistance;

            // Определяем трансформ удерживаемого предмета и его тип
            Transform heldItemTransform = null;
            BunkerAssemblyItemType heldItem = BunkerAssemblyItemType.None;
            var handsInv = BunkerPlayerInventory.Instance;
            if (handsInv != null)
            {
                heldItem = handsInv.HeldItem;
                if (handsInv.HeldGameObject != null)
                {
                    heldItemTransform = handsInv.HeldGameObject.transform;
                }
            }

            // 1. Точный прямой луч сквозь все коллайдеры с фильтрацией игрока и удерживаемого предмета
            RaycastHit[] hits = Physics.RaycastAll(ray, effectiveMaxDist, interactableLayers, QueryTriggerInteraction.Collide);
            found = FindBestInteractable(hits, heldItem, heldItemTransform);

            // 2. Если прямой луч не попал точно в интерактивный объект — мягкий объемный SphereCastAll
            if (found == null || found is GarageVehicleBoarding)
            {
                RaycastHit[] sphereHits = Physics.SphereCastAll(ray, sphereCastRadius, effectiveMaxDist, interactableLayers, QueryTriggerInteraction.Collide);
                var softTarget = FindBestInteractable(sphereHits, heldItem, heldItemTransform);
                if(found==null || (softTarget is Component detail && IsVehicleDetail(softTarget)
                    && detail.GetComponentInParent<VehicleModularState>()==((Component)found).GetComponentInParent<VehicleModularState>()))
                    found=softTarget;
            }

            if (found != null)
            {
                targetConfirmed = true;
                currentTarget = found;
                lostTargetTimer = targetGracePeriod; // Сбрасываем таймер удержания
            }
            else
            {
                // Если луч соскочил, даем время буфера (0.25 сек) перед скрытием подсказки, чтобы избежать мерцания
                if (lostTargetTimer > 0f)
                {
                    lostTargetTimer -= Time.deltaTime;
                }
                else
                {
                    currentTarget = null;
                }
            }
        }

        private IGarageInteractable FindBestInteractable(RaycastHit[] hits, BunkerAssemblyItemType heldItem, Transform heldItemTransform)
        {
            if (hits == null || hits.Length == 0) return null;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // Если в руках держим деталь, сначала ищем соответствующий хотспот среди всех пересечений
            if (heldItem != BunkerAssemblyItemType.None)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider col = hits[i].collider;
                    if (col == null) continue;
                    if (player != null && (col.transform.IsChildOf(player.transform) || col.gameObject == player.gameObject)) continue;
                    if (heldItemTransform != null && (col.transform.IsChildOf(heldItemTransform) || col.gameObject == heldItemTransform.gameObject)) continue;

                    var hotspot = col.GetComponentInParent<VehiclePartHotspot>();
                    if (hotspot != null && hotspot.enabled && !hotspot.IsInstalled && hotspot.RequiredItem == heldItem && HasLineOfSight(hits[i], hotspot, heldItemTransform))
                    {
                        return hotspot;
                    }
                }
            }

            // The whole body may carry boarding. Keep it as fallback while checking
            // the same vehicle's smaller service triggers, which can sit inside its body.
            IGarageInteractable boardingFallback=null;
            VehicleModularState boardingCar=null;
            // Иначе возвращаем ближайший валидный интерактивный объект
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (player != null && (col.transform.IsChildOf(player.transform) || col.gameObject == player.gameObject)) continue;
                if (heldItemTransform != null && (col.transform.IsChildOf(heldItemTransform) || col.gameObject == heldItemTransform.gameObject)) continue;

                var interactable = col.GetComponentInParent<IGarageInteractable>();
                if (interactable != null)
                {
                    if (!HasLineOfSight(hits[i], interactable as Component, heldItemTransform)) continue;
                    if (interactable is MonoBehaviour mb && !mb.enabled) continue;
                    if (string.IsNullOrEmpty(interactable.GetPromptText()) && !interactable.CanInteract()) continue;
                    if(interactable is GarageVehicleBoarding boarding)
                    {
                        if(boardingFallback==null){boardingFallback=boarding;boardingCar=boarding.GetComponentInParent<VehicleModularState>();}
                        continue;
                    }
                    if(interactable is VehiclePassengerEntry passengerEntry)
                    {
                        if(boardingFallback==null){boardingFallback=passengerEntry;boardingCar=passengerEntry.GetComponentInParent<VehicleModularState>();}
                        continue;
                    }
                    if(boardingFallback==null || (IsVehicleDetail(interactable)
                        && col.GetComponentInParent<VehicleModularState>()==boardingCar))return interactable;
                }
            }

            return boardingFallback;
        }

        private static bool IsVehicleDetail(IGarageInteractable target) =>
            target is VehicleInspectionHotspot || target is VehicleTireHotspot || target is VehiclePartHotspot;

        private bool HasLineOfSight(RaycastHit candidate, Component target, Transform held)
        {
            if (target == null || playerCam == null) return false;
            Vector3 delta = candidate.point - playerCam.transform.position;
            var targetCar = target.GetComponentInParent<VehicleModularState>();
            foreach (var hit in Physics.RaycastAll(playerCam.transform.position, delta.normalized,
                delta.magnitude, interactableLayers, QueryTriggerInteraction.Ignore))
            {
                var t = hit.transform;
                if (player != null && t.IsChildOf(player.transform)) continue;
                if (held != null && t.IsChildOf(held)) continue;
                if (t.IsChildOf(target.transform) || target.transform.IsChildOf(t)) continue;
                if (targetCar != null && t.GetComponentInParent<VehicleModularState>() == targetCar) continue;
                if (hit.collider != candidate.collider && hit.distance < delta.magnitude - 0.05f) return false;
            }
            return true;
        }

        private void OnDisable()
        {
            currentTarget = null;
            targetConfirmed = false;
            ClearOutline();

            if (GarageInteractionUI.Instance != null)
            {
                GarageInteractionUI.Instance.HidePrompt();
            }
        }
    }
}
