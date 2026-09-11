using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>HUD РїСЂРѕС‚РѕС‚РёРїР° Р·Р°РµР·РґР° СЃ РёРЅРґРёРєР°С‚РѕСЂР°РјРё Р·РґРѕСЂРѕРІСЊСЏ, С‚РѕРїР»РёРІР°, РЅРёС‚СЂРѕ, СЃРїРёРґРѕРјРµС‚СЂРѕРј, Р±РёРѕРјРѕРј Рё РѕРїРѕРІРµС‰РµРЅРёСЏРјРё.</summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        public static PrototypeHud Instance { get; private set; }

        [SerializeField] private GameRunController run;
        [SerializeField] private ArcadeCarController car;
        [SerializeField] private bool useSceneUI = true;
        public bool UseSceneUI => useSceneUI;
        public string BiomeTitle => currentBiomeTitle;
        public string Banner => bannerTimer > 0f ? bannerTitle + "\n" + bannerSubtitle : string.Empty;
        public BossJuggernaut ActiveBoss => activeBoss;


        string currentBiomeTitle = "РЁРћРЎРЎР•: РџР РР“РћР РћР”";
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
        public bool ShowVictoryModal => showVictoryModal;

        public void ShowCampaignVictoryScreen()
        {
            showVictoryModal = true;
        }

        private void Awake()
        {
            Instance = this;
            useSceneUI = true;

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

        }
    }
}
