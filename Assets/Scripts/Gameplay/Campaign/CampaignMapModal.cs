using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Окно глобальной карты кампании.
    /// Отображает 4 сектора кампании, индикаторы открытия, статус босса,
    /// и режим бесконечного выживания (Endless Highway).
    /// Позволяет выбрать любой открытый сектор для старта заезда.
    /// </summary>
    public sealed class CampaignMapModal : MonoBehaviour
    {
        public static CampaignMapModal Instance { get; private set; }

        public static int SelectedStartSector = 1;

        public bool IsVisible { get; private set; }
        [SerializeField] private bool useSceneUI = true;

        GUIStyle titleStyle;
        GUIStyle sectorTitleStyle;
        GUIStyle descStyle;
        GUIStyle statusStyle;
        GUIStyle btnStyle;
        Texture2D panelBgTex;

        private void Awake()
        {
            useSceneUI = true;
            if (Instance == null)
            {
                Instance = this;
                if (!useSceneUI) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void Show()
        {
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public void Toggle()
        {
            IsVisible = !IsVisible;
        }

        private void OnGUI()
        {
            if (useSceneUI) return;
            if (!IsVisible) return;

            EnsureStyles();

            float w = Mathf.Min(780f, Screen.width - 40f);
            float h = Mathf.Min(560f, Screen.height - 40f);
            Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.Box(panel, string.Empty);

            // Заголовок
            GUI.Label(new Rect(panel.x, panel.y + 15f, w, 35f), "ГЛОБАЛЬНАЯ КАРТА КАМПАНИИ", titleStyle);
            GUI.Label(new Rect(panel.x, panel.y + 48f, w, 22f), "Выберите целевой сектор маршрута или бесконечный заезд на рекорд", descStyle);

            var meta = RogueDrive.Meta.SaveService.GetActiveProgress();
            int highestUnlocked = meta != null ? Mathf.Max(1, meta.Data.HighestCampaignLevel) : 1;

            float cardW = (w - 50f) * 0.5f;
            float cardH = 95f;

            // 4 Сектора
            DrawSectorCard(new Rect(panel.x + 20f, panel.y + 80f, cardW, cardH),
                1, "СЕКТОР 1: ШОССЕ", "0 – 1000 м", "Пригородная трасса, базовые орды зомби", highestUnlocked >= 1);

            DrawSectorCard(new Rect(panel.x + 30f + cardW, panel.y + 80f, cardW, cardH),
                2, "СЕКТОР 2: ПУСТОШЬ", "1000 – 2000 м", "Радиационная пыль, бегуны и камикадзе", highestUnlocked >= 2);

            DrawSectorCard(new Rect(panel.x + 20f, panel.y + 185f, cardW, cardH),
                3, "СЕКТОР 3: ПРОМЗОНА", "2000 – 3000 м", "Затопленные эстакады, лидеры стай и тяжелые громилы", highestUnlocked >= 3);

            DrawSectorCard(new Rect(panel.x + 30f + cardW, panel.y + 185f, cardW, cardH),
                4, "СЕКТОР 4: ЦИТАДЕЛЬ", "3000 – 4000 м", "ФИНАЛЬНЫЙ БОСС: Джаггернаут-перехватчик!", highestUnlocked >= 4);

            // Карточка Endless режима
            Rect endlessRect = new Rect(panel.x + 20f, panel.y + 295f, w - 40f, 100f);
            bool isEndlessUnlocked = highestUnlocked >= 5;
            DrawEndlessCard(endlessRect, isEndlessUnlocked, meta?.Data.BestEndlessDistance ?? 0f);

            // Кнопка Закрыть
            float closeY = panel.y + h - 55f;
            if (GUI.Button(new Rect(panel.x + (w - 180f) * 0.5f, closeY, 180f, 40f), "ЗАКРЫТЬ КАРТУ"))
            {
                Hide();
            }
        }

        void DrawSectorCard(Rect rect, int sectorNum, string title, string distText, string desc, bool isUnlocked)
        {
            GUI.Box(rect, string.Empty);

            string statusText = isUnlocked
                ? (SelectedStartSector == sectorNum ? "<color=#2ecc71>▶ ВЫБРАН ДЛЯ СТАРТА ◀</color>" : "<color=#3498db>ОТКРЫТ ★</color>")
                : "<color=#e74c3c>🔒 ЗАБЛОКИРОВАН</color>";

            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 22f), $"{title}  <size=12>({distText})</size>", sectorTitleStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 32f, rect.width - 24f, 20f), desc, descStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 55f, 180f, 22f), statusText, statusStyle);

            if (isUnlocked)
            {
                if (GUI.Button(new Rect(rect.x + rect.width - 125f, rect.y + 50f, 115f, 34f), "ВЫБРАТЬ"))
                {
                    SelectedStartSector = sectorNum;
                    LaunchSector(sectorNum);
                }
            }
        }

        void DrawEndlessCard(Rect rect, bool isUnlocked, float bestDist)
        {
            GUI.Box(rect, string.Empty);

            string title = "РЕЖИМ: БЕСКОНЕЧНАЯ ТРАССА (ENDLESS HIGHWAY)";
            string status = isUnlocked
                ? $"<color=#f39c12>РЕКОРД: {bestDist:0} МЕТРОВ ★</color>"
                : "<color=#7f8c8d>🔒 ОТКРЫВАЕТСЯ ПОСЛЕ ПОБЕДЫ НАД ДЖАГГЕРНАУТОМ В 4 СЕКТОРЕ</color>";

            GUI.Label(new Rect(rect.x + 15f, rect.y + 10f, rect.width - 30f, 24f), title, sectorTitleStyle);
            GUI.Label(new Rect(rect.x + 15f, rect.y + 36f, rect.width - 30f, 20f),
                "Бесконечная процедурная дорога без финиша с непрерывно нарастающей плотностью орды врагов.", descStyle);
            GUI.Label(new Rect(rect.x + 15f, rect.y + 62f, 350f, 22f), status, statusStyle);

            if (isUnlocked)
            {
                if (GUI.Button(new Rect(rect.x + rect.width - 180f, rect.y + 50f, 165f, 38f), "СТАРТ ENDLESS"))
                {
                    SelectedStartSector = 5;
                    LaunchSector(5);
                }
            }
        }

        void LaunchSector(int sector)
        {
            Hide();

            // Если мы находимся в гараже, загружаем сцену заезда
            if (SceneManager.GetActiveScene().name == "GarageScene")
            {
                SceneManager.LoadScene(0);
            }
            else
            {
                // Если мы уже в заезде, перезапускаем с выбранного сектора
                GameRunController run = FindFirstObjectByType<GameRunController>();
                if (run != null)
                {
                    run.Restart();
                }
            }
        }

        void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
            }

            if (sectorTitleStyle == null)
            {
                sectorTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                sectorTitleStyle.normal.textColor = Color.white;
            }

            if (descStyle == null)
            {
                descStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11
                };
                descStyle.normal.textColor = new Color(0.8f, 0.8f, 0.85f);
            }

            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    richText = true
                };
            }
        }
    }
}
