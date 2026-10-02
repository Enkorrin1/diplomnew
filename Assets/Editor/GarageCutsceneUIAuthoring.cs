using System;
using RogueDrive.Gameplay.Hub;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

// Интерфейс пролога «Пробуждение в бункере» как объект GarageScene: веки, субтитры и подсказка пропуска.
// Раньше BunkerPrologueCutscene создавал этот Canvas кодом при каждом запуске пролога.
public static class GarageCutsceneUIAuthoring
{
    const string CanvasName = "Cutscene_Canvas";

    [MenuItem("RogueDrive/Bunker/Author Prologue Cutscene UI")]
    public static void Author()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GarageScene.unity", OpenSceneMode.Single);
        var cutscene = Object.FindFirstObjectByType<BunkerPrologueCutscene>(FindObjectsInactive.Include);
        var old = cutscene.transform.Find(CanvasName);
        if(old != null) Object.DestroyImmediate(old.gameObject);

        var canvasGo = new GameObject(CanvasName, typeof(RectTransform));
        canvasGo.transform.SetParent(cutscene.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 999;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;

        // Веки: верхнее и нижнее, по половине экрана; открытость задаёт пролог через якоря.
        var top = Panel(canvasGo.transform, "TopEyelid", Color.black, new Vector2(0, .5f), Vector2.one);
        var bottom = Panel(canvasGo.transform, "BottomEyelid", Color.black, Vector2.zero, new Vector2(1, .5f));

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var subtitle = Panel(canvasGo.transform, "SubtitlePanel", new Color(.04f, .05f, .08f, .88f), new Vector2(.5f, 0), new Vector2(.5f, 0));
        subtitle.pivot = new Vector2(.5f, 0); subtitle.anchoredPosition = new Vector2(0, 45); subtitle.sizeDelta = new Vector2(980, 84);
        var subtitleText = Text(subtitle, "SubtitleText", font, 20, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(.95f, .9f, .7f, .95f), "");
        var textRect = subtitleText.rectTransform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = new Vector2(24, 8); textRect.offsetMax = new Vector2(-24, -8);

        var skip = Text(canvasGo.transform, "SkipPrompt", font, 14, FontStyle.Normal, TextAnchor.MiddleRight, new Color(1, 1, 1, .5f), "[Пробел / ESC] Пропустить");
        var skipRect = skip.rectTransform;
        skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = Vector2.one; skipRect.anchoredPosition = new Vector2(-30, -20); skipRect.sizeDelta = new Vector2(300, 35);

        var so = new SerializedObject(cutscene);
        so.FindProperty("cutsceneCanvas").objectReferenceValue = canvas;
        so.FindProperty("topEyelid").objectReferenceValue = top;
        so.FindProperty("bottomEyelid").objectReferenceValue = bottom;
        so.FindProperty("subtitleText").objectReferenceValue = subtitleText;
        so.FindProperty("subtitlePanel").objectReferenceValue = subtitle.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        canvasGo.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Cutscene] Prologue UI authored in GarageScene.");
    }

    static RectTransform Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return rect;
    }

    static Text Text(Transform parent, string name, Font font, int size, FontStyle style, TextAnchor anchor, Color color, string value)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font; text.fontSize = size; text.fontStyle = style; text.alignment = anchor; text.color = color; text.text = value;
        text.raycastTarget = false;
        return text;
    }
}
