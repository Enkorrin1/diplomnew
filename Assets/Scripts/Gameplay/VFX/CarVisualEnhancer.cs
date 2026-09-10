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

        // Дым при заносе (Drift Smoke)
        private ParticleSystem driftSmokeRL;
        private ParticleSystem driftSmokeRR;

        private void Awake()
        {
            car = GetComponent<ArcadeCarController>();
            body = GetComponent<Rigidbody>();
            visualBody = transform.Find("VisualBody");

            SetupWheelsIfMissing();
            SetupLights();
            SetupDriftSmoke();
        }

        public void RefreshWheels()
        {
            // Удаляем старые процедурные колёса, чтобы не дублировались
            Transform oldWheels = transform.Find("Wheels");
            if (oldWheels != null)
            {
                Destroy(oldWheels.gameObject);
            }

            wheelFL = null;
            wheelFR = null;
            wheelRL = null;
            wheelRR = null;
            SetupWheelsIfMissing();
        }

        private void SetupWheelsIfMissing()
        {
            // Прокачиваемые колёса всегда имеют четыре явных точки крепления.
            // Это приоритетнее эвристики имён штатной модели и исключает дубли.
            Transform upgradeMounts = transform.Find("WheelMounts");
            if (upgradeMounts != null)
            {
                wheelFL = upgradeMounts.Find("Wheel_FL");
                wheelFR = upgradeMounts.Find("Wheel_FR");
                wheelRL = upgradeMounts.Find("Wheel_RL");
                wheelRR = upgradeMounts.Find("Wheel_RR");
                if (wheelFL != null && wheelFR != null && wheelRL != null && wheelRR != null)
                    return;
            }

            // 1. Сначала ищем реальные 3D-колеса среди дочерних объектов модели автомобиля
            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == transform) continue;
                string n = t.name.ToUpperInvariant();
                if ((n.Contains("TIRE") || n.Contains("WHEEL")) && !n.Equals("WHEELS"))
                {
                    if (wheelFL == null && (n.Contains("FL") || n.Contains("FRONT_L") || n.Contains("FORWARD_L"))) wheelFL = t;
                    else if (wheelFR == null && (n.Contains("FR") || n.Contains("FRONT_R") || n.Contains("FORWARD_R"))) wheelFR = t;
                    else if (wheelRL == null && (n.Contains("RL") || n.Contains("REAR_L") || n.Contains("BACK_L") || n.Contains("BL"))) wheelRL = t;
                    else if (wheelRR == null && (n.Contains("RR") || n.Contains("REAR_R") || n.Contains("BACK_R") || n.Contains("BR"))) wheelRR = t;
                }
            }

            if (wheelFL != null && wheelFR != null && wheelRL != null && wheelRR != null)
            {
                return; // Колеса 3D-модели успешно найдены и подключены к системе анимации
            }

            // У прокачиваемой машины визуал колёс создаёт CarWheelUpgradeVisuals.
            // Нельзя добавлять сюда второй процедурный набор: он появляется на
            // кузове, если импорт модели ещё не завершил инициализацию.
            if (GetComponent<CarWheelUpgradeVisuals>() != null)
                return;

            // 2. Резерв: создание процедурных колес, если модель не содержит раздельных колес
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

        private void SetupDriftSmoke()
        {
            driftSmokeRL = CreateDriftSmokeEmitter("DriftSmoke_RL", new Vector3(-0.85f, 0.15f, -1.3f));
            driftSmokeRR = CreateDriftSmokeEmitter("DriftSmoke_RR", new Vector3(0.85f, 0.15f, -1.3f));
        }

        private ParticleSystem CreateDriftSmokeEmitter(string name, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 0.7f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.4f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.gravityModifier = -0.1f; // дым поднимается вверх

            var emission = ps.emission;
            emission.rateOverTime = 0f; // управляем вручную

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.5f, 1.2f), new Keyframe(1f, 2f)));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.9f, 0.9f, 0.9f), 0f), new GradientColorKey(new Color(0.7f, 0.7f, 0.7f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0.15f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            // Отключаем рендерер тени для производительности
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.material = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Standard"));
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private void UpdateDriftSmoke()
        {
            if (body == null || driftSmokeRL == null || driftSmokeRR == null) return;

            float speed = car.SpeedMps;
            float lateralSpeed = Mathf.Abs(Vector3.Dot(body.linearVelocity, transform.right));

            // Дым появляется при боковом скольжении на скорости или при активном ручном тормозе
            bool isHandbrakeDrift = car != null && car.IsHandbrakeActive && Mathf.Abs(speed) > 4f;
            bool isDrifting = isHandbrakeDrift || (speed > 10f && lateralSpeed > 4.5f);

            if (isDrifting)
            {
                float driftIntensity = isHandbrakeDrift 
                    ? Mathf.Max(0.6f, Mathf.Clamp01((lateralSpeed - 2.5f) / 6f)) 
                    : Mathf.Clamp01((lateralSpeed - 4.5f) / 8f);
                float rate = Mathf.Lerp(8f, 35f, driftIntensity);

                var emRL = driftSmokeRL.emission;
                emRL.rateOverTime = rate;
                var emRR = driftSmokeRR.emission;
                emRR.rateOverTime = rate;

                if (!driftSmokeRL.isPlaying) driftSmokeRL.Play();
                if (!driftSmokeRR.isPlaying) driftSmokeRR.Play();
            }
            else
            {
                var emRL = driftSmokeRL.emission;
                emRL.rateOverTime = 0f;
                var emRR = driftSmokeRR.emission;
                emRR.rateOverTime = 0f;
            }
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

            if (GetComponent<CarWheelUpgradeVisuals>() == null)
            {
                if (wheelFL != null) wheelFL.localRotation = steerRot * rollRot;
                if (wheelFR != null) wheelFR.localRotation = steerRot * rollRot;
                if (wheelRL != null) wheelRL.localRotation = rollRot;
                if (wheelRR != null) wheelRR.localRotation = rollRot;
            }

            // 3. Индикация стоп-сигналов при торможении или ручном тормозе
            bool isBraking = (Input.GetAxis("Vertical") < -0.05f) || (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) || (car != null && car.IsHandbrakeActive);
            Material curBrakeMat = isBraking ? brakeLightActiveMat : brakeLightIdleMat;

            if (brakeLightLeftRenderer != null) brakeLightLeftRenderer.sharedMaterial = curBrakeMat;
            if (brakeLightRightRenderer != null) brakeLightRightRenderer.sharedMaterial = curBrakeMat;

            // 4. Крен кузова (Body tilt) при рулении
            if (visualBody != null && GetComponent<CarSuspensionUpgradeVisuals>() == null)
            {
                float targetRoll = -steerInput * 4.2f;
                float targetPitch = (isBraking ? 1.5f : 0f) + (car.IsNitroActive ? -2.2f : 0f);
                visualBody.localRotation = Quaternion.Slerp(visualBody.localRotation, Quaternion.Euler(targetPitch, 0f, targetRoll), dt * 8f);
            }

            // 5. Дым из-под задних колёс при дрифте
            UpdateDriftSmoke();
        }
    }
}
