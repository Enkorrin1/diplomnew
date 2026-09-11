using System.Collections;
using System.Collections.Generic;
using RogueDrive.Audio;
using RogueDrive.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Combat
{
    /// <summary>
    /// Боевая система активных спецспособностей автомобиля на PC:
    /// — [ЛКМ] Залп самонаводящихся ракет (Homing Missiles)
    /// — [Q]   Сброс шипов и мин под колеса (Spike / Mine Drop)
    /// — [F]   Кинетический таран (Kinetic Ram Blast)
    /// — Шкала Адреналина: дрифт и нитро ускоряют откат всех способностей в 3 раза.
    /// Переведена на современный UGUI Canvas (полностью без OnGUI).
    /// </summary>
    [RequireComponent(typeof(ArcadeCarController))]
    public sealed class VehicleCombatSkills : MonoBehaviour
    {
        [Header("Cooldowns (Base Seconds)")]
        [SerializeField] private float missileBaseCooldown = 5.0f;
        [SerializeField] private float spikeBaseCooldown = 7.0f;
        [SerializeField] private float ramBaseCooldown = 6.0f;

        [Header("Missile Specs")]
        [SerializeField] private int missileSalvoCount = 2;
        [SerializeField] private float missileDamage = 65f;

        [Header("Spike Specs")]
        [SerializeField] private float spikeDamage = 45f;
        [SerializeField] private float spikeSlowDuration = 3.0f;

        [Header("Ram Specs")]
        [SerializeField] private float ramImpulseForce = 1800f;
        [SerializeField] private float ramRadius = 5.5f;
        [SerializeField] private float ramDamage = 110f;

        private ArcadeCarController car;
        private Rigidbody body;

        private float missileTimer;
        private float spikeTimer;
        private float ramTimer;

        // UGUI Elements
        private Canvas skillsCanvas;
        private GameObject skillsRoot;
        private Text missileText;
        private Text spikeText;
        private Text ramText;
        private GameObject adrenalineBadge;

        public bool IsMissileReady => missileTimer <= 0f;
        public bool IsSpikeReady => spikeTimer <= 0f;
        public bool IsRamReady => ramTimer <= 0f;

        public float MissileCooldownNormalized => Mathf.Clamp01(missileTimer / missileBaseCooldown);
        public float SpikeCooldownNormalized => Mathf.Clamp01(spikeTimer / spikeBaseCooldown);
        public float RamCooldownNormalized => Mathf.Clamp01(ramTimer / ramBaseCooldown);

        public bool IsAdrenalineActive => car != null && (car.IsNitroActive || (car.IsHandbrakeActive && car.SpeedKmh > 20f));

        private void Awake()
        {
            car = GetComponent<ArcadeCarController>();
            body = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (car != null && car.Run != null && car.Run.IsGameOver) return;
            if (Time.timeScale <= 0f) return;

            // 1. Обновление таймеров перезарядки с бонусом адреналина
            float cdSpeedMultiplier = IsAdrenalineActive ? 3.0f : 1.0f;
            float dt = Time.deltaTime * cdSpeedMultiplier;

            if (missileTimer > 0f) missileTimer = Mathf.Max(0f, missileTimer - dt);
            if (spikeTimer > 0f) spikeTimer = Mathf.Max(0f, spikeTimer - dt);
            if (ramTimer > 0f) ramTimer = Mathf.Max(0f, ramTimer - dt);

            // 2. Чтение нажатий клавиш на PC
            if (Input.GetMouseButtonDown(0) && IsMissileReady)
            {
                TriggerMissiles();
            }

            if (Input.GetKeyDown(KeyCode.Q) && IsSpikeReady)
            {
                TriggerSpikes();
            }

            if (Input.GetKeyDown(KeyCode.F) && IsRamReady)
            {
                TriggerKineticRam();
            }

            UpdateSkillsUI();
        }

        public void TriggerMissiles()
        {
            missileTimer = missileBaseCooldown;

            // Ищем цели в радиусе 45м перед автомобилем
            List<Transform> candidateTargets = new List<Transform>();

            // Приоритет 1: машины рейдеров
            foreach (var raider in FindObjectsByType<RaiderVehicleAI>(FindObjectsSortMode.None))
            {
                if (raider != null && raider.gameObject.activeInHierarchy)
                {
                    candidateTargets.Add(raider.transform);
                }
            }

            // Приоритет 2: зомби-враги
            if (candidateTargets.Count < missileSalvoCount)
            {
                foreach (var enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
                {
                    if (enemy != null && enemy.gameObject.activeInHierarchy)
                    {
                        candidateTargets.Add(enemy.transform);
                        if (candidateTargets.Count >= 8) break;
                    }
                }
            }

            // Залп микро-ракет
            StartCoroutine(LaunchSalvoRoutine(candidateTargets));

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayShoot(0.2f);
            }
        }

        private IEnumerator LaunchSalvoRoutine(List<Transform> targets)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;

            for (int i = 0; i < missileSalvoCount; i++)
            {
                Transform chosenTarget = targets.Count > 0 ? targets[i % targets.Count] : null;

                GameObject missileObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                missileObj.name = $"HomingMissile_{i}";
                missileObj.transform.localScale = new Vector3(0.2f, 0.45f, 0.2f);

                // Смещенный старт с легким разбросом
                Vector3 spawnPos = origin + transform.right * ((i - 1) * 0.45f);
                missileObj.transform.position = spawnPos;
                missileObj.transform.rotation = transform.rotation * Quaternion.Euler(Random.Range(-12f, -5f), Random.Range(-15f, 15f), 0f);

                // Материал ракеты
                Renderer r = missileObj.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.25f, 0.15f) };
                }

                Collider col = missileObj.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                HomingMissile missile = missileObj.AddComponent<HomingMissile>();
                missile.Launch(chosenTarget, missileDamage);

                yield return new WaitForSeconds(0.08f);
            }
        }

        public void TriggerSpikes()
        {
            spikeTimer = spikeBaseCooldown;

            // Спавн шипов позади машины
            Vector3 spawnPos = transform.position - transform.forward * 2.8f;
            GameObject spikeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spikeObj.name = "SpikeTrap_Deployed";
            spikeObj.transform.position = spawnPos;
            spikeObj.transform.rotation = transform.rotation;
            spikeObj.transform.localScale = new Vector3(2.4f, 0.12f, 0.8f);

            Renderer r = spikeObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.22f, 0.24f, 0.28f) };
            }

            Collider col = spikeObj.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            SpikeTrap trap = spikeObj.AddComponent<SpikeTrap>();
            trap.Configure(spikeDamage, spikeSlowDuration);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayImpact(0.8f);
            }
        }

        public void TriggerKineticRam()
        {
            ramTimer = ramBaseCooldown;

            // Мгновенный импульс вперед
            if (body != null)
            {
                body.AddForce(transform.forward * (ramImpulseForce * body.mass * 0.05f), ForceMode.Impulse);
            }

            // Сотрясение экрана
            ArcadeCameraFollow.Instance?.TriggerShake(0.85f, 0.35f);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayExplosion(1.0f);
            }

            // Ударная кинетическая волна перед бампером
            Vector3 ramOrigin = transform.position + transform.forward * 2.2f;
            Collider[] colliders = Physics.OverlapSphere(ramOrigin, ramRadius);

            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col.transform.IsChildOf(transform) || col.gameObject == gameObject) continue;

                // Урон и отброс врагов (зомби, разрушаемые объекты)
                var enemy = col.GetComponentInParent<EnemyBase>() ?? col.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    enemy.TakeDamage(ramDamage);
                }

                // Урон и жесткий толчок рейдерам
                var raider = col.GetComponentInParent<RaiderVehicleAI>() ?? col.GetComponent<RaiderVehicleAI>();
                if (raider != null)
                {
                    raider.TakeDamage(ramDamage);
                    raider.ApplyRamForce(transform.forward * 40f + transform.right * Random.Range(-25f, 25f));
                }

                // Физический толчок
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 pushDir = (col.transform.position - transform.position).normalized + Vector3.up * 0.35f;
                    rb.AddForce(pushDir * 1200f, ForceMode.Impulse);
                }
            }
        }

        private void UpdateSkillsUI()
        {
            if (car != null && car.Run != null && car.Run.IsGameOver)
            {
                if (skillsRoot != null && skillsRoot.activeSelf) skillsRoot.SetActive(false);
                return;
            }

            BuildUIIfNeeded();

            if (skillsRoot != null && !skillsRoot.activeSelf) skillsRoot.SetActive(true);

            if (missileText != null)
            {
                missileText.text = IsMissileReady ? "[ЛКМ] РАКЕТЫ\n<color=#55FF77>ГОТОВО</color>" : $"[ЛКМ] РАКЕТЫ\n<color=#FFAA44>{missileTimer:0.0} с</color>";
            }

            if (spikeText != null)
            {
                spikeText.text = IsSpikeReady ? "[Q] ШИПЫ\n<color=#55FF77>ГОТОВО</color>" : $"[Q] ШИПЫ\n<color=#FFAA44>{spikeTimer:0.0} с</color>";
            }

            if (ramText != null)
            {
                ramText.text = IsRamReady ? "[F] ТАРАН\n<color=#55FF77>ГОТОВО</color>" : $"[F] ТАРАН\n<color=#FFAA44>{ramTimer:0.0} с</color>";
            }

            if (adrenalineBadge != null)
            {
                adrenalineBadge.SetActive(IsAdrenalineActive);
            }
        }

        private void BuildUIIfNeeded()
        {
            if (skillsCanvas != null) return;

            GameObject canvasObj = new GameObject("CombatSkills_Canvas");
            skillsCanvas = canvasObj.AddComponent<Canvas>();
            skillsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            skillsCanvas.sortingOrder = 40;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? 
                               Resources.GetBuiltinResource<Font>("Arial.ttf");

            skillsRoot = new GameObject("SkillsRoot");
            skillsRoot.transform.SetParent(canvasObj.transform, false);

            var rootRect = skillsRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-24f, 24f);
            rootRect.sizeDelta = new Vector2(460f, 65f);

            missileText = CreateSlot(skillsRoot.transform, standardFont, new Vector2(0f, 0f), new Vector2(140f, 54f));
            spikeText = CreateSlot(skillsRoot.transform, standardFont, new Vector2(150f, 0f), new Vector2(140f, 54f));
            ramText = CreateSlot(skillsRoot.transform, standardFont, new Vector2(300f, 0f), new Vector2(140f, 54f));

            // Адреналин бэдж
            adrenalineBadge = new GameObject("AdrenalineBadge");
            adrenalineBadge.transform.SetParent(skillsRoot.transform, false);
            var adImg = adrenalineBadge.AddComponent<Image>();
            adImg.color = new Color(0.24f, 0.16f, 0.04f, 0.95f);
            adImg.raycastTarget = false;

            var adRect = adrenalineBadge.GetComponent<RectTransform>();
            adRect.anchorMin = new Vector2(0f, 1f);
            adRect.anchorMax = new Vector2(1f, 1f);
            adRect.pivot = new Vector2(0.5f, 0f);
            adRect.anchoredPosition = new Vector2(0f, 6f);
            adRect.sizeDelta = new Vector2(0f, 26f);

            GameObject adTextObj = new GameObject("Text");
            adTextObj.transform.SetParent(adrenalineBadge.transform, false);
            var adText = adTextObj.AddComponent<Text>();
            if (standardFont != null) adText.font = standardFont;
            adText.fontSize = 12;
            adText.fontStyle = FontStyle.Bold;
            adText.alignment = TextAnchor.MiddleCenter;
            adText.color = new Color(1f, 0.85f, 0.2f);
            adText.text = "⚡ АДРЕНАЛИН: ОТКАТ x3";
            adText.raycastTarget = false;

            var atRect = adTextObj.GetComponent<RectTransform>();
            atRect.anchorMin = Vector2.zero;
            atRect.anchorMax = Vector2.one;
            atRect.offsetMin = Vector2.zero;
            atRect.offsetMax = Vector2.zero;

            adrenalineBadge.SetActive(false);
        }

        private Text CreateSlot(Transform parent, Font font, Vector2 anchoredPos, Vector2 size)
        {
            GameObject slotObj = new GameObject("SkillSlot");
            slotObj.transform.SetParent(parent, false);

            var img = slotObj.AddComponent<Image>();
            img.color = new Color(0.08f, 0.12f, 0.16f, 0.92f);
            img.raycastTarget = false;

            var rect = slotObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(slotObj.transform, false);
            var text = textObj.AddComponent<Text>();
            if (font != null) text.font = font;
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var tRect = textObj.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(4f, 2f);
            tRect.offsetMax = new Vector2(-4f, -2f);

            return text;
        }

        private void OnDestroy()
        {
            if (skillsCanvas != null)
            {
                Destroy(skillsCanvas.gameObject);
            }
        }
    }
}
