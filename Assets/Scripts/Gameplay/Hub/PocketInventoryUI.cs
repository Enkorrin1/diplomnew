using System.Collections.Generic;
using UnityEngine;
using RogueDrive.UI;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Нижний HUD карманного инвентаря на 5 слотов [1] [2] [3] [4] [5].
    /// Выполнен в low-poly постапокалиптическом стиле с подсветкой активного слота
    /// и отображением названий мелких предметов (ключи, инструменты, расходники).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PocketInventoryUI : MonoBehaviour
    {
        public static PocketInventoryUI Instance { get; private set; }

        private struct SlotView
        {
            public GameObject root;
            public Image background;
            public Image border;
            public Text keyBadge;
            public Text nameText;
            public Text countBadge;
            public Image iconImage;
        }

        [Header("Canvas & Layout")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform containerRect;

        private readonly List<SlotView> slotViews = new List<SlotView>(5);

        private readonly Color normalBgColor = LowPolyUi.Ink;
        private readonly Color activeBgColor = LowPolyUi.Surface;
        private readonly Color normalBorderColor = LowPolyUi.Border;
        private readonly Color activeBorderColor = LowPolyUi.Amber;
        private readonly Color keyBadgeColor = LowPolyUi.Muted;
        private readonly Color activeKeyBadgeColor = LowPolyUi.Paper;

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

        private void Start()
        {
            BindInventoryEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnbindInventoryEvents();
        }

        private void BindInventoryEvents()
        {
            var inv = PlayerPocketInventory.Instance;
            if (inv == null) return;

            inv.OnSlotUpdated += UpdateSlotView;
            inv.OnActiveSlotChanged += UpdateActiveSlot;

            UpdateActiveSlot(inv.ActiveSlotIndex);
            for (int i = 0; i < PlayerPocketInventory.SlotCount; i++)
            {
                UpdateSlotView(i, inv.GetSlot(i));
            }
        }

        private void UnbindInventoryEvents()
        {
            var inv = PlayerPocketInventory.Instance;
            if (inv == null) return;

            inv.OnSlotUpdated -= UpdateSlotView;
            inv.OnActiveSlotChanged -= UpdateActiveSlot;
        }

        public void UpdateActiveSlot(int activeIndex)
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                bool isActive = (i == activeIndex);
                SlotView sv = slotViews[i];

                if (sv.border != null)
                {
                    sv.border.color = isActive ? activeBorderColor : normalBorderColor;
                    Sprite frameSprite = LowPolyUi.Sprite("panel");
                    if (frameSprite != null)
                    {
                        sv.border.sprite = frameSprite;
                        sv.border.type = Image.Type.Sliced;
                    }
                }
                if (sv.background != null)
                {
                    sv.background.color = isActive ? activeBgColor : normalBgColor;
                }
                if (sv.keyBadge != null)
                {
                    sv.keyBadge.color = isActive ? activeKeyBadgeColor : keyBadgeColor;
                }
            }
        }

        public void UpdateSlotView(int index, PocketSlotData data)
        {
            if (index < 0 || index >= slotViews.Count) return;

            SlotView sv = slotViews[index];

            if (data.IsEmpty)
            {
                if (sv.nameText != null) sv.nameText.text = "";
                if (sv.countBadge != null) sv.countBadge.text = "";
                if (sv.iconImage != null) sv.iconImage.gameObject.SetActive(false);
            }
            else
            {
                if (sv.nameText != null) sv.nameText.text = data.displayName;
                if (sv.countBadge != null)
                {
                    sv.countBadge.text = data.count > 1 ? $"x{data.count}" : "";
                }
                if (sv.iconImage != null)
                {
                    Sprite iconToUse = LowPolyUi.ItemSprite(data);
                    if (iconToUse == null)
                    {
                        if (data.id.Contains("axe")) iconToUse = LoadSprite("Icons/icon_axe.png");
                        else if (data.id.Contains("fuel")) iconToUse = LoadSprite("Icons/icon_fuel_canister.png");
                        else if (data.id.Contains("water")) iconToUse = LoadSprite("Icons/icon_water_canister.png");
                        else if (data.id.Contains("battery")) iconToUse = LoadSprite("Icons/icon_battery.png");
                        else if (data.id.Contains("wheel")) iconToUse = LoadSprite("Icons/icon_wheel.png");
                    }

                    if (iconToUse != null)
                    {
                        sv.iconImage.sprite = iconToUse;
                        sv.iconImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        sv.iconImage.gameObject.SetActive(false);
                    }
                }
            }
        }

        private static Sprite LoadSprite(string path)
        {
            string cleanName = System.IO.Path.GetFileNameWithoutExtension(path);
            var s = LowPolyUi.Sprite(cleanName);
            if (s == null) s = Resources.Load<Sprite>("UI/" + cleanName);
            if (s != null) return s;

#if UNITY_EDITOR
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/" + path);
            if (s != null) return s;
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/" + cleanName + ".png");
            if (s != null) return s;
#endif
            return null;
        }

        private void BuildUIIfNeeded()
        {
            if (canvas != null) return;

            // 1. Создаем Canvas ScreenSpaceOverlay
            GameObject canvasObj = new GameObject("PocketInventory_Canvas");
            canvasObj.transform.SetParent(transform, false);

            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                               Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 2. Корневая панель хотбара (внизу по центру)
            GameObject barObj = new GameObject("HotbarContainer");
            barObj.transform.SetParent(canvasObj.transform, false);
            containerRect = barObj.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0f);
            containerRect.anchorMax = new Vector2(0.5f, 0f);
            containerRect.pivot = new Vector2(0.5f, 0f);
            containerRect.anchoredPosition = new Vector2(0f, 22f);

            const int slotCount = 5;
            const float slotWidth = 116f;
            const float slotHeight = 72f;
            const float spacing = 10f;
            float totalWidth = slotCount * slotWidth + (slotCount - 1) * spacing;
            containerRect.sizeDelta = new Vector2(totalWidth, slotHeight);

            var inventoryHint = LowPolyUi.Label(containerRect, "InventoryHint", "TAB / I  —  ИНВЕНТАРЬ", new Vector2(0, 25), new Vector2(totalWidth, 22), 14, LowPolyUi.Muted);
            inventoryHint.alignment = TextAnchor.MiddleCenter;
            slotViews.Clear();

            // 3. Создаем 5 ячеек
            float startX = -totalWidth * 0.5f + slotWidth * 0.5f;

            for (int i = 0; i < slotCount; i++)
            {
                GameObject slotObj = new GameObject($"Slot_{i + 1}");
                slotObj.transform.SetParent(containerRect, false);

                var slotRect = slotObj.AddComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0.5f, 0.5f);
                slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotRect.pivot = new Vector2(0.5f, 0.5f);
                slotRect.sizeDelta = new Vector2(slotWidth, slotHeight);
                slotRect.anchoredPosition = new Vector2(startX + i * (slotWidth + spacing), 0f);

                // Внешняя рамка (Border)
                var borderImg = slotObj.AddComponent<Image>();
                borderImg.color = normalBorderColor;
                borderImg.raycastTarget = false;

                // Внутренний фон (Background)
                GameObject bgObj = new GameObject("Background");
                bgObj.transform.SetParent(slotObj.transform, false);
                var bgRect = bgObj.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = new Vector2(2f, 2f);
                bgRect.offsetMax = new Vector2(-2f, -2f);

                var bgImg = bgObj.AddComponent<Image>();
                bgImg.color = normalBgColor;
                bgImg.raycastTarget = false;

                // Бейдж номера клавиши [1]..[5] в верхнем левом углу
                GameObject keyObj = new GameObject("KeyBadge");
                keyObj.transform.SetParent(bgObj.transform, false);
                var keyRect = keyObj.AddComponent<RectTransform>();
                keyRect.anchorMin = new Vector2(0f, 1f);
                keyRect.anchorMax = new Vector2(0f, 1f);
                keyRect.pivot = new Vector2(0f, 1f);
                keyRect.anchoredPosition = new Vector2(4f, -3f);
                keyRect.sizeDelta = new Vector2(24f, 16f);

                var keyText = keyObj.AddComponent<Text>();
                if (standardFont != null) keyText.font = standardFont;
                keyText.fontSize = 12;
                keyText.fontStyle = FontStyle.Bold;
                keyText.alignment = TextAnchor.UpperLeft;
                keyText.text = $"[{i + 1}]";
                keyText.color = keyBadgeColor;
                keyText.raycastTarget = false;

                // Иконка предмета (по центру)
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(bgObj.transform, false);
                var iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(34f, 34f);
                iconRect.anchoredPosition = new Vector2(0f, 9f);
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconObj.SetActive(false);

                // Название предмета (по центру/внизу)
                GameObject nameObj = new GameObject("ItemName");
                nameObj.transform.SetParent(bgObj.transform, false);
                var nameRect = nameObj.AddComponent<RectTransform>();
                nameRect.anchorMin = Vector2.zero;
                nameRect.anchorMax = Vector2.one;
                nameRect.offsetMin = new Vector2(3f, 4f);
                nameRect.offsetMax = new Vector2(-3f, -48f);

                var nameText = nameObj.AddComponent<Text>();
                if (standardFont != null) nameText.font = standardFont;
                nameText.fontSize = 12;
                nameText.fontStyle = FontStyle.Normal;
                nameText.alignment = TextAnchor.MiddleCenter;
                nameText.text = "";
                nameText.color = new Color(0.9f, 0.95f, 1.0f);
                nameText.raycastTarget = false;

                // Количество xN в нижнем правом углу
                GameObject countObj = new GameObject("CountBadge");
                countObj.transform.SetParent(bgObj.transform, false);
                var countRect = countObj.AddComponent<RectTransform>();
                countRect.anchorMin = new Vector2(1f, 1f);
                countRect.anchorMax = new Vector2(1f, 1f);
                countRect.pivot = new Vector2(1f, 1f);
                countRect.anchoredPosition = new Vector2(-4f, -3f);
                countRect.sizeDelta = new Vector2(28f, 14f);

                var countText = countObj.AddComponent<Text>();
                if (standardFont != null) countText.font = standardFont;
                countText.fontSize = 10;
                countText.fontStyle = FontStyle.Bold;
                countText.alignment = TextAnchor.LowerRight;
                countText.text = "";
                countText.color = LowPolyUi.Amber;
                countText.raycastTarget = false;

                slotViews.Add(new SlotView
                {
                    root = slotObj,
                    background = bgImg,
                    border = borderImg,
                    keyBadge = keyText,
                    nameText = nameText,
                    countBadge = countText,
                    iconImage = iconImg
                });
            }
        }
    }
}

