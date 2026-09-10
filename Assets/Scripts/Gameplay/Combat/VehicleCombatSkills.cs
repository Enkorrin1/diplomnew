using System.Collections;
using System.Collections.Generic;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Combat
{
    /// <summary>
    /// Боевая система активных спецспособностей автомобиля на PC:
    /// — [ЛКМ] Залп самонаводящихся ракет (Homing Missiles)
    /// — [Q]   Сброс шипов и мин под колеса (Spike / Mine Drop)
    /// — [F]   Кинетический таран (Kinetic Ram Blast)
    /// — Шкала Адреналина: дрифт и нитро ускоряют откат всех способностей в 3 раза.
    /// </summary>
    [RequireComponent(typeof(ArcadeCarController))]
    public sealed class VehicleCombatSkills : MonoBehaviour
    {
        [Header("Cooldowns (Base Seconds)")]
        [SerializeField] private float missileBaseCooldown = 5.0f;
        [SerializeField] private float spikeBaseCooldown = 6.5f;
        [SerializeField] private float ramBaseCooldown = 7.5f;

        [Header("Skill Parameters")]
        [SerializeField] private int missileSalvoCount = 3;
        [SerializeField] private float ramImpulseForce = 1800f;
        [SerializeField] private float ramDamage = 140f;
        [SerializeField] private float ramRadius = 6.0f;

        private ArcadeCarController car;
        private Rigidbody body;

        private float missileTimer;
        private float spikeTimer;
        private float ramTimer;

        // UI Styles
        private GUIStyle skillBoxReady;
        private GUIStyle skillBoxCooldown;
        private GUIStyle adrenalineStyle;
        private Texture2D readyBgTex;
        private Texture2D cdBgTex;
        private Texture2D adrenalineBgTex;

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
                foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
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
                missile.Launch(chosenTarget);

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
            trap.Configure();

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

                // Урон и отброс зомби
                var enemy = col.GetComponentInParent<EnemyHealth>() ?? col.GetComponent<EnemyHealth>();
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

        private void OnGUI()
        {
            if (car != null && car.Run != null && car.Run.IsGameOver) return;
            if (Time.timeScale <= 0f) return;

            EnsureStyles();

            // Отображение 3 слотов способностей в правом нижнем углу
            float boxW = 140f;
            float boxH = 46f;
            float margin = 18f;
            float spacing = 8f;

            float totalW = boxW * 3f + spacing * 2f;
            float startX = Screen.width - totalW - margin;
            float y = Screen.height - boxH - margin;

            // Слот 1: Ракеты
            DrawSkillBox(new Rect(startX, y, boxW, boxH), "[ЛКМ] РАКЕТЫ", IsMissileReady, missileTimer);

            // Слот 2: Шипы
            DrawSkillBox(new Rect(startX + (boxW + spacing), y, boxW, boxH), "[Q] ШИПЫ / МИНА", IsSpikeReady, spikeTimer);

            // Слот 3: Таран
            DrawSkillBox(new Rect(startX + (boxW + spacing) * 2f, y, boxW, boxH), "[F] ТАРАН", IsRamReady, ramTimer);

            // Индикатор Адреналина
            if (IsAdrenalineActive)
            {
                float adW = 280f;
                float adH = 24f;
                float adX = Screen.width - adW - margin;
                float adY = y - adH - 6f;
                GUI.Box(new Rect(adX, adY, adW, adH), "⚡ АДРЕНАЛИН: ОТКАТ x3", adrenalineStyle);
            }
        }

        private void DrawSkillBox(Rect rect, string label, bool isReady, float timer)
        {
            if (isReady)
            {
                GUI.Box(rect, label + "\n<color=#55FF77>ГОТОВО</color>", skillBoxReady);
            }
            else
            {
                GUI.Box(rect, label + $"\n<color=#FFAA44>{timer:0.0} с</color>", skillBoxCooldown);
            }
        }

        private void EnsureStyles()
        {
            if (readyBgTex == null)
            {
                readyBgTex = MakeTex(new Color(0.08f, 0.16f, 0.12f, 0.90f));
            }
            if (cdBgTex == null)
            {
                cdBgTex = MakeTex(new Color(0.14f, 0.10f, 0.08f, 0.90f));
            }
            if (adrenalineBgTex == null)
            {
                adrenalineBgTex = MakeTex(new Color(0.24f, 0.16f, 0.04f, 0.95f));
            }

            if (skillBoxReady == null)
            {
                skillBoxReady = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = readyBgTex, textColor = Color.white },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
            }

            if (skillBoxCooldown == null)
            {
                skillBoxCooldown = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = cdBgTex, textColor = new Color(0.85f, 0.85f, 0.85f) },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
            }

            if (adrenalineStyle == null)
            {
                adrenalineStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = adrenalineBgTex, textColor = new Color(1f, 0.85f, 0.2f) },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private Texture2D MakeTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (readyBgTex != null) Destroy(readyBgTex);
            if (cdBgTex != null) Destroy(cdBgTex);
            if (adrenalineBgTex != null) Destroy(adrenalineBgTex);
        }
    }
}
