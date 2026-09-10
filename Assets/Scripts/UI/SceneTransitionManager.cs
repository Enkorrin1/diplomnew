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

            blackTexture = new Texture2D(2, 2);
            Color[] pixels = new Color[] { Color.black, Color.black, Color.black, Color.black };
            blackTexture.SetPixels(pixels);
            blackTexture.Apply();

            fadeStyle = new GUIStyle();
            fadeStyle.normal.background = blackTexture;
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

        private void OnGUI()
        {
            if (fadeOverlay != null) return;
            if (currentAlpha > 0.001f)
            {
                GUI.depth = -1000;
                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, currentAlpha);
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, fadeStyle);
                GUI.color = prevColor;
            }
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
