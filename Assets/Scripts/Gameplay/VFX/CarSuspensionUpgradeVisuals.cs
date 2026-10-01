using RogueDrive.Meta;
using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Аркадная лучевая подвеска и прокачка клиренса.
    /// В заезде каждое колесо — независимая пружина с демпфером (raycast-suspension):
    /// сила = преднатяг + жёсткость × сжатие − демпфирование × скорость хода,
    /// плюс стабилизатор поперечной устойчивости между колёсами одной оси и отбойник
    /// на пробое. В гараже просто поднимает кузов относительно колёс.
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
        [SerializeField, Min(0.05f)] float travel = 0.3f;                 // рабочий ход пружины, м
        [SerializeField, Min(0.5f)] float springFrequency = 2.2f;         // собственная частота, Гц (1.8–2.6 — аркада)
        [SerializeField, Range(0.1f, 1.5f)] float dampingRatio = 0.7f;     // коэффициент демпфирования (0.6–0.8 — без раскачки)
        [SerializeField, Range(0f, 1.5f)] float antiRollStiffness = 0.6f;  // стабилизатор, доля жёсткости пружины
        [SerializeField, Min(1f)] float bumpStopStiffness = 6f;            // кратность жёсткости на отбойнике
        [SerializeField, Min(0.05f)] float groundClearance = 0.22f;        // просвет днища (коллайдера) над дорогой, м
        [SerializeField] LayerMask groundMask = ~0;
        float rideLift;
        int upgradeLevel;

        // Буферы на кадр физики (без аллокаций)
        Vector3[] anchors = new Vector3[0];
        float[] compressions = new float[0];
        float[] strokeVelocities = new float[0];
        float[] distances = new float[0];
        float[] radii = new float[0];
        bool[] contacts = new bool[0];
        bool[] hasWheelVisual = new bool[0];
        Rigidbody[] groundBodies = new Rigidbody[0];
        Vector3[] groundPoints = new Vector3[0];
        int[] pairIndices = new int[0];

        private readonly System.Collections.Generic.List<Vector3> fallbackWheelCenters = new System.Collections.Generic.List<Vector3>();

        /// <summary>Просвет днища над дорогой, который контроллер обязан обеспечить коллайдеру шасси.</summary>
        public float GroundClearance => groundClearance;

        /// <summary>Рабочий ход пружины с учётом уровня прокачки.</summary>
        public float Stroke => travel + upgradeLevel * 0.025f;

        private void EnsureFallbackWheelCenters()
        {
            if (fallbackWheelCenters.Count > 0) return;
            Transform[] candidates = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                string name = candidates[i].name.ToUpperInvariant();
                // Hotspot_Tire_* — диегетические триггеры осмотра шин, а не колёса
                if (name.Contains("HOTSPOT")) continue;
                if ((name.Contains("TIRE") || name.Contains("WHEEL")) && !name.Equals("WHEELS") && !name.Contains("MOUNT"))
                {
                    if (name.Contains("FL") || name.Contains("FR") || name.Contains("RL") || name.Contains("RR"))
                    {
                        fallbackWheelCenters.Add(transform.InverseTransformPoint(candidates[i].position));
                    }
                }
            }
            if (fallbackWheelCenters.Count == 0)
            {
                fallbackWheelCenters.Add(new Vector3(-0.78f, 0.38f, 1.38f));
                fallbackWheelCenters.Add(new Vector3(0.78f, 0.38f, 1.38f));
                fallbackWheelCenters.Add(new Vector3(-0.78f, 0.38f, -1.35f));
                fallbackWheelCenters.Add(new Vector3(0.78f, 0.38f, -1.35f));
            }
        }

        int ResolveWheelCount(out CarWheelUpgradeVisuals wheels)
        {
            wheels = GetComponent<CarWheelUpgradeVisuals>();
            int count = wheels != null ? wheels.SuspensionWheelCount : 0;
            if (count == 0)
            {
                EnsureFallbackWheelCenters();
                count = fallbackWheelCenters.Count;
            }
            return count;
        }

        bool ResolveWheel(CarWheelUpgradeVisuals wheels, int index, out Vector3 center, out float radius)
        {
            center = Vector3.zero;
            radius = 0.35f;
            bool hasWheel = wheels != null && wheels.GetSuspensionWheel(index, out center, out radius);
            if (!hasWheel)
            {
                EnsureFallbackWheelCenters();
                if (index >= fallbackWheelCenters.Count) return false;
                center = transform.TransformPoint(fallbackWheelCenters[index]);
                radius = 0.35f;
            }
            return hasWheel;
        }

        /// <summary>
        /// Геометрия колёс в локальных координатах кузова: самая низкая ось и средний радиус.
        /// Нужна контроллеру для просвета коллайдера и высоты центра масс.
        /// </summary>
        public bool TryGetWheelGeometry(out float lowestWheelCenterLocalY, out float averageRadius)
        {
            lowestWheelCenterLocalY = 0.35f;
            averageRadius = 0.35f;
            int count = ResolveWheelCount(out CarWheelUpgradeVisuals wheels);
            if (count == 0) return false;

            float minY = float.PositiveInfinity;
            float radiusSum = 0f;
            int resolved = 0;
            for (int i = 0; i < count; i++)
            {
                ResolveWheel(wheels, i, out Vector3 center, out float radius);
                float localY = transform.InverseTransformPoint(center).y;
                if (localY < minY) minY = localY;
                radiusSum += radius;
                resolved++;
            }

            if (resolved == 0) return false;
            lowestWheelCenterLocalY = minY;
            averageRadius = radiusSum / resolved;
            return true;
        }

        void EnsureBuffers(int count)
        {
            if (anchors.Length == count) return;
            anchors = new Vector3[count];
            compressions = new float[count];
            strokeVelocities = new float[count];
            distances = new float[count];
            radii = new float[count];
            contacts = new bool[count];
            hasWheelVisual = new bool[count];
            groundBodies = new Rigidbody[count];
            groundPoints = new Vector3[count];
            pairIndices = new int[count];
        }

        // Колесо-пара для стабилизатора: противоположная сторона той же оси
        void ResolvePairs(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 li = transform.InverseTransformPoint(anchors[i]);
                int best = -1;
                float bestDz = float.PositiveInfinity;
                for (int j = 0; j < count; j++)
                {
                    if (j == i) continue;
                    Vector3 lj = transform.InverseTransformPoint(anchors[j]);
                    if (Mathf.Sign(lj.x) == Mathf.Sign(li.x) || Mathf.Abs(lj.x) < 0.05f) continue;
                    float dz = Mathf.Abs(lj.z - li.z);
                    if (dz < bestDz)
                    {
                        bestDz = dz;
                        best = j;
                    }
                }
                pairIndices[i] = bestDz < 0.6f ? best : -1;
            }
        }

        // Вызывается контроллером в FixedUpdate до тяги и сцепления.
        public bool Simulate(Rigidbody body)
        {
            int count = ResolveWheelCount(out CarWheelUpgradeVisuals wheels);
            if (count == 0) return false;
            EnsureBuffers(count);

            float wheelMass = body.mass / count;
            float omega = 2f * Mathf.PI * springFrequency;
            float stiffness = wheelMass * omega * omega;
            float damping = 2f * wheelMass * omega * Mathf.Min(1f, dampingRatio + upgradeLevel * 0.04f);
            float stroke = Stroke;
            float preload = wheelMass * Physics.gravity.magnitude;
            Vector3 up = transform.up;
            bool grounded = false;

            // Проход 1: лучи от каждой ступицы вниз
            for (int i = 0; i < count; i++)
            {
                hasWheelVisual[i] = ResolveWheel(wheels, i, out Vector3 center, out float radius);
                anchors[i] = center;
                radii[i] = radius;
                contacts[i] = false;

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
                    // В воздухе: колесо вывешивается вниз на часть хода
                    if (hasWheelVisual[i]) wheels.SetSuspensionWheelCenter(i, center - up * (rideLift + stroke * 0.5f));
                    continue;
                }

                grounded = true;
                contacts[i] = true;
                distances[i] = distance;
                compressions[i] = desired - distance; // > 0 — сжатие, < 0 — отбой
                Vector3 groundVelocity = nearest.rigidbody != null ? nearest.rigidbody.GetPointVelocity(nearest.point) : Vector3.zero;
                strokeVelocities[i] = Vector3.Dot(body.GetPointVelocity(center) - groundVelocity, up);
                groundBodies[i] = nearest.rigidbody;
                groundPoints[i] = nearest.point;
            }

            if (!grounded) return false;
            ResolvePairs(count);

            // Проход 2: силы пружин, стабилизатора и отбойников
            for (int i = 0; i < count; i++)
            {
                if (!contacts[i]) continue;

                float compression = compressions[i];
                float force = preload + stiffness * compression - damping * strokeVelocities[i];

                // Стабилизатор: перераспределяет нагрузку на более сжатую сторону оси
                int pair = pairIndices[i];
                if (pair >= 0 && contacts[pair])
                {
                    force += (compression - compressions[pair]) * stiffness * antiRollStiffness;
                }

                // Отбойник: пробой хода даёт резко нарастающее сопротивление
                float overTravel = compression - stroke;
                if (overTravel > 0f)
                {
                    force += stiffness * bumpStopStiffness * overTravel;
                }

                force = Mathf.Clamp(force, 0f, preload * 10f);
                Vector3 center = anchors[i];
                body.AddForceAtPosition(up * force, center, ForceMode.Force);
                if (groundBodies[i] != null && !groundBodies[i].isKinematic)
                    groundBodies[i].AddForceAtPosition(-up * force, groundPoints[i], ForceMode.Force);

                if (hasWheelVisual[i])
                    wheels.SetSuspensionWheelCenter(i, center + up * stroke - up * Mathf.Max(0f, distances[i] - radii[i]));
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

        public void RefreshForCurrentProgress(string carId = null)
        {
            var data = SaveService.GetActiveProgress().Data;
            string targetCarId = !string.IsNullOrEmpty(carId) ? carId : (!string.IsNullOrEmpty(data.SelectedCarId) ? data.SelectedCarId : "light");
            int level = data.GetUpgradeLevel(targetCarId, "suspension");
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
                // Кузов поднимается физикой (rideLift), а колёса остаются на уровне дороги.
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
