using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.UI
{
    /// <summary>
    /// Глобальный менеджер переходов между сценами с плавным затемнением (Fade to Black).
    /// </summary>
    public sealed class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Fade Settings")]
        [SerializeField] private float fadeDuration = 0.45f;
        [SerializeField] private UnityEngine.UI.Image fadeOverlay;

        private float currentAlpha = 0f;
        private bool isTransitioning = false;
        private Texture2D blackTexture;
        private GUIStyle fadeStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Защита: если SceneTransitionManager по ошибке повешен на GameObject с другими логическими
            // компонентами (например, MainMenuController), отсоединяем его в отдельный чистый объект,
            // чтобы DontDestroyOnLoad не сделал бессмертным весь контроллер сцены.
            Component[] components = gameObject.GetComponents<Component>();
            bool hasSharedScripts = false;
            for (int i = 0; i < components.Length; i++)
            {
                Component comp = components[i];
                if (comp != null && !(comp is Transform) && !(comp is SceneTransitionManager))
                {
                    hasSharedScripts = true;
                    break;
                }
            }

            if (hasSharedScripts)
            {
                Debug.LogWarning("[SceneTransitionManager] Обнаружено совместное размещение с другими компонентами. Миграция на отдельный GameObject.");
                GameObject dedicatedObj = new GameObject("SceneTransitionManager");
                SceneTransitionManager dedicatedManager = dedicatedObj.AddComponent<SceneTransitionManager>();
                dedicatedManager.fadeDuration = this.fadeDuration;
                Instance = dedicatedManager;
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureCanvasOverlay();
        }

        private void EnsureCanvasOverlay()
        {
            if (fadeOverlay != null) return;

            GameObject canvasObj = new GameObject("TransitionCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvasObj.transform.SetParent(transform, false);
            Canvas c = canvasObj.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 9999;

            GameObject imgObj = new GameObject("FadeImage", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            imgObj.transform.SetParent(canvasObj.transform, false);
            RectTransform rt = imgObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            fadeOverlay = imgObj.GetComponent<UnityEngine.UI.Image>();
            fadeOverlay.color = Color.clear;
            fadeOverlay.raycastTarget = false;
        }

        public static void SwitchScene(string sceneName)
        {
            if (Instance == null)
            {
                var existing = FindFirstObjectByType<SceneTransitionManager>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    GameObject go = new GameObject("SceneTransitionManager");
                    Instance = go.AddComponent<SceneTransitionManager>();
                }
            }

            Instance.TransitionToScene(sceneName);
        }

        public void TransitionToScene(string sceneName)
        {
            if (!isTransitioning)
            {
                StartCoroutine(FadeAndSwitch(sceneName));
            }
        }

        private IEnumerator FadeAndSwitch(string sceneName)
        {
            isTransitioning = true;
            Time.timeScale = 1f;

            // Fade Out (в черноту)
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                currentAlpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            currentAlpha = 1f;

            // Загрузка сцены
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (asyncLoad != null && !asyncLoad.isDone)
            {
                yield return null;
            }

            // Небольшая пауза для инициализации сцены
            yield return new WaitForSecondsRealtime(0.1f);

            // Fade In (из черноты)
            t = fadeDuration;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime;
                currentAlpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            currentAlpha = 0f;
            isTransitioning = false;
        }

        private void OnDestroy()
        {
            if (blackTexture != null)
            {
                Destroy(blackTexture);
            }
        }

        private void LateUpdate()
        {
            if (fadeOverlay == null) return;
            fadeOverlay.color = new Color(0,0,0,currentAlpha);
            fadeOverlay.raycastTarget = isTransitioning;
        }
    }
}
