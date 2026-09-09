using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>HUD прототипа заезда с индикаторами здоровья, топлива, нитро, спидометром и подсказками управления.</summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private GameRunController run;
        [SerializeField] private ArcadeCarController car;

        GUIStyle headingStyle;
        GUIStyle valueStyle;
        GUIStyle warningStyle;
        GUIStyle endStyle;
        GUIStyle speedStyle;

        Texture2D barBgTex;
        Texture2D healthTex;
        Texture2D fuelTex;
        Texture2D nitroTex;

        public void Configure(GameRunController controller, ArcadeCarController carController = null)
        {
            run = controller;
            car = carController;
        }

        private void Awake()
        {
            if (car == null)
                car = FindFirstObjectByType<ArcadeCarController>();
            if (run == null)
                run = FindFirstObjectByType<GameRunController>();
        }

        private void OnDestroy()
        {
            if (barBgTex != null) Destroy(barBgTex);
            if (healthTex != null) Destroy(healthTex);
            if (fuelTex != null) Destroy(fuelTex);
            if (nitroTex != null) Destroy(nitroTex);
        }

        void OnGUI()
        {
            if (run == null)
                return;

            EnsureStyles();
            EnsureTextures();

            // 1. Главная панель приборов слева сверху
            const float panelWidth = 320f;
            const float panelHeight = 195f;
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

            // Предупреждение о накате при 0 топлива
            if (run.IsOutOfFuel && !run.IsGameOver)
            {
                GUI.Label(new Rect(32f, 168f, 280f, 22f), "⚠ БАК ПУСТ! НАКАТ ПО ИНЕРЦИИ...", warningStyle);
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

            // 4. Окно завершения заезда
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
        }

        void EnsureTextures()
        {
            if (barBgTex != null)
                return;

            barBgTex = MakeColorTex(new Color(0.12f, 0.14f, 0.18f, 0.9f));
            healthTex = MakeColorTex(new Color(0.2f, 0.85f, 0.35f));
            fuelTex = MakeColorTex(new Color(0.95f, 0.75f, 0.15f));
            nitroTex = MakeColorTex(new Color(0.2f, 0.65f, 1f));
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
