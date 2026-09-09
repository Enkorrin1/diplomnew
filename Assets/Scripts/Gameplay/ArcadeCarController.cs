using UnityEngine;
using RogueDrive.Modifiers;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Физический аркадный контроллер автомобиля на базе Rigidbody.
    /// Поддерживает свободное руление по широкой дороге, разгон, торможение,
    /// нитро-ускорение, накат по инерции при пустом баке и боковое сцепление (lateral grip).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeCarController : MonoBehaviour
    {
        [Header("Engine & Speed")]
        [SerializeField, Min(1f)] private float topSpeedMps = 28f;         // ~100 км/ч
        [SerializeField, Min(1f)] private float nitroTopSpeedMps = 42f;    // ~150 км/ч
        [SerializeField, Min(1f)] private float acceleration = 30f;
        [SerializeField, Min(1f)] private float nitroAcceleration = 55f;
        [SerializeField, Min(1f)] private float brakeForce = 45f;
        [SerializeField, Min(1f)] private float reverseSpeedMps = 10f;

        [Header("Steering & Handling")]
        [SerializeField, Min(1f)] private float steerSpeed = 65f;          // градусов в секунду
        [SerializeField, Range(0f, 1f)] private float lateralGrip = 0.88f; // гашение бокового скольжения
        [SerializeField, Min(0f)] private float downforce = 15f;          // прижимная сила к дороге
        [SerializeField, Min(0f)] private float bodyRollTilt = 3.5f;       // наклон кузова в повороте

        [Header("Fuel & Nitro Consumption")]
        [SerializeField, Min(0f)] private float fuelPerSecond = 1.8f;
        [SerializeField, Min(0f)] private float nitroPerSecond = 35f;

        [Header("Collision Damage")]
        [SerializeField, Min(0f)] private float baseObstacleDamage = 20f;

        [Header("Visual Body (Optional tilt)")]
        [SerializeField] private Transform visualBody;

        Rigidbody body;
        GameRunController run;
        SocketRegistry sockets;

        float throttleInput;
        float steerInput;
        bool isNitroRequested;
        bool isGrounded;
        Vector3 lastPosition;

        public float SpeedMps { get; private set; }
        public float SpeedKmh => SpeedMps * 3.6f;
        public float TopSpeedMps => IsNitroActive ? nitroTopSpeedMps : topSpeedMps;
        public bool IsNitroActive { get; private set; }
        public bool IsGrounded => isGrounded;
        public SocketRegistry Sockets => sockets;

        public void Configure(GameRunController controller)
        {
            run = controller;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            sockets = GetComponent<SocketRegistry>();

            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.4f;
            body.angularDamping = 2.5f;

            lastPosition = transform.position;
        }

        private void Update()
        {
            if (run == null || run.IsGameOver)
            {
                throttleInput = 0f;
                steerInput = 0f;
                isNitroRequested = false;
                IsNitroActive = false;
                return;
            }

            // Клавиатурный / экранный ввод
            throttleInput = Input.GetAxisRaw("Vertical");
            steerInput = Input.GetAxisRaw("Horizontal");
            isNitroRequested = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift);

            // Обработка расхода нитро
            if (isNitroRequested && throttleInput > 0f && !run.IsOutOfFuel)
            {
                IsNitroActive = run.TryConsumeNitro(nitroPerSecond * Time.deltaTime);
            }
            else
            {
                IsNitroActive = false;
            }

            // Расход топлива при нажатой педали газа
            if (throttleInput > 0f && !run.IsOutOfFuel)
            {
                float fuelCost = fuelPerSecond * (IsNitroActive ? 1.5f : 1.0f) * Time.deltaTime;
                run.ConsumeFuel(fuelCost);
            }

            // Расчёт пройденной дистанции
            float distanceDelta = Vector3.Distance(transform.position, lastPosition);
            if (distanceDelta > 0f && Vector3.Dot(body.linearVelocity, transform.forward) > 0f)
            {
                run.ReportTravelled(distanceDelta);
            }
            lastPosition = transform.position;

            // Проверка остановки при пустом баке (инерционный накат окончен)
            if (run.IsOutOfFuel && Mathf.Abs(SpeedMps) < 0.25f)
            {
                run.ReportCarStopped();
            }

            // Визуальный крен кузова в поворотах
            UpdateVisualRoll(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            SpeedMps = Vector3.Dot(body.linearVelocity, transform.forward);

            if (!isGrounded)
            {
                // В воздухе прижимаем автомобиль вниз для стабильности
                body.AddForce(Vector3.down * (downforce * 2f), ForceMode.Acceleration);
                return;
            }

            // 1. Прижимная сила (Downforce) пропорциональна скорости
            float currentDownforce = downforce * (1f + Mathf.Abs(SpeedMps) / topSpeedMps);
            body.AddForce(Vector3.down * currentDownforce, ForceMode.Acceleration);

            // 2. Продольная тяга и торможение
            ApplyDriveForces();

            // 3. Руление (поворот машины вокруг оси Y)
            ApplySteering();

            // 4. Боковое сцепление (подавление бокового скольжения)
            ApplyLateralGrip();
        }

        void ApplyDriveForces()
        {
            if (run != null && run.IsGameOver)
                return;

            // Если топливо закончилось — двигатель заглушен, тяга отсутствует (чистый накат)
            if (run != null && run.IsOutOfFuel)
            {
                return;
            }

            float currentTopSpeed = IsNitroActive ? nitroTopSpeedMps : topSpeedMps;
            float currentAccel = IsNitroActive ? nitroAcceleration : acceleration;

            if (throttleInput > 0.05f)
            {
                if (SpeedMps < currentTopSpeed)
                {
                    float force = currentAccel * throttleInput;
                    body.AddForce(transform.forward * force, ForceMode.Acceleration);
                }
            }
            else if (throttleInput < -0.05f)
            {
                if (SpeedMps > 1f)
                {
                    // Тормоз при движении вперед
                    body.AddForce(-transform.forward * (brakeForce * Mathf.Abs(throttleInput)), ForceMode.Acceleration);
                }
                else if (SpeedMps > -reverseSpeedMps)
                {
                    // Задний ход
                    body.AddForce(-transform.forward * (acceleration * 0.6f * Mathf.Abs(throttleInput)), ForceMode.Acceleration);
                }
            }
        }

        void ApplySteering()
        {
            if (Mathf.Abs(steerInput) < 0.05f)
                return;

            // На месте или очень низкой скорости руление приглушается
            float speedFactor = Mathf.Clamp01(Mathf.Abs(SpeedMps) / 4f);
            float turnAmount = steerInput * steerSpeed * speedFactor * Time.fixedDeltaTime;

            // Инвертируем поворот при движении назад
            if (SpeedMps < -0.5f)
                turnAmount = -turnAmount;

            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            body.MoveRotation(body.rotation * turnRotation);
        }

        void ApplyLateralGrip()
        {
            Vector3 right = transform.right;
            float lateralVelocity = Vector3.Dot(body.linearVelocity, right);
            Vector3 counterForce = -right * (lateralVelocity * lateralGrip);
            body.AddForce(counterForce, ForceMode.VelocityChange);
        }

        void CheckGrounded()
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, Vector3.down);
            isGrounded = Physics.Raycast(ray, 0.9f);
        }

        void UpdateVisualRoll(float dt)
        {
            if (visualBody == null)
                return;

            float targetRoll = -steerInput * bodyRollTilt * Mathf.Clamp01(Mathf.Abs(SpeedMps) / 5f);
            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetRoll);
            visualBody.localRotation = Quaternion.Slerp(visualBody.localRotation, targetRot, 1f - Mathf.Exp(-8f * dt));
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleObstacleHit(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleObstacleHit(other.gameObject);
        }

        void HandleObstacleHit(GameObject targetGo)
        {
            if (run == null || run.IsGameOver)
                return;

            TrackObstacle obstacle = targetGo.GetComponent<TrackObstacle>();
            if (obstacle == null || !obstacle.TryConsume())
                return;

            // Эффект удара: сотрясение камеры
            ArcadeCameraFollow.Instance?.TriggerShake(0.7f, 0.3f);

            // Если на полном ходу или на нитро — препятствие сносится легче
            float damage = baseObstacleDamage;
            if (IsNitroActive)
            {
                damage *= 0.4f; // нитро защищает корпус при таране
                run.AddNitro(15f); // бонус заряда за агрессивный таран
            }

            run.TakeDamage(damage);
        }
    }
}
