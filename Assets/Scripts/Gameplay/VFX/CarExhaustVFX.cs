using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Визуальные эффекты выхлопной системы и нитро.
    /// Создает две выхлопные трубы сзади кузова автомобиля, испускает дым при разгоне
    /// и снопы сине-голубого реактивного пламени с динамическим свечением при включении нитро.
    /// </summary>
    public sealed class CarExhaustVFX : MonoBehaviour
    {
        [Header("Exhaust Tip Positions")]
        [SerializeField] private Vector3 leftExhaustLocal = new Vector3(-0.55f, 0.35f, -1.95f);
        [SerializeField] private Vector3 rightExhaustLocal = new Vector3(0.55f, 0.35f, -1.95f);

        [Header("Colors")]
        [SerializeField] private Color nitroFlameColor = new Color(0.15f, 0.75f, 1f, 0.95f);
        [SerializeField] private Color nitroCoreColor = new Color(0.9f, 0.98f, 1f, 1f);

        ArcadeCarController car;
        Transform leftPipe;
        Transform rightPipe;

        GameObject leftFlame;
        GameObject rightFlame;
        Light nitroGlowLight;

        private void Awake()
        {
            car = GetComponent<ArcadeCarController>();
            SetupExhaustPipes();
        }

        void SetupExhaustPipes()
        {
            // Левая выхлопная труба
            GameObject pL = new GameObject("ExhaustPipe_L");
            pL.transform.SetParent(transform, false);
            pL.transform.localPosition = leftExhaustLocal;
            leftPipe = pL.transform;

            // Правая выхлопная труба
            GameObject pR = new GameObject("ExhaustPipe_R");
            pR.transform.SetParent(transform, false);
            pR.transform.localPosition = rightExhaustLocal;
            rightPipe = pR.transform;

            // Визуальные конусы пламени нитро
            leftFlame = CreateFlameCone(leftPipe, "NitroFlame_L");
            rightFlame = CreateFlameCone(rightPipe, "NitroFlame_R");

            // Динамический свет вспышки нитро
            GameObject lightObj = new GameObject("NitroGlowLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = (leftExhaustLocal + rightExhaustLocal) * 0.5f + Vector3.back * 0.5f;
            nitroGlowLight = lightObj.AddComponent<Light>();
            nitroGlowLight.type = LightType.Point;
            nitroGlowLight.color = nitroFlameColor;
            nitroGlowLight.range = 6.5f;
            nitroGlowLight.intensity = 0f;
            nitroGlowLight.enabled = false;

            SetFlamesActive(false);
        }

        GameObject CreateFlameCone(Transform parent, string name)
        {
            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            flame.name = name;
            flame.transform.SetParent(parent, false);
            flame.transform.localPosition = new Vector3(0f, 0f, -0.65f);
            flame.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            flame.transform.localScale = new Vector3(0.22f, 0.65f, 0.22f);

            Collider col = flame.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = flame.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                m.color = nitroFlameColor;
                r.sharedMaterial = m;
            }

            return flame;
        }

        void SetFlamesActive(bool active)
        {
            if (leftFlame != null) leftFlame.SetActive(active);
            if (rightFlame != null) rightFlame.SetActive(active);
            if (nitroGlowLight != null)
            {
                nitroGlowLight.enabled = active;
                nitroGlowLight.intensity = active ? 2.5f : 0f;
            }
        }

        private void Update()
        {
            if (car == null) return;

            bool nitroActive = car.IsNitroActive;

            if (leftFlame != null && leftFlame.activeSelf != nitroActive)
            {
                SetFlamesActive(nitroActive);
            }

            // Пульсация и трепет реактивного пламени нитро
            if (nitroActive)
            {
                float flicker = 1f + (Mathf.Sin(Time.time * 45f) * 0.18f) + (Random.value * 0.12f);
                Vector3 flameScale = new Vector3(0.24f * flicker, 0.75f * flicker, 0.24f * flicker);

                if (leftFlame != null) leftFlame.transform.localScale = flameScale;
                if (rightFlame != null) rightFlame.transform.localScale = flameScale;

                if (nitroGlowLight != null)
                {
                    nitroGlowLight.intensity = 2.2f + Random.value * 0.8f;
                }
            }
        }
    }
}
