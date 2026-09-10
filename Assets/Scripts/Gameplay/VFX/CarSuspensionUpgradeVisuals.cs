using RogueDrive.Meta;
using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Применяет отдельную прокачку подвески. В гараже поднимает кузов относительно
    /// колёс; в заезде передаёт клиренс контроллеру физики и уменьшает крен.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class CarSuspensionUpgradeVisuals : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float clearancePerLevel = 0.06f;

        Vector3 baseLocalPosition;
        Transform visualBody;
        Vector3 baseVisualBodyPosition;
        bool initialized;
        [Header("Physical suspension")]
        [SerializeField, Min(0.05f)] float travel = 0.3f;
        [SerializeField, Min(0.5f)] float springFrequency = 2.2f;
        [SerializeField, Range(0.1f, 1.5f)] float dampingRatio = 0.7f;
        [SerializeField] LayerMask groundMask = ~0;
        float rideLift;
        int upgradeLevel;

        // Called by the controller before applying traction, in the same physics tick.
        public bool Simulate(Rigidbody body)
        {
            var wheels = GetComponent<CarWheelUpgradeVisuals>();
            if (wheels == null) return false;
            int count = wheels.SuspensionWheelCount;
            if (count == 0) return false;
            bool grounded = false;
            float wheelMass = body.mass / count;
            float omega = 2f * Mathf.PI * springFrequency;
            float stiffness = wheelMass * omega * omega;
            float damping = 2f * wheelMass * omega * Mathf.Min(1f, dampingRatio + upgradeLevel * 0.04f);
            float stroke = travel + upgradeLevel * 0.025f;
            Vector3 up = transform.up;
            for (int i = 0; i < count; i++)
            {
                if (!wheels.GetSuspensionWheel(i, out Vector3 center, out float radius)) continue;
                Vector3 origin = center + up * stroke;
                float desired = stroke + radius + rideLift;
                float rayLength = desired + stroke;
                RaycastHit nearest = default;
                float distance = float.PositiveInfinity;
                foreach (var hit in Physics.RaycastAll(origin, -up, rayLength, groundMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(transform) || Vector3.Dot(hit.normal, up) < 0.4f || hit.distance >= distance) continue;
                    nearest = hit;
                    distance = hit.distance;
                }
                if (float.IsPositiveInfinity(distance))
                {
                    wheels.SetSuspensionWheelCenter(i, center - up * (rideLift + stroke));
                    continue;
                }
                grounded = true;
                Vector3 groundVelocity = nearest.rigidbody != null ? nearest.rigidbody.GetPointVelocity(nearest.point) : Vector3.zero;
                float velocity = Vector3.Dot(body.GetPointVelocity(center) - groundVelocity, up);
                float preload = wheelMass * Physics.gravity.magnitude;
                float force = Mathf.Clamp(preload + stiffness * (desired - distance) - damping * velocity, 0f, preload * 8f);
                body.AddForceAtPosition(up * force, center, ForceMode.Force);
                if (nearest.rigidbody != null && !nearest.rigidbody.isKinematic)
                    nearest.rigidbody.AddForceAtPosition(-up * force, nearest.point, ForceMode.Force);
                wheels.SetSuspensionWheelCenter(i, origin - up * Mathf.Max(0f, distance - radius));
            }
            return grounded;
        }

        private void OnEnable()
        {
            SaveService.OnProgressUpdated += OnProgressUpdated;
            if (GetComponent<ArcadeCarController>() == null)
                RefreshForCurrentProgress();
        }

        private void Start() => RefreshForCurrentProgress();

        private void OnDisable()
        {
            SaveService.OnProgressUpdated -= OnProgressUpdated;
        }

        void OnProgressUpdated(MetaProgress _) => RefreshForCurrentProgress();

        public void RefreshForCurrentProgress()
        {
            int level = SaveService.GetActiveProgress().Data.GetUpgradeLevel("suspension");
            ApplyLevel(level);
        }

        public void ApplyLevel(int level)
        {
            if (!initialized)
            {
                baseLocalPosition = transform.localPosition;
                visualBody = transform.Find("RealCarModel_3D") ?? transform.Find("VisualBody");
                if (visualBody != null)
                    baseVisualBodyPosition = visualBody.localPosition;
                initialized = true;
            }

            float clearance = Mathf.Max(0, level) * clearancePerLevel;
            CarWheelUpgradeVisuals wheels = GetComponent<CarWheelUpgradeVisuals>();
            ArcadeCarController controller = GetComponent<ArcadeCarController>();

            if (controller != null)
            {
                rideLift = clearance;
                upgradeLevel = Mathf.Max(0, level);
                controller.ApplySuspensionUpgrade(level, clearance);
                if (visualBody != null)
                    visualBody.localPosition = baseVisualBodyPosition;
                // Кузов поднимается, а колёса компенсируют его смещение и
                // остаются на уровне дороги.
                wheels?.SetMountHeightOffset(0f);
            }
            else
            {
                // Preview-модель целиком поднимается, а WheelMounts компенсируют сдвиг.
                transform.localPosition = baseLocalPosition + Vector3.up * clearance;
                wheels?.SetMountHeightOffset(-clearance);
            }
        }
    }
}
