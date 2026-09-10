using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Красная пульсирующая виньетка по краям экрана при низком HP.
    /// Чем ниже здоровье — тем интенсивнее и чаще пульсация.
    /// </summary>
    public sealed class CriticalHealthOverlay : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.5f)] private float criticalThreshold = 0.25f;
        [SerializeField, Range(0f, 0.2f)] private float dangerThreshold = 0.10f;

        private GameRunController run;
        private Texture2D whiteTex;

        private void Awake()
        {
            run = FindFirstObjectByType<GameRunController>();

            whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
        }

        private void OnGUI()
        {
            if (run == null)
            {
                run = FindFirstObjectByType<GameRunController>();
                if (run == null) return;
            }

            if (run.IsGameOver) return;

            float hpPercent = run.MaxHealth > 0f ? run.Health / run.MaxHealth : 1f;
            if (hpPercent > criticalThreshold) return;

            // Определяем интенсивность и частоту пульсации
            float intensity;
            float pulseFreq;

            if (hpPercent <= dangerThreshold)
            {
                // Экстремально низкий HP — сильная и быстрая пульсация
                intensity = Mathf.Lerp(0.45f, 0.6f, 1f - hpPercent / dangerThreshold);
                pulseFreq = 4f;
            }
            else
            {
                // Критический HP — умеренная пульсация
                intensity = Mathf.Lerp(0.1f, 0.35f, 1f - (hpPercent - dangerThreshold) / (criticalThreshold - dangerThreshold));
                pulseFreq = 2f;
            }

            // Пульсация через синусоиду
            float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * pulseFreq * Mathf.PI));
            float alpha = intensity * pulse;

            if (alpha < 0.01f) return;

            float sw = Screen.width;
            float sh = Screen.height;
            float borderX = sw * 0.15f;
            float borderY = sh * 0.15f;

            Color vigColor = new Color(0.8f, 0.05f, 0.02f, alpha);
            Color prevColor = GUI.color;
            GUI.color = vigColor;

            // Рисуем красные полосы по краям экрана (имитация виньетки)
            // Top
            GUI.DrawTexture(new Rect(0, 0, sw, borderY), whiteTex);
            // Bottom
            GUI.DrawTexture(new Rect(0, sh - borderY, sw, borderY), whiteTex);
            // Left
            GUI.DrawTexture(new Rect(0, 0, borderX, sh), whiteTex);
            // Right
            GUI.DrawTexture(new Rect(sw - borderX, 0, borderX, sh), whiteTex);

            // Дополнительный пульсирующий слой в углах (более насыщенный)
            if (hpPercent <= dangerThreshold)
            {
                float cornerAlpha = alpha * 0.5f;
                GUI.color = new Color(1f, 0f, 0f, cornerAlpha);
                float cornerSize = Mathf.Min(sw, sh) * 0.1f;
                GUI.DrawTexture(new Rect(0, 0, cornerSize, cornerSize), whiteTex);
                GUI.DrawTexture(new Rect(sw - cornerSize, 0, cornerSize, cornerSize), whiteTex);
                GUI.DrawTexture(new Rect(0, sh - cornerSize, cornerSize, cornerSize), whiteTex);
                GUI.DrawTexture(new Rect(sw - cornerSize, sh - cornerSize, cornerSize, cornerSize), whiteTex);
            }

            GUI.color = prevColor;
        }
    }
}
