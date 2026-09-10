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
        private float bobTimer;
        private float footstepTimer;
        private bool isMovementLocked;

        public Camera PlayerCamera => playerCamera;
        public bool IsMovementLocked => isMovementLocked;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }

            if (playerCamera != null)
            {
                defaultCameraLocalPos = playerCamera.transform.localPosition;
            }

            LockCursor(true);
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

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            if (isMovementLocked) return;

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

            float inputX = Input.GetAxisRaw("Horizontal");
            float inputZ = Input.GetAxisRaw("Vertical");

            Vector3 moveDirection = (transform.right * inputX + transform.forward * inputZ).normalized;
            bool isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float currentSpeed = (isSprinting && inputZ > 0.1f) ? sprintSpeed : walkSpeed;

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
                    footstepTimer = isSprinting ? footstepInterval * 0.7f : footstepInterval;
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

            if (controller.isGrounded && speed > 0.2f)
            {
                bobTimer += Time.deltaTime * (speed * bobFrequency * 0.45f);
                float bobOffset = Mathf.Sin(bobTimer) * bobAmplitude;
                playerCamera.transform.localPosition = defaultCameraLocalPos + new Vector3(0f, bobOffset, 0f);
            }
            else
            {
                bobTimer = 0f;
                playerCamera.transform.localPosition = Vector3.Lerp(
                    playerCamera.transform.localPosition,
                    defaultCameraLocalPos,
                    Time.deltaTime * 6f);
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
