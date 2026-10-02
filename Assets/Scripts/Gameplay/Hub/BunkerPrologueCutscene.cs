using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Кинематографичный пролог «Пробуждение в бункере»:
    /// 1. Черный экран и звук радиопомех.
    /// 2. Экстренное сообщение Цитадели о протоколе «Закат».
    /// 3. Эффект размытого открывания глаз (моргание на базе UGUI Canvas).
    /// 4. Подъем с раскладушки и поворот камеры на боевой автомобиль.
    /// 5. Бесшовная передача управления игроку от 1-го лица.
    /// Полностью очищен от OnGUI.
    /// </summary>
    public sealed class BunkerPrologueCutscene : MonoBehaviour
    {
        [Header("Player & Camera")]
        [SerializeField] private GaragePlayerController player;
        [SerializeField] private Camera playerCamera;

        [Header("Awakening Transforms (Bed Position)")]
        [SerializeField] private Transform bedHeadTransform;
        [SerializeField] private Vector3 bedLyingOffset = new Vector3(0f, 0.45f, 0f);
        [SerializeField] private Vector3 bedLyingEuler = new Vector3(-65f, 0f, 0f); // взгляд в потолок
        [SerializeField] private Transform wakeEyePose;
        [SerializeField] private Transform wakeStandingPose;

        [Header("Timing")]
        [SerializeField] private float radioStaticDuration = 3.5f;
        [SerializeField] private float broadcastDuration = 6.0f;
        [SerializeField] private float riseDuration = 2.5f;

        [Header("Cutscene UI (объекты сцены, Canvas выключен до начала пролога)")]
        [SerializeField] private Canvas cutsceneCanvas;
        [SerializeField] private RectTransform topEyelid;
        [SerializeField] private RectTransform bottomEyelid;
        [SerializeField] private Text subtitleText;
        [SerializeField] private GameObject subtitlePanel;

        private bool isCutsceneRunning = false;
        private float eyeBlinkProgress = 0f; // 0 = закрыты (черный), 1 = открыты

        private Vector3 cutsceneStandingPos;
        private Quaternion cutsceneStandingRot;
        private readonly System.Collections.Generic.List<Canvas> hiddenCanvases = new System.Collections.Generic.List<Canvas>();

        public bool IsRunning => isCutsceneRunning;

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<GaragePlayerController>();
            if (playerCamera == null && player != null) playerCamera = player.PlayerCamera;
            if (playerCamera == null) playerCamera = Camera.main;
            if (bedHeadTransform == null)
            {
                bedHeadTransform = GameObject.Find("Placeholder_Pillow")?.transform
                    ?? GameObject.Find("Placeholder_Bed")?.transform;
            }
        }

        private void Start()
        {
            if (GarageDepartureCheckpoint.HasPendingRestore) return;
            // Проверяем, видел ли игрок пролог или был ли он форсирован
            int seen = PlayerPrefs.GetInt("BunkerPrologueSeen_V1", 0);
            if (seen == 0 || GaragePrologueManager.ForcePrologueAwakening ||
                (GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsPreviewRun))
            {
                StartCoroutine(PlayAwakeningSequence());
            }
        }

        public void PlayCutsceneManually()
        {
            StopAllCoroutines();
            StartCoroutine(PlayAwakeningSequence());
        }

        private IEnumerator PlayAwakeningSequence()
        {
            isCutsceneRunning = true;
            eyeBlinkProgress = 0f;
            ShowCutsceneUI();

            Vector3 bedPos = bedHeadTransform != null ? bedHeadTransform.position : new Vector3(-8.5f, 0.4f, -8.5f);
            cutsceneStandingPos = new Vector3(bedPos.x + 1.2f, 0.1f, bedPos.z);

            Vector3 targetHeadPos = cutsceneStandingPos + Vector3.up * 1.65f;
            Vector3 lookDir = (new Vector3(0f, 0.8f, 0f) - targetHeadPos).normalized;
            cutsceneStandingRot = Quaternion.LookRotation(new Vector3(lookDir.x, 0f, lookDir.z));
            if (wakeStandingPose != null)
            {
                cutsceneStandingPos = wakeStandingPose.position;
                cutsceneStandingRot = wakeStandingPose.rotation;
                targetHeadPos = cutsceneStandingPos + Vector3.up * 1.65f;
            }

            if (player != null)
            {
                player.transform.position = cutsceneStandingPos;
                player.transform.rotation = cutsceneStandingRot;
                player.SetMovementLocked(true);
            }

            Vector3 lyingPos = bedHeadTransform != null ? bedHeadTransform.position + bedLyingOffset : bedPos + Vector3.up * 0.45f;
            // Keep the eye above the mattress, including authored pillow meshes.
            if (bedHeadTransform != null)
                foreach (var renderer in bedHeadTransform.GetComponentsInChildren<Renderer>())
                    lyingPos.y = Mathf.Max(lyingPos.y, renderer.bounds.max.y + 0.18f);
            Quaternion lyingRot = Quaternion.Euler(bedLyingEuler);
            if (wakeEyePose != null)
            {
                lyingPos = wakeEyePose.position;
                lyingRot = wakeEyePose.rotation;
            }

            if (playerCamera != null)
            {
                playerCamera.transform.position = lyingPos;
                playerCamera.transform.rotation = lyingRot;
            }

            // ── Фаза 1: Темнота и помехи ─────────────────────────────────────────────
            SetSubtitle("[Шипение радиоволн... *кххх-пшшш*]");
            yield return new WaitForSeconds(radioStaticDuration);

            // ── Фаза 2: Экстренная радиограмма Цитадели ──────────────────────────────
            SetSubtitle("РАДИО: «...Внимание всем бортам! Протокол \"Закат\" активирован! Шлюз Цитадели закроется навсегда через... [помехи]... Кто слышит — выезжайте немедленно!»");
            yield return new WaitForSeconds(broadcastDuration);

            // ── Фаза 3: Эффект моргания (первое открытие глаз) ───────────────────────
            SetSubtitle("ГЕРОЙ: «Цитадель запечатывают... Чёрт! Надо шевелиться!»");

            // Моргнули 1 раз
            yield return AnimateBlink(0f, 0.7f, 0.6f);
            yield return AnimateBlink(0.7f, 0.1f, 0.3f);
            // Открыли глаза
            yield return AnimateBlink(0.1f, 1.0f, 0.8f);

            // ── Фаза 4: Подъем с кровати на ноги прямо в жилом отсеке ────────────────
            float elapsed = 0f;
            Vector3 camStart = playerCamera != null ? playerCamera.transform.position : lyingPos;
            Quaternion rotStart = playerCamera != null ? playerCamera.transform.rotation : lyingRot;

            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / riseDuration);

                if (playerCamera != null)
                {
                    playerCamera.transform.position = Vector3.Lerp(camStart, targetHeadPos, t);
                    playerCamera.transform.rotation = Quaternion.Slerp(rotStart, cutsceneStandingRot, t);
                }
                yield return null;
            }

            // Завершение катсцены
            FinishCutscene();
        }

        private IEnumerator AnimateBlink(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                eyeBlinkProgress = Mathf.Lerp(from, to, elapsed / duration);
                UpdateEyelids();
                yield return null;
            }
            eyeBlinkProgress = to;
            UpdateEyelids();
        }

        private void FinishCutscene()
        {
            isCutsceneRunning = false;
            eyeBlinkProgress = 1f;

            if (cutsceneCanvas != null) cutsceneCanvas.gameObject.SetActive(false);

            RestoreGameplayUI();
            if (GaragePrologueManager.Instance == null || !GaragePrologueManager.Instance.IsPreviewRun)
            {
                PlayerPrefs.SetInt("BunkerPrologueSeen_V1", 1);
                PlayerPrefs.Save();
            }

            if (player != null)
            {
                player.transform.position = cutsceneStandingPos;
                player.transform.rotation = cutsceneStandingRot;
                if (playerCamera != null)
                {
                    if (playerCamera.transform.parent != player.transform)
                    {
                        playerCamera.transform.SetParent(player.transform, false);
                    }
                    playerCamera.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                    player.SetCameraPitch(0f);
                }
                player.SetMovementLocked(false);
            }

            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.RefreshObjective();
                GaragePrologueManager.Instance.ShowNotification("ЦЕЛЬ: Запустите дизель-генератор на стене бункера!", 5.5f);
            }
        }

        private void Update()
        {
            if (isCutsceneRunning)
            {
                // Пропуск по пробелу или ESC
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                {
                    StopAllCoroutines();
                    FinishCutscene();
                }
            }
        }

        private void LateUpdate()
        {
            if (!isCutsceneRunning) return;
            foreach (var overlay in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (overlay == cutsceneCanvas || !overlay.enabled || overlay.renderMode == RenderMode.WorldSpace) continue;
                if (!hiddenCanvases.Contains(overlay)) hiddenCanvases.Add(overlay);
                overlay.enabled = false;
            }
        }

        private void RestoreGameplayUI()
        {
            foreach (var overlay in hiddenCanvases)
                if (overlay != null) overlay.enabled = true;
            hiddenCanvases.Clear();
        }

        private void OnDisable()
        {
            if (isCutsceneRunning) { StopAllCoroutines(); FinishCutscene(); }
            RestoreGameplayUI();
        }

        private void SetSubtitle(string text)
        {
            if (subtitleText != null)
            {
                subtitleText.text = text;
                subtitlePanel.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        private void UpdateEyelids()
        {
            if (topEyelid == null || bottomEyelid == null) return;

            float closedFraction = (1f - eyeBlinkProgress) * 0.5f;
            topEyelid.anchorMin = new Vector2(0f, 1f - closedFraction);
            topEyelid.anchorMax = new Vector2(1f, 1f);

            bottomEyelid.anchorMin = new Vector2(0f, 0f);
            bottomEyelid.anchorMax = new Vector2(1f, closedFraction);
        }

        private void ShowCutsceneUI()
        {
            if (cutsceneCanvas == null) return;
            cutsceneCanvas.gameObject.SetActive(true);
            UpdateEyelids();
        }
    }
}
