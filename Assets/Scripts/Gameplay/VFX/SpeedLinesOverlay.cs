using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Элемент интерфейса сцены (UI Canvas Overlay), создающий эффект радиальных линий скорости и виньетки ускорения.
    /// Является полноценным объектом сцены в иерархии UI_Canvas, не использует устаревший OnGUI.
    /// Активируется на высокой скорости и во время работы нитро-ускорителя.
    /// </summary>
    public sealed class SpeedLinesOverlay : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float speedThreshold = 0.55f;

        private ArcadeCarController car;
        private GameObject overlayObject;
        private Image overlayImage;
        private Texture2D speedLinesTexture;

        private void Start()
        {
            car = FindFirstObjectByType<ArcadeCarController>();
            CreateSceneUIElement();
        }

        private void CreateSceneUIElement()
        {
            if (overlayObject != null) return;

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject cObj = new GameObject("UI_Canvas_VFX", typeof(Canvas), typeof(CanvasScaler));
                canvas = cObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            overlayObject = new GameObject("UI_SpeedLines_Overlay", typeof(RectTransform), typeof(Image));
            overlayObject.transform.SetParent(canvas.transform, false);

            RectTransform rt = overlayObject.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            overlayImage = overlayObject.GetComponent<Image>();
            overlayImage.raycastTarget = false;

            GenerateSpeedTunnelTexture();
            overlayImage.sprite = Sprite.Create(speedLinesTexture, new Rect(0, 0, speedLinesTexture.width, speedLinesTexture.height), new Vector2(0.5f, 0.5f));
            overlayImage.color = new Color(1f, 1f, 1f, 0f);

            overlayObject.SetActive(false);
        }

        private void GenerateSpeedTunnelTexture()
        {
            const int size = 128;
            speedLinesTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            speedLinesTexture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float dist = p.magnitude / maxR;
                    float angle = Mathf.Atan2(p.y, p.x);

                    // Радиальные лучи скорости по углам
                    float streaks = Mathf.Sin(angle * 24f) * 0.5f + 0.5f;
                    streaks = Mathf.Pow(streaks, 3f);

                    // Плавное нарастание от центра к периферии
                    float edgeMask = Mathf.Clamp01((dist - 0.45f) / 0.55f);
                    float alpha = edgeMask * streaks * 0.7f + Mathf.Pow(edgeMask, 3f) * 0.35f;

                    speedLinesTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                }
            }
            speedLinesTexture.Apply();
        }

        private void Update()
        {
            if (car == null)
            {
                car = FindFirstObjectByType<ArcadeCarController>();
                if (car == null) return;
            }

            if (overlayObject == null)
            {
                CreateSceneUIElement();
                if (overlayObject == null) return;
            }

            float speedPercent = Mathf.Clamp01(car.SpeedMps / car.TopSpeedMps);
            bool nitro = car.IsNitroActive;

            float effectIntensity = 0f;
            if (nitro)
            {
                effectIntensity = Mathf.Clamp01((speedPercent - speedThreshold * 0.5f) / (1f - speedThreshold * 0.5f));
                effectIntensity = Mathf.Max(effectIntensity, 0.65f);
            }
            else if (speedPercent > speedThreshold)
            {
                effectIntensity = (speedPercent - speedThreshold) / (1f - speedThreshold);
            }

            if (effectIntensity < 0.02f)
            {
                if (overlayObject.activeSelf) overlayObject.SetActive(false);
                return;
            }

            if (!overlayObject.activeSelf) overlayObject.SetActive(true);

            // Пульсация линий на высокой скорости
            float pulse = Mathf.Sin(Time.time * 18f) * 0.15f + 0.85f;
            float finalAlpha = effectIntensity * pulse * 0.5f;

            Color targetColor = nitro
                ? new Color(0.4f, 0.85f, 1f, finalAlpha) // Голубой неоновый оттенок при нитро
                : new Color(1f, 1f, 1f, finalAlpha * 0.75f); // Белый на обычной скорости

            if (overlayImage != null)
            {
                overlayImage.color = targetColor;
            }
        }

        private void OnDestroy()
        {
            if (overlayObject != null)
            {
                Destroy(overlayObject);
            }
            if (speedLinesTexture != null)
            {
                Destroy(speedLinesTexture);
            }
        }
    }
}
