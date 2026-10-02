using System.Collections;
using RogueDrive.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Карта эвакуации «Протокол Закат» на столе бункера.
    /// По [E] камера игрока наезжает на лист карты (позу задаёт viewPose), маршрут
    /// прорисовывается по сегментам, а сектора выбираются мышью или [A]/[D].
    /// Все элементы — объекты сцены (Assets/Editor/BunkerScenesAuthoring.cs), здесь ничего не строится.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BunkerEvacuationMap : MonoBehaviour, IGarageInteractable
    {
        public static BunkerEvacuationMap Instance { get; private set; }

        [Header("Camera")]
        [SerializeField] private Transform viewPose;
        [SerializeField] private float viewFieldOfView = 46f;
        [SerializeField] private float moveInDuration = 0.85f;
        [SerializeField] private float moveOutDuration = 0.5f;
        [SerializeField] private float lookSway = 1.6f;

        [Header("Map (world-space canvas on the sheet)")]
        [SerializeField] private EvacuationMapSector[] sectors;
        [SerializeField] private Image[] routeSegments;
        [SerializeField] private RectTransform hereMarker;
        [SerializeField] private float routeRevealDuration = 1.4f;

        [Header("Overlay (screen-space)")]
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private Text sectorIndexText;
        [SerializeField] private Text sectorTitleText;
        [SerializeField] private Text sectorConditionsText;
        [SerializeField] private Text sectorBodyText;

        [Header("Light & Audio")]
        [SerializeField] private Light mapLamp;
        [SerializeField] private float lampBoost = 1.6f;
        [SerializeField] private AudioClip paperOpenClip;
        [SerializeField] private AudioClip paperCloseClip;
        [SerializeField] private AudioClip sectorClip;

        private GaragePlayerController player;
        private Camera viewCamera;
        private Vector3 returnLocalPos;
        private Quaternion returnLocalRot;
        private float returnFov;
        private float baseLampIntensity;
        private readonly System.Collections.Generic.List<Canvas> hiddenCanvases = new System.Collections.Generic.List<Canvas>();
        private int selected = -1;
        private bool isMapOpen;
        private bool isTransitioning;
        private Coroutine routine;

        public bool IsOpen => isMapOpen || isTransitioning;
        public int LastInteractionFrame { get; private set; } = -1;

        private void Awake()
        {
            Instance = this;
            if (mapLamp != null) baseLampIntensity = mapLamp.intensity;
            if (overlay != null)
            {
                overlay.alpha = 0f;
                overlay.gameObject.SetActive(false);
            }
            SetRouteReveal(1f);
            for (int i = 0; sectors != null && i < sectors.Length; i++)
                if (sectors[i] != null) sectors[i].SetHighlighted(false, true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            if (!IsOpen) return;
            if (routine != null) StopCoroutine(routine);
            RestoreCameraImmediate();
        }

        public string GetPromptText() => "[E] Изучить карту эвакуации";

        public bool CanInteract() => !IsOpen;

        public void Interact(GaragePlayerController interactingPlayer)
        {
            if (IsOpen) return;
            player = interactingPlayer != null ? interactingPlayer : FindFirstObjectByType<GaragePlayerController>();
            viewCamera = player != null ? player.PlayerCamera : Camera.main;
            if (viewCamera == null || viewPose == null) return;
            LastInteractionFrame = Time.frameCount;
            routine = StartCoroutine(OpenRoutine());
        }

        private void Update()
        {
            if (!isMapOpen || isTransitioning) return;

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                LastInteractionFrame = Time.frameCount;
                routine = StartCoroutine(CloseRoutine());
                return;
            }

            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Select(selected + 1);
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Select(selected - 1);
            for (int i = 0; sectors != null && i < sectors.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);

            int hovered = SectorUnderCursor();
            if (hovered >= 0 && hovered != selected) Select(hovered);

            // Лёгкое «дыхание» взгляда за курсором, чтобы лист не выглядел скриншотом.
            Vector2 cursor = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width), Input.mousePosition.y / Mathf.Max(1, Screen.height)) - Vector2.one * 0.5f;
            Quaternion target = viewPose.rotation * Quaternion.Euler(-cursor.y * lookSway, cursor.x * lookSway, 0f);
            viewCamera.transform.rotation = Quaternion.Slerp(viewCamera.transform.rotation, target, Time.deltaTime * 4f);

            if (hereMarker != null)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.12f;
                hereMarker.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        private IEnumerator OpenRoutine()
        {
            isTransitioning = true;
            player.SetMovementLocked(true);
            GarageInteractionUI.Instance?.HidePrompt();

            Transform cam = viewCamera.transform;
            returnLocalPos = cam.localPosition;
            returnLocalRot = cam.localRotation;
            returnFov = viewCamera.fieldOfView;

            PlayClip(paperOpenClip, 0.8f);
            SetRouteReveal(0f);
            HideGameplayUI();
            if (overlay != null)
            {
                overlay.gameObject.SetActive(true);
                overlay.alpha = 0f;
            }

            Vector3 fromPos = cam.position;
            Quaternion fromRot = cam.rotation;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / moveInDuration);
                float k = EaseInOut(t);
                // Дуга: сначала наклон к столу, затем приближение — как человек, склоняющийся над картой.
                Vector3 arc = Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.08f;
                cam.position = Vector3.Lerp(fromPos, viewPose.position, k) + arc;
                cam.rotation = Quaternion.Slerp(fromRot, viewPose.rotation, EaseInOut(Mathf.Clamp01(t * 1.25f)));
                viewCamera.fieldOfView = Mathf.Lerp(returnFov, viewFieldOfView, k);
                if (mapLamp != null) mapLamp.intensity = Mathf.Lerp(baseLampIntensity, baseLampIntensity * lampBoost, k);
                yield return null;
            }

            isMapOpen = true;
            isTransitioning = false;
            Select(0);

            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / routeRevealDuration);
                SetRouteReveal(t);
                if (overlay != null) overlay.alpha = Mathf.Clamp01(t * 2.5f);
                yield return null;
            }
            routine = null;
        }

        private IEnumerator CloseRoutine()
        {
            isMapOpen = false;
            isTransitioning = true;
            PlayClip(paperCloseClip, 0.6f);
            for (int i = 0; sectors != null && i < sectors.Length; i++)
                if (sectors[i] != null) sectors[i].SetHighlighted(false, false);

            Transform cam = viewCamera.transform;
            Vector3 fromPos = cam.position;
            Quaternion fromRot = cam.rotation;
            Transform parent = cam.parent;
            Vector3 toPos = parent != null ? parent.TransformPoint(returnLocalPos) : returnLocalPos;
            Quaternion toRot = parent != null ? parent.rotation * returnLocalRot : returnLocalRot;
            float fromAlpha = overlay != null ? overlay.alpha : 0f;

            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / moveOutDuration);
                float k = EaseInOut(t);
                cam.position = Vector3.Lerp(fromPos, toPos, k);
                cam.rotation = Quaternion.Slerp(fromRot, toRot, k);
                viewCamera.fieldOfView = Mathf.Lerp(viewFieldOfView, returnFov, k);
                if (overlay != null) overlay.alpha = Mathf.Lerp(fromAlpha, 0f, Mathf.Clamp01(t * 2f));
                if (mapLamp != null) mapLamp.intensity = Mathf.Lerp(baseLampIntensity * lampBoost, baseLampIntensity, k);
                yield return null;
            }

            RestoreCameraImmediate();
            routine = null;
        }

        private void RestoreCameraImmediate()
        {
            if (viewCamera != null)
            {
                viewCamera.transform.localPosition = returnLocalPos;
                viewCamera.transform.localRotation = returnLocalRot;
                viewCamera.fieldOfView = returnFov;
            }
            if (overlay != null)
            {
                overlay.alpha = 0f;
                overlay.gameObject.SetActive(false);
            }
            if (mapLamp != null) mapLamp.intensity = baseLampIntensity;
            foreach (var canvas in hiddenCanvases)
                if (canvas != null) canvas.enabled = true;
            hiddenCanvases.Clear();
            SetRouteReveal(1f);
            if (hereMarker != null) hereMarker.localScale = Vector3.one;
            selected = -1;
            isMapOpen = false;
            isTransitioning = false;
            if (player != null) player.SetMovementLocked(false);
        }

        // Карта — полноэкранный момент: игровой HUD (задача, хотбар, здоровье) убираем до возврата камеры.
        private void HideGameplayUI()
        {
            Canvas own = overlay != null ? overlay.GetComponentInParent<Canvas>(true) : null;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas == own || !canvas.enabled || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                canvas.enabled = false;
                hiddenCanvases.Add(canvas);
            }
        }

        private void Select(int index)
        {
            if (sectors == null || sectors.Length == 0) return;
            index = (index % sectors.Length + sectors.Length) % sectors.Length;
            if (index == selected) return;
            if (selected >= 0) PlayClip(sectorClip, 0.35f);
            selected = index;

            for (int i = 0; i < sectors.Length; i++)
                if (sectors[i] != null) sectors[i].SetHighlighted(i == selected, false);

            var sector = sectors[selected];
            if (sector == null) return;
            if (sectorIndexText != null) sectorIndexText.text = $"СЕКТОР {selected + 1:00} / {sectors.Length:00}";
            if (sectorTitleText != null) sectorTitleText.text = sector.Title;
            if (sectorConditionsText != null) sectorConditionsText.text = sector.Conditions;
            if (sectorBodyText != null) sectorBodyText.text = sector.Body;
        }

        private int SectorUnderCursor()
        {
            if (sectors == null || viewCamera == null) return -1;
            Vector2 mouse = Input.mousePosition;
            for (int i = 0; i < sectors.Length; i++)
                if (sectors[i] != null && sectors[i].Hotspot != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(sectors[i].Hotspot, mouse, viewCamera))
                    return i;
            return -1;
        }

        private void SetRouteReveal(float t)
        {
            if (routeSegments == null || routeSegments.Length == 0) return;
            float scaled = t * routeSegments.Length;
            for (int i = 0; i < routeSegments.Length; i++)
                if (routeSegments[i] != null) routeSegments[i].fillAmount = Mathf.Clamp01(scaled - i);
        }

        private static void PlayClip(AudioClip clip, float volume)
        {
            if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(clip, volume, 1f);
        }

        private static float EaseInOut(float t) => t * t * (3f - 2f * t);
    }
}
