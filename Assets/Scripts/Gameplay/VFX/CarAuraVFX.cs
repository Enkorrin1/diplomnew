using System.Collections;
using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Визуализатор эффектов аур вооружения:
    /// - Огненный след за машиной (Fire Trail)
    /// - Вращающиеся стальные дисковые пилы по бокам кузова (Side Saws)
    /// - Силовое пульсирующее поле энергощита (Energy Shield)
    /// - Электрические зигзагообразные дуги молний (Lightning Arcs)
    /// - Расширяющееся кольцо ударной волны (Shockwave)
    /// </summary>
    public sealed class CarAuraVFX : MonoBehaviour
    {
        TrailRenderer fireTrail;
        GameObject leftSaw;
        GameObject rightSaw;
        GameObject energyShieldObj;
        Material shieldMat;

        bool isFireActive;
        bool isSawsActive;
        bool isShieldActive;
        float shieldBaseRadius = 3.2f;

        private void Awake()
        {
            SetupFireTrail();
            SetupSideSaws();
            SetupEnergyShield();
        }

        void SetupFireTrail()
        {
            GameObject trailObj = new GameObject("FireTrail_Emitter");
            trailObj.transform.SetParent(transform, false);
            trailObj.transform.localPosition = new Vector3(0f, 0.08f, -1.8f);

            fireTrail = trailObj.AddComponent<TrailRenderer>();
            fireTrail.time = 1.4f;
            fireTrail.startWidth = 2.4f;
            fireTrail.endWidth = 0.6f;
            fireTrail.minVertexDistance = 0.35f;
            fireTrail.emitting = false;
            fireTrail.autodestruct = false;

            Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            m.color = new Color(1f, 0.45f, 0.05f, 0.85f);
            fireTrail.sharedMaterial = m;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.2f, 0.05f), 0.6f), new GradientColorKey(new Color(0.2f, 0.05f, 0.02f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            fireTrail.colorGradient = grad;
        }

        void SetupSideSaws()
        {
            leftSaw = CreateSawBlade(new Vector3(-1.35f, 0.45f, 0f), "SawBlade_L");
            rightSaw = CreateSawBlade(new Vector3(1.35f, 0.45f, 0f), "SawBlade_R");

            leftSaw.SetActive(false);
            rightSaw.SetActive(false);
        }

        GameObject CreateSawBlade(Vector3 localPos, string name)
        {
            GameObject saw = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            saw.name = name;
            saw.transform.SetParent(transform, false);
            saw.transform.localPosition = localPos;
            saw.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            saw.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f);

            Collider col = saw.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = saw.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.85f, 0.88f, 0.92f);
                r.sharedMaterial = m;
            }

            return saw;
        }

        void SetupEnergyShield()
        {
            energyShieldObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            energyShieldObj.name = "EnergyShield_Bubble";
            energyShieldObj.transform.SetParent(transform, false);
            energyShieldObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            energyShieldObj.transform.localScale = Vector3.one * (shieldBaseRadius * 2f);

            Collider c = energyShieldObj.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer r = energyShieldObj.GetComponent<Renderer>();
            if (r != null)
            {
                shieldMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                shieldMat.color = new Color(0.2f, 0.85f, 1f, 0.28f);
                r.sharedMaterial = shieldMat;
            }

            energyShieldObj.SetActive(false);
        }

        public void SetFireTrail(bool active)
        {
            isFireActive = active;
            if (fireTrail != null) fireTrail.emitting = active;
        }

        public void SetSideSaws(bool active)
        {
            isSawsActive = active;
            if (leftSaw != null) leftSaw.SetActive(active);
            if (rightSaw != null) rightSaw.SetActive(active);
        }

        public void SetEnergyShield(bool active, float radius)
        {
            isShieldActive = active;
            shieldBaseRadius = radius;
            if (energyShieldObj != null)
            {
                energyShieldObj.SetActive(active);
                energyShieldObj.transform.localScale = Vector3.one * (radius * 2f);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Вращение дисковых пил со скоростью 1200 об/мин
            if (isSawsActive)
            {
                float rotSpeed = 1200f * dt;
                if (leftSaw != null) leftSaw.transform.Rotate(Vector3.up, rotSpeed, Space.Self);
                if (rightSaw != null) rightSaw.transform.Rotate(Vector3.up, -rotSpeed, Space.Self);
            }

            // Мерцание и пульсация силового поля энергощита
            if (isShieldActive && energyShieldObj != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
                energyShieldObj.transform.localScale = Vector3.one * (shieldBaseRadius * 2f * pulse);

                if (shieldMat != null)
                {
                    float alpha = 0.22f + Mathf.Sin(Time.time * 8f) * 0.08f;
                    shieldMat.color = new Color(0.2f, 0.85f, 1f, alpha);
                }
            }
        }

        public void PlayShockwave(float radius)
        {
            StartCoroutine(ShockwaveRoutine(radius));
        }

        IEnumerator ShockwaveRoutine(float maxRadius)
        {
            GameObject wave = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wave.name = "Shockwave_Ring";
            wave.transform.position = transform.position + Vector3.up * 0.1f;
            wave.transform.localScale = new Vector3(0.5f, 0.04f, 0.5f);

            Collider c = wave.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Renderer r = wave.GetComponent<Renderer>();
            Material mat = null;
            if (r != null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                mat.color = new Color(0.3f, 0.9f, 1f, 0.8f);
                r.sharedMaterial = mat;
            }

            float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float currentR = Mathf.Lerp(1f, maxRadius * 2f, progress);
                wave.transform.localScale = new Vector3(currentR, 0.04f, currentR);

                if (mat != null)
                {
                    mat.color = new Color(0.3f, 0.9f, 1f, Mathf.Lerp(0.85f, 0f, progress));
                }

                yield return null;
            }

            Destroy(wave);
        }

        public void PlayLightningArc(Vector3 startPos, Vector3 targetPos)
        {
            StartCoroutine(LightningArcRoutine(startPos, targetPos));
        }

        IEnumerator LightningArcRoutine(Vector3 start, Vector3 target)
        {
            GameObject bolt = new GameObject("LightningBolt");
            LineRenderer line = bolt.AddComponent<LineRenderer>();
            line.startWidth = 0.18f;
            line.endWidth = 0.06f;

            Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            m.color = new Color(0.4f, 0.95f, 1f, 1f);
            line.sharedMaterial = m;

            const int segments = 6;
            line.positionCount = segments;
            line.SetPosition(0, start);
            line.SetPosition(segments - 1, target);

            // Создаем зигзаг молнии со случайным смещением
            for (int i = 1; i < segments - 1; i++)
            {
                float t = (float)i / (segments - 1);
                Vector3 basePt = Vector3.Lerp(start, target, t);
                Vector3 jitter = Random.insideUnitSphere * 0.45f;
                line.SetPosition(i, basePt + jitter);
            }

            yield return new WaitForSeconds(0.12f);
            Destroy(bolt);
        }
    }
}
