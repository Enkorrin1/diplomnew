using UnityEngine;
using RogueDrive.Gameplay.VFX;
using RogueDrive.Modifiers;
using RogueDrive.Audio;
using RogueDrive.Meta;

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

        [Header("Run Controller Reference")]
        [SerializeField] private GameRunController run;

        Rigidbody body;
        SocketRegistry sockets;
        BoxCollider chassisCollider;
        Vector3 baseColliderCenter;
        Vector3 baseCenterOfMass;
        float baseLateralGrip;
        float baseDownforce;
        float baseBodyRollTilt;
        float baseLinearDamping;
        float currentLateralGrip;
        CarSuspensionUpgradeVisuals suspension;

        float throttleInput;
        float steerInput;
        bool isNitroRequested;
        bool isHandbrakeActive;
        bool isGrounded;
        float lastGroundedTime;
        Vector3 lastPosition;

        public float SpeedMps { get; private set; }
        public float SpeedKmh => SpeedMps * 3.6f;
        public float SteeringAngle => steerInput * 28f;
        public float TopSpeedMps => IsNitroActive ? nitroTopSpeedMps : topSpeedMps;
        public bool IsNitroActive { get; private set; }
        public bool IsHandbrakeActive => isHandbrakeActive;
        public bool IsGrounded => isGrounded;
        public SocketRegistry Sockets => sockets;
        public GameRunController Run => run;
        public float PickupRadius => activeStats != null && activeStats.Get(StatId.PickupRadius) > 0f ? activeStats.Get(StatId.PickupRadius) : 6.5f;

        StatBlock activeStats;

        public void Configure(GameRunController controller)
        {
            run = controller;
        }

        public void PlaceAtStart(Vector3 position, Quaternion rotation)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            body.position = position; body.rotation = rotation;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position,rotation); lastPosition = position;
        }

        public void BindStats(StatBlock stats)
        {
            activeStats = stats;
            if (stats != null && body != null)
            {
                float mass = stats.Get(StatId.Mass);
                if (mass > 100f)
                {
                    body.mass = mass;
                }
                RefreshUpgradeHandling();
            }
        }

        public void SetBodyColor(Color color)
        {
            if (visualBody == null)
                visualBody = transform.Find("VisualBody");

            if (visualBody != null)
            {
                Transform chassis = visualBody.Find("Chassis");
                if (chassis != null)
                {
                    Renderer r = chassis.GetComponent<Renderer>();
                    if (r != null)
                    {
                        r.material.color = color;
                    }
                }
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            sockets = GetComponent<SocketRegistry>();

            if (run == null)
            {
                run = FindFirstObjectByType<GameRunController>();
            }

            if (visualBody == null)
            {
                visualBody = transform.Find("VisualBody");
            }

            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.None;
            body.linearDamping = 0.35f;
            body.angularDamping = 2.5f;
            body.centerOfMass = new Vector3(0f, -0.2f, 0f);
            chassisCollider = GetComponent<BoxCollider>();
            baseColliderCenter = chassisCollider != null ? chassisCollider.center : Vector3.zero;
            baseCenterOfMass = body.centerOfMass;
            baseLateralGrip = lateralGrip;
            baseDownforce = downforce;
            baseBodyRollTilt = bodyRollTilt;
            baseLinearDamping = body.linearDamping;
            currentLateralGrip = lateralGrip;

            // Обеспечиваем скользящий аркадный физический контакт для основного коллайдера
            Collider rootCol = GetComponent<Collider>();
            if (rootCol != null)
            {
                PhysicsMaterial frictionlessMat = new PhysicsMaterial("ArcadeCarFrictionless")
                {
                    dynamicFriction = 0.05f,
                    staticFriction = 0.05f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounciness = 0f
                };
                rootCol.sharedMaterial = frictionlessMat;
            }

            // Интеграция реальной 3D-модели выбранного автомобиля
            ApplySelectedCarVisualModel();

            // Удаляем паразитные коллайдеры с декоративных дочерних деталей (колеса, бампер, кузов, турель),
            // чтобы они не тормозили автомобиль и не мешали лучам
            CleanChildColliders();

            // Автоматическое подключение компонентов VFX и аудио при старте
            if (GetComponent<WheelSkidmarks>() == null)
                gameObject.AddComponent<WheelSkidmarks>();

            if (GetComponent<CarExhaustVFX>() == null)
                gameObject.AddComponent<CarExhaustVFX>();

            if (GetComponent<CarWreckEffect>() == null)
                gameObject.AddComponent<CarWreckEffect>();

            if (GetComponent<CarVisualEnhancer>() == null)
                gameObject.AddComponent<CarVisualEnhancer>();

            if (GetComponent<CarSuspensionUpgradeVisuals>() == null)
                gameObject.AddComponent<CarSuspensionUpgradeVisuals>();
            suspension = GetComponent<CarSuspensionUpgradeVisuals>();

            if (GetComponent<RogueDrive.Gameplay.Combat.VehicleCombatSkills>() == null)
                gameObject.AddComponent<RogueDrive.Gameplay.Combat.VehicleCombatSkills>();

            if (AudioManager.Instance == null)
            {
                GameObject audioGo = new GameObject("AudioManager");
                audioGo.AddComponent<AudioManager>();
            }

            lastPosition = transform.position;
        }

        void RefreshUpgradeHandling()
        {
            float gripBonus = activeStats != null ? activeStats.Get(StatId.Grip) : 0f;
            float suspensionQuality = activeStats != null ? activeStats.Get(StatId.Suspension) : 0f;
            currentLateralGrip = Mathf.Clamp(baseLateralGrip + gripBonus, 0.55f, 0.98f);
            downforce = baseDownforce * (1f + suspensionQuality);
            bodyRollTilt = baseBodyRollTilt * Mathf.Clamp(1f - suspensionQuality * 0.65f, 0.55f, 1f);
            if (body != null)
                body.linearDamping = baseLinearDamping + suspensionQuality * 0.35f;
        }

        /// <summary>Настраивает реальный дорожный просвет и устойчивость шасси.</summary>
        public void ApplySuspensionUpgrade(int level, float clearance)
        {
            if (chassisCollider != null)
                chassisCollider.center = baseColliderCenter;

            if (body != null)
                body.centerOfMass = baseCenterOfMass - Vector3.up * Mathf.Min(0.12f, level * 0.02f);

            RefreshUpgradeHandling();
        }

        void CleanChildColliders()
        {
            Collider rootCol = GetComponent<Collider>();
            Collider[] allColliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != rootCol)
                {
                    allColliders[i].enabled = false;
                    if (Application.isPlaying)
                    {
                        Destroy(allColliders[i]);
                    }
                }
            }
        }

        private void ApplySelectedCarVisualModel()
        {
            // Resolve the selected car before applying either visual upgrade.
            CarDefinition selectedDef = null;
            GarageCatalog catalog = Resources.Load<GarageCatalog>("GarageCatalog");
#if UNITY_EDITOR
            if (catalog == null)
            {
                catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<GarageCatalog>("Assets/Content/GarageCatalog.asset");
            }
#endif
            if (catalog != null)
            {
                var meta = RogueDrive.Meta.SaveService.GetActiveProgress(catalog.Upgrades, catalog.Cars);
                selectedDef = meta?.SelectedCar;
                if (selectedDef == null && catalog.Cars != null && catalog.Cars.Count > 0)
                {
                    selectedDef = catalog.Cars[0];
                }
            }

            GameObject prefab = selectedDef != null ? selectedDef.EffectivePrefab : null;
            if (prefab != null)
            {
                // Скрываем старые примитивные детали кузова
                if (visualBody != null)
                {
                    Renderer[] oldRenderers = visualBody.GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < oldRenderers.Length; i++)
                    {
                        oldRenderers[i].enabled = false;
                    }
                }

                Transform existingModel = transform.Find("RealCarModel_3D");
                if (existingModel != null)
                {
                    existingModel.gameObject.SetActive(false);
                    existingModel.SetParent(null);
                    Destroy(existingModel.gameObject);
                }

                GameObject modelObj = Instantiate(prefab, transform);
                modelObj.name = "RealCarModel_3D";
                modelObj.transform.localPosition = Vector3.zero;
                modelObj.transform.localRotation = Quaternion.identity;

                // Переназначаем visualBody на реальную модель для красивого динамического крена при дрифте
                visualBody = modelObj.transform;

                // Точно позиционируем сокет крыши под модель
                Transform roofSocket = transform.Find("Socket_Roof");
                if (roofSocket != null)
                {
                    roofSocket.localPosition = new Vector3(0f, 1.25f, -0.1f);
                }

                var enhancer = GetComponent<RogueDrive.Gameplay.VFX.CarVisualEnhancer>();
                if (enhancer != null)
                {
                    enhancer.RefreshWheels();
                }

                // До старта заезда колесный визуал мог быть рассчитан по
                // временному кузову. После подстановки выбранной модели он
                // обязан заново взять позиции её штатных колёс.
                var wheels = GetComponent<CarWheelUpgradeVisuals>();
                if (wheels == null) wheels = gameObject.AddComponent<CarWheelUpgradeVisuals>();
                wheels.Configure(catalog.WheelUpgradePrefabs);
                wheels.RebuildForCurrentCarModel();
                GetComponent<CarVisualEnhancer>()?.RefreshWheels();

                var tuning = GetComponent<RogueDrive.Gameplay.VFX.CarVisualTuning>();
                if (tuning == null) tuning = gameObject.AddComponent<RogueDrive.Gameplay.VFX.CarVisualTuning>();
                var metaProgress = RogueDrive.Meta.SaveService.GetActiveProgress(catalog != null ? catalog.Upgrades : null, catalog != null ? catalog.Cars : null);
                tuning.RefreshTuning(selectedDef != null ? selectedDef.Id : "light", metaProgress);
            }
        }

        private void Update()
        {
            if (run != null && run.IsGameOver)
            {
                throttleInput = 0f;
                steerInput = 0f;
                isNitroRequested = false;
                IsNitroActive = false;
                return;
            }

            // Чтение осей InputManager
            float v = Input.GetAxisRaw("Vertical");
            float h = Input.GetAxisRaw("Horizontal");

            // Прямой опрос клавиш (гарантирует отклик в любых настройках проекта)
            if (Mathf.Abs(v) < 0.01f)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            }
            if (Mathf.Abs(h) < 0.01f)
            {
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            }

            throttleInput = Mathf.Clamp(v, -1f, 1f);
            steerInput = Mathf.Clamp(h, -1f, 1f);
            // Нитро: Shift, ПКМ (правая кнопка мыши), геймпад A / X / RB
            isNitroRequested = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                || Input.GetMouseButton(1)
                || Input.GetKey(KeyCode.JoystickButton0) || Input.GetKey(KeyCode.JoystickButton2) || Input.GetKey(KeyCode.JoystickButton5);
            // Ручной тормоз: Пробел, геймпад B / Circle
            isHandbrakeActive = Input.GetKey(KeyCode.Space)
                || Input.GetKey(KeyCode.JoystickButton1);

            // Ручной тормоз подавляет газ
            if (isHandbrakeActive)
            {
                throttleInput = 0f;
                isNitroRequested = false;
            }

            // Обработка расхода нитро
            bool hasFuel = run == null || !run.IsOutOfFuel;
            if (isNitroRequested && throttleInput > 0f && hasFuel)
            {
                IsNitroActive = run == null || run.TryConsumeNitro(nitroPerSecond * Time.deltaTime);
            }
            else
            {
                IsNitroActive = false;
            }

            // Расход топлива при нажатой педали газа (с учётом модификатора FuelDrain)
            if (run != null && throttleInput > 0f && !run.IsOutOfFuel)
            {
                float fuelDrainMult = (activeStats != null && activeStats.Get(StatId.FuelDrain) > 0f) ? (activeStats.Get(StatId.FuelDrain) / 1.8f) : 1.0f;
                float fuelCost = fuelPerSecond * fuelDrainMult * (IsNitroActive ? 1.5f : 1.0f) * Time.deltaTime;
                run.ConsumeFuel(fuelCost);
            }

            // Расчёт пройденной дистанции
            float distanceDelta = Vector3.Distance(transform.position, lastPosition);
            if (run != null && distanceDelta > 0f && Vector3.Dot(body.linearVelocity, transform.forward) > 0f)
            {
                run.ReportTravelled(distanceDelta);
            }
            lastPosition = transform.position;

            // Проверка остановки при пустом баке (инерционный накат окончен)
            if (run != null && run.IsOutOfFuel && Mathf.Abs(SpeedMps) < 0.25f)
            {
                run.ReportCarStopped();
            }

            // Визуальный крен кузова в поворотах
            UpdateVisualRoll(Time.deltaTime);

            // Обновление звука мотора и нитро
            AudioManager.Instance?.UpdateEngineSound(SpeedKmh, TopSpeedMps * 3.6f, IsNitroActive);
        }

        private void FixedUpdate()
        {
            isGrounded = suspension != null && suspension.Simulate(body);
            if (isGrounded) lastGroundedTime = Time.time;
            SpeedMps = Vector3.Dot(body.linearVelocity, transform.forward);

            // 1. Прижимная сила (Downforce) пропорциональна скорости
            float currentDownforce = downforce * (1f + Mathf.Abs(SpeedMps) / topSpeedMps);
            if (isGrounded) body.AddForce(Vector3.down * currentDownforce * 0.1f, ForceMode.Acceleration);

            bool canDrive = isGrounded || (Time.time - lastGroundedTime < 0.35f);

            if (canDrive)
            {
                // 2. Продольная тяга и торможение
                ApplyDriveForces();

                // 3. Руление (поворот машины вокруг оси Y)
                ApplySteering();

                // 4. Боковое сцепление (подавление бокового скольжения)
                ApplyLateralGrip();
            }
            else
            {
                // В воздухе: дополнительная стабилизация и возможность подруливания
                ApplySteeringAirborne();
            }
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

            float speedMult = (activeStats != null && activeStats.Get(StatId.Speed) > 0f) ? (activeStats.Get(StatId.Speed) / 28f) : 1.0f;
            float currentTopSpeed = (IsNitroActive ? nitroTopSpeedMps : topSpeedMps) * speedMult;
            float currentAccel = (IsNitroActive ? nitroAcceleration : acceleration) * Mathf.Max(0.7f, speedMult);

            // Ручной тормоз (Пробел)
            if (isHandbrakeActive)
            {
                if (Mathf.Abs(SpeedMps) > 0.3f)
                {
                    float hbForce = brakeForce * 1.6f;
                    body.AddForce(-transform.forward * (Mathf.Sign(SpeedMps) * hbForce), ForceMode.Acceleration);
                }
                return;
            }

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
                if (SpeedMps > 0.5f)
                {
                    // Тормоз при движении вперед
                    body.AddForce(-transform.forward * (brakeForce * Mathf.Abs(throttleInput)), ForceMode.Acceleration);
                }
                else if (SpeedMps > -reverseSpeedMps)
                {
                    // Задний ход
                    body.AddForce(-transform.forward * (acceleration * 0.7f * Mathf.Abs(throttleInput)), ForceMode.Acceleration);
                }
            }
        }

        void ApplySteering()
        {
            if (Mathf.Abs(steerInput) < 0.05f)
                return;

            // Heading changes require longitudinal motion; stopped wheels can
            // steer without rotating the chassis.
            if (Mathf.Abs(SpeedMps) < 0.1f)
                return;
            float speedRatio = Mathf.Clamp01(Mathf.Abs(SpeedMps) / 5f);
            float speedFactor = speedRatio;
            float handbrakeSteerMultiplier = isHandbrakeActive ? 1.35f : 1.0f;
            float turnAmount = steerInput * steerSpeed * Mathf.Clamp(PlayerPrefs.GetFloat("SteerSensitivity",1f),.5f,2f) * speedFactor * handbrakeSteerMultiplier * Time.fixedDeltaTime;

            // Инвертируем поворот при движении назад
            if (SpeedMps < 0f)
                turnAmount = -turnAmount;

            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            body.MoveRotation(body.rotation * turnRotation);
        }

        void ApplySteeringAirborne()
        {
            if (Mathf.Abs(steerInput) < 0.05f || Mathf.Abs(SpeedMps) < 0.1f)
                return;

            float turnAmount = steerInput * (steerSpeed * 0.45f) *
                Mathf.Clamp(SpeedMps / 5f, -1f, 1f) * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            body.MoveRotation(body.rotation * turnRotation);
        }

        void ApplyLateralGrip()
        {
            Vector3 right = transform.right;
            float lateralVelocity = Vector3.Dot(body.linearVelocity, right);
            // При активном ручном тормозе ослабляем боковое сцепление для эффектного входа в занос
            float grip = isHandbrakeActive ? (currentLateralGrip * 0.35f) : currentLateralGrip;
            Vector3 counterForce = -right * (lateralVelocity * grip);
            body.AddForce(counterForce, ForceMode.VelocityChange);
        }

        void CheckGrounded()
        {
            Vector3 origin = transform.position + Vector3.up * 0.4f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 1.8f, ~0, QueryTriggerInteraction.Ignore);
            bool hitGround = false;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != null && hits[i].collider.transform.root != transform.root)
                {
                    hitGround = true;
                    break;
                }
            }

            isGrounded = hitGround;
            if (isGrounded)
            {
                lastGroundedTime = Time.time;
            }
        }

        void UpdateVisualRoll(float dt)
        {
            if (suspension != null) return;
            if (visualBody == null)
                return;

            float targetRoll = -steerInput * bodyRollTilt * Mathf.Clamp01(Mathf.Abs(SpeedMps) / 5f);
            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetRoll);
            visualBody.localRotation = Quaternion.Slerp(visualBody.localRotation, targetRot, 1f - Mathf.Exp(-8f * dt));
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude / 12f;
            if (impact > 0.2f)
            {
                AudioManager.Instance?.PlayCrash(impact);
            }
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

            TrackObstacle obstacle = targetGo.GetComponentInParent<TrackObstacle>();
            if (obstacle == null || !obstacle.TryConsume())
                return;

            // Эффект удара: сотрясение камеры и звук скрежета
            AudioManager.Instance?.PlayCrash(0.9f);
            ArcadeCameraFollow.Instance?.TriggerShake(0.7f, 0.3f);
            // Ordinary road furniture produces a small impact, not the explosive ram effect.
            CombatVfxCatalog.Instance?.SpawnBulletHit(targetGo.transform.position, Quaternion.LookRotation(transform.forward));

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
