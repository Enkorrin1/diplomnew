using System;
using System.Collections.Generic;
using UnityEngine;
using RogueDrive.Gameplay.VFX;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Физический контроллер управления автомобилем при интерактивном выезде из Бункера 07.
    /// Реализует полноценную физику подвески (4-точечные независимые амортизаторы на лучах),
    /// непрерывное вращение и руление колес (как штатных, так и прокачанных tier-колес),
    /// сцепление с дорогой и динамический крен шасси при разгоне, торможении и поворотах.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GarageDriveOutVehicle : MonoBehaviour
    {
        [Header("Vehicle Dynamics")]
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private float maxSpeed = 22f;
        [SerializeField] private float reverseSpeed = 8f;
        [SerializeField] private float turnSpeed = 65f;
        [SerializeField] private float brakeForce = 25f;
        [SerializeField] private float lateralGrip = 14f;

        [Header("Suspension Physics")]
        [SerializeField] private float suspensionRestLength = 0.35f;
        [SerializeField] private float suspensionTravel = 0.18f;
        [SerializeField] private float springStiffness = 34000f; // N/m
        [SerializeField] private float damperStiffness = 2900f;  // Ns/m
        [SerializeField] private float wheelRadius = 0.38f;
        [SerializeField] private LayerMask groundLayerMask = ~0;

        [Header("Wheel Transforms & Steering")]
        [SerializeField] private Transform[] frontWheels;
        [SerializeField] private Transform[] rearWheels;
        [SerializeField] private float maxSteerAngle = 28f;

        [Header("Audio & Effects")]
        [SerializeField] private AudioSource engineAudio;
        [SerializeField] private ParticleSystem exhaustParticles;
        [SerializeField] private Light[] headlights;

        [Header("Chassis Tilt Dynamics")]
        [SerializeField] private Transform visualBody;
        [SerializeField] private float bodyRollTilt = 3.5f;
        [SerializeField] private float bodyPitchTilt = 2.5f;

        private Rigidbody rb;
        private float currentSteer;
        private float accumulatedSpin;
        private bool isDrivingEnabled = false;

        public float SpeedMps { get; private set; }
        public float SpeedKmh => SpeedMps * 3.6f;
        public float SteeringAngle => currentSteer;
        public bool IsDrivingEnabled => isDrivingEnabled;
        public bool IsGrounded { get; private set; }
        public float ThrottleInput { get; private set; }
        public float SimulatedThrottle { get; set; } = 0f;
        public float SimulatedSteer { get; set; } = 0f;
        public bool SimulatedHandbrake { get; set; }
        public bool IsHandbrakeActive { get; private set; }
        public bool CinematicControl { get; set; }
        public float CinematicRevs { get; set; }
        public float CinematicWheelSlip { get; set; }
        // Network authority supplies input explicitly; never mix it with host keyboard input.
        public bool ExternalInput { get; set; }
        public void SetExternalInput(float throttle, float steer, bool handbrake)
        {
            SimulatedThrottle = Mathf.Clamp(throttle, -1f, 1f);
            SimulatedSteer = Mathf.Clamp(steer, -1f, 1f);
            SimulatedHandbrake = handbrake;
        }
        public void SetCinematicSpeed(float speed) => SpeedMps = speed;

        private sealed class WheelInfo
        {
            public Transform transform;
            public bool isFront;
            public bool isLeft;
            public Vector3 localMountPos;
            public Vector3 baseLocalPos;
            public Vector3 meshCenter;
            public float radius;
            public float spin;
            public Quaternion baseLocalRot;
            public float currentCompression;
            public float currentSuspensionY;
            public bool isGrounded;
            public RaycastHit hit;
        }

        private readonly List<WheelInfo> suspensionWheels = new List<WheelInfo>();
        private CarWheelUpgradeVisuals wheelUpgrades;
        private CarSuspensionUpgradeVisuals suspensionUpgrades;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = 1200f;
                rb.linearDamping = 0.45f;
                rb.angularDamping = 2.5f;
                // The follow camera samples the rendered pose in LateUpdate.
                // Smooth the 50 Hz physics steps for both garage and road driving.
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.constraints = RigidbodyConstraints.None;
                rb.centerOfMass = new Vector3(0f, 0.35f, 0f);
            }

            wheelUpgrades = GetComponent<CarWheelUpgradeVisuals>();
            suspensionUpgrades = GetComponent<CarSuspensionUpgradeVisuals>();

            SetupWheelData();
            EnsureEngineAudio();
            EnsureHeadlights();
            FindVisualBody();
        }

        public void EnableDriving()
        {
            isDrivingEnabled = true;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.constraints = RigidbodyConstraints.None;
                rb.centerOfMass = new Vector3(0f, 0.35f, 0f);
            }

            if (headlights != null)
            {
                foreach (var light in headlights)
                {
                    if (light != null)
                    {
                        light.enabled = true;
                        light.intensity = 2.5f;
                    }
                }
            }

            if (engineAudio != null && !engineAudio.isPlaying)
            {
                engineAudio.Play();
            }

            if (exhaustParticles != null)
            {
                exhaustParticles.Play();
            }
        }

        private void Update()
        {
            if (!isDrivingEnabled) return;

            float dt = Time.deltaTime;

            // Чтение клавиш управления
            float v = CinematicControl ? 0f : ExternalInput ? SimulatedThrottle : Input.GetAxisRaw("Vertical");
            float h = CinematicControl ? 0f : ExternalInput ? SimulatedSteer : Input.GetAxisRaw("Horizontal");

            if (!CinematicControl && !ExternalInput && Mathf.Abs(v) < 0.01f)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            }
            if (!CinematicControl && !ExternalInput && Mathf.Abs(h) < 0.01f)
            {
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            }

            if (!CinematicControl && Mathf.Abs(h) < 0.01f) h = SimulatedSteer;
            h = Mathf.Clamp(h, -1f, 1f);

            // Плавное руление
            float targetSteer = h * maxSteerAngle;
            currentSteer = Mathf.Lerp(currentSteer, targetSteer, dt * 12f);

            // Непрерывный расчет вращения колес от линейной скорости
            float speed = SpeedMps;
            float rollDelta = (speed * dt / wheelRadius) * Mathf.Rad2Deg;
            accumulatedSpin = (accumulatedSpin + rollDelta) % 360f;

            // Звук двигателя
            if (engineAudio != null)
            {
                float targetPitch = Mathf.Lerp(0.85f, 1.65f, Mathf.Abs(SpeedMps) / maxSpeed);
                if (CinematicControl) targetPitch = Mathf.Lerp(0.85f, 1.9f, CinematicRevs);
                engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, targetPitch, dt * 6f);
            }

            // Динамический наклон кузова при маневрах
            UpdateChassisTilt(dt, h, v);
        }

        private void FixedUpdate()
        {
            if (!isDrivingEnabled || rb == null) return;

            if (!CinematicControl) SpeedMps = Vector3.Dot(rb.linearVelocity, transform.forward);

            // 1. Моделирование 4-точечной подвески на лучах
            int groundedCount = 0;
            Vector3 up = transform.up;

            for (int i = 0; i < suspensionWheels.Count; i++)
            {
                var w = suspensionWheels[i];
                Vector3 mountPos = transform.TransformPoint(w.localMountPos);
                Vector3 rayOrigin = mountPos + up * (suspensionRestLength * 0.5f);
                var serviceStats = GetComponent<VehicleModularState>()?.Workshop?.Stats ?? WorkshopStats.Standard;
                float restLength = suspensionRestLength + serviceStats.clearance;
                float rayLength = suspensionRestLength * 0.5f + restLength + w.radius + suspensionTravel;

                RaycastHit nearest = default;
                float minDistance = float.MaxValue;
                bool hasHit = false;

                RaycastHit[] hits = Physics.RaycastAll(rayOrigin, -up, rayLength, groundLayerMask, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < hits.Length; j++)
                {
                    var hit = hits[j];
                    if (hit.collider.transform.IsChildOf(transform)) continue;
                    if (Vector3.Dot(hit.normal, up) < 0.25f) continue;
                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        nearest = hit;
                        hasHit = true;
                    }
                }

                if (hasHit)
                {
                    w.isGrounded = true;
                    w.hit = nearest;
                    groundedCount++;

                    float groundDist = nearest.distance - (suspensionRestLength * 0.5f);
                    float targetDist = restLength + w.radius;
                    float compression = Mathf.Clamp(targetDist - groundDist, -suspensionTravel, suspensionTravel);
                    w.currentCompression = compression;

                    Vector3 groundVel = nearest.rigidbody != null ? nearest.rigidbody.GetPointVelocity(nearest.point) : Vector3.zero;
                    Vector3 mountVel = rb.GetPointVelocity(mountPos);
                    float compVel = Vector3.Dot(mountVel - groundVel, up);

                    float preload = rb.mass * Physics.gravity.magnitude / suspensionWheels.Count;
                    float springForce = preload + compression * springStiffness * Mathf.Max(.3f,serviceStats.spring);
                    float damperForce = compVel * damperStiffness * Mathf.Max(.3f,serviceStats.damping);
                    float totalForce = Mathf.Clamp(springForce - damperForce, 0f, rb.mass * 9.81f * 3.5f);

                    Vector3 forceDir = (nearest.normal + up).normalized;
                    if (!CinematicControl) rb.AddForceAtPosition(forceDir * totalForce, mountPos, ForceMode.Force);
                }
                else
                {
                    w.isGrounded = false;
                    w.currentCompression = -suspensionTravel;
                }
            }

            IsGrounded = groundedCount > 0;

            // 2. Тяга и управление при контакте с землей
            if (!CinematicControl) ApplyDriveAndHandling();

            // Spring preload supports the weight; no artificial downward load at rest.
        }

        private void ApplyDriveAndHandling()
        {
            float v = ExternalInput ? SimulatedThrottle : Input.GetAxisRaw("Vertical");
            float h = ExternalInput ? SimulatedSteer : Input.GetAxisRaw("Horizontal");

            if (!ExternalInput && Mathf.Abs(v) < 0.01f)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
                if (Mathf.Abs(SimulatedThrottle) > 0.01f) v += SimulatedThrottle;
            }
            if (!ExternalInput && Mathf.Abs(h) < 0.01f)
            {
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
                if (Mathf.Abs(SimulatedSteer) > 0.01f) h += SimulatedSteer;
            }

            v = Mathf.Clamp(v, -1f, 1f);
            h = Mathf.Clamp(h, -1f, 1f);

            var roadCar = GetComponent<ArcadeCarController>();
            var run = roadCar != null && roadCar.enabled ? roadCar.Run : null;
            var modules = GetComponent<VehicleModularState>();
            var workshop=modules!=null?modules.Workshop:null;
            bool serviceBlocked=(workshop!=null&&workshop.Busy)||(RogueDrive.UI.VehicleDashboardPanelsUI.Instance?.IsAnyPanelOpen??false);
            if(serviceBlocked){v=0;h=0;}
            float servicePower=workshop!=null?Mathf.Max(.25f,workshop.Stats.power)*1200f/(1200f+workshop.Stats.mass):1;
            servicePower*=modules!=null?modules.EnginePowerMultiplier:1;
            bool engineAvailable = (modules == null || modules.CanProvidePower)
                && (run == null || (!run.IsGameOver && !run.IsOutOfFuel));
            // Braking remains available even when the engine has no fuel.
            if (!engineAvailable && !(Mathf.Abs(SpeedMps)>.3f && v*SpeedMps<0)) v = 0f;
            IsHandbrakeActive = !serviceBlocked && (SimulatedHandbrake || (!ExternalInput && (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton1))));
            if (IsHandbrakeActive) v = 0f;
            ThrottleInput = v;

            if (IsGrounded)
            {
                // Продольное ускорение
                if (IsHandbrakeActive || (v < -0.05f && SpeedMps > 0.3f) || (v > 0.05f && SpeedMps < -0.3f))
                {
                    float deceleration = Mathf.Min(brakeForce, Mathf.Abs(SpeedMps) / Time.fixedDeltaTime);
                    rb.AddForce(-transform.forward * (Mathf.Sign(SpeedMps) * deceleration), ForceMode.Acceleration);
                }
                else if (v > 0.05f)
                {
                    if (SpeedMps < maxSpeed)
                    {
                        rb.AddForce(transform.forward * (v * acceleration * servicePower * rb.mass), ForceMode.Force);
                    }
                }
                else if (v < -0.05f)
                {
                    if (SpeedMps > -reverseSpeed)
                    {
                        rb.AddForce(transform.forward * (v * (acceleration * servicePower * 0.65f) * rb.mass), ForceMode.Force);
                    }
                }
                else
                {
                    // Торможение накатом
                    Vector3 forwardVel = transform.forward * SpeedMps;
                    rb.AddForce(-forwardVel * (brakeForce * 15f), ForceMode.Force);
                }

                // Боковое сцепление шин (гашение бокового скольжения)
                Vector3 lateralVel = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
                rb.AddForce(-lateralVel * (lateralGrip * (IsHandbrakeActive && Mathf.Abs(SpeedMps) > 2f ? 0.25f : 1f) * (modules!=null?modules.GetAverageTireGrip():1) * rb.mass), ForceMode.Force);

                // Поворот автомобиля
                if (Mathf.Abs(h) > 0.05f && (Mathf.Abs(SpeedMps) > 0.2f || Mathf.Abs(v) > 0.05f))
                {
                    float turnDir = SpeedMps >= -0.1f ? 1f : -1f;
                    float turnAmount = h * turnSpeed * turnDir * Time.fixedDeltaTime;
                    Quaternion turnRot = Quaternion.Euler(0f, turnAmount, 0f);
                    rb.MoveRotation(rb.rotation * turnRot);
                }
            }
        }

        private void LateUpdate()
        {
            if (!isDrivingEnabled) return;

            bool hasUpgradedWheels = wheelUpgrades != null && wheelUpgrades.SuspensionWheelCount > 0;
            float dt = Time.deltaTime;

            for (int i = 0; i < suspensionWheels.Count; i++)
            {
                var w = suspensionWheels[i];
                if (w.transform == null) continue;

                // The spring and the mesh use the same hub center in chassis space.
                // Reproject from the interpolated pose so the tire stays on the road between physics ticks.
                float clearance = GetComponent<VehicleModularState>()?.Workshop?.Stats.clearance ?? 0f;
                Vector3 mount = transform.TransformPoint(w.localMountPos);
                float length = suspensionRestLength + clearance - w.currentCompression;
                if (w.isGrounded)
                {
                    float normalUp = Vector3.Dot(w.hit.normal, transform.up);
                    if (normalUp > 0.25f)
                        length = (Vector3.Dot(mount - w.hit.point, w.hit.normal) - w.radius) / normalUp;
                }
                length = Mathf.Clamp(length, Mathf.Max(0f, suspensionRestLength + clearance - suspensionTravel),
                    suspensionRestLength + clearance + suspensionTravel);
                Vector3 center = mount - transform.up * length;
                if (!hasUpgradedWheels)
                {
                    if (w.isFront || !IsHandbrakeActive)
                        w.spin = (w.spin + (SpeedMps + (!w.isFront ? CinematicWheelSlip : 0f)) * dt / w.radius * Mathf.Rad2Deg) % 360f;
                    Quaternion localRotation = Quaternion.Euler(0f, w.isFront ? currentSteer : 0f, 0f)
                        * Quaternion.Euler(w.spin, 0f, 0f) * w.baseLocalRot;
                    w.transform.localRotation = localRotation;
                    w.transform.position = center - w.transform.TransformVector(w.meshCenter);
                }
                else
                {
                    wheelUpgrades.SetSuspensionWheelCenter(i, center);
                }
            }
        }

        private void UpdateChassisTilt(float dt, float steerInput, float accelInput)
        {
            if (visualBody == null || visualBody == transform) return;

            // Крен кузова в повороте
            float targetRoll = -steerInput * bodyRollTilt * Mathf.Clamp01(Mathf.Abs(SpeedMps) / 6f);
            // Клевок при торможении и приседание при разгоне
            float targetPitch = accelInput * bodyPitchTilt * Mathf.Clamp01(1f - Mathf.Abs(SpeedMps) / maxSpeed);

            Quaternion targetRot = Quaternion.Euler(targetPitch, 0f, targetRoll);
            visualBody.localRotation = Quaternion.Slerp(visualBody.localRotation, targetRot, dt * 8f);
        }

        private void SetupWheelData()
        {
            suspensionWheels.Clear();

            Transform[] all = GetComponentsInChildren<Transform>(true);
            Transform fl = null, fr = null, rl = null, rr = null;

            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == transform) continue;
                string n = t.name.ToUpperInvariant();
                if (n.Contains("HOTSPOT")) continue;
                if ((n.Contains("TIRE") || n.Contains("WHEEL")) && !n.Equals("WHEELS") && !n.Contains("MOUNT"))
                {
                    if (fl == null && (n.Contains("FL") || n.Contains("FRONT_L") || n.Contains("FORWARD_L"))) fl = t;
                    else if (fr == null && (n.Contains("FR") || n.Contains("FRONT_R") || n.Contains("FORWARD_R"))) fr = t;
                    else if (rl == null && (n.Contains("RL") || n.Contains("REAR_L") || n.Contains("BACK_L") || n.Contains("BL"))) rl = t;
                    else if (rr == null && (n.Contains("RR") || n.Contains("REAR_R") || n.Contains("BACK_R") || n.Contains("BR"))) rr = t;
                }
            }

            AddWheelInfo(fl, true, true, new Vector3(-0.78f, 0.38f, 1.38f));
            AddWheelInfo(fr, true, false, new Vector3(0.78f, 0.38f, 1.38f));
            AddWheelInfo(rl, false, true, new Vector3(-0.78f, 0.38f, -1.35f));
            AddWheelInfo(rr, false, false, new Vector3(0.78f, 0.38f, -1.35f));

            var frontList = new List<Transform>();
            var rearList = new List<Transform>();
            if (fl != null) frontList.Add(fl);
            if (fr != null) frontList.Add(fr);
            if (rl != null) rearList.Add(rl);
            if (rr != null) rearList.Add(rr);

            frontWheels = frontList.ToArray();
            rearWheels = rearList.ToArray();
        }

        private void AddWheelInfo(Transform t, bool isFront, bool isLeft, Vector3 fallbackLocalPos)
        {
            Vector3 meshCenter = Vector3.zero;
            float radius = wheelRadius;
            var mesh = t != null ? t.GetComponent<MeshFilter>() : null;
            if (mesh != null && mesh.sharedMesh != null)
            {
                meshCenter = mesh.sharedMesh.bounds.center;
                radius = Mathf.Max(0.1f, t.TransformVector(Vector3.up * mesh.sharedMesh.bounds.extents.y).magnitude);
            }
            Vector3 center = t != null ? transform.InverseTransformPoint(t.TransformPoint(meshCenter)) : fallbackLocalPos;
            Vector3 mountPos = center + transform.InverseTransformVector(transform.up * suspensionRestLength);
            Vector3 basePos = t != null ? t.localPosition : fallbackLocalPos;
            Quaternion baseRot = t != null ? t.localRotation : Quaternion.identity;

            suspensionWheels.Add(new WheelInfo
            {
                transform = t,
                isFront = isFront,
                isLeft = isLeft,
                localMountPos = mountPos,
                meshCenter = meshCenter,
                radius = radius,
                baseLocalPos = basePos,
                baseLocalRot = baseRot,
                currentCompression = 0f,
                currentSuspensionY = basePos.y
            });
        }

        private void FindVisualBody()
        {
            if (visualBody != null) return;
            visualBody = transform.Find("Classic Car_9 Body") ??
                         transform.Find("VisualBody") ??
                         transform.Find("RealCarModel_3D");
        }

        private void EnsureEngineAudio()
        {
            if (engineAudio == null)
            {
                engineAudio = GetComponent<AudioSource>();
            }
            if (engineAudio == null)
            {
                engineAudio = gameObject.AddComponent<AudioSource>();
                engineAudio.playOnAwake = false;
                engineAudio.loop = true;
                engineAudio.spatialBlend = 0.5f;
                engineAudio.volume = 0.65f;
            }
        }

        private void EnsureHeadlights()
        {
            if (headlights == null || headlights.Length == 0)
            {
                headlights = GetComponentsInChildren<Light>();
            }
        }
    }
}
