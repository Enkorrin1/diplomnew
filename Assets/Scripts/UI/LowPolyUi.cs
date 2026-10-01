using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    /// <summary>Shared, build-safe art and controls for the low-poly survival UI.</summary>
    public static class LowPolyUi
    {
        public static readonly Color Ink = new Color32(15, 24, 29, 255);
        public static readonly Color Surface = new Color32(30, 43, 49, 255);
        public static readonly Color Border = new Color32(72, 91, 98, 255);
        public static readonly Color Paper = new Color32(241, 237, 224, 255);
        public static readonly Color Muted = new Color32(165, 183, 186, 255);
        public static readonly Color Amber = new Color32(232, 181, 96, 255);
        public static readonly Color Danger = new Color32(223, 108, 90, 255);
        public static readonly Color Healthy = new Color32(157, 188, 125, 255);
        public static readonly Color Water = new Color32(119, 169, 196, 255);
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static Font headingFont;
        public static Font HeadingFont => headingFont != null ? headingFont : (headingFont = Resources.Load<Font>("UI/LowPoly/Fonts/Heading"));

        public static Sprite Sprite(string name)
        {
            if (name == "panel" || name == "button") return ExpeditionPresentation.SurfaceSprite;
            if (sprites.TryGetValue(name, out var sprite) && sprite != null) return sprite;
            if (name == "icon_pistol" || name == "icon_crowbar" || name == "icon_ammo_9mm")
            {
                var sheet = Resources.Load<Sprite>("UI/LowPoly/combat_inventory_atlas");
                if (sheet != null)
                {
                    string[] names = { "icon_pistol", "icon_crowbar", "icon_ammo_9mm" };
                    Rect[] crops = { new Rect(57,109,648,508), new Rect(761,27,664,672), new Rect(1515,102,604,448) };
                    for (int i = 0; i < names.Length; i++)
                    {
                        var item = UnityEngine.Sprite.Create(sheet.texture, crops[i], new Vector2(.5f,.5f), 100);
                        item.name = names[i]; sprites[names[i]] = item;
                    }
                }
                if (sprites.TryGetValue(name, out sprite) && sprite != null) return sprite;
            }
            string[] atlasNames = { "icon_fuel_canister", "icon_water_canister", "icon_wheel", "icon_battery", "icon_axe", "icon_repair", "icon_engine", "icon_radiator", "icon_scrap" };
            int index = System.Array.IndexOf(atlasNames, name);
            if (index >= 0)
            {
                foreach (var item in Resources.LoadAll<Sprite>("UI/LowPoly/inventory_atlas")) sprites[item.name]=item;
                if(sprites.TryGetValue(name,out sprite) && sprite!=null) return sprite;
            }
            if (name == "panel" || name == "button" || name == "gauge") sprite = Resources.Load<Sprite>("UI/LowPoly/" + name + "_v2");
            if (sprite != null) { sprites[name] = sprite; return sprite; }
            sprite = Resources.Load<Sprite>("UI/LowPoly/" + name);
            if (sprite != null) sprites[name] = sprite;
            return sprite;
        }

        public static Sprite ItemSprite(RogueDrive.Gameplay.Hub.PocketSlotData data)
        {
            string id = ((data.id ?? "") + " " + (data.displayName ?? "")).ToLowerInvariant();
            Sprite specific = id.Contains("ammo") || id.Contains("патрон") ? Sprite("icon_ammo_9mm")
                : id.Contains("crowbar") || id.Contains("монтиров") ? Sprite("icon_crowbar")
                : id.Contains("pistol") || id.Contains("firearm") || id.Contains("пистолет") ? Sprite("icon_pistol") : null;
            return specific != null ? specific : data.icon != null ? data.icon : Sprite(VehicleDashboardPanelsUI.ItemIcon(data.legacyType));
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
            image.sprite = Sprite("panel"); image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            if (name.Contains("Bar") || name == "Fill") image.color = color;
            return image;
        }

        public static Image Icon(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
            image.sprite = Sprite(name); image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        public static Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize = 20, Color? color = null)
        {
            var text = Rect(parent, name, position, size).gameObject.AddComponent<Text>();
            text.font = fontSize >= 21 && HeadingFont != null ? HeadingFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = fontSize; text.color = color ?? Paper;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action, bool primary = true)
        {
            var image = Panel(parent, name, position, size, Color.white);
            var button = image.gameObject.AddComponent<Button>();
            image.raycastTarget = true;
            var text = Label(image.transform, "Label", label, new Vector2(12, -2), size - new Vector2(24, 4), 18);
            text.alignment = TextAnchor.MiddleCenter; text.fontStyle = FontStyle.Bold;
            if (HeadingFont != null) text.font = HeadingFont;
            StyleButton(button, primary);
            if (action != null) button.onClick.AddListener(action);
            return button;
        }

        public static void StyleButton(Button button, bool primary)
        {
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = Sprite("button"); image.type = Image.Type.Sliced;
                image.color = Color.white; button.targetGraphic = image;
            }
            button.transition = Selectable.Transition.ColorTint;
            var normal = primary ? Amber : Surface;
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = primary ? new Color32(245, 206, 115, 255) : Border;
            colors.selectedColor = primary ? new Color32(245, 206, 115, 255) : Border;
            colors.pressedColor = primary ? new Color32(169, 126, 48, 255) : Ink;
            colors.disabledColor = new Color32(43, 52, 56, 255);
            colors.colorMultiplier = 1; colors.fadeDuration = .12f;
            button.colors = colors;
            foreach (var text in button.GetComponentsInChildren<Text>(true)) { text.color = button.IsInteractable() ? primary ? Ink : Paper : Muted; if (HeadingFont != null) text.font = HeadingFont; }
            if (Application.isPlaying && button.GetComponent<ExpeditionButtonFeedback>() == null) button.gameObject.AddComponent<ExpeditionButtonFeedback>();
        }

        public static void Apply(Transform root)
        {
            // Authored menu illustration sits behind the existing live controls.
            var obsoleteArt=root.Find("LowPoly_MenuArt");
            if(root.name!="UI_Canvas" && obsoleteArt!=null)
            {
                obsoleteArt.gameObject.SetActive(false);
                if(Application.isPlaying) Object.Destroy(obsoleteArt.gameObject); else Object.DestroyImmediate(obsoleteArt.gameObject);
            }
            if (root.gameObject.scene.name == "MainMenuScene" && root.name == "UI_Canvas" && root.Find("LowPoly_MenuArt") == null && Sprite("menu_backdrop") != null)
            {
                var art=Icon(root,"menu_backdrop",Vector2.zero,Vector2.zero);
                art.name="LowPoly_MenuArt";art.preserveAspect=false;
                art.rectTransform.anchorMin=Vector2.zero;art.rectTransform.anchorMax=Vector2.one;
                art.rectTransform.offsetMin=art.rectTransform.offsetMax=Vector2.zero;
                art.transform.SetAsFirstSibling();
            }
            var menuArt=root.Find("LowPoly_MenuArt");
            if(root.name=="UI_Canvas" && menuArt!=null && menuArt.GetComponent<AspectRatioFitter>()==null)
            {
                var art=menuArt.GetComponent<Image>();
                var fit=menuArt.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=art.sprite.rect.width/art.sprite.rect.height;
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                // Preserve masks, artwork, crosshairs, fills and transparent input blockers.
                if (image.color.a < .1f || image.GetComponent<Mask>() != null || image.GetComponent<Button>() != null) continue;
                string name = image.name.ToLowerInvariant();
                if (image.sprite != null && image.sprite.name.StartsWith("icon_"))
                {
                    var replacement = Sprite(image.sprite.name);
                    if (replacement != null) { image.sprite = replacement; image.color = Color.white; }
                    continue;
                }
                bool panel = name.Contains("panel") || name.Contains("background") || name.Contains("card") || name.Contains("slot") || name == "header"
                    || name == "lowpoly_modalsurface" || name == "mainmenu" || name == "garage" || name == "settings" || name == "about" || name == "campaign" || name == "pause" || name == "runresults" || name == "levelup" || name == "hud";
                if (!panel || name.Contains("fill") || name.Contains("fade") || name.Contains("overlay")) continue;
                if (image.sprite != null && image.type != Image.Type.Sliced && !image.sprite.name.Contains("panel") && !image.sprite.name.Contains("frame")) continue;
                image.sprite = Sprite("panel"); image.type = Image.Type.Sliced;
                image.color = name.Contains("slot") || name.Contains("card") ? Surface : Ink;
                // ModalBlocker is a full-screen child; paint the local surface after it.
                var blocker = image.transform.Find("ModalBlocker");
                if (blocker != null && image.transform.Find("LowPoly_ModalSurface") == null)
                {
                    var surface = Panel(image.transform, "LowPoly_ModalSurface", Vector2.zero, Vector2.zero, Surface);
                    var rt = surface.rectTransform;
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                    rt.SetSiblingIndex(blocker.GetSiblingIndex() + 1);
                    surface.raycastTarget = false;
                }
            }
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.color.r > .7f && text.color.g > .7f && text.color.b > .7f) text.color = Paper;
                text.raycastTarget = false;
                if (HeadingFont != null && (text.fontSize >= 24 || text.fontStyle == FontStyle.Bold)) text.font = HeadingFont;
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                string name = button.name.ToLowerInvariant();
                bool primary = name.Contains("startrun") || name.Contains("continue") || name.Contains("resume") || name.Contains("save") || name.Contains("buy") || name.Contains("install") || name.Contains("apply") || name == "hostbutton" || name == "readybutton" || name == "создать экипаж" || name == "продолжить";
                StyleButton(button, primary);
            }
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
            {
                if (slider.fillRect != null && slider.fillRect.TryGetComponent<Image>(out var fill)) fill.color = Amber;
                if (slider.handleRect != null && slider.handleRect.TryGetComponent<Image>(out var handle)) handle.color = Paper;
            }
            ExpeditionPresentation.Apply(root);
        }
    }
}
