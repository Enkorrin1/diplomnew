using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    // The authored settings panel supplies the project's fonts and controls; this view groups them by purpose.
    public sealed class GraphicsSettingsPanel : MonoBehaviour
    {
        private static readonly string[] SectionNames = { "ЭКРАН", "ГРАФИКА", "ЗВУК", "УПРАВЛЕНИЕ" };
        private static readonly string[] OptionLabels =
        {
            "КАЧЕСТВО", "РЕЖИМ ЭКРАНА", "РАЗРЕШЕНИЕ", "СГЛАЖИВАНИЕ",
            "ТЕНИ", "ДАЛЬНОСТЬ ТЕНЕЙ", "ТЕКСТУРЫ", "ДЕТАЛИЗАЦИЯ",
            "ВЕРТИК. СИНХР.", "ПОСТЭФФЕКТЫ"
        };
        private static readonly int[][] SectionOptions =
        {
            new[] { 1, 2, 8 },
            new[] { 0, 3, 4, 5, 6, 7, 9 }
        };
        private static GraphicsSettingsPanel openPanel;
        private static int lastClosedFrame = -1;

        private readonly Text[] values = new Text[GraphicsQualitySettings.OptionCount];
        private readonly GameObject[] sections = new GameObject[4];
        private readonly Button[] tabs = new Button[4];
        private readonly Slider[] savedSliders = new Slider[4];
        private Action onClosed;
        private int selectedSection;

        public bool IsOpen => gameObject.activeSelf;

        public static GraphicsSettingsPanel Create(GameObject settingsPanel, Action onClosed)
        {
            if (settingsPanel == null) return null;
            Transform labelTemplate = settingsPanel.transform.Find("steeringSliderLabel");
            Transform buttonTemplate = settingsPanel.transform.Find("Save");
            if (labelTemplate == null || buttonTemplate == null) return null;

            GameObject overlay = new GameObject("SettingsSectionsOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(settingsPanel.transform.parent, false);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, .78f);
            overlay.SetActive(false);

            GraphicsSettingsPanel view = overlay.AddComponent<GraphicsSettingsPanel>();
            view.onClosed = onClosed;
            GameObject panel = new GameObject("SettingsSectionsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
            panelRect.pivot = new Vector2(.5f, .5f);
            panelRect.sizeDelta = new Vector2(790f, 610f);
            panelRect.anchoredPosition = Vector2.zero;
            Image panelImage = panel.GetComponent<Image>();
            Image source = settingsPanel.transform.Find("LowPoly_ModalSurface")?.GetComponent<Image>();
            if (source == null) source = settingsPanel.GetComponent<Image>();
            if (source != null && source.sprite != null)
            {
                panelImage.sprite = source.sprite;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = source.color;
            }
            else panelImage.color = new Color(.10f, .15f, .18f, 1f);

            Text title = CreateText(labelTemplate, panel.transform, "SettingsTitle", "НАСТРОЙКИ", 34f, -17f, 460f, 46f, 28, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            CreateText(labelTemplate, panel.transform, "ApplyHint", "ИЗМЕНЕНИЯ ПРИМЕНЯЮТСЯ СРАЗУ", 508f, -20f, 245f, 40f, 13, TextAnchor.MiddleRight);

            for (int i = 0; i < view.tabs.Length; i++)
            {
                int section = i;
                view.tabs[i] = CreateButton(buttonTemplate, panel.transform, "SettingsTab" + i, SectionNames[i], 34f + i * 184f, -78f, 170f, 40f, 16);
                view.tabs[i].onClick.AddListener(() => view.ShowSection(section));
                view.sections[i] = new GameObject(SectionNames[i] + "Section", typeof(RectTransform));
                view.sections[i].transform.SetParent(panel.transform, false);
                RectTransform rect = view.sections[i].GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }

            view.BuildGraphicRows(labelTemplate, buttonTemplate);
            view.BuildSliderRows(settingsPanel, labelTemplate, view.sections[2].transform,
                new[] { "masterSlider", "musicSlider", "effectsSlider" },
                new[] { "ОБЩАЯ ГРОМКОСТЬ", "МУЗЫКА", "ЗВУКОВЫЕ ЭФФЕКТЫ" },
                new[] { "MasterVolume", "MusicVolume", "SfxVolume" }, 0);
            view.BuildSliderRows(settingsPanel, labelTemplate, view.sections[3].transform,
                new[] { "steeringSlider" }, new[] { "ЧУВСТВИТЕЛЬНОСТЬ РУЛЯ" },
                new[] { "SteerSensitivity" }, 3);
            CreateText(labelTemplate, view.sections[3].transform, "ControlsHint",
                "Управление автомобилем зависит от выбранного устройства ввода.",
                36f, -248f, 710f, 40f, 15, TextAnchor.MiddleLeft);

            Button done = CreateButton(buttonTemplate, panel.transform, "SettingsDone", "ГОТОВО", 278f, -549f, 235f, 48f, 18);
            done.onClick.AddListener(view.Close);
            view.ShowSection(0);
            return view;
        }

        public void Open()
        {
            RefreshValues();
            RefreshSliders();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openPanel = this;
            ShowSection(selectedSection);
        }

        public void Close()
        {
            if (!IsOpen) return;
            PlayerPrefs.Save();
            gameObject.SetActive(false);
            lastClosedFrame = Time.frameCount;
            if (openPanel == this) openPanel = null;
            onClosed?.Invoke();
        }

        public static bool TryCloseOpen()
        {
            if (openPanel == null || !openPanel.IsOpen) return lastClosedFrame == Time.frameCount;
            openPanel.Close();
            return true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && (PauseMenuUI.Instance == null || !PauseMenuUI.Instance.IsPaused))
                Close();
        }

        private void ShowSection(int index)
        {
            selectedSection = Mathf.Clamp(index, 0, sections.Length - 1);
            for (int i = 0; i < sections.Length; i++)
            {
                sections[i].SetActive(i == selectedSection);
                LowPolyUi.StyleButton(tabs[i], i == selectedSection);
            }
            if (EventSystem.current != null && IsOpen)
                EventSystem.current.SetSelectedGameObject(tabs[selectedSection].gameObject);
        }

        private void BuildGraphicRows(Transform labelTemplate, Transform buttonTemplate)
        {
            for (int section = 0; section < SectionOptions.Length; section++)
            {
                int[] options = SectionOptions[section];
                for (int row = 0; row < options.Length; row++)
                {
                    int option = options[row];
                    float y = -156f - row * 55f;
                    Transform parent = sections[section].transform;
                    CreateText(labelTemplate, parent, "OptionLabel" + option, OptionLabels[option], 36f, y, 327f, 35f, 18, TextAnchor.MiddleLeft);
                    Button previous = CreateButton(buttonTemplate, parent, "Previous" + option, "<", 372f, y, 38f, 35f, 23);
                    previous.onClick.AddListener(() => { GraphicsQualitySettings.Change(option, -1); RefreshValues(); });
                    values[option] = CreateText(labelTemplate, parent, "OptionValue" + option, "", 416f, y, 292f, 35f, 18, TextAnchor.MiddleCenter);
                    Button next = CreateButton(buttonTemplate, parent, "Next" + option, ">", 716f, y, 38f, 35f, 23);
                    next.onClick.AddListener(() => { GraphicsQualitySettings.Change(option, 1); RefreshValues(); });
                }
            }
        }

        private void BuildSliderRows(GameObject settingsPanel, Transform labelTemplate, Transform parent,
            string[] sliderNames, string[] labels, string[] keys, int startIndex)
        {
            for (int i = 0; i < sliderNames.Length; i++)
            {
                Slider template = settingsPanel.transform.Find(sliderNames[i])?.GetComponent<Slider>();
                if (template == null) continue;
                int sliderIndex = startIndex + i;
                string key = keys[i];
                float y = -162f - i * 72f;
                CreateText(labelTemplate, parent, "SliderLabel" + sliderIndex, labels[i], 36f, y, 330f, 38f, 18, TextAnchor.MiddleLeft);
                Slider slider = Instantiate(template, parent);
                slider.name = "SettingsSlider" + sliderIndex;
                RectTransform rect = slider.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(382f, y + 2f);
                rect.sizeDelta = new Vector2(280f, 34f);
                slider.onValueChanged = new Slider.SliderEvent();
                savedSliders[sliderIndex] = slider;
                Text value = CreateText(labelTemplate, parent, "SliderValue" + sliderIndex, "", 680f, y, 74f, 38f, 17, TextAnchor.MiddleRight);
                slider.onValueChanged.AddListener(v =>
                {
                    PlayerPrefs.SetFloat(key, v);
                    if (key == "MasterVolume") AudioListener.volume = v;
                    if (key == "SteerSensitivity" && PauseMenuUI.Instance != null) PauseMenuUI.Instance.ReloadSettings();
                    value.text = key == "SteerSensitivity" ? v.ToString("0.0") + "×" : v.ToString("P0");
                });
            }
        }

        private void RefreshValues()
        {
            for (int i = 0; i < values.Length; i++)
                if (values[i] != null) values[i].text = GraphicsQualitySettings.GetValue(i);
        }

        private void RefreshSliders()
        {
            string[] keys = { "MasterVolume", "MusicVolume", "SfxVolume", "SteerSensitivity" };
            float[] defaults = { .85f, .75f, .9f, 1f };
            for (int i = 0; i < savedSliders.Length; i++)
            {
                if (savedSliders[i] == null) continue;
                float value = PlayerPrefs.GetFloat(keys[i], defaults[i]);
                savedSliders[i].SetValueWithoutNotify(value);
                Text label = savedSliders[i].transform.parent.Find("SliderValue" + i)?.GetComponent<Text>();
                if (label != null) label.text = i == 3 ? value.ToString("0.0") + "×" : value.ToString("P0");
            }
        }

        private static Text CreateText(Transform template, Transform parent, string name, string content,
            float x, float y, float width, float height, int fontSize, TextAnchor alignment)
        {
            Transform copy = Instantiate(template, parent);
            copy.name = name;
            RectTransform rect = copy as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            Text label = copy.GetComponent<Text>();
            label.text = content;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform template, Transform parent, string name, string caption,
            float x, float y, float width, float height, int fontSize)
        {
            Transform copy = Instantiate(template, parent);
            copy.name = name;
            RectTransform rect = copy as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            Button button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            Text text = copy.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = caption;
                text.fontSize = fontSize;
                text.alignment = TextAnchor.MiddleCenter;
                RectTransform textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            }
            return button;
        }
    }
}
