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

        void LaunchSector(int sector)
        {
            Hide();
            SelectedStartSector = sector;
            RogueDrive.UI.SceneUIView.LoadStage(sector);
        }
    }
}

