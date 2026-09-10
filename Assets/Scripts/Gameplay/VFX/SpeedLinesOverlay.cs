using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Радиальные линии скорости и лёгкая виньетка при высокой скорости / нитро.
    /// Рисуется через OnGUI поверх всего.
    /// </summary>
    public sealed class SpeedLinesOverlay : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float speedThreshold = 0.55f;
        [SerializeField, Range(8, 32)] private int lineCount = 20;

        private ArcadeCarController car;
        private Texture2D whiteTex;

        // Предзаданные направления линий (от краёв к центру)
        private Vector2[] lineDirections;
        private float[] lineLengths;
        private float[] lineOffsets;

        private void Awake()
        {
            car = FindFirstObjectByType<ArcadeCarController>();

            whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();

            // Генерируем случайные фиксированные направления для линий
            lineDirections = new Vector2[lineCount];
            lineLengths = new float[lineCount];
            lineOffsets = new float[lineCount];

            for (int i = 0; i < lineCount; i++)
            {
                float angle = (float)i / lineCount * 360f + Random.Range(-8f, 8f);
                float rad = angle * Mathf.Deg2Rad;
                lineDirections[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                lineLengths[i] = Random.Range(0.08f, 0.18f);
                lineOffsets[i] = Random.Range(0.35f, 0.48f);
            }
        }

        private void OnGUI()
        {
            if (car == null)
            {
                car = FindFirstObjectByType<ArcadeCarController>();
                if (car == null) return;
            }

            float speedPercent = Mathf.Clamp01(car.SpeedMps / car.TopSpeedMps);
            bool nitro = car.IsNitroActive;

            // Не показываем ниже порога
            float effectIntensity = 0f;
            if (nitro)
            {
                effectIntensity = Mathf.Clamp01((speedPercent - speedThreshold * 0.5f) / (1f - speedThreshold * 0.5f));
                effectIntensity = Mathf.Max(effectIntensity, 0.6f);
            }
            else if (speedPercent > speedThreshold)
            {
                effectIntensity = (speedPercent - speedThreshold) / (1f - speedThreshold);
            }

            if (effectIntensity < 0.01f) return;

            float sw = Screen.width;
            float sh = Screen.height;
            Vector2 center = new Vector2(sw * 0.5f, sh * 0.5f);

            Color lineColor = nitro
                ? new Color(0.6f, 0.85f, 1f, effectIntensity * 0.35f)
                : new Color(1f, 1f, 1f, effectIntensity * 0.2f);

            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;

            for (int i = 0; i < lineCount; i++)
            {
                Vector2 dir = lineDirections[i];

                // Анимация: линии пульсируют по длине
                float timePulse = Mathf.Sin(Time.time * 6f + lineOffsets[i] * 20f) * 0.3f + 0.7f;
                float len = lineLengths[i] * timePulse * effectIntensity;
                float offset = lineOffsets[i] + effectIntensity * 0.08f;

                // Начальная и конечная точки (от краёв к центру, на расстоянии offset от центра)
                float maxRadius = Mathf.Min(sw, sh) * 0.5f;
                Vector2 start = center + dir * maxRadius * (offset + len);
                Vector2 end = center + dir * maxRadius * offset;

                // Рисуем линию как повёрнутый прямоугольник
                float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
                float lineLen = Vector2.Distance(start, end);
                float lineWidth = Mathf.Lerp(1f, 2.5f, effectIntensity);

                GUI.color = lineColor;
                GUIUtility.RotateAroundPivot(angle, start);
                GUI.DrawTexture(new Rect(start.x, start.y - lineWidth * 0.5f, lineLen, lineWidth), whiteTex);
                GUI.matrix = prevMatrix; // reset rotation
            }

            // Виньетка — затемнение по краям при высокой скорости
            if (effectIntensity > 0.3f)
            {
                float vigAlpha = (effectIntensity - 0.3f) / 0.7f * 0.25f;
                Color vigColor = nitro
                    ? new Color(0f, 0.1f, 0.2f, vigAlpha)
                    : new Color(0f, 0f, 0f, vigAlpha);

                DrawVignette(vigColor, sw, sh);
            }

            GUI.color = prevColor;
        }

        private void DrawVignette(Color color, float sw, float sh)
        {
            float borderSize = Mathf.Min(sw, sh) * 0.2f;
            GUI.color = color;

            // Top
            GUI.DrawTexture(new Rect(0, 0, sw, borderSize), whiteTex);
            // Bottom
            GUI.DrawTexture(new Rect(0, sh - borderSize, sw, borderSize), whiteTex);
            // Left
            GUI.DrawTexture(new Rect(0, 0, borderSize, sh), whiteTex);
            // Right
            GUI.DrawTexture(new Rect(sw - borderSize, 0, borderSize, sh), whiteTex);
        }
    }
}
