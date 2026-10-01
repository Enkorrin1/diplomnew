using RogueDrive.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивная настенная карта эвакуации бункера (Bunker Evacuation Plan):
    /// Позволяет игроку изучить стратегический маршрут экспедиции через 4 сектора
    /// к спасительному шлюзу Цитадели «Врата-01». Реализует IGarageInteractable.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BunkerEvacuationMap : MonoBehaviour, IGarageInteractable
    {
        public static BunkerEvacuationMap Instance { get; private set; }

        private Canvas mapCanvas;
        private GameObject mapPanel;
        private bool isMapOpen = false;

        public bool IsOpen => isMapOpen;

        private void Awake()
        {
            Instance = this;
            EnsureCollider();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (mapCanvas != null) Destroy(mapCanvas.gameObject);
        }

        private void Update()
        {
            if (!isMapOpen) return;

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMap();
            }
        }

        private void EnsureCollider()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(1.2f, 0.9f, 0.2f);
            }
        }

        public string GetPromptText()
        {
            return "[E] Изучить карту: План «Протокол Закат»";
        }

        public bool CanInteract()
        {
            return !isMapOpen;
        }

        public void Interact(GaragePlayerController player)
        {
            OpenMap();
        }

        public void OpenMap()
        {
            if (isMapOpen) return;
            isMapOpen = true;

            EnsureUI();
            if (mapPanel != null) mapPanel.SetActive(true);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySwitchClick();
            }

            var player = FindFirstObjectByType<GaragePlayerController>();
            if (player != null)
            {
                player.LockCursor(false);
            }
        }

        public void CloseMap()
        {
            if (!isMapOpen) return;
            isMapOpen = false;

            if (mapPanel != null) mapPanel.SetActive(false);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySwitchClick();
            }

            var player = FindFirstObjectByType<GaragePlayerController>();
            if (player != null && !player.IsMovementLocked)
            {
                player.LockCursor(true);
            }
        }

        private void EnsureUI()
        {
            if (mapCanvas != null) return;

            GameObject canvasGo = new GameObject("EvacuationMap_Canvas");
            DontDestroyOnLoad(canvasGo);
            mapCanvas = canvasGo.AddComponent<Canvas>();
            mapCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mapCanvas.sortingOrder = 850;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Полупрозрачный фон-затемнение
            mapPanel = new GameObject("MapPanel");
            mapPanel.transform.SetParent(canvasGo.transform, false);
            var bgImg = mapPanel.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);

            var panelRect = mapPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1040f, 680f);

            // Рамка панели
            var outline = mapPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.55f, 0.15f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Заголовок
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(mapPanel.transform, false);
            var title = titleObj.AddComponent<Text>();
            title.font = font;
            title.fontSize = 24;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.75f, 0.25f);
            title.text = "КАРТА ЭВАКУАЦИИ: ПЛАН «ПРОТОКОЛ ЗАКАТ»\n<size=15><color=#AAAAAA>МАРШРУТ ПРОРЫВА К ЦИТАДЕЛИ ЧЕРЕЗ 4 СЕКТОРА</color></size>";

            var tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 1f);
            tRect.anchorMax = new Vector2(1f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -20f);
            tRect.sizeDelta = new Vector2(0f, 65f);

            // Текст секторов
            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(mapPanel.transform, false);
            var body = bodyObj.AddComponent<Text>();
            body.font = font;
            body.fontSize = 16;
            body.lineSpacing = 1.35f;
            body.alignment = TextAnchor.UpperLeft;
            body.color = new Color(0.92f, 0.94f, 0.96f);
            body.text =
                "<b>[ТЕКУЩАЯ ТОЧКА: БУНКЕР 07]</b> — Автомобиль: Седан «Скиталец»\n\n" +
                "<b>1. СЕКТОР 01: ПРИГОРОДНОЕ ШОССЕ (ЗАКАТ)</b>\n" +
                "   • Бывшие жилые кварталы и автострада. Первая волна мутантов и завалы.\n" +
                "   • <b>СТО №1 «Северная»:</b> укрытие, базовые детали, ремонт и торговля.\n\n" +
                "<b>2. СЕКТОР 02: ПЕСЧАНАЯ ПУСТОШЬ (ДЕНЬ)</b>\n" +
                "   • Палящее солнце, разбитые конвои, развилки дорог.\n" +
                "   • <b>Точка интереса:</b> Неоновое Казино «Фортуна Пустоши» (слот-машина за золотые жетоны).\n" +
                "   • <b>СТО №2 «Каньон»:</b> двигатели, внедорожная подвеска и броня.\n\n" +
                "<b>3. СЕКТОР 03: ТОКСИЧНАЯ ПРОМЗОНА (НОЧЬ)</b>\n" +
                "   • Кислотные испарения, заброшенные заводы, свет фар жизненно необходим.\n" +
                "   • <b>СТО №3 «Рубеж»:</b> продвинутые детали и подготовка к Цитадели.\n\n" +
                "<b>4. СЕКТОР 04: ПОДСТУПЫ К ЦИТАДЕЛИ (ПЫЛЕВОЙ ШТОРМ)</b>\n" +
                "   • Надвигающаяся смертоносная буря. Финальный бой с Тяжелым Джаггернаутом рейдеров.\n" +
                "   • <b>Цель экспедиции:</b> Цитадель. Одна машина на всём пути.\n" +
                "   • На СТО: переждите фронт, подготовьте машину и выезжайте до новой волны.";

            var bRect = bodyObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 0f);
            bRect.anchorMax = new Vector2(1f, 1f);
            bRect.pivot = new Vector2(0.5f, 0.5f);
            bRect.offsetMin = new Vector2(40f, 60f);
            bRect.offsetMax = new Vector2(-40f, -95f);

            // Кнопка закрытия
            GameObject closeObj = new GameObject("CloseHint");
            closeObj.transform.SetParent(mapPanel.transform, false);
            var close = closeObj.AddComponent<Text>();
            close.font = font;
            close.fontSize = 15;
            close.fontStyle = FontStyle.Bold;
            close.alignment = TextAnchor.MiddleCenter;
            close.color = new Color(0.95f, 0.55f, 0.15f);
            close.text = "[E] или [ESC] — ЗАКРЫТЬ КАРТУ";

            var cRect = closeObj.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 0f);
            cRect.anchorMax = new Vector2(1f, 0f);
            cRect.pivot = new Vector2(0.5f, 0f);
            cRect.anchoredPosition = new Vector2(0f, 15f);
            cRect.sizeDelta = new Vector2(0f, 35f);

            mapPanel.SetActive(false);
        }
    }
}
