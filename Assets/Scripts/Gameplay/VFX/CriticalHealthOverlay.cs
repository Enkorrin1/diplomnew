using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Элемент интерфейса сцены (UI Canvas Image), сигнализирующий о критическом уровне здоровья.
    /// Является полноценным объектом иерархии сцены (дочерним для UI_Canvas),
    /// не использует OnGUI. При падении здоровья ниже 25% плавно пульсирует красным свечением по краям экрана.
    /// </summary>
    public sealed class CriticalHealthOverlay : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.5f)] private float criticalThreshold = 0.25f;
        [SerializeField, Range(0f, 0.2f)] private float dangerThreshold = 0.10f;

        private GameRunController run;
        private GameObject vignetteObject;
        private Image vignetteImage;
        private Texture2D vignetteTexture;

        private void Start()
        {
            run = FindFirstObjectByType<GameRunController>();
            CreateSceneUIElement();
        }

        private void CreateSceneUIElement()
        {
            if (vignetteObject != null) return;

            // Ищем Canvas в сцене (например, UI_Canvas)
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                // Если Canvas не найден, создаем временный
                GameObject cObj = new GameObject("UI_Canvas_VFX", typeof(Canvas), typeof(CanvasScaler));
                canvas = cObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            vignetteObject = new GameObject("UI_CriticalHealth_Vignette", typeof(RectTransform), typeof(Image));
            vignetteObject.transform.SetParent(canvas.transform, false);

            RectTransform rt = vignetteObject.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            vignetteImage = vignetteObject.GetComponent<Image>();
            vignetteImage.raycastTarget = false;

            // Генерируем процедурную мягкую текстуру виньетки
            GenerateVignetteTexture();
            vignetteImage.sprite = Sprite.Create(vignetteTexture, new Rect(0, 0, vignetteTexture.width, vignetteTexture.height), new Vector2(0.5f, 0.5f));
            vignetteImage.color = new Color(0.85f, 0.05f, 0.02f, 0f);

            vignetteObject.SetActive(false);
        }

        private void GenerateVignetteTexture()
        {
            const int size = 64;
            vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            vignetteTexture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                float ny = (float)y / (size - 1) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / (size - 1) * 2f - 1f;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    // Прозрачный центр, мягкое нарастание к краям
                    float alpha = Mathf.Clamp01(Mathf.Pow(dist, 2.5f));
                    vignetteTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            vignetteTexture.Apply();
        }

        private void Update()
        {
            if (run == null)
            {
                run = FindFirstObjectByType<GameRunController>();
                if (run == null) return;
            }

            if (vignetteObject == null)
            {
                CreateSceneUIElement();
                if (vignetteObject == null) return;
            }

            if (run.IsGameOver)
            {
                if (vignetteObject.activeSelf) vignetteObject.SetActive(false);
                return;
            }

            float hpPercent = run.MaxHealth > 0f ? run.Health / run.MaxHealth : 1f;
            if (hpPercent > criticalThreshold)
            {
                if (vignetteObject.activeSelf) vignetteObject.SetActive(false);
                return;
            }

            if (!vignetteObject.activeSelf) vignetteObject.SetActive(true);

            float intensity;
            float pulseFreq;

            if (hpPercent <= dangerThreshold)
            {
                intensity = Mathf.Lerp(0.55f, 0.85f, 1f - hpPercent / dangerThreshold);
                pulseFreq = 4.2f;
            }
            else
            {
                intensity = Mathf.Lerp(0.2f, 0.45f, 1f - (hpPercent - dangerThreshold) / (criticalThreshold - dangerThreshold));
                pulseFreq = 2.2f;
            }

            float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * pulseFreq * Mathf.PI));
            float alpha = intensity * pulse;

            if (vignetteImage != null)
            {
                vignetteImage.color = new Color(0.9f, 0.04f, 0.02f, alpha);
            }
        }

        private void OnDestroy()
        {
            if (vignetteObject != null)
            {
                Destroy(vignetteObject);
            }
            if (vignetteTexture != null)
            {
                Destroy(vignetteTexture);
            }
        }
    }
}
