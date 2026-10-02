using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Пролог «Пробуждение в бункере» (~12 с):
    /// 1. Темнота, нарастающие радиопомехи.
    /// 2. Радиограмма Цитадели печатается с обрывами сигнала.
    /// 3. Мутное неровное моргание: мягкие веки и сонная дымка (PostProcessVolume со свечением).
    /// 4. Взгляд на машину, герой садится на диване, встаёт и оказывается лицом к машине.
    /// 5. Управление передаётся без рывка: поворот тела и наклон головы берутся из последнего кадра.
    /// Позы, UI и звук — объекты сцены (Assets/Editor/BunkerScenesAuthoring.cs).
    /// </summary>
    public sealed class BunkerPrologueCutscene : MonoBehaviour
    {
        public static BunkerPrologueCutscene Instance { get; private set; }

        private const string RadioSpeaker = "<color=#F0A040><b>РАДИО ЦИТАДЕЛИ</b></color>\n";
        private const string HeroSpeaker = "<color=#C8D2DC><b>Я</b></color>\n";
        private const string GlitchChars = "▒░▓#/\\%";

        [Header("Player & Camera")]
        [SerializeField] private GaragePlayerController player;
        [SerializeField] private Camera playerCamera;

        [Header("Poses (объекты сцены)")]
        [SerializeField] private Transform wakeEyePose;
        [SerializeField] private Transform wakeSittingPose;
        [SerializeField] private Transform wakeStandingPose;
        [SerializeField] private Transform wakeLookTarget;

        [Header("Timing")]
        [SerializeField] private float radioStaticDuration = 1.0f;
        [SerializeField] private float typeCharsPerSecond = 42f;
        [SerializeField] private float glanceDuration = 0.9f;
        [SerializeField] private float sitDuration = 1.2f;
        [SerializeField] private float riseDuration = 1.3f;

        [Header("Script")]
        [SerializeField, TextArea(2, 4)] private string radioLineA = "...всем, кто слышит. Протокол «Закат» активирован.";
        [SerializeField, TextArea(2, 4)] private string radioLineB = "Шлюз Цитадели закроется через ▒▒▒... Выезжайте немедленно!";
        [SerializeField, TextArea(2, 4)] private string heroLine = "Цитадель закрывают... Надо выбираться.";

        [Header("Cutscene UI (объекты сцены, Canvas выключен до начала пролога)")]
        [SerializeField] private Canvas cutsceneCanvas;
        [SerializeField] private RectTransform topEyelid;
        [SerializeField] private RectTransform bottomEyelid;
        [SerializeField] private Text subtitleText;
        [SerializeField] private GameObject subtitlePanel;
        [SerializeField] private float eyelidHeight = 0.8f;

        [Header("Blur & Audio")]
        [SerializeField] private PostProcessVolume wakeVolume;
        [SerializeField] private AudioSource staticSource;
        [SerializeField] private AudioClip radioGlitchClip;
        [SerializeField] private AudioClip sofaCreakClip;
        [SerializeField] private AudioClip clothClip;
        [SerializeField] private AudioClip footstepClip;

        private bool isCutsceneRunning;
        private float eyeOpen; // 0 = закрыты, 1 = открыты
        private Vector2 subtitleRestPos;
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly StringBuilder typeBuffer = new StringBuilder(128);

        public bool IsRunning => isCutsceneRunning;
        public int LastSkipFrame { get; private set; } = -1;

        private void Awake()
        {
            Instance = this;
            if (player == null) player = FindFirstObjectByType<GaragePlayerController>();
            if (playerCamera == null && player != null) playerCamera = player.PlayerCamera;
            if (playerCamera == null) playerCamera = Camera.main;
            if (subtitlePanel != null) subtitleRestPos = ((RectTransform)subtitlePanel.transform).anchoredPosition;
            if (wakeVolume != null) wakeVolume.weight = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (GarageDepartureCheckpoint.HasPendingRestore) return;
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
            if (wakeEyePose == null || wakeSittingPose == null || wakeStandingPose == null)
            {
                Debug.LogWarning("[Prologue] Не заданы позы пробуждения — пролог пропущен.");
                yield break;
            }

            isCutsceneRunning = true;
            eyeOpen = 0f;
            ShowCutsceneUI();
            SetBlur(1f);
            SetStatic(0f);

            if (player != null)
            {
                player.transform.SetPositionAndRotation(wakeStandingPose.position, Quaternion.Euler(0f, wakeStandingPose.eulerAngles.y, 0f));
                player.SetMovementLocked(true);
            }

            Transform cam = playerCamera != null ? playerCamera.transform : null;
            if (cam != null) cam.SetPositionAndRotation(wakeEyePose.position, wakeEyePose.rotation);

            // ── 1. Темнота, помехи нарастают ────────────────────────────────────
            SetSubtitle(null);
            yield return Fade(radioStaticDuration, k => SetStatic(Mathf.Lerp(0f, 0.55f, k)));

            // ── 2. Радиограмма с обрывом сигнала ────────────────────────────────
            yield return TypeLine(RadioSpeaker, radioLineA, true);
            yield return new WaitForSeconds(0.45f);
            yield return SignalDrop();
            yield return TypeLine(RadioSpeaker, radioLineB, true);
            yield return new WaitForSeconds(0.6f);

            // ── 3. Мутное пробуждение ───────────────────────────────────────────
            SetSubtitle(null);
            StartCoroutine(Fade(2.2f, k => SetStatic(Mathf.Lerp(0.55f, 0.12f, k))));
            StartCoroutine(Breathe(cam, wakeEyePose.position, 3.2f));
            yield return Blink(0.00f, 0.35f, 0.90f);
            yield return Blink(0.35f, 0.05f, 0.22f);
            StartCoroutine(TypeLine(HeroSpeaker, heroLine, false));
            yield return Blink(0.05f, 0.70f, 0.65f);
            yield return Blink(0.70f, 0.45f, 0.16f);
            yield return Blink(0.45f, 1.00f, 0.70f);

            // ── 4. Взгляд на машину, сесть, встать ─────────────────────────────
            Vector3 target = wakeLookTarget != null ? wakeLookTarget.position : wakeStandingPose.position + wakeStandingPose.forward * 5f;
            Quaternion lyingGlance = Quaternion.Slerp(wakeEyePose.rotation, Quaternion.LookRotation(target - wakeEyePose.position), 0.55f);
            yield return MoveCamera(cam, wakeEyePose.position, lyingGlance, glanceDuration, 0f, k => SetBlur(Mathf.Lerp(0.75f, 0.55f, k)));

            PlayClip(sofaCreakClip, 0.7f);
            PlayClip(clothClip, 0.45f);
            Quaternion sitLook = Quaternion.Slerp(wakeSittingPose.rotation, Quaternion.LookRotation(target - wakeSittingPose.position), 0.6f);
            yield return MoveCamera(cam, wakeSittingPose.position, sitLook, sitDuration, -0.05f, k => SetBlur(Mathf.Lerp(0.55f, 0.25f, k)));
            yield return new WaitForSeconds(0.25f);
            SetSubtitle(null);

            PlayClip(clothClip, 0.6f);
            Vector3 headPos = wakeStandingPose.position + Vector3.up * StandingEyeHeight();
            Quaternion standLook = Quaternion.LookRotation(target - headPos);
            yield return MoveCamera(cam, headPos, standLook, riseDuration, 0.12f, k => SetBlur(Mathf.Lerp(0.25f, 0f, k)));
            PlayClip(footstepClip, 0.5f);
            yield return SettleBob(cam, headPos);

            FinishCutscene();
        }

        // ── Фазы ────────────────────────────────────────────────────────────────

        private IEnumerator TypeLine(string speaker, string line, bool radio)
        {
            if (string.IsNullOrEmpty(line)) yield break;
            float shown = 0f;
            int last = -1;
            while (last < line.Length)
            {
                shown += Time.deltaTime * typeCharsPerSecond;
                int count = Mathf.Min(line.Length, Mathf.FloorToInt(shown));
                if (count != last || radio)
                {
                    typeBuffer.Clear().Append(speaker).Append(line, 0, count);
                    if (radio && count < line.Length && Random.value < 0.3f)
                        typeBuffer.Append(GlitchChars[Random.Range(0, GlitchChars.Length)]);
                    SetSubtitle(typeBuffer.ToString());
                    last = count == line.Length ? line.Length + 1 : count;
                }
                yield return null;
            }
            SetSubtitle(speaker + line);
        }

        private IEnumerator SignalDrop()
        {
            PlayClip(radioGlitchClip, 0.8f);
            string text = subtitleText != null ? subtitleText.text : string.Empty;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                if (subtitlePanel != null)
                    ((RectTransform)subtitlePanel.transform).anchoredPosition = subtitleRestPos + new Vector2(Random.Range(-6f, 6f), Random.Range(-2f, 2f));
                if (subtitleText != null) subtitleText.text = Random.value < 0.5f ? text : RadioSpeaker + "▒▒░▒ ░░▒▓ ▒░";
                SetStatic(Random.Range(0.6f, 0.9f));
                yield return null;
            }
            if (subtitlePanel != null) ((RectTransform)subtitlePanel.transform).anchoredPosition = subtitleRestPos;
            SetStatic(0.55f);
        }

        private IEnumerator Blink(float from, float to, float duration)
        {
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                // Открытие медленное в конце, закрытие — резкое: так моргают сонные глаза.
                float k = to > from ? 1f - (1f - t) * (1f - t) : t * t;
                eyeOpen = Mathf.Lerp(from, to, k);
                UpdateEyelids();
                SetBlur(1f - eyeOpen * 0.25f);
                yield return null;
            }
        }

        private IEnumerator Breathe(Transform cam, Vector3 basePos, float duration)
        {
            if (cam == null) yield break;
            for (float t = 0f; t < duration && isCutsceneRunning; t += Time.deltaTime)
            {
                cam.position = basePos + Vector3.up * (Mathf.Sin(t * 1.9f) * 0.012f);
                yield return null;
            }
        }

        private IEnumerator MoveCamera(Transform cam, Vector3 toPos, Quaternion toRot, float duration, float arcHeight, System.Action<float> onStep)
        {
            if (cam == null) yield break;
            Vector3 fromPos = cam.position;
            Quaternion fromRot = cam.rotation;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                float k = t * t * (3f - 2f * t);
                // Голова опережает тело: поворот завершается раньше перемещения.
                float r = Mathf.Clamp01(t * 1.3f);
                cam.position = Vector3.Lerp(fromPos, toPos, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * arcHeight);
                cam.rotation = Quaternion.Slerp(fromRot, toRot, r * r * (3f - 2f * r));
                onStep?.Invoke(k);
                yield return null;
            }
        }

        private IEnumerator SettleBob(Transform cam, Vector3 headPos)
        {
            if (cam == null) yield break;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                cam.position = headPos + Vector3.down * (Mathf.Sin(t / 0.35f * Mathf.PI) * 0.035f);
                yield return null;
            }
            cam.position = headPos;
        }

        private static IEnumerator Fade(float duration, System.Action<float> onStep)
        {
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, duration));
                onStep(t);
                yield return null;
            }
        }

        // ── Завершение ─────────────────────────────────────────────────────────

        private void FinishCutscene()
        {
            isCutsceneRunning = false;
            eyeOpen = 1f;
            UpdateEyelids();
            SetBlur(0f);
            if (staticSource != null) staticSource.Stop();
            if (subtitlePanel != null) ((RectTransform)subtitlePanel.transform).anchoredPosition = subtitleRestPos;
            if (cutsceneCanvas != null) cutsceneCanvas.gameObject.SetActive(false);

            RestoreGameplayUI();
            if (GaragePrologueManager.Instance == null || !GaragePrologueManager.Instance.IsPreviewRun)
            {
                PlayerPrefs.SetInt("BunkerPrologueSeen_V1", 1);
                PlayerPrefs.Save();
            }

            if (player != null)
            {
                // Продолжаем ровно с того взгляда, которым закончилась сцена.
                Vector3 forward = playerCamera != null ? playerCamera.transform.forward : wakeStandingPose.forward;
                Vector3 flat = new Vector3(forward.x, 0f, forward.z);
                if (flat.sqrMagnitude < 0.0001f) flat = wakeStandingPose.forward;
                float pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;

                player.transform.SetPositionAndRotation(wakeStandingPose.position, Quaternion.LookRotation(flat.normalized, Vector3.up));
                if (playerCamera != null)
                {
                    if (playerCamera.transform.parent != player.transform)
                        playerCamera.transform.SetParent(player.transform, false);
                    playerCamera.transform.localPosition = new Vector3(0f, StandingEyeHeight(), 0f);
                    player.SetCameraPitch(pitch);
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
            if (!isCutsceneRunning) return;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
            {
                LastSkipFrame = Time.frameCount;
                StopAllCoroutines();
                FinishCutscene();
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

        // ── Вспомогательное ────────────────────────────────────────────────────

        private float StandingEyeHeight()
        {
            if (player != null && playerCamera != null && playerCamera.transform.parent == player.transform)
            {
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) return controller.height - 0.15f;
            }
            return 1.65f;
        }

        private void SetSubtitle(string text)
        {
            if (subtitleText == null) return;
            subtitleText.text = text ?? string.Empty;
            if (subtitlePanel != null) subtitlePanel.SetActive(!string.IsNullOrEmpty(text));
        }

        private void SetBlur(float weight)
        {
            if (wakeVolume != null) wakeVolume.weight = Mathf.Clamp01(weight);
        }

        private void SetStatic(float volume)
        {
            if (staticSource == null) return;
            if (!staticSource.isPlaying && volume > 0f) staticSource.Play();
            staticSource.volume = volume * PlayerPrefs.GetFloat("SfxVolume", 0.8f);
        }

        private static void PlayClip(AudioClip clip, float volume)
        {
            if (clip != null && Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlaySfx(clip, volume, 1f);
        }

        private void UpdateEyelids()
        {
            if (topEyelid == null || bottomEyelid == null) return;

            // Веки — мягкие градиенты высотой eyelidHeight; при eyeOpen = 0 они перекрываются по центру,
            // при eyeOpen = 1 полностью уходят за край экрана вместе с мягкой кромкой.
            float edge = Mathf.Lerp(0.5f, -eyelidHeight * 0.3f, eyeOpen);
            topEyelid.anchorMin = new Vector2(0f, 1f - edge - eyelidHeight * 0.3f);
            topEyelid.anchorMax = new Vector2(1f, 1f - edge - eyelidHeight * 0.3f + eyelidHeight);
            bottomEyelid.anchorMin = new Vector2(0f, edge + eyelidHeight * 0.3f - eyelidHeight);
            bottomEyelid.anchorMax = new Vector2(1f, edge + eyelidHeight * 0.3f);
            topEyelid.offsetMin = topEyelid.offsetMax = Vector2.zero;
            bottomEyelid.offsetMin = bottomEyelid.offsetMax = Vector2.zero;
        }

        private void ShowCutsceneUI()
        {
            if (cutsceneCanvas == null) return;
            cutsceneCanvas.gameObject.SetActive(true);
            UpdateEyelids();
        }
    }
}
