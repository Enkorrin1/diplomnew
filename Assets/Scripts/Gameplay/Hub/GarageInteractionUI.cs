using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Современный легковесный интерфейс взаимодействия в гараже/бункере на базе Unity Canvas (UGUI).
    /// Полностью заменяет устаревший OnGUI. Отображает точечный прицел (Crosshair),
    /// интерактивную плашку подсказки [E] и верхний баннер сюжетных уведомлений.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageInteractionUI : MonoBehaviour
    {
        public static GarageInteractionUI Instance { get; private set; }

        [Header("Canvas References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image crosshairDot;
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Text promptText;
        [SerializeField] private GameObject bannerPanel;
        [SerializeField] private Text bannerText;

        private float bannerTimer = 0f;
        private Color crosshairDefaultColor = new Color(1f, 1f, 1f, 0.7f);
        private Color crosshairActiveColor = new Color(0.25f, 0.95f, 1f, 1f);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            BuildUIIfNeeded();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.unscaledDeltaTime;
                if (bannerTimer <= 0f && bannerPanel != null)
                {
                    bannerPanel.SetActive(false);
                }
            }
        }

        public void ShowPrompt(string text, bool canInteract)
        {
            if (promptPanel == null || promptText == null) return;

            if (string.IsNullOrEmpty(text))
            {
                HidePrompt();
                return;
            }

            promptPanel.SetActive(true);
            promptText.text = text;
            promptText.color = canInteract ? new Color(0.35f, 0.95f, 1f, 1f) : new Color(0.85f, 0.85f, 0.85f, 0.8f);

            if (crosshairDot != null)
            {
                crosshairDot.color = canInteract ? crosshairActiveColor : crosshairDefaultColor;
                crosshairDot.rectTransform.sizeDelta = new Vector2(8f, 8f);
            }
        }

        public void HidePrompt()
        {
            if (promptPanel != null) promptPanel.SetActive(false);
            if (crosshairDot != null)
            {
                crosshairDot.color = crosshairDefaultColor;
                crosshairDot.rectTransform.sizeDelta = new Vector2(5f, 5f);
            }
        }

        public void ShowBanner(string message, float duration = 4.5f)
        {
            if (bannerPanel == null || bannerText == null) return;

            bannerText.text = message;
            bannerPanel.SetActive(true);
            bannerTimer = duration;
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (crosshairDot != null) crosshairDot.gameObject.SetActive(visible);
        }

        private void BuildUIIfNeeded()
        {
            if (canvas != null) return;

            // 1. Создаем Canvas
            GameObject canvasObj = new GameObject("GarageInteraction_Canvas");
            canvasObj.transform.SetParent(transform, false);

            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? 
                               Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 2. Прицел (Crosshair Dot в центре)
            GameObject dotObj = new GameObject("CrosshairDot");
            dotObj.transform.SetParent(canvasObj.transform, false);
            crosshairDot = dotObj.AddComponent<Image>();
            crosshairDot.color = crosshairDefaultColor;
            crosshairDot.raycastTarget = false;
            crosshairDot.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairDot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairDot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crosshairDot.rectTransform.sizeDelta = new Vector2(5f, 5f);

            // 3. Плашка подсказки взаимодействия (снизу от центра)
            promptPanel = new GameObject("PromptPanel");
            promptPanel.transform.SetParent(canvasObj.transform, false);
            var promptImg = promptPanel.AddComponent<Image>();
            promptImg.color = new Color(0.06f, 0.1f, 0.16f, 0.92f);
            promptImg.raycastTarget = false;

            var promptRect = promptPanel.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 1f);
            promptRect.anchoredPosition = new Vector2(0f, -42f);
            promptRect.sizeDelta = new Vector2(580f, 52f);

            GameObject promptTextObj = new GameObject("PromptText");
            promptTextObj.transform.SetParent(promptPanel.transform, false);
            promptText = promptTextObj.AddComponent<Text>();
            if (standardFont != null) promptText.font = standardFont;
            promptText.fontSize = 18;
            promptText.fontStyle = FontStyle.Bold;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color = Color.white;
            promptText.raycastTarget = false;

            var textRect = promptTextObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 6f);
            textRect.offsetMax = new Vector2(-16f, -6f);

            promptPanel.SetActive(false);

            // 4. Верхний баннер сюжетных уведомлений
            bannerPanel = new GameObject("BannerPanel");
            bannerPanel.transform.SetParent(canvasObj.transform, false);
            var bannerImg = bannerPanel.AddComponent<Image>();
            bannerImg.color = new Color(0.05f, 0.08f, 0.12f, 0.95f);
            bannerImg.raycastTarget = false;

            var bannerRect = bannerPanel.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -28f);
            bannerRect.sizeDelta = new Vector2(720f, 50f);

            GameObject bannerTextObj = new GameObject("BannerText");
            bannerTextObj.transform.SetParent(bannerPanel.transform, false);
            bannerText = bannerTextObj.AddComponent<Text>();
            if (standardFont != null) bannerText.font = standardFont;
            bannerText.fontSize = 17;
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.alignment = TextAnchor.MiddleCenter;
            bannerText.color = new Color(0.35f, 0.95f, 1f);
            bannerText.raycastTarget = false;

            var bTextRect = bannerTextObj.GetComponent<RectTransform>();
            bTextRect.anchorMin = Vector2.zero;
            bTextRect.anchorMax = Vector2.one;
            bTextRect.offsetMin = new Vector2(20f, 4f);
            bTextRect.offsetMax = new Vector2(-20f, -4f);

            bannerPanel.SetActive(false);
        }
    }
}

