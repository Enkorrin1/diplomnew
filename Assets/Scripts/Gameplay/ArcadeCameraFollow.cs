using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Аркадная динамическая камера сзади-сверху (Cinemachine-style).
    /// Обеспечивает плавное следование, упреждающий фокус на трассу вперед,
    /// динамический FOV на высокой скорости/нитро и эффект сотрясения экрана (Screen Shake).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ArcadeCameraFollow : MonoBehaviour
    {
        public static ArcadeCameraFollow Instance { get; private set; }

        [Header("Target & Follow")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 6.5f, -10.5f);
        [SerializeField, Min(0.1f)] private float positionSharpness = 7f;
        [SerializeField, Min(0.1f)] private float rotationSharpness = 6f;
        [SerializeField, Min(0f)] private float lookAheadDistance = 12f;

        [Header("Dynamic FOV")]
        [SerializeField, Range(40f, 90f)] private float normalFov = 60f;
        [SerializeField, Range(50f, 100f)] private float maxSpeedFov = 72f;
        [SerializeField, Range(60f, 110f)] private float nitroFov = 78f;
        [SerializeField, Min(0.1f)] private float fovSharpness = 5f;

        Camera cam;
        float currentYaw;
        float shakeDuration;
        float shakeIntensity;

        public void Configure(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                currentYaw = target.eulerAngles.y;
                transform.position = CalculateTargetPosition(currentYaw);
                transform.LookAt(CalculateLookTarget());
            }
        }

        private void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            if (target != null)
                currentYaw = target.eulerAngles.y;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void TriggerShake(float intensity = 0.5f, float duration = 0.25f)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeDuration = Mathf.Max(shakeDuration, duration);
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            float dt = Time.unscaledDeltaTime; // независим от timeScale при Slow-Mo

            // Плавное сглаживание угла поворота за машиной
            float targetYaw = target.eulerAngles.y;
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, 1f - Mathf.Exp(-rotationSharpness * dt));

            // Позиция камеры с учетом сглаженного поворота
            Vector3 desiredPosition = CalculateTargetPosition(currentYaw);
            Vector3 shakeOffset = Vector3.zero;

            if (shakeDuration > 0f)
            {
                shakeDuration -= dt;
                shakeOffset = Random.insideUnitSphere * shakeIntensity;
                shakeIntensity = Mathf.MoveTowards(shakeIntensity, 0f, dt * 2f);
            }

            transform.position = Vector3.Lerp(transform.position, desiredPosition + shakeOffset, 1f - Mathf.Exp(-positionSharpness * dt));

            // Фокус взгляда направлен на трассу перед машиной
            Vector3 lookTarget = CalculateLookTarget();
            Quaternion desiredRotation = Quaternion.LookRotation((lookTarget - transform.position).normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSharpness * dt));

            // Динамический FOV
            UpdateFov(dt);
        }

        Vector3 CalculateTargetPosition(float yaw)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            return target.position + rotation * offset;
        }

        Vector3 CalculateLookTarget()
        {
            return target.position + target.forward * lookAheadDistance + Vector3.up * 1.5f;
        }

        void UpdateFov(float dt)
        {
            if (cam == null)
                return;

            float targetFov = normalFov;
            ArcadeCarController car = target.GetComponent<ArcadeCarController>();

            if (car != null)
            {
                float speedPercent = Mathf.Clamp01(car.SpeedMps / car.TopSpeedMps);

                if (car.IsNitroActive)
                {
                    targetFov = nitroFov;
                }
                else
                {
                    targetFov = Mathf.Lerp(normalFov, maxSpeedFov, speedPercent);
                }
            }

            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSharpness * dt));
        }
    }
}
