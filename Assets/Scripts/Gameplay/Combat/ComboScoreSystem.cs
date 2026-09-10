using System;
using System.Collections;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Combat
{
    /// <summary>
    /// Аркадная система комбо, очков стиля и дрифта (Combo & Juiciness System).
    /// Отслеживает серии уничтожения врагов, награждает бонусными монетами и нитро,
    /// выводит сочные динамические баннеры ("DOUBLE KILL", "RAMPAGE!", "DRIFT KING!")
    /// и активирует сотрясение экрана при масштабных взрывах.
    /// </summary>
    public sealed class ComboScoreSystem : MonoBehaviour
    {
        public static ComboScoreSystem Instance { get; private set; }
        [SerializeField] private bool useSceneUI = true;
        public string Banner => bannerTimer > 0f ? activeBannerText : string.Empty;

        [Header("References")]
        [SerializeField] private GameRunController run;
        [SerializeField] private ArcadeCarController car;

        [Header("Combo Configuration")]
        [SerializeField] private float comboWindow = 2.8f;

        private int comboCount = 0;
        private float comboTimer = 0f;
        private string activeBannerText = string.Empty;
        private Color activeBannerColor = Color.yellow;
        private float bannerTimer = 0f;
        private float bannerScale = 1f;

        // Дрифт
        private float currentDriftTime = 0f;
        private bool isDrifting = false;

        // GUI
        private GUIStyle comboTitleStyle;
        private GUIStyle comboSubStyle;
        private Texture2D bannerBgTex;

        private void Awake()
        {
            Instance = this;
            useSceneUI = true;
            if (run == null) run = FindFirstObjectByType<GameRunController>();
            if (car == null) car = FindFirstObjectByType<ArcadeCarController>();
        }

        private void Update()
        {
            if (car == null) car = FindFirstObjectByType<ArcadeCarController>();
            if (run == null) run = FindFirstObjectByType<GameRunController>();

            float dt = Time.deltaTime;

            // 1. Таймер комбо
            if (comboTimer > 0f)
            {
                comboTimer -= dt;
                if (comboTimer <= 0f)
                {
                    EndCombo();
                }
            }

            // 2. Таймер анимации баннера
            if (bannerTimer > 0f)
            {
                bannerTimer -= dt;
                bannerScale = Mathf.MoveTowards(bannerScale, 1f, dt * 3.5f);
            }

            // 3. Отслеживание дрифта автомобиля
            TrackDrift(dt);
        }

        private void TrackDrift(float dt)
        {
            if (car == null || run == null || run.IsGameOver) return;

            Rigidbody body = car.GetComponent<Rigidbody>();
            if (body == null) return;

            float speed = car.SpeedMps;
            float lateralSpeed = Mathf.Abs(Vector3.Dot(body.linearVelocity, car.transform.right));

            if (speed > 12f && lateralSpeed > 4.5f && Mathf.Abs(Input.GetAxis("Horizontal")) > 0.4f)
            {
                isDrifting = true;
                currentDriftTime += dt;

                if (currentDriftTime >= 1.5f && currentDriftTime - dt < 1.5f)
                {
                    // Награда за затяжной дрифт
                    TriggerBanner("🔥 СИЛОВОЙ ЗАНОС! +15 НИТРО", new Color(1f, 0.5f, 0.1f));
                    run.AddNitro(15f);
                    run.AddCoins(3);
                    ArcadeCameraFollow.Instance?.TriggerShake(0.18f, 0.25f);
                }
            }
            else
            {
                if (isDrifting && currentDriftTime > 2.5f)
                {
                    TriggerBanner("🏆 МАСТЕР ДРИФТА! +5 МОНЕТ", new Color(0.2f, 0.9f, 1f));
                    run.AddCoins(5);
                }
                isDrifting = false;
                currentDriftTime = 0f;
            }
        }

        /// <summary>
        /// Вызывается при уничтожении врага (тараном, турелью или аурой).
        /// </summary>
        public void RegisterKill(Vector3 position, int xpValue)
        {
            comboCount++;
            comboTimer = comboWindow;
            bannerScale = 1.4f;

            int bonusCoins = 0;
            string title = string.Empty;
            Color col = Color.yellow;
            float shake = 0.2f;

            switch (comboCount)
            {
                case 2:
                    title = "DOUBLE KILL! x2";
                    col = new Color(1f, 0.9f, 0.2f);
                    bonusCoins = 1;
                    shake = 0.25f;
                    break;
                case 3:
                    title = "TRIPLE KILL! x3";
                    col = new Color(1f, 0.6f, 0.1f);
                    bonusCoins = 2;
                    shake = 0.35f;
                    break;
                case 5:
                    title = "MEGA KILL! x5";
                    col = new Color(1f, 0.25f, 0.25f);
                    bonusCoins = 4;
                    shake = 0.45f;
                    break;
                case 8:
                    title = "💥 RAMPAGE! x8 💥";
                    col = new Color(0.9f, 0.2f, 1f);
                    bonusCoins = 8;
                    shake = 0.6f;
                    break;
                case 12:
                default:
                    if (comboCount >= 12 && comboCount % 4 == 0)
                    {
                        title = $"⚡ UNSTOPPABLE x{comboCount}!! ⚡";
                        col = new Color(0.1f, 1f, 0.85f);
                        bonusCoins = 12;
                        shake = 0.75f;
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(title))
            {
                TriggerBanner(title, col);
                if (bonusCoins > 0 && run != null)
                {
                    run.AddCoins(bonusCoins);
                    run.AddNitro(8f);
                }
                ArcadeCameraFollow.Instance?.TriggerShake(0.22f, shake);

                // Кинематический рапид при мега-комбо (5+): кратковременное замедление времени
                if (comboCount >= 5)
                {
                    StartCoroutine(KillStreakSlowMo());
                }
            }
        }

        public void TriggerBanner(string text, Color color)
        {
            activeBannerText = text;
            activeBannerColor = color;
            bannerTimer = 2.2f;
            bannerScale = 1.35f;
        }

        private bool isKillSlowMo;

        private IEnumerator KillStreakSlowMo()
        {
            if (isKillSlowMo) yield break; // не накладываем несколько замедлений
            isKillSlowMo = true;

            float originalTimeScale = Time.timeScale;
            Time.timeScale = 0.3f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            yield return new WaitForSecondsRealtime(0.25f);

            // Плавное восстановление
            float elapsed = 0f;
            float restoreDuration = 0.15f;
            while (elapsed < restoreDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / restoreDuration;
                Time.timeScale = Mathf.Lerp(0.3f, 1f, t * t);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return null;
            }

            Time.timeScale = originalTimeScale > 0.01f ? originalTimeScale : 1f;
            Time.fixedDeltaTime = 0.02f;
            isKillSlowMo = false;
        }

        private void EndCombo()
        {
            comboCount = 0;
            comboTimer = 0f;
        }

        private void OnGUI()
        {
            if (useSceneUI) return;
            EnsureStyles();

            if (bannerTimer <= 0f || string.IsNullOrEmpty(activeBannerText))
                return;

            float baseW = 420f;
            float baseH = 55f;
            float w = baseW * bannerScale;
            float h = baseH * bannerScale;

            Rect bannerRect = new Rect((Screen.width - w) / 2f, 95f, w, h);

            Color prevCol = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.08f, Mathf.Clamp01(bannerTimer * 1.5f) * 0.85f);
            GUI.DrawTexture(bannerRect, Texture2D.whiteTexture);

            GUI.color = activeBannerColor;
            comboTitleStyle.fontSize = Mathf.RoundToInt(22f * bannerScale);
            GUI.Label(bannerRect, activeBannerText, comboTitleStyle);

            GUI.color = prevCol;
        }

        private void EnsureStyles()
        {
            if (comboTitleStyle == null)
            {
                comboTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
        }
    }
}
