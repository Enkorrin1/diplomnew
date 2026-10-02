using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Сектор на листе карты эвакуации: зона наведения, подсветка и текст справки.
    /// Объект сцены на world-space канвасе карты, читается BunkerEvacuationMap.
    /// </summary>
    public sealed class EvacuationMapSector : MonoBehaviour
    {
        [SerializeField] private string title;
        [SerializeField] private string conditions;
        [SerializeField, TextArea(3, 8)] private string body;
        [SerializeField] private RectTransform hotspot;
        [SerializeField] private Graphic highlight;
        [SerializeField] private Graphic label;
        [SerializeField] private Color labelIdle = new Color(0.22f, 0.16f, 0.1f, 0.85f);
        [SerializeField] private Color labelActive = new Color(0.62f, 0.12f, 0.06f, 1f);

        private Color highlightOn = Color.white;
        private float current;
        private float target;
        private bool cached;

        public string Title => title;
        public string Conditions => conditions;
        public string Body => body;
        public RectTransform Hotspot => hotspot;

        public void SetHighlighted(bool on, bool instant)
        {
            CacheColors();
            target = on ? 1f : 0f;
            if (instant) Apply(current = target);
        }

        private void CacheColors()
        {
            if (cached) return;
            cached = true;
            if (highlight != null) highlightOn = highlight.color;
        }

        private void Update()
        {
            if (Mathf.Approximately(current, target)) return;
            current = Mathf.MoveTowards(current, target, Time.deltaTime * 6f);
            Apply(current);
        }

        private void Apply(float k)
        {
            if (highlight != null)
            {
                var c = highlightOn;
                c.a = highlightOn.a * k;
                highlight.color = c;
            }
            if (label != null) label.color = Color.Lerp(labelIdle, labelActive, k);
        }
    }
}
