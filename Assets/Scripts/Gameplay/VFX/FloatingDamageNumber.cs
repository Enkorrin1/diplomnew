using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Всплывающее число урона над врагом.
    /// Создаётся через статический метод Spawn и автоматически уничтожается.
    /// </summary>
    public sealed class FloatingDamageNumber : MonoBehaviour
    {
        private float lifetime = 0.85f;
        private float elapsed;
        private Vector3 startPos;
        private Color color;
        private float fontSize;
        private string text;
        private Camera mainCam;

        /// <summary>
        /// Создать всплывающее число урона над указанной позицией.
        /// </summary>
        public static void Spawn(Vector3 worldPosition, float damageAmount, bool isBurn = false)
        {
            if (damageAmount < 0.5f) return; // не показываем микроурон

            GameObject go = new GameObject("DmgNum");
            go.transform.position = worldPosition + Vector3.up * 1.8f + Random.insideUnitSphere * 0.3f;

            var dn = go.AddComponent<FloatingDamageNumber>();
            dn.startPos = go.transform.position;
            dn.text = Mathf.RoundToInt(damageAmount).ToString();

            // Цвет в зависимости от типа урона
            if (isBurn)
            {
                dn.color = new Color(1f, 0.5f, 0.1f); // оранжевый для горения
                dn.fontSize = 28;
            }
            else if (damageAmount >= 50f)
            {
                dn.color = new Color(1f, 0.95f, 0.2f); // жёлтый для критического
                dn.fontSize = 38;
            }
            else if (damageAmount >= 25f)
            {
                dn.color = Color.white;
                dn.fontSize = 32;
            }
            else
            {
                dn.color = new Color(0.9f, 0.9f, 0.9f);
                dn.fontSize = 26;
            }
        }

        private void Awake()
        {
            mainCam = Camera.main;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float t = elapsed / lifetime;

            // Подъём вверх с замедлением
            float rise = Mathf.Lerp(0f, 2.2f, 1f - (1f - t) * (1f - t));
            transform.position = startPos + Vector3.up * rise;

            // Биллбординг — всегда лицом к камере
            if (mainCam != null)
            {
                transform.rotation = mainCam.transform.rotation;
            }
        }

        private GUIStyle style;

        private void OnGUI()
        {
            if (mainCam == null) return;

            float t = elapsed / lifetime;
            float alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);

            Vector3 screenPos = mainCam.WorldToScreenPoint(transform.position);
            if (screenPos.z < 0f) return; // за камерой

            // Unity GUI: Y инвертирован
            float guiY = Screen.height - screenPos.y;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            // Масштаб на входе (pop-in)
            float scale = t < 0.1f ? Mathf.Lerp(1.5f, 1f, t / 0.1f) : 1f;
            int fs = Mathf.RoundToInt(fontSize * scale);
            style.fontSize = fs;

            // Тень
            float sw = 160f;
            float sh = 40f;
            Rect shadowRect = new Rect(screenPos.x - sw / 2f + 1.5f, guiY - sh / 2f + 1.5f, sw, sh);
            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.7f);
            GUI.Label(shadowRect, text, style);

            // Основной текст
            Rect mainRect = new Rect(screenPos.x - sw / 2f, guiY - sh / 2f, sw, sh);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(mainRect, text, style);

            GUI.color = prevColor;
        }
    }
}
