using UnityEngine;
using RogueDrive.UI;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Современный легковесный интерфейс взаимодействия в гараже/бункере на базе Unity Canvas (UGUI).
    /// Отображает точечный прицел (Crosshair), интерактивную плашку подсказки [E],
    /// постоянный трекер текущей сюжетной цели (Objective Tracker) и баннер уведомлений.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageInteractionUI : MonoBehaviour
    {
        private static GarageInteractionUI instance;
        public static GarageInteractionUI Instance
        {
            get
            {
                if(instance==null)instance=FindFirstObjectByType<GarageInteractionUI>();
                return instance;
            }
            private set=>instance=value;
        }

        [Header("Canvas References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image crosshairDot;
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Text promptText;
        [SerializeField] private GameObject bannerPanel;
        [SerializeField] private Text bannerText;
        [SerializeField] private GameObject objectivePanel;
        [SerializeField] private Text objectiveText;
        [SerializeField] private GameObject heldHintPanel;
        [SerializeField] private Text heldHintText;

        private float bannerTimer = 0f;
        private Color crosshairDefaultColor = new Color(1f, 1f, 1f, 0.7f);
        private Color crosshairActiveColor = LowPolyUi.Amber;

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
            if (canvas != null) LowPolyUi.Apply(canvas.transform);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            bool driving = GarageDriveOutController.Instance != null && GarageDriveOutController.Instance.IsDriving;
            if (driving && heldHintPanel != null && heldHintPanel.activeSelf)
            {
                heldHintPanel.SetActive(false);
            }
            if (bannerPanel != null)
            {
                bannerPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, driving ? -330f : -190f);
            }
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
            promptText.color = canInteract ? new Color(0.35f, 0.95f, 1f, 1f) : new Color(0.95f, 0.75f, 0.35f, 1f);

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

        public void ClearPrompt() => HidePrompt();

        public void ShowBanner(string message, float duration = 5.0f)
        {
            if (bannerPanel == null || bannerText == null) return;

            bannerText.text = message;
            bannerPanel.SetActive(true);
            bannerTimer = duration;
        }

        public void HideBanner()
        {
            if (bannerPanel != null) bannerPanel.SetActive(false);
            bannerTimer = 0f;
        }

        public void ShowNotification(string message, float duration = 5.0f) => ShowBanner(message, duration);

        public void SetObjective(string text)
        {
            if (objectivePanel == null || objectiveText == null) return;

            if (string.IsNullOrEmpty(text))
            {
                objectivePanel.SetActive(false);
                return;
            }

            objectivePanel.SetActive(true);
            objectiveText.text = text;
            var objectiveRect = objectivePanel.GetComponent<RectTransform>();
            if (objectiveRect != null) objectiveRect.sizeDelta = new Vector2(460f, Mathf.Max(142f, 30f + text.Split('\n').Length * 25f));
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (crosshairDot != null) crosshairDot.gameObject.SetActive(visible);
        }

        public void ShowHeldHint(string text)
        {
            if (heldHintPanel == null || heldHintText == null) return;
            bool driving = GarageDriveOutController.Instance != null && GarageDriveOutController.Instance.IsDriving;
            if (driving) return;
            heldHintPanel.SetActive(true);
            heldHintText.text = text;
        }

        public void HideHeldHint()
        {
            if (heldHintPanel != null) heldHintPanel.SetActive(false);
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

            // 3. Плашка подсказки взаимодействия (снизу от прицела)
            promptPanel = new GameObject("PromptPanel");
            promptPanel.transform.SetParent(canvasObj.transform, false);
            var promptImg = promptPanel.AddComponent<Image>();
            promptImg.color = new Color(0.06f, 0.1f, 0.16f, 0.94f);
            promptImg.raycastTarget = false;

            var promptRect = promptPanel.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 1f);
            promptRect.anchoredPosition = new Vector2(0f, -40f);
            promptRect.sizeDelta = new Vector2(650f, 54f);

            GameObject promptTextObj = new GameObject("PromptText");
            promptTextObj.transform.SetParent(promptPanel.transform, false);
            promptText = promptTextObj.AddComponent<Text>();
            if (standardFont != null) promptText.font = standardFont;
            promptText.fontSize = 17;
            promptText.fontStyle = FontStyle.Bold;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color = Color.white;
            promptText.raycastTarget = false;

            var textRect = promptTextObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 4f);
            textRect.offsetMax = new Vector2(-16f, -4f);

            promptPanel.SetActive(false);

            // 4. Верхний баннер сюжетных уведомлений
            bannerPanel = new GameObject("BannerPanel");
            bannerPanel.transform.SetParent(canvasObj.transform, false);
            var bannerImg = bannerPanel.AddComponent<Image>();
            bannerImg.color = new Color(0.05f, 0.08f, 0.14f, 0.95f);
            bannerImg.raycastTarget = false;

            var bannerRect = bannerPanel.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -190f);
            bannerRect.sizeDelta = new Vector2(760f, 52f);

            GameObject bannerTextObj = new GameObject("BannerText");
            bannerTextObj.transform.SetParent(bannerPanel.transform, false);
            bannerText = bannerTextObj.AddComponent<Text>();
            if (standardFont != null) bannerText.font = standardFont;
            bannerText.fontSize = 17;
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.alignment = TextAnchor.MiddleCenter;
            bannerText.color = LowPolyUi.Amber;
            bannerText.raycastTarget = false;

            var bTextRect = bannerTextObj.GetComponent<RectTransform>();
            bTextRect.anchorMin = Vector2.zero;
            bTextRect.anchorMax = Vector2.one;
            bTextRect.offsetMin = new Vector2(20f, 4f);
            bTextRect.offsetMax = new Vector2(-20f, -4f);

            bannerPanel.SetActive(false);

            // 5. Постоянный трекер текущей задачи (в левом верхнем углу)
            objectivePanel = new GameObject("ObjectivePanel");
            objectivePanel.transform.SetParent(canvasObj.transform, false);
            var objImg = objectivePanel.AddComponent<Image>();
            objImg.color = new Color(0.04f, 0.07f, 0.11f, 0.88f);
            objImg.raycastTarget = false;

            var objRect = objectivePanel.GetComponent<RectTransform>();
            objRect.anchorMin = new Vector2(0f, 1f);
            objRect.anchorMax = new Vector2(0f, 1f);
            objRect.pivot = new Vector2(0f, 1f);
            objRect.anchoredPosition = new Vector2(28f, -28f);
            objRect.sizeDelta = new Vector2(460f, 142f);

            GameObject objTextObj = new GameObject("ObjectiveText");
            objTextObj.transform.SetParent(objectivePanel.transform, false);
            objectiveText = objTextObj.AddComponent<Text>();
            if (standardFont != null) objectiveText.font = standardFont;
            objectiveText.fontSize = 18;
            objectiveText.alignment = TextAnchor.UpperLeft;
            objectiveText.color = new Color(0.92f, 0.94f, 0.97f);
            objectiveText.raycastTarget = false;
            objectiveText.lineSpacing = 1.15f;

            var objTextRect = objTextObj.GetComponent<RectTransform>();
            objTextRect.anchorMin = Vector2.zero;
            objTextRect.anchorMax = Vector2.one;
            objTextRect.offsetMin = new Vector2(16f, 10f);
            objTextRect.offsetMax = new Vector2(-16f, -10f);

            objectivePanel.SetActive(false);

            // 6. Подсказка действий с удерживаемым предметом (внизу по центру)
            heldHintPanel = new GameObject("HeldHintPanel");
            heldHintPanel.transform.SetParent(canvasObj.transform, false);
            var hintImg = heldHintPanel.AddComponent<Image>();
            hintImg.color = new Color(0.04f, 0.07f, 0.12f, 0.88f);
            hintImg.raycastTarget = false;

            var hintRect = heldHintPanel.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 96f);
            hintRect.sizeDelta = new Vector2(580f, 44f);

            GameObject hintTextObj = new GameObject("HeldHintText");
            hintTextObj.transform.SetParent(heldHintPanel.transform, false);
            heldHintText = hintTextObj.AddComponent<Text>();
            if (standardFont != null) heldHintText.font = standardFont;
            heldHintText.fontSize = 15;
            heldHintText.fontStyle = FontStyle.Bold;
            heldHintText.alignment = TextAnchor.MiddleCenter;
            heldHintText.color = LowPolyUi.Amber;
            heldHintText.raycastTarget = false;

            var hTextRect = hintTextObj.GetComponent<RectTransform>();
            hTextRect.anchorMin = Vector2.zero;
            hTextRect.anchorMax = Vector2.one;
            hTextRect.offsetMin = new Vector2(16f, 4f);
            hTextRect.offsetMax = new Vector2(-16f, -4f);

            heldHintPanel.SetActive(false);
        }
    }
}
