using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    /// <summary>Shared expedition surfaces and authored-screen presentation, without replacing event bindings.</summary>
    public static class ExpeditionPresentation
    {
        static Sprite surface;
        public static Sprite SurfaceSprite
        {
            get
            {
                if (surface != null) return surface;
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "ExpeditionSurface";
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                        bool cut = x + y < 4 || x + size - 1 - y < 4 || size - 1 - x + y < 4 || 2 * (size - 1) - x - y < 4;
                        float shade = edge == 0 ? 1.65f : edge == 1 ? 1.12f : 1f;
                        pixels[y * size + x] = cut ? Color.clear : new Color(shade, shade, shade, 1);
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                surface = UnityEngine.Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
                surface.name = "ExpeditionSurface";
                return surface;
            }
        }

        public static void Surface(Image image, bool inset = false)
        {
            if (image == null) return;
            image.sprite = SurfaceSprite;
            image.type = Image.Type.Sliced;
            image.color = inset ? LowPolyUi.Surface : LowPolyUi.Ink;
        }

        public static void Place(Transform item, float x, float y, float width, float height)
        {
            if (item == null) return;
            var rect = item as RectTransform;
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void Caption(Transform parent, string name, string value, float x, float y, float width, float height, int size, Color color)
        {
            var item = parent.Find(name);
            var text = item != null ? item.GetComponent<Text>() : LowPolyUi.Label(parent, name, value, Vector2.zero, Vector2.zero, size, color);
            if (text == null) return;
            Place(text.transform, x, y, width, height);
            text.text = value; text.fontSize = size; text.color = color;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        static void Rule(Transform parent, string name, float x, float y, float width, Color color)
        {
            if (parent.Find(name) != null) return;
            var line = LowPolyUi.Rect(parent, name, new Vector2(x, -y), new Vector2(width, 2)).gameObject.AddComponent<Image>();
            line.color = color; line.raycastTarget = false;
        }

        public static void Apply(Transform root)
        {
            foreach (var input in root.GetComponentsInChildren<InputField>(true))
            {
                Surface(input.GetComponent<Image>(), true);
                input.customCaretColor = true; input.caretColor = LowPolyUi.Amber;
                input.selectionColor = new Color(.91f, .71f, .38f, .3f);
                if (input.textComponent != null)
                {
                    input.textComponent.color = LowPolyUi.Paper;
                    input.textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    input.textComponent.fontSize = 20;
                }
                if (input.placeholder is Text placeholder) placeholder.color = LowPolyUi.Muted;
            }
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                // Short instrument labels must not disappear when font metrics exceed their authored height.
                if (text.rectTransform.rect.height > 0 && text.rectTransform.rect.height < text.fontSize * 1.7f)
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                if (text.name == "Subtitle" || text.name.EndsWith("Hint")) text.color = LowPolyUi.Muted;
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                string name = image.name.ToLowerInvariant();
                if (name.Contains("slot") && image.GetComponent<Button>() == null && image.sprite == null) Surface(image, true);
                if (name == "modalblocker") image.color = new Color(.015f, .022f, .027f, .82f);
                if (image.transform.Find("Title") != null && image.transform.Find("HeaderRule") == null && image.rectTransform.rect.width > 350 && !name.Contains("overlay"))
                    Rule(image.transform, "ExpeditionHeaderRule", 28, 76, Mathf.Max(100, image.rectTransform.rect.width - 56), LowPolyUi.Border);
            }
            if (root.gameObject.scene.name == "MainMenuScene") MainMenu(root);
        }

        static void MainMenu(Transform root)
        {
            var panel = root.Find("SafeArea/MainMenu");
            if (panel == null) return;
            Surface(panel.GetComponent<Image>());
            Caption(panel, "Title", "ROGUE DRIVE", 28, 20, 330, 55, 42, LowPolyUi.Paper);
            Caption(panel, "Subtitle", "ОДНА МАШИНА. ПУТЬ СКВОЗЬ БУРЮ.", 28, 76, 326, 32, 15, LowPolyUi.Muted);
            var rule = panel.Find("ExpeditionHeaderRule"); if (rule != null) rule.gameObject.SetActive(false);
            Rule(panel, "ExpeditionMenuRule", 28, 119, 326, LowPolyUi.Border);
            Caption(panel, "ExpeditionEyebrow", "ЭКСПЕДИЦИЯ  /  БУНКЕР 07", 28, 131, 326, 25, 13, LowPolyUi.Amber);
            string[] names = { "Campaign", "StartRun", "Garage", "Coop", "Settings" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = panel.Find(names[i]);
                Place(button, 28, 171 + 67 * i, 326, 53);
                if (button == null) continue;
                var label = button.GetComponentInChildren<Text>(true);
                if (label != null) { Place(label.transform, 16, 2, 294, 49); label.alignment = TextAnchor.MiddleLeft; label.fontSize = 20; }
            }
            var coop = panel.Find("Coop")?.GetComponentInChildren<Text>(true);
            if (coop != null) coop.text = "СОВМЕСТНАЯ ИГРА  /  2";
            Place(panel.Find("About"), 28, 512, 155, 46);
            Place(panel.Find("Quit"), 199, 512, 155, 46);
            Caption(panel, "ExpeditionFooter", "СОБЕРИТЕ МАШИНУ. ОБГОНИТЕ БУРЮ.", 28, 572, 326, 20, 12, LowPolyUi.Muted);
            var rect = panel as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, .5f);
                rect.anchoredPosition = new Vector2(30, 0); rect.sizeDelta = new Vector2(382, 616);
            }
        }

        public static void Lobby(Transform panel)
        {
            if (panel == null) return;
            var rect = panel as RectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(940, 690);
            Surface(panel.GetComponent<Image>());
            var blocker = panel.Find("ModalBlocker") as RectTransform;
            if (blocker != null)
            {
                blocker.anchorMin = blocker.anchorMax = blocker.pivot = Vector2.one * .5f;
                blocker.anchoredPosition = Vector2.zero; blocker.sizeDelta = new Vector2(6000, 6000);
                blocker.GetComponent<Image>().color = new Color(.015f, .022f, .027f, .84f);
            }
            var oldRule = panel.Find("ExpeditionHeaderRule"); if (oldRule != null) oldRule.gameObject.SetActive(false);
            Caption(panel, "Title", "СОВМЕСТНАЯ ЭКСПЕДИЦИЯ", 34, 24, 870, 48, 34, LowPolyUi.Paper);
            Caption(panel, "Subtitle", "ДВОЕ. ОДНА МАШИНА. ОБЩАЯ ДОРОГА.", 34, 73, 870, 28, 17, LowPolyUi.Muted);
            var art = panel.Find("ExpeditionCrewArt")?.GetComponent<Image>();
            if (art == null)
            {
                art = LowPolyUi.Icon(panel, "coop_backdrop", new Vector2(34, -115), new Vector2(872, 160));
                art.name = "ExpeditionCrewArt"; art.preserveAspect = false;
                var source = art.sprite;
                if (source != null)
                {
                    float height = source.rect.width * 160f / 872f;
                    var crop = new Rect(source.rect.x, source.rect.y + source.rect.height * .34f, source.rect.width, height);
                    art.sprite = UnityEngine.Sprite.Create(source.texture, crop, Vector2.one * .5f, 100);
                }
                art.color = new Color(.65f, .65f, .65f, 1);
            }
            Caption(panel, "ExpeditionCrewHero", "02  /  ЭКИПАЖ", 54, 137, 430, 43, 30, LowPolyUi.Paper);
            Caption(panel, "ExpeditionCrewHint", "Готовность обоих участников запускает выезд.", 54, 185, 470, 38, 17, LowPolyUi.Paper);
            Place(panel.Find("MyIP"), 34, 287, 870, 26);
            Place(panel.Find("AddressInput"), 34, 326, 255, 46);
            Place(panel.Find("JoinButton"), 303, 326, 198, 46);
            Place(panel.Find("HostButton"), 515, 326, 226, 46);
            Place(panel.Find("LeaveButton"), 755, 326, 151, 46);
            Place(panel.Find("Slot1_Commander"), 34, 390, 429, 98);
            Place(panel.Find("Slot2_Partner"), 477, 390, 429, 98);
            foreach (string slot in new[] { "Slot1_Commander", "Slot2_Partner" })
            {
                var card = panel.Find(slot); if (card == null) continue;
                Surface(card.GetComponent<Image>(), true);
                var text = card.GetComponentInChildren<Text>(true);
                if (text != null) { text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 18; text.lineSpacing = 1.25f; text.alignment = TextAnchor.MiddleLeft; }
            }
            Place(panel.Find("StatusText"), 34, 501, 870, 28);
            Place(panel.Find("LaunchStatusText"), 34, 535, 870, 42);
            Place(panel.Find("ReadyButton"), 34, 614, 590, 48);
            Place(panel.Find("CloseButton"), 638, 614, 268, 48);
            Place(panel.Find("SoloLaunchButton"), 34, 580, 590, 28);
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                LowPolyUi.StyleButton(button, button.name == "HostButton" || button.name == "ReadyButton");
                var label = button.GetComponentInChildren<Text>(true); if (label != null) label.fontSize = button.name == "SoloLaunchButton" ? 13 : 18;
            }
        }
    }
}
