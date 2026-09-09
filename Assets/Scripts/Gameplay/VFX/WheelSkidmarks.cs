using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Эффект следов протектора шин (Skidmarks) на асфальте.
    /// Активируется при боковом заносе/дрифте, резком торможении или ускорении с нитро.
    /// </summary>
    public sealed class WheelSkidmarks : MonoBehaviour
    {
        [Header("Tire Positions")]
        [SerializeField] private Transform[] wheelTransforms;

        [Header("Drift & Slip Thresholds")]
        [SerializeField, Min(0.5f)] private float slipThreshold = 3.2f;
        [SerializeField, Range(0.05f, 0.5f)] private float skidmarkWidth = 0.32f;
        [SerializeField] private Color skidmarkColor = new Color(0.08f, 0.08f, 0.09f, 0.85f);

        ArcadeCarController car;
        Rigidbody carBody;
        TrailRenderer[] skidTrails;

        private void Awake()
        {
            car = GetComponent<ArcadeCarController>();
            carBody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            SetupSkidTrails();
        }

        void SetupSkidTrails()
        {
            if (wheelTransforms == null || wheelTransforms.Length == 0)
            {
                // Если колеса не заданы явно, генерируем базовые точки под задними колесами
                Transform rl = new GameObject("SkidPoint_RL").transform;
                rl.SetParent(transform, false);
                rl.localPosition = new Vector3(-0.95f, 0.05f, -1.2f);

                Transform rr = new GameObject("SkidPoint_RR").transform;
                rr.SetParent(transform, false);
                rr.localPosition = new Vector3(0.95f, 0.05f, -1.2f);

                wheelTransforms = new Transform[] { rl, rr };
            }

            skidTrails = new TrailRenderer[wheelTransforms.Length];
            Material skidMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            skidMat.color = skidmarkColor;

            for (int i = 0; i < wheelTransforms.Length; i++)
            {
                GameObject trailObj = new GameObject($"SkidTrail_{i}");
                trailObj.transform.SetParent(wheelTransforms[i], false);
                trailObj.transform.localPosition = Vector3.zero;

                TrailRenderer tr = trailObj.AddComponent<TrailRenderer>();
                tr.time = 4.5f; // След держится 4.5 секунды
                tr.startWidth = skidmarkWidth;
                tr.endWidth = skidmarkWidth * 0.9f;
                tr.sharedMaterial = skidMat;
                tr.emitting = false;
                tr.autodestruct = false;
                tr.alignment = LineAlignment.TransformZ;
                tr.minVertexDistance = 0.25f;

                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(skidmarkColor, 0f), new GradientColorKey(skidmarkColor, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) }
                );
                tr.colorGradient = grad;

                skidTrails[i] = tr;
            }
        }

        private void Update()
        {
            if (carBody == null || skidTrails == null) return;

            // Вычисляем боковую скорость сноса машины (дрифт)
            Vector3 localVel = transform.InverseTransformDirection(carBody.linearVelocity);
            float lateralSlip = Mathf.Abs(localVel.x);

            bool isDrifting = lateralSlip > slipThreshold;
            bool isNitroSkid = car != null && car.IsNitroActive && car.SpeedKmh > 10f;
            bool shouldEmit = isDrifting || isNitroSkid;

            for (int i = 0; i < skidTrails.Length; i++)
            {
                if (skidTrails[i] != null)
                {
                    skidTrails[i].emitting = shouldEmit;
                }
            }
        }
    }
}
