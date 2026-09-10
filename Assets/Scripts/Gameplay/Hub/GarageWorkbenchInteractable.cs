using RogueDrive.Meta;
using RogueDrive.UI;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивный верстак модернизации автомобиля в убежище.
    /// При взаимодействии открывает интерфейс прокачки 6 веток машины
    /// и освобождает курсор для кликов. Выход по ESC возвращает в режим ходьбы.
    /// </summary>
    public sealed class GarageWorkbenchInteractable : MonoBehaviour, IGarageInteractable
    {
        [Header("References")]
        [SerializeField] private GarageUIController garageUI;
        [SerializeField] private SceneUIView sceneUI;

        private bool isOpen;
        private GaragePlayerController activePlayer;

        public bool IsOpen => isOpen;

        private void Start()
        {
            if (garageUI == null) garageUI = FindFirstObjectByType<GarageUIController>();
            if (sceneUI == null) sceneUI = FindFirstObjectByType<SceneUIView>();
        }

        public string GetPromptText()
        {
            return "[E] Верстак: модернизация узлов машины";
        }

        public bool CanInteract() => !isOpen;

        public void Interact(GaragePlayerController player)
        {
            activePlayer = player;
            OpenWorkbench();
        }

        public void OpenWorkbench()
        {
            isOpen = true;
            if (activePlayer != null)
            {
                activePlayer.SetMovementLocked(true);
            }

            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.ShowNotification("ВЕРСТАК: Прокачайте узлы машины. [ESC] — отойти от верстака", 6f);
            }
        }

        public void CloseWorkbench()
        {
            isOpen = false;
            if (activePlayer != null)
            {
                activePlayer.SetMovementLocked(false);
            }
        }

        private static Texture2D bgDimTex;
        private static Texture2D panelTex;
        private static Texture2D btnTex;
        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle textStyle;
        private static GUIStyle btnStyle;
        private static GUIStyle closeBtnStyle;
        private static GUIStyle maxStyle;

        private static void EnsureStyles()
        {
            if (panelTex == null)
            {
                panelTex = new Texture2D(1, 1);
                panelTex.SetPixel(0, 0, new Color(0.08f, 0.10f, 0.14f, 0.94f));
                panelTex.Apply();

                btnTex = new Texture2D(1, 1);
                btnTex.SetPixel(0, 0, new Color(0.18f, 0.44f, 0.38f, 1f));
                btnTex.Apply();

                titleStyle = new GUIStyle
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(1f, 0.85f, 0.4f) }
                };

                subtitleStyle = new GUIStyle
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.8f, 0.85f, 0.9f) }
                };

                textStyle = new GUIStyle
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };

                btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };

                closeBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.5f, 0.5f) }
                };

                maxStyle = new GUIStyle
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.5f, 0.9f, 0.6f) }
                };
            }
        }

        private void OnGUI()
        {
            if (!isOpen || garageUI == null || garageUI.Catalog == null) return;

            EnsureStyles();

            // Окно верстака справа, чтобы по центру было видно стоящий автомобиль
            float winW = 500f;
            float winH = 540f;
            float winX = Screen.width - winW - 35f;
            float winY = (Screen.height - winH) * 0.5f;

            // Фон панели
            GUI.DrawTexture(new Rect(winX, winY, winW, winH), panelTex);

            // Заголовок
            GUI.Label(new Rect(winX + 24, winY + 18, winW - 48, 30), "🛠️ ВЕРСТАК МОДЕРНИЗАЦИИ", titleStyle);

            int coins = garageUI.Progress != null ? garageUI.Progress.Coins : 0;
            var car = garageUI.Progress != null ? garageUI.Progress.SelectedCar : null;
            string carName = car != null ? car.DisplayName : "Автомобиль";
            string carId = car != null ? car.Id : "";

            GUI.Label(new Rect(winX + 24, winY + 50, winW - 48, 24), $"Машина: {carName}  |  💰 Баланс: {coins} монет", subtitleStyle);

            // Список веток прокачки
            var upgrades = garageUI.Catalog.Upgrades;
            float startY = winY + 86f;

            for (int i = 0; i < upgrades.Count; i++)
            {
                var track = upgrades[i];
                int curLevel = garageUI.Progress != null ? garageUI.Progress.GetUpgradeLevel(carId, track) : 0;
                int cost = track.GetCost(curLevel);
                bool canAfford = coins >= cost && curLevel < track.MaxLevel;

                float rowY = startY + i * 54f;

                // Название и уровень
                GUI.Label(new Rect(winX + 24, rowY, 280, 22), $"{track.DisplayName}", textStyle);
                string levelStr = $"Уровень {curLevel} / {track.MaxLevel}";
                GUI.Label(new Rect(winX + 24, rowY + 20, 280, 20), levelStr, subtitleStyle);

                if (curLevel >= track.MaxLevel)
                {
                    GUI.Label(new Rect(winX + winW - 170, rowY + 8, 146, 32), "✔ МАКСИМУМ", maxStyle);
                }
                else
                {
                    string btnText = $"Улучшить ({cost} 💰)";
                    GUI.enabled = canAfford;
                    if (GUI.Button(new Rect(winX + winW - 175, rowY + 6, 150, 36), btnText, btnStyle))
                    {
                        garageUI.BuyUpgradeAt(i);
                        if (RogueDrive.Audio.AudioManager.Instance != null)
                        {
                            RogueDrive.Audio.AudioManager.Instance.PlaySwitchClick();
                        }
                    }
                    GUI.enabled = true;
                }
            }

            // Кнопка закрытия
            if (GUI.Button(new Rect(winX + 24, winY + winH - 52, winW - 48, 38), "ОТОЙТИ ОТ ВЕРСТАКА [ESC]", closeBtnStyle))
            {
                CloseWorkbench();
            }
        }
    }
}
