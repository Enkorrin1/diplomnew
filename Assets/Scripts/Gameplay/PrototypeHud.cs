using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>HUD прототипа заезда с индикаторами здоровья, топлива, нитро, спидометром, биомом и оповещениями.</summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        public static PrototypeHud Instance { get; private set; }

        [SerializeField] private GameRunController run;
        [SerializeField] private ArcadeCarController car;
        [SerializeField] private bool useSceneUI;
        public string BiomeTitle => currentBiomeTitle;
        public string Banner => bannerTimer > 0f ? bannerTitle + "\n" + bannerSubtitle : string.Empty;
        public BossJuggernaut ActiveBoss => activeBoss;

        GUIStyle headingStyle;
        GUIStyle valueStyle;
        GUIStyle warningStyle;
        GUIStyle endStyle;
        GUIStyle speedStyle;
        GUIStyle bannerTitleStyle;
        GUIStyle bannerSubStyle;

        Texture2D barBgTex;
        Texture2D healthTex;
        Texture2D fuelTex;
        Texture2D nitroTex;
        Texture2D bannerBgTex;
        Texture2D bannerAccentTex;

        string currentBiomeTitle = "ШОССЕ: ПРИГОРОД";
        Color currentBiomeColor = new Color(0.35f, 0.9f, 1f);

        float bannerTimer;
        string bannerTitle = string.Empty;
        string bannerSubtitle = string.Empty;

        public void Configure(GameRunController controller, ArcadeCarController carController = null)
        {
            run = controller;
            car = carController;
        }

        public void ShowBiomeNotification(string title, string subtitle, Color color)
        {
            currentBiomeTitle = title;
            currentBiomeColor = color;
            bannerTitle = title;
            bannerSubtitle = subtitle;
            bannerTimer = 4.0f;
        }

        BossJuggernaut activeBoss;
        bool showVictoryModal;

        public void ShowCampaignVictoryScreen()
        {
            showVictoryModal = true;
        }

        private void Awake()
        {
            Instance = this;

            if (car == null)
                car = FindFirstObjectByType<ArcadeCarController>();
            if (run == null)
                run = FindFirstObjectByType<GameRunController>();

            BossJuggernaut.BossSpawned += HandleBossSpawned;
            BossJuggernaut.BossDefeated += HandleBossDefeated;
        }

        void HandleBossSpawned(BossJuggernaut b)
        {
            activeBoss = b;
        }

        void HandleBossDefeated(BossJuggernaut b)
        {
            if (activeBoss == b)
                activeBoss = null;
        }

        private void Update()
        {
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            BossJuggernaut.BossSpawned -= HandleBossSpawned;
            BossJuggernaut.BossDefeated -= HandleBossDefeated;

            if (barBgTex != null) Destroy(barBgTex);
            if (healthTex != null) Destroy(healthTex);
            if (fuelTex != null) Destroy(fuelTex);
            if (nitroTex != null) Destroy(nitroTex);
            if (bannerBgTex != null) Destroy(bannerBgTex);
            if (bannerAccentTex != null) Destroy(bannerAccentTex);
        }

        void OnGUI()
        {
            if (useSceneUI) return;
            if (run == null)
                return;

            EnsureStyles();
            EnsureTextures();

            // 1. Главная панель приборов слева сверху
            const float panelWidth = 320f;
            const float panelHeight = 215f;
            GUI.Box(new Rect(18f, 18f, panelWidth, panelHeight), string.Empty);

            GUI.Label(new Rect(32f, 24f, 240f, 26f), "ROGUE DRIVE: SURVIVAL", headingStyle);

            // Полоса Прочности (HP)
            DrawStatBar(32f, 54f, 180f, 14f, run.Health, run.MaxHealth, healthTex);
            GUI.Label(new Rect(220f, 50f, 110f, 22f), $"HP: {run.Health:0}/{run.MaxHealth:0}", valueStyle);

            // Полоса Топлива
            DrawStatBar(32f, 76f, 180f, 14f, run.Fuel, run.MaxFuel, fuelTex);
            GUI.Label(new Rect(220f, 72f, 110f, 22f), $"Бак: {run.Fuel:0}/{run.MaxFuel:0}", valueStyle);

            // Полоса Нитро
            DrawStatBar(32f, 98f, 180f, 14f, run.Nitro, run.MaxNitro, nitroTex);
            string nitroLabel = car != null && car.IsNitroActive ? "НИТРО (АКТИВНО)" : $"Нитро: {run.Nitro:0}%";
            GUI.Label(new Rect(220f, 94f, 110f, 22f), nitroLabel, valueStyle);

            // Дистанция и монеты
            GUI.Label(new Rect(32f, 122f, 260f, 22f), $"Дистанция: {run.Distance:0} м", valueStyle);
            GUI.Label(new Rect(32f, 144f, 260f, 22f), $"Монеты заезда: <color=#ffd700>+{run.CoinsCollected}</color>", valueStyle);

            // Текущий биом
            string biomeHex = ColorUtility.ToHtmlStringRGB(currentBiomeColor);
            GUI.Label(new Rect(32f, 166f, 260f, 22f), $"Локация: <color=#{biomeHex}>{currentBiomeTitle}</color>", valueStyle);

            // Предупреждение о накате при 0 топлива
            if (run.IsOutOfFuel && !run.IsGameOver)
            {
                GUI.Label(new Rect(32f, 188f, 280f, 22f), "⚠ БАК ПУСТ! НАКАТ ПО ИНЕРЦИИ...", warningStyle);
            }

            // 2. Спидометр справа снизу
            if (car != null)
            {
                float speed = Mathf.Max(0f, car.SpeedKmh);
                float speedX = Screen.width - 190f;
                float speedY = Screen.height - 95f;
                GUI.Box(new Rect(speedX, speedY, 172f, 75f), string.Empty);
                GUI.Label(new Rect(speedX + 10f, speedY + 10f, 152f, 38f), $"{speed:0} <size=15>КМ/Ч</size>", speedStyle);
                string gear = car.IsNitroActive ? "РЕЖИМ: НИТРО" : (speed > 1f ? "РЕЖИМ: ДРАЙВ" : "НЕЙТРАЛЬ");
                GUI.Label(new Rect(speedX + 10f, speedY + 46f, 152f, 20f), gear, valueStyle);
            }

            // 3. Подсказка по управлению слева снизу
            GUI.Label(new Rect(18f, Screen.height - 35f, 600f, 25f), "W / S — Газ/Тормоз | A / D — Руление | Пробел / Shift — НИТРО", valueStyle);

            // 4. Баннер смены биома по центру сверху
            if (bannerTimer > 0f)
            {
                DrawBiomeBanner();
            }

            // 5. Полоса здоровья босса Джаггернаута
            if (activeBoss != null && !activeBoss.IsDead)
            {
                DrawBossHealthBar();
            }

            // 6. Окно триумфальной победы в кампании
            if (showVictoryModal)
            {
                DrawCampaignVictoryModal();
                return;
            }

            // 7. Окно завершения заезда
            if (!run.IsGameOver)
                return;

            float width = 420f;
            float height = 260f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x, panel.y + 20f, width, 32f), "ЗАЕЗД ЗАВЕРШЁН", endStyle);
            GUI.Label(new Rect(panel.x, panel.y + 56f, width, 22f), run.EndReason, valueStyle);
            GUI.Label(new Rect(panel.x, panel.y + 82f, width, 22f), $"Дистанция: {run.Distance:0} м", valueStyle);
            GUI.Label(new Rect(panel.x, panel.y + 106f, width, 22f), $"Заработано: <color=#ffd700>+{run.CoinsCollected} монет</color>", valueStyle);

            int totalCoins = RogueDrive.Meta.SaveService.GetActiveProgress()?.Coins ?? 0;
            GUI.Label(new Rect(panel.x, panel.y + 130f, width, 22f), $"Всего в банке: <color=#ffd700>{totalCoins} монет</color>", valueStyle);

            float btnY = panel.y + 175f;
            if (GUI.Button(new Rect(panel.x + 30f, btnY, 170f, 45f), "В ГАРАЖ"))
            {
                run.LoadGarage();
            }

            if (GUI.Button(new Rect(panel.x + 220f, btnY, 170f, 45f), "ЕЩЁ ЗАЕЗД"))
            {
                run.Restart();
            }
        }

        void DrawBossHealthBar()
        {
            if (activeBoss == null || activeBoss.IsDead) return;

            float barW = 480f;
            float barH = 34f;
            float barX = (Screen.width - barW) * 0.5f;
            float barY = 22f;

            GUI.Box(new Rect(barX - 10f, barY - 6f, barW + 20f, barH + 28f), string.Empty);

            string phaseStr = activeBoss.Phase switch
            {
                BossPhase.Phase1_Minefield => "ФАЗА 1: МИНЫ",
                BossPhase.Phase2_ArtilleryEscort => "ФАЗА 2: ЗАЛПЫ И ЭСКОРТ",
                BossPhase.Phase3_BerserkRam => "ФАЗА 3: ТАРАН",
                _ => string.Empty
            };

            GUI.Label(new Rect(barX, barY - 2f, barW, 20f), $"☠ {activeBoss.BossTitle} ({phaseStr}) ☠", headingStyle);

            float hpPercent = Mathf.Clamp01(activeBoss.CurrentHealth / activeBoss.MaxHealth);
            GUI.DrawTexture(new Rect(barX, barY + 22f, barW, 14f), barBgTex);

            Color prev = GUI.color;
            GUI.color = Color.Lerp(Color.red, new Color(1f, 0.4f, 0f), hpPercent);
            GUI.DrawTexture(new Rect(barX, barY + 22f, barW * hpPercent, 14f), healthTex);
            GUI.color = prev;

            GUI.Label(new Rect(barX, barY + 20f, barW, 18f), $"{activeBoss.CurrentHealth:0} / {activeBoss.MaxHealth:0} HP", valueStyle);
        }

        void DrawCampaignVictoryModal()
        {
            float width = 500f;
            float height = 310f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x, panel.y + 18f, width, 32f), "★ ЦИТАДЕЛЬ ПРОБИТА! ПОБЕДА! ★", endStyle);
            GUI.Label(new Rect(panel.x, panel.y + 54f, width, 22f), "Джаггернаут уничтожен! Эвакуационный вертолет на подходе!", valueStyle);
            GUI.Label(new Rect(panel.x, panel.y + 80f, width, 22f), $"Дистанция кампании: {run.Distance:0} м", valueStyle);
            GUI.Label(new Rect(panel.x, panel.y + 104f, width, 22f), "Бонус победы: <color=#ffd700>+50 монет</color>", valueStyle);
            GUI.Label(new Rect(panel.x, panel.y + 130f, width, 22f), "<color=#2ecc71>РЕЖИМ БЕСКОНЕЧНОГО ВЫЖИВАНИЯ (ENDLESS) РАЗБЛОКИРОВАН!</color>", valueStyle);

            int totalCoins = RogueDrive.Meta.SaveService.GetActiveProgress()?.Coins ?? 0;
            GUI.Label(new Rect(panel.x, panel.y + 158f, width, 22f), $"Всего в банке: <color=#ffd700>{totalCoins} монет</color>", valueStyle);

            float btnY = panel.y + 205f;
            if (GUI.Button(new Rect(panel.x + 35f, btnY, 200f, 48f), "В ГАРАЖ С ПОБЕДОЙ"))
            {
                showVictoryModal = false;
                run.LoadGarage();
            }

            if (GUI.Button(new Rect(panel.x + 265f, btnY, 200f, 48f), "ПРОДОЛЖИТЬ В ENDLESS"))
            {
                showVictoryModal = false;
                CampaignMapModal.SelectedStartSector = 5;
            }
        }

        void DrawBiomeBanner()
        {
            float alpha = 1f;
            if (bannerTimer > 3.5f)
                alpha = Mathf.Clamp01((4.0f - bannerTimer) / 0.5f);
            else if (bannerTimer < 1.0f)
                alpha = Mathf.Clamp01(bannerTimer / 1.0f);

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            float bannerW = 540f;
            float bannerH = 76f;
            float bannerX = (Screen.width - bannerW) * 0.5f;
            float bannerY = 28f;

            // Фон баннера
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerW, bannerH), bannerBgTex);

            // Верхняя и нижняя неоновая акцентная полоска
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerW, 3f), bannerAccentTex);
            GUI.DrawTexture(new Rect(bannerX, bannerY + bannerH - 3f, bannerW, 3f), bannerAccentTex);

            bannerTitleStyle.normal.textColor = currentBiomeColor;
            GUI.Label(new Rect(bannerX, bannerY + 10f, bannerW, 32f), $"▶  {bannerTitle}  ◀", bannerTitleStyle);
            GUI.Label(new Rect(bannerX, bannerY + 44f, bannerW, 22f), bannerSubtitle, bannerSubStyle);

            GUI.color = prevColor;
        }

        void DrawStatBar(float x, float y, float w, float h, float current, float max, Texture2D fillTex)
        {
            float fillPct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            GUI.DrawTexture(new Rect(x, y, w, h), barBgTex);
            if (fillPct > 0f)
            {
                GUI.DrawTexture(new Rect(x + 1f, y + 1f, (w - 2f) * fillPct, h - 2f), fillTex);
            }
        }

        void EnsureStyles()
        {
            if (headingStyle != null)
                return;

            headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.78f, 0.2f) }
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                richText = true,
                normal = { textColor = Color.white }
            };

            warningStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.35f, 0.2f) }
            };

            endStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            speedStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = new Color(0.35f, 0.9f, 1f) }
            };

            bannerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };

            bannerSubStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f) }
            };
        }

        void EnsureTextures()
        {
            if (barBgTex != null)
                return;

            barBgTex = MakeColorTex(new Color(0.12f, 0.14f, 0.18f, 0.9f));
            healthTex = MakeColorTex(new Color(0.2f, 0.85f, 0.35f));
            fuelTex = MakeColorTex(new Color(0.95f, 0.75f, 0.15f));
            nitroTex = MakeColorTex(new Color(0.2f, 0.65f, 1f));
            bannerBgTex = MakeColorTex(new Color(0.06f, 0.08f, 0.12f, 0.92f));
            bannerAccentTex = MakeColorTex(new Color(1f, 0.75f, 0.15f));
        }

        Texture2D MakeColorTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}
