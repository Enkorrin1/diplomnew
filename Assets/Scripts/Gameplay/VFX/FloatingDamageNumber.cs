using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Трехмерный элемент сцены: всплывающее число урона над противником.
    /// Создается как физический объект сцены (GameObject с TextMesh и MeshRenderer),
    /// не использует устаревший OnGUI, поворачивается лицом к камере (Billboard)
    /// и плавно поднимается вверх, затухая.
    /// </summary>
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class FloatingDamageNumber : MonoBehaviour
    {
        private const float Lifetime = 0.85f;
        private float elapsed;
        private Vector3 startPos;
        private Color baseColor;
        private TextMesh textMesh;
        private Camera mainCam;
        private static Font defaultFont;

        /// <summary>
        /// Создать 3D-число урона как полноценный объект сцены над указанной позицией.
        /// </summary>
        public static void Spawn(Vector3 worldPosition, float damageAmount, bool isBurn = false)
        {
            if (damageAmount < 0.5f) return;

            GameObject go = new GameObject("DamageNumber_3D");
            go.transform.position = worldPosition + Vector3.up * 1.6f + Random.insideUnitSphere * 0.25f;

            var dn = go.AddComponent<FloatingDamageNumber>();
            dn.Setup(Mathf.RoundToInt(damageAmount), isBurn, damageAmount >= 50f);
        }

        private void Setup(int damage, bool isBurn, bool isCritical)
        {
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (defaultFont == null)
                {
                    defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }

            textMesh = GetComponent<TextMesh>();
            if (defaultFont != null)
            {
                textMesh.font = defaultFont;
                var mr = GetComponent<MeshRenderer>();
                if (mr != null && defaultFont.material != null)
                {
                    mr.sharedMaterial = defaultFont.material;
                }
            }

            textMesh.text = damage.ToString();
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;

            if (isBurn)
            {
                baseColor = new Color(1f, 0.55f, 0.1f, 1f); // Оранжевый
                textMesh.fontSize = 54;
                textMesh.characterSize = 0.045f;
            }
            else if (isCritical)
            {
                baseColor = new Color(1f, 0.95f, 0.15f, 1f); // Золотой крит
                textMesh.fontSize = 68;
                textMesh.characterSize = 0.06f;
            }
            else if (damage >= 25)
            {
                baseColor = Color.white;
                textMesh.fontSize = 56;
                textMesh.characterSize = 0.048f;
            }
            else
            {
                baseColor = new Color(0.9f, 0.9f, 0.92f, 1f);
                textMesh.fontSize = 46;
                textMesh.characterSize = 0.04f;
            }

            textMesh.color = baseColor;
            startPos = transform.position;
            mainCam = Camera.main;

            if (mainCam != null)
            {
                transform.rotation = mainCam.transform.rotation;
            }
        }

        private void Awake()
        {
            mainCam = Camera.main;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float t = elapsed / Lifetime;

            // Движение вверх с плавным замедлением (ease-out)
            float rise = Mathf.Lerp(0f, 1.8f, 1f - Mathf.Pow(1f - t, 2f));
            transform.position = startPos + Vector3.up * rise;

            // Поворот лицом к камере
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam != null)
            {
                transform.rotation = mainCam.transform.rotation;
            }

            // Плавное угасание прозрачности к концу жизни
            float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
            if (textMesh != null)
            {
                textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }

            // Легкая пульсация масштаба на старте (pop-in)
            float scale = t < 0.12f ? Mathf.Lerp(1.4f, 1f, t / 0.12f) : 1f;
            transform.localScale = Vector3.one * scale;
        }
    }
}
