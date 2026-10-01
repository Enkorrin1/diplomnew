using System;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Контроллер персонажа от первого лица в убежище (Garage Hub).
    /// Поддерживает плавное перемещение на WASD, бег на Shift, обзор мышью,
    /// покачивание камеры при ходьбе (head bobbing) и блокировку курсора.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class GaragePlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(1f)] private float walkSpeed = 3.2f;
        [SerializeField, Min(1f)] private float sprintSpeed = 5.2f;
        [SerializeField] private float gravity = -18f;

        [Header("Jump & Crouch")]
        [SerializeField, Min(0.5f)] private float jumpHeight = 1.15f;
        [SerializeField] private float crouchHeight = 1.0f;
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchCenterY = 0.5f;
        [SerializeField] private float standingCenterY = 0.9f;
        [SerializeField] private float crouchCameraY = 0.85f;
        [SerializeField] private float standingCameraY = 1.65f;
        [SerializeField] private float crouchTransitionSpeed = 10f;
        [SerializeField, Range(0.2f, 1f)] private float crouchSpeedMultiplier = 0.55f;

        [Header("Mouse Look")]
        [SerializeField, Min(0.1f)] private float mouseSensitivity = 2.0f;
        [SerializeField] private float lookUpLimit = -80f;
        [SerializeField] private float lookDownLimit = 80f;
        [SerializeField] private bool invertY = false;

        [Header("Camera & Head Bob")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float bobFrequency = 8f;
        [SerializeField] private float bobAmplitude = 0.04f;

        [Header("Audio")]
        [SerializeField] private float footstepInterval = 0.5f;

        private CharacterController controller;
        private float verticalVelocity;
        private float cameraPitch;
        private Vector3 defaultCameraLocalPos;
        private float currentCameraY;
        private bool isCrouching;
        private float bobTimer;
        private float footstepTimer;
        private bool isMovementLocked;

        public Camera PlayerCamera => playerCamera;
        public bool IsMovementLocked => isMovementLocked;
        public bool IsCrouching => isCrouching;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                standingHeight = controller.height;
                standingCenterY = controller.center.y;
                crouchHeight = Mathf.Max(0.75f, standingHeight * 0.55f);
                crouchCenterY = standingCenterY * 0.55f;
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }

            if (playerCamera != null)
            {
                playerCamera.nearClipPlane = 0.02f;
                defaultCameraLocalPos = playerCamera.transform.localPosition;
                standingCameraY = defaultCameraLocalPos.y;
                crouchCameraY = standingCameraY * 0.55f;
                currentCameraY = standingCameraY;
            }

            if (GetComponent<BunkerPlayerInventory>() == null)
            {
                gameObject.AddComponent<BunkerPlayerInventory>();
            }

            if (GetComponent<PlayerPocketInventory>() == null)
            {
                gameObject.AddComponent<PlayerPocketInventory>();
            }

            if (GetComponent<PlayerMeleeCombat>() == null) gameObject.AddComponent<PlayerMeleeCombat>();
            if (GetComponent<PlayerFirearmCombat>() == null) gameObject.AddComponent<PlayerFirearmCombat>();
            if (GetComponent<PlayerDownedState>() == null) gameObject.AddComponent<PlayerDownedState>();
            LockCursor(true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && !isMovementLocked && !RogueDrive.UI.InventoryWindowUI.BlockGameplayInput)
            {
                LockCursor(true);
            }
        }

        private void OnDestroy()
        {
            LockCursor(false);
        }

        public void SetMovementLocked(bool locked)
        {
            isMovementLocked = locked;
            LockCursor(!locked);
        }

        public void SetCameraPitch(float pitch)
        {
            cameraPitch = Mathf.Clamp(pitch, lookUpLimit, lookDownLimit);
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            if (GetComponent<PlayerFieldNeeds>()?.IsDead == true) return;
            if (RogueDrive.UI.InventoryWindowUI.BlockGameplayInput) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LockCursor(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked && !isMovementLocked)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.anyKeyDown)
                {
                    LockCursor(true);
                }
            }

            if (isMovementLocked || Cursor.lockState != CursorLockMode.Locked) return;

            HandleMouseLook();
            HandleMovement();
            HandleHeadBob();
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity * (invertY ? 1f : -1f);

            // Поворот всего тела по горизонтали
            transform.Rotate(Vector3.up * mouseX);

            // Наклон головы/камеры по вертикали с ограничением угла
            if (playerCamera != null)
            {
                cameraPitch = Mathf.Clamp(cameraPitch + mouseY, lookUpLimit, lookDownLimit);
                playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            bool isGrounded = controller.isGrounded;
            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            // Обработка приседания (LeftControl, RightControl, C)
            bool wantsCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.C);
            if (!wantsCrouch && isCrouching)
            {
                // Проверяем свободное пространство над головой перед вставанием
                Vector3 checkStart = transform.position + Vector3.up * (crouchHeight - controller.radius * 0.5f);
                float clearanceNeeded = standingHeight - crouchHeight;
                if (Physics.SphereCast(checkStart, controller.radius * 0.85f, Vector3.up, out _, clearanceNeeded, ~0, QueryTriggerInteraction.Ignore))
                {
                    // Над головой препятствие — игрок остается присевшим
                    wantsCrouch = true;
                }
            }

            isCrouching = wantsCrouch;

            // Плавное изменение высоты и центра коллайдера персонажа
            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            float targetCenterY = isCrouching ? crouchCenterY : standingCenterY;
            float targetCamY = isCrouching ? crouchCameraY : standingCameraY;

            if (Mathf.Abs(controller.height - targetHeight) > 0.005f)
            {
                controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
                controller.center = new Vector3(0f, Mathf.Lerp(controller.center.y, targetCenterY, Time.deltaTime * crouchTransitionSpeed), 0f);
            }
            currentCameraY = Mathf.Lerp(currentCameraY, targetCamY, Time.deltaTime * crouchTransitionSpeed);

            // Прыжок на Пробел (Space)
            if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            {
                if (isCrouching)
                {
                    // Если нажали прыжок в приседе, пытаемся встать и подпрыгнуть при наличии свободного места
                    Vector3 checkStart = transform.position + Vector3.up * (crouchHeight - controller.radius * 0.5f);
                    float clearanceNeeded = standingHeight - crouchHeight;
                    if (!Physics.SphereCast(checkStart, controller.radius * 0.85f, Vector3.up, out _, clearanceNeeded, ~0, QueryTriggerInteraction.Ignore))
                    {
                        isCrouching = false;
                        verticalVelocity = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
                        PlayJumpSound();
                    }
                }
                else
                {
                    verticalVelocity = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
                    PlayJumpSound();
                }
            }

            float inputX = Input.GetAxisRaw("Horizontal");
            float inputZ = Input.GetAxisRaw("Vertical");

            var downed = GetComponent<PlayerDownedState>();
            bool isDowned = downed != null && downed.IsDowned;

            Vector3 moveDirection = (transform.right * inputX + transform.forward * inputZ).normalized;
            bool isSprinting = !isCrouching && !isDowned && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            
            float currentSpeed = walkSpeed;
            if (isDowned)
            {
                currentSpeed = 0.9f;
            }
            else if (isCrouching)
            {
                currentSpeed = walkSpeed * crouchSpeedMultiplier;
            }
            else if (isSprinting && inputZ > 0.1f)
            {
                currentSpeed = sprintSpeed;
            }

            Vector3 velocity = moveDirection * currentSpeed;

            // Гравитация
            verticalVelocity += gravity * Time.deltaTime;
            velocity.y = verticalVelocity;

            controller.Move(velocity * Time.deltaTime);

            // Звуки шагов при движении по полу
            if (isGrounded && moveDirection.sqrMagnitude > 0.1f)
            {
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0f)
                {
                    float interval = isCrouching ? footstepInterval * 1.35f : (isSprinting ? footstepInterval * 0.7f : footstepInterval);
                    footstepTimer = interval;
                    PlayFootstepSound();
                }
            }
            else
            {
                footstepTimer = 0.1f;
            }
        }

        private void HandleHeadBob()
        {
            if (playerCamera == null) return;

            Vector3 horizontalMove = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
            float speed = horizontalMove.magnitude;
            Vector3 baseCamPos = new Vector3(defaultCameraLocalPos.x, currentCameraY, defaultCameraLocalPos.z);

            if (controller.isGrounded && speed > 0.2f)
            {
                float freq = isCrouching ? bobFrequency * 0.7f : bobFrequency;
                float amp = isCrouching ? bobAmplitude * 0.5f : bobAmplitude;
                bobTimer += Time.deltaTime * (speed * freq * 0.45f);
                float bobOffset = Mathf.Sin(bobTimer) * amp;
                playerCamera.transform.localPosition = baseCamPos + new Vector3(0f, bobOffset, 0f);
            }
            else
            {
                bobTimer = 0f;
                playerCamera.transform.localPosition = Vector3.Lerp(
                    playerCamera.transform.localPosition,
                    baseCamPos,
                    Time.deltaTime * 8f);
            }
        }

        private void PlayJumpSound()
        {
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayImpact();
            }
        }

        private void PlayFootstepSound()
        {
            // Процедурный щелчок подошвы по бетону
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayImpact();
            }
        }
    }
}
