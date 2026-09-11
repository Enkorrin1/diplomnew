using System;
using System.Collections.Generic;
using RogueDrive.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Narrative
{
    /// <summary>
    /// Тактическая радиосвязь с Цитаделью / диспетчером «Маяк».
    /// Переведена на современный UGUI Canvas (полностью без OnGUI).
    /// </summary>
    public sealed class RadioTransmissionSystem : MonoBehaviour
    {
        private static RadioTransmissionSystem _instance;
        public static RadioTransmissionSystem Instance => _instance;

        public sealed class TransmissionEntry
        {
            public string Speaker;
            public string Text;
            public float Duration;
            public Action OnCompleted;

            public TransmissionEntry(string speaker, string text, float duration = 6f, Action onCompleted = null)
            {
                Speaker = speaker;
                Text = text;
                Duration = duration;
                OnCompleted = onCompleted;
            }
        }

        [Header("Settings")]
        [SerializeField] private float charsPerSecond = 35f;
        [SerializeField] private Color textColor = new Color(0.2f, 1.0f, 0.4f, 0.95f); // Фосфорный зеленый CRT
        [SerializeField] private Color headerColor = new Color(1.0f, 0.85f, 0.2f, 1.0f); // Тактический желтый

        private readonly Queue<TransmissionEntry> queue = new Queue<TransmissionEntry>();
        private TransmissionEntry currentEntry;
        private bool isTransmitting;
        private float displayedCharCount;
        private float displayTimer;

        // UGUI Elements
        private Canvas radioCanvas;
        private GameObject radioBoxPanel;
        private Text headerText;
        private Text messageText;
        private Text hintText;

        public bool IsTransmitting => isTransmitting;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUIIfNeeded();
        }

        public void EnqueueTransmission(string speaker, string text, float duration = 6f, Action onCompleted = null)
        {
            queue.Enqueue(new TransmissionEntry(speaker, text, duration, onCompleted));
            if (!isTransmitting)
            {
                StartNextTransmission();
            }
        }

        private void StartNextTransmission()
        {
            if (queue.Count == 0)
            {
                isTransmitting = false;
                if (radioBoxPanel != null) radioBoxPanel.SetActive(false);
                return;
            }

            currentEntry = queue.Dequeue();
            isTransmitting = true;
            displayedCharCount = 0f;
            displayTimer = currentEntry.Duration;

            if (radioBoxPanel != null) radioBoxPanel.SetActive(true);
            PlayRadioChirp();
        }

        private void PlayRadioChirp()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySwitchClick();
            }
        }

        private void Update()
        {
            if (!isTransmitting)
            {
                if (radioBoxPanel != null && radioBoxPanel.activeSelf)
                {
                    radioBoxPanel.SetActive(false);
                }
                return;
            }

            // Посимвольный вывод
            if (displayedCharCount < currentEntry.Text.Length)
            {
                displayedCharCount += charsPerSecond * Time.unscaledDeltaTime;
                if (displayedCharCount > currentEntry.Text.Length)
                {
                    displayedCharCount = currentEntry.Text.Length;
                }
            }
            else
            {
                displayTimer -= Time.unscaledDeltaTime;
                if (displayTimer <= 0f)
                {
                    CompleteCurrentTransmission();
                    return;
                }
            }

            // Пропуск по клавише пробел или Enter
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (displayedCharCount < currentEntry.Text.Length)
                {
                    displayedCharCount = currentEntry.Text.Length;
                }
                else
                {
                    CompleteCurrentTransmission();
                    return;
                }
            }

            // Обновление UI
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (radioBoxPanel == null || currentEntry == null) return;

            if (!radioBoxPanel.activeSelf) radioBoxPanel.SetActive(true);

            bool blink = Mathf.PingPong(Time.unscaledTime * 2.5f, 1f) > 0.35f;
            string liveIndicator = blink ? "● ПРИЕМ СИГНАЛА" : "○ ПРИЕМ СИГНАЛА";

            if (headerText != null)
            {
                headerText.text = $"{liveIndicator} | {currentEntry.Speaker.ToUpper()}";
            }

            if (messageText != null)
            {
                int charCount = Mathf.Clamp(Mathf.FloorToInt(displayedCharCount), 0, currentEntry.Text.Length);
                messageText.text = currentEntry.Text.Substring(0, charCount);
            }
        }

        private void CompleteCurrentTransmission()
        {
            currentEntry.OnCompleted?.Invoke();
            if (queue.Count > 0)
            {
                StartNextTransmission();
            }
            else
            {
                isTransmitting = false;
                if (radioBoxPanel != null) radioBoxPanel.SetActive(false);
            }
        }

        #region Сюжетные реплики пролога и аванпостов

        public void PlayBunkerWakeup()
        {
            EnqueueTransmission(
                "МАЯК // ЦИТАДЕЛЬ",
                "Внимание всем выжившим в Секторе 07! Протокол 'Закат' приведен в действие. Шлюзы Цитадели будут запечатаны! Повторяю: запечатаны!",
                7.0f
            );
        }

        public void PlayGeneratorOnline()
        {
            EnqueueTransmission(
                "МАЯК // ЦИТАДЕЛЬ",
                "Энергосеть убежища активна. Путь свободен: откройте распашные броневорота и выезжайте на магистраль Сектора 01!",
                6.0f
            );
        }

        public void PlayOutpost1Reached()
        {
            EnqueueTransmission(
                "МАЯК // ЦИТАДЕЛЬ",
                "Скиталец, ты добрался до передовой СТО! На подъемнике стоит пикап 'Следопыт'. Отремонтируй его, чтобы забрать в гараж!",
                7.0f
            );
        }

        public void PlayOutpost2Reached()
        {
            EnqueueTransmission(
                "МАЯК // ЦИТАДЕЛЬ",
                "Ты на складе рейдеров Пустоши. Здесь брошен тяжелый броневик 'Бастион'. Запусти генератор и завари броню!",
                7.0f
            );
        }

        public void PlayOutpost3Reached()
        {
            EnqueueTransmission(
                "МАЯК // ЦИТАДЕЛЬ",
                "Химзавод 'Спектр'. В ангаре заблокирован прототип спорткара 'Фантом'. Продуй турбины и сними блокировку ЭБУ!",
                7.0f
            );
        }

        public void PlayBossEncounter()
        {
            EnqueueTransmission(
                "МАЯК // ЦИТАДЕЛЬ",
                "ТРЕВОГА! Сканеры фиксируют гигантский бронепоезд-таран рейдеров 'Джаггернаут'! Он закрывает въезд в Цитадель! Уничтожь его!",
                8.0f
            );
        }

        public void PlayCitadelVictory()
        {
            EnqueueTransmission(
                "МАЯК // ЦИТАДЕЛЬ",
                "'Джаггернаут' уничтожен! Шлюз открыт, заезжай скорее! Закрываем шлюз... Запечатано! Ты успел, Скиталец. Добро пожаловать в Цитадель!",
                10f
            );
        }

        public void PlayOutpostArrival(int stageIndex)
        {
            if (stageIndex == 1) PlayOutpost1Reached();
            else if (stageIndex == 2) PlayOutpost2Reached();
            else if (stageIndex == 3) PlayOutpost3Reached();
            else PlayOutpost1Reached();
        }

        #endregion

        #region UGUI Canvas Construction

        private void BuildUIIfNeeded()
        {
            if (radioCanvas != null) return;

            GameObject canvasObj = new GameObject("Radio_Canvas");
            canvasObj.transform.SetParent(transform, false);

            radioCanvas = canvasObj.AddComponent<Canvas>();
            radioCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            radioCanvas.sortingOrder = 500;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? 
                               Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Фоновая тактическая панель
            radioBoxPanel = new GameObject("RadioBoxPanel");
            radioBoxPanel.transform.SetParent(canvasObj.transform, false);

            var bgImg = radioBoxPanel.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.06f, 0.04f, 0.94f);
            bgImg.raycastTarget = false;

            var panelRect = radioBoxPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -24f);
            panelRect.sizeDelta = new Vector2(960f, 115f);

            // Верхняя зеленая полоса-рамка
            GameObject topBorder = new GameObject("TopBorder");
            topBorder.transform.SetParent(radioBoxPanel.transform, false);
            var topImg = topBorder.AddComponent<Image>();
            topImg.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            topImg.raycastTarget = false;
            var tRect = topBorder.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 1f);
            tRect.anchorMax = new Vector2(1f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.sizeDelta = new Vector2(0f, 2.5f);

            // Нижняя зеленая полоса-рамка
            GameObject btmBorder = new GameObject("BottomBorder");
            btmBorder.transform.SetParent(radioBoxPanel.transform, false);
            var btmImg = btmBorder.AddComponent<Image>();
            btmImg.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            btmImg.raycastTarget = false;
            var bRect = btmBorder.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 0f);
            bRect.anchorMax = new Vector2(1f, 0f);
            bRect.pivot = new Vector2(0.5f, 0f);
            bRect.sizeDelta = new Vector2(0f, 2.5f);

            // Заголовок (Позывной)
            GameObject headerObj = new GameObject("HeaderText");
            headerObj.transform.SetParent(radioBoxPanel.transform, false);
            headerText = headerObj.AddComponent<Text>();
            if (standardFont != null) headerText.font = standardFont;
            headerText.fontSize = 15;
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = headerColor;
            headerText.alignment = TextAnchor.MiddleLeft;
            headerText.raycastTarget = false;
            var hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0f, 1f);
            hRect.anchoredPosition = new Vector2(18f, -14f);
            hRect.sizeDelta = new Vector2(-36f, 22f);

            // Текст радиограммы
            GameObject msgObj = new GameObject("MessageText");
            msgObj.transform.SetParent(radioBoxPanel.transform, false);
            messageText = msgObj.AddComponent<Text>();
            if (standardFont != null) messageText.font = standardFont;
            messageText.fontSize = 17;
            messageText.fontStyle = FontStyle.Bold;
            messageText.color = textColor;
            messageText.alignment = TextAnchor.UpperLeft;
            messageText.raycastTarget = false;
            var mRect = msgObj.GetComponent<RectTransform>();
            mRect.anchorMin = Vector2.zero;
            mRect.anchorMax = Vector2.one;
            mRect.offsetMin = new Vector2(18f, 18f);
            mRect.offsetMax = new Vector2(-18f, -38f);

            // Подсказка о пропуске
            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(radioBoxPanel.transform, false);
            hintText = hintObj.AddComponent<Text>();
            if (standardFont != null) hintText.font = standardFont;
            hintText.fontSize = 11;
            hintText.color = new Color(1f, 1f, 1f, 0.45f);
            hintText.text = "[Пробел / Enter] Пропустить";
            hintText.alignment = TextAnchor.LowerRight;
            hintText.raycastTarget = false;
            var htRect = hintObj.GetComponent<RectTransform>();
            htRect.anchorMin = new Vector2(1f, 0f);
            htRect.anchorMax = new Vector2(1f, 0f);
            htRect.pivot = new Vector2(1f, 0f);
            htRect.anchoredPosition = new Vector2(-18f, 6f);
            htRect.sizeDelta = new Vector2(200f, 18f);

            radioBoxPanel.SetActive(false);
        }

        #endregion
    }
}

