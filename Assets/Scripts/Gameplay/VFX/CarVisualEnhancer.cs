using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Визуальный модуль полировки автомобиля:
    /// 1. Анимация вращения колес соразмерно скорости движения.
    /// 2. Поворот передних колес при рулении.
    /// 3. Передние фары (Headlights) со светом и конусами.
    /// 4. Стоп-сигналы (Brake Lights), вспыхивающие при торможении.
    /// 5. Динамический крен кузова (Body Roll) при резких маневрах.
    /// </summary>
    [RequireComponent(typeof(ArcadeCarController))]
    public sealed class CarVisualEnhancer : MonoBehaviour
    {
        [Header("Wheel Transforms")]
        [SerializeField] private Transform wheelFL;
        [SerializeField] private Transform wheelFR;
        [SerializeField] private Transform wheelRL;
        [SerializeField] private Transform wheelRR;

        [Header("Lights")]
        [SerializeField] private Light headlightLeft;
        [SerializeField] private Light headlightRight;
        [SerializeField] private Renderer brakeLightLeftRenderer;
        [SerializeField] private Renderer brakeLightRightRenderer;

        private ArcadeCarController car;
        private Rigidbody body;
        private Transform visualBody;

        private float wheelRotationDeg = 0f;
        private const float WheelRadius = 0.35f;

        private Material brakeLightActiveMat;
        private Material brakeLightIdleMat;

        private void Awake()
        {
            car = GetComponent<ArcadeCarController>();
            body = GetComponent<Rigidbody>();
            visualBody = transform.Find("VisualBody");

            SetupWheelsIfMissing();
            SetupLights();
        }

        private void SetupWheelsIfMissing()
        {
            Transform wheelsRoot = transform.Find("Wheels");
            if (wheelsRoot == null)
            {
                wheelsRoot = new GameObject("Wheels").transform;
                wheelsRoot.SetParent(transform, false);

                // Создаем 4 процедурных колеса с черными шинами и металлическими дисками
                wheelFL = CreateProceduralWheel("Wheel_FL", wheelsRoot, new Vector3(-0.95f, 0.35f, 1.25f));
                wheelFR = CreateProceduralWheel("Wheel_FR", wheelsRoot, new Vector3(0.95f, 0.35f, 1.25f));
                wheelRL = CreateProceduralWheel("Wheel_RL", wheelsRoot, new Vector3(-0.95f, 0.35f, -1.25f));
                wheelRR = CreateProceduralWheel("Wheel_RR", wheelsRoot, new Vector3(0.95f, 0.35f, -1.25f));
            }
            else
            {
                if (wheelFL == null) wheelFL = wheelsRoot.Find("Wheel_FL");
                if (wheelFR == null) wheelFR = wheelsRoot.Find("Wheel_FR");
                if (wheelRL == null) wheelRL = wheelsRoot.Find("Wheel_RL");
                if (wheelRR == null) wheelRR = wheelsRoot.Find("Wheel_RR");
            }
        }

        private Transform CreateProceduralWheel(string name, Transform parent, Vector3 localPos)
        {
            GameObject wheelObj = new GameObject(name);
            wheelObj.transform.SetParent(parent, false);
            wheelObj.transform.localPosition = localPos;

            // Шина (Cylinder, ориентированный по X)
            GameObject tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tire.name = "Tire";
            tire.transform.SetParent(wheelObj.transform, false);
            tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            tire.transform.localScale = new Vector3(WheelRadius * 2f, 0.22f, WheelRadius * 2f);

            // Удаляем паразитный коллайдер, чтобы не мешать физике
            Collider col = tire.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = tire.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.12f, 0.12f, 0.13f);
                m.SetFloat("_Glossiness", 0.3f);
                r.sharedMaterial = m;
            }

            // Диск колеса (контрастный обод)
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Rim";
            rim.transform.SetParent(wheelObj.transform, false);
            rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rim.transform.localScale = new Vector3(WheelRadius * 1.2f, 0.24f, WheelRadius * 1.2f);

            Collider rimCol = rim.GetComponent<Collider>();
            if (rimCol != null) Destroy(rimCol);

            Renderer rimR = rim.GetComponent<Renderer>();
            if (rimR != null)
            {
                Material rm = new Material(Shader.Find("Standard"));
                rm.color = new Color(0.75f, 0.75f, 0.8f);
                rm.SetFloat("_Metallic", 0.8f);
                rimR.sharedMaterial = rm;
            }

            return wheelObj.transform;
        }

        private void SetupLights()
        {
            Transform lightsRoot = transform.Find("CarLights");
            if (lightsRoot == null)
            {
                lightsRoot = new GameObject("CarLights").transform;
                lightsRoot.SetParent(transform, false);

                // 1. Передние фары (Headlights)
                headlightLeft = CreateHeadlight("Headlight_L", lightsRoot, new Vector3(-0.65f, 0.55f, 1.85f));
                headlightRight = CreateHeadlight("Headlight_R", lightsRoot, new Vector3(0.65f, 0.55f, 1.85f));

                // 2. Стоп-сигналы (Brake lights)
                brakeLightIdleMat = new Material(Shader.Find("Standard"));
                brakeLightIdleMat.color = new Color(0.45f, 0.05f, 0.05f);

                brakeLightActiveMat = new Material(Shader.Find("Standard"));
                brakeLightActiveMat.color = Color.red;
                brakeLightActiveMat.EnableKeyword("_EMISSION");
                brakeLightActiveMat.SetColor("_EmissionColor", Color.red * 2.2f);

                brakeLightLeftRenderer = CreateBrakeLightMesh("BrakeLight_L", lightsRoot, new Vector3(-0.65f, 0.65f, -1.85f));
                brakeLightRightRenderer = CreateBrakeLightMesh("BrakeLight_R", lightsRoot, new Vector3(0.65f, 0.65f, -1.85f));
            }
        }

        private Light CreateHeadlight(string name, Transform parent, Vector3 localPos)
        {
            GameObject lightObj = new GameObject(name);
            lightObj.transform.SetParent(parent, false);
            lightObj.transform.localPosition = localPos;
            lightObj.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.95f, 0.85f);
            l.intensity = 2.8f;
            l.range = 35f;
            l.spotAngle = 45f;
            return l;
        }

        private Renderer CreateBrakeLightMesh(string name, Transform parent, Vector3 localPos)
        {
            GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshObj.name = name;
            meshObj.transform.SetParent(parent, false);
            meshObj.transform.localPosition = localPos;
            meshObj.transform.localScale = new Vector3(0.28f, 0.12f, 0.08f);

            Collider col = meshObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = meshObj.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = brakeLightIdleMat;
            return r;
        }

        private void Update()
        {
            if (car == null) return;

            float speed = car.SpeedMps;
            float dt = Time.deltaTime;

            // 1. Вращение колес вокруг поперечной оси
            float distMoved = speed * dt;
            float angleDelta = (distMoved / (2f * Mathf.PI * WheelRadius)) * 360f;
            wheelRotationDeg += angleDelta;

            // 2. Угол поворота передних колес
            float steerInput = Input.GetAxis("Horizontal");
            float steerAngle = steerInput * 25f;

            Quaternion rollRot = Quaternion.Euler(wheelRotationDeg, 0f, 0f);
            Quaternion steerRot = Quaternion.Euler(0f, steerAngle, 0f);

            if (wheelFL != null) wheelFL.localRotation = steerRot * rollRot;
            if (wheelFR != null) wheelFR.localRotation = steerRot * rollRot;
            if (wheelRL != null) wheelRL.localRotation = rollRot;
            if (wheelRR != null) wheelRR.localRotation = rollRot;

            // 3. Индикация стоп-сигналов при торможении
            bool isBraking = (Input.GetAxis("Vertical") < -0.05f) || (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow));
            Material curBrakeMat = isBraking ? brakeLightActiveMat : brakeLightIdleMat;

            if (brakeLightLeftRenderer != null) brakeLightLeftRenderer.sharedMaterial = curBrakeMat;
            if (brakeLightRightRenderer != null) brakeLightRightRenderer.sharedMaterial = curBrakeMat;

            // 4. Крен кузова (Body tilt) при рулении
            if (visualBody != null)
            {
                float targetRoll = -steerInput * 4.2f;
                float targetPitch = (isBraking ? 1.5f : 0f) + (car.IsNitroActive ? -2.2f : 0f);
                visualBody.localRotation = Quaternion.Slerp(visualBody.localRotation, Quaternion.Euler(targetPitch, 0f, targetRoll), dt * 8f);
            }
        }
    }
}
