using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Combat;
using RogueDrive.Meta;
using RogueDrive.Modifiers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RogueDrive.UI
{
    /// <summary>Updates authored Canvas objects. Layout and event targets are saved in the scene.</summary>
    public sealed class SceneUIView : MonoBehaviour
    {
        [SerializeField] private GarageCatalog catalog;
        [SerializeField] private GarageUIController garage;
        [SerializeField] private GameRunController run;
        [SerializeField] private ArcadeCarController car;
        [SerializeField] private PrototypeHud hud;
        [SerializeField] private LevelUpView levelUp;
        [SerializeField] private PauseMenuUI pause;
        [SerializeField] private RunExperienceManager experience;
        [SerializeField] private ComboScoreSystem combo;
        [SerializeField] private BuffCasinoView casino;
        [SerializeField] private GameSessionCoordinator sessionCoordinator;
        [SerializeField] private GameObject mainPanel, garagePanel, hudPanel, settingsPanel, aboutPanel, campaignPanel, pausePanel, resultsPanel, levelPanel;
        [SerializeField] private Text wallet, carInfo, buyCarLabel, resultText, hudText, bannerText, bossText, xpText, comboText;
        [SerializeField] private Button buyCarButton;
        [SerializeField] private Button[] carButtons, upgradeButtons, sectorButtons, offerButtons;
        [SerializeField] private Text[] upgradeLabels, offerLabels;
        [SerializeField] private Button rerollButton;
        [SerializeField] private Slider healthBar, fuelBar, nitroBar, xpBar, bossBar;
        [SerializeField] private Slider masterSlider, musicSlider, effectsSlider, steeringSlider;
        [SerializeField] private Text settingsValues;
        private GraphicsSettingsPanel graphicsSettingsPanel;
        [SerializeField] private GameSettingsDefaults defaults;
        private bool settingsOpen, aboutOpen, campaignOpen;
        private MetaProgress progress;
        private GameObject legacyPauseButton;
        [SerializeField] private GameObject newJourneyPanel;
        [SerializeField] private RogueDrive.Gameplay.Coop.CoopLobbyModal coopLobbyModal;
        [SerializeField] private Button continueJourneyButton, prepareJourneyButton;
        private bool newJourneyOpen, journeyLoading;
        private bool canContinueJourney, canPrepareJourney;
        private bool IsMainMenu => gameObject.scene.name == "MainMenuScene";
        public bool BlocksBackgroundInput => settingsOpen || aboutOpen || campaignOpen || newJourneyOpen || (coopLobbyModal != null && coopLobbyModal.IsOpen);
        public void BindArrivingCar(ArcadeCarController arrivingCar) => car = arrivingCar;

        void Start()
        {
            ResolveMissingReferences();
            CreateGraphicsSettingsControls();
            progress = SaveService.GetActiveProgress(catalog != null ? catalog.Upgrades : null, catalog != null ? catalog.Cars : null);
            LoadSettings();
            if (IsMainMenu)
            {
                canContinueJourney = RogueDrive.Gameplay.Hub.GarageDepartureCheckpoint.CanContinue;
                canPrepareJourney = RogueDrive.Gameplay.Hub.GarageDepartureCheckpoint.CanPrepare;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (masterSlider != null) AudioListener.volume = masterSlider.value;
            Refresh();
            FocusMainMenu();
        }

        void FocusMainMenu()
        {
            if (!IsMainMenu || UnityEngine.EventSystems.EventSystem.current == null) return;
            GameObject panel = graphicsSettingsPanel != null && graphicsSettingsPanel.IsOpen ? graphicsSettingsPanel.gameObject
                : newJourneyOpen ? newJourneyPanel : settingsOpen ? settingsPanel : aboutOpen ? aboutPanel : mainPanel;
            if (panel == null) return;
            foreach (var button in panel.GetComponentsInChildren<Button>())
                if (button.IsInteractable())
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
                    break;
                }
        }

        void ResolveMissingReferences()
        {
            if (run == null) run = FindFirstObjectByType<GameRunController>();
            if (car == null) car = FindFirstObjectByType<ArcadeCarController>();
            if (hud == null) hud = FindFirstObjectByType<PrototypeHud>();
            if (levelUp == null) levelUp = FindFirstObjectByType<LevelUpView>();
            if (pause == null) pause = FindFirstObjectByType<PauseMenuUI>();
            if (experience == null) experience = FindFirstObjectByType<RunExperienceManager>();
            if (combo == null) combo = FindFirstObjectByType<ComboScoreSystem>();
            if (garage == null) garage = FindFirstObjectByType<GarageUIController>();
            if (casino == null) casino = FindFirstObjectByType<BuffCasinoView>();
            if (sessionCoordinator == null) sessionCoordinator = FindFirstObjectByType<GameSessionCoordinator>();
        }

        void Update() => Refresh();

        void Refresh()
        {
            if (run == null || pause == null || combo == null) ResolveMissingReferences();
            bool choosing = levelUp != null && levelUp.IsVisible;
            bool gambling = casino != null && casino.IsVisible;
            bool paused = pause != null && pause.IsPaused;
            bool ended = run != null && run.IsGameOver;
            bool survivalDriving = car != null && car.UsesGarageDriving;
            bool drivingHud = VehicleModularTacticalHud.Instance != null && VehicleModularTacticalHud.Instance.OwnsDrivingHud;
            if (legacyPauseButton == null && hudPanel != null)
                legacyPauseButton = hudPanel.transform.parent.Find("PauseButton")?.gameObject;
            Set(legacyPauseButton, !drivingHud && !paused && !ended);
            if (bannerText != null) bannerText.gameObject.SetActive(!drivingHud);
            if (xpText != null) xpText.gameObject.SetActive(!survivalDriving);
            if (xpBar != null) xpBar.gameObject.SetActive(!survivalDriving);
            if (comboText != null) comboText.gameObject.SetActive(!survivalDriving);
            if (!IsMainMenu && run != null && !paused) settingsOpen = false;
            if (!settingsOpen && graphicsSettingsPanel != null && graphicsSettingsPanel.IsOpen) graphicsSettingsPanel.Close();
            Set(settingsPanel, settingsOpen && graphicsSettingsPanel == null);
            Set(aboutPanel, aboutOpen);
            Set(campaignPanel, campaignOpen);
            if (IsMainMenu)
            {
                Set(mainPanel, !BlocksBackgroundInput);
                Set(newJourneyPanel, newJourneyOpen);
                if (continueJourneyButton != null) continueJourneyButton.interactable = canContinueJourney && !journeyLoading;
                if (prepareJourneyButton != null) prepareJourneyButton.interactable = canPrepareJourney && !journeyLoading;
            }
            Set(pausePanel, paused && !settingsOpen && !choosing && !gambling && !ended);
            Set(resultsPanel, ended && !campaignOpen);
            if (ended && resultsPanel != null && (RogueDrive.Gameplay.Hub.GarageDepartureCheckpoint.OwnsCurrentRun||RogueDrive.Gameplay.Hub.JourneyCheckpoint.OwnsCurrentRun))
                foreach (var label in resultsPanel.GetComponentsInChildren<Text>(true))
                    if (label.text.ToUpperInvariant().Contains("ГАРАЖ")) label.text = "ИЗМЕНИТЬ ПОДГОТОВКУ";
            Set(levelPanel, choosing && !ended);
            Set(hudPanel, run != null && !drivingHud && !ended && !choosing && !gambling && !paused);
            if (wallet != null && progress != null) wallet.text = $"МОНЕТЫ  {progress.Coins}     РЕКОРД  {progress.Data.BestEndlessDistance:0} м";
            if (IsMainMenu && wallet != null) wallet.text = journeyLoading ? "ЗАГРУЗКА…" : canContinueJourney
                  ? (RogueDrive.Gameplay.Hub.JourneyCheckpoint.CanContinue?"КОНТРОЛЬНАЯ ТОЧКА СОХРАНЕНА • СТО":"КОНТРОЛЬНАЯ ТОЧКА СОХРАНЕНА • БУНКЕР 07")
                : "НАЧНИТЕ НОВУЮ ИГРУ • БУНКЕР 07";
            if (settingsValues != null && masterSlider != null)
                settingsValues.text = $"{masterSlider.value:P0}\n{musicSlider.value:P0}\n{effectsSlider.value:P0}\n{steeringSlider.value:0.0}×";
            if (sectorButtons != null && progress != null)
                for (int i = 0; i < sectorButtons.Length; i++) if (sectorButtons[i] != null) sectorButtons[i].interactable = progress.Data.HighestCampaignLevel >= i + 1 || i == 0;
            RefreshGarage();
            if (run != null)
            {
                // Vehicle condition belongs to individual modules; the tactical HUD owns fuel.
                if (healthBar != null) healthBar.gameObject.SetActive(false);
                if (nitroBar != null) nitroBar.gameObject.SetActive(false);
                if (fuelBar != null) fuelBar.gameObject.SetActive(false);
                float targetDist = run.StageTargetDistance;
                float stageProgress = Mathf.Clamp01(run.Distance / targetDist);
                string stageTitle = $"ЭТАП {run.CurrentStageIndex}: {run.Distance:0} / {targetDist:0} м ({stageProgress * 100:0}%)";
                if (hudText != null) hudText.text = $"{stageTitle}\n+{run.CoinsCollected} монет" + (run.IsOutOfFuel ? "\nБак пуст — движение по инерции" : "");

                if (resultText != null)
                {
                    if (run.IsStageVictory)
                    {
                        string nextInfo = survivalDriving ? "\n\nБезопасная СТО достигнута."
                            : run.CurrentStageIndex < 4
                            ? $"\n\n★ ОТКРЫТ ЭТАП {run.CurrentStageIndex + 1} И НОВЫЙ АВТОМОБИЛЬ В ГАРАЖЕ! ★"
                            : "\n\n★ ВСЯ КАМПАНИЯ ПРОЙДЕНА! ОТКРЫТ РЕЖИМ ENDLESS! ★";
                        resultText.text = $"🏆 ЭТАП {run.CurrentStageIndex} ПРОЙДЕН!\n\n{run.EndReason}\nДистанция: {run.Distance:0} м\nЗаработано за этап: +{run.CoinsCollected} монет\nБаланс: {progress?.Coins ?? 0}{nextInfo}";
                    }
                    else
                    {
                        float pct = Mathf.Clamp01(run.Distance / run.StageTargetDistance) * 100f;
                        string rewards = (RogueDrive.Gameplay.Hub.GarageDepartureCheckpoint.OwnsCurrentRun||RogueDrive.Gameplay.Hub.JourneyCheckpoint.OwnsCurrentRun)
                            ? "Добыча этой попытки потеряна. Припасы восстановятся при повторе."
                            : $"Заработано: +{run.CoinsCollected} монет";
                        resultText.text = $"ЗАЕЗД ЗАВЕРШЁН\n\n{run.EndReason}\nПройдено: {run.Distance:0} м из {run.StageTargetDistance:0} м ({pct:0}%)\n{rewards}\nБаланс: {progress?.Coins ?? 0}";
                    }
                }
            }
            if (bannerText != null) bannerText.text = hud != null ? hud.Banner : "";
            if (comboText != null) comboText.text = combo != null ? combo.Banner : "";
            var boss = hud != null ? hud.ActiveBoss : null;
            if (bossBar != null) { bossBar.gameObject.SetActive(boss != null && !boss.IsDead); if (boss != null) Fill(bossBar, boss.CurrentHealth, boss.MaxHealth); }
            if (bossText != null) bossText.text = boss != null && !boss.IsDead ? $"{boss.BossTitle} — {boss.Phase}\n{boss.CurrentHealth:0}/{boss.MaxHealth:0}" : "";
            if (experience != null)
            {
                Fill(xpBar, experience.CurrentXp, experience.RequiredXp);
                if (xpText != null)
                {
                    int tokens = sessionCoordinator != null ? sessionCoordinator.CasinoTokens : 0;
                    xpText.text = $"УРОВЕНЬ {experience.CurrentLevel}    ОПЫТ {experience.CurrentXp:0}/{experience.RequiredXp:0}    ЖЕТОНЫ КАЗИНО: {tokens}";
                }
            }
            if (choosing && offerButtons != null)
            {
                for (int i = 0; i < offerButtons.Length; i++)
                {
                    bool available = levelUp.Offers != null && i < levelUp.Offers.Count;
                    offerButtons[i].gameObject.SetActive(available);
                    if (!available) continue;
                    var offer = levelUp.Offers[i];
                    string pickLabel = Application.isMobilePlatform ? "ВЫБРАТЬ" : $"[{i + 1}] ВЫБРАТЬ";
                    offerLabels[i].text = $"{offer.Rarity} • {offer.Category}\n\n{offer.DisplayName}\nУровень {levelUp.OfferLevel(i) + 1}\n\n{offer.Description}\n\n" + (levelUp.OfferCompletesSynergy(i) ? "СОБИРАЕТ СИНЕРГИЮ!\n" : "") + pickLabel;
                }
                if (rerollButton != null)
                {
                    rerollButton.interactable = levelUp.RemainingRerolls > 0;
                    var rerollTxt = rerollButton.GetComponentInChildren<Text>();
                    if (rerollTxt != null)
                    {
                        rerollTxt.text = Application.isMobilePlatform
                            ? $"ОБНОВИТЬ КАРТЫ ({levelUp.RemainingRerolls})"
                            : $"[R] ОБНОВИТЬ КАРТЫ ({levelUp.RemainingRerolls})";
                    }
                }
            }
        }

        void RefreshGarage()
        {
            if (garage == null || garage.Progress == null || garage.Catalog == null) return;
            var meta = garage.Progress; var data = garage.Catalog;
            if (garage.SelectedIndex < 0 || garage.SelectedIndex >= data.Cars.Count) return;
            var selected = data.Cars[garage.SelectedIndex];
            if (selected == null) return;
            bool owned = meta.OwnsCar(selected.Id), equipped = meta.SelectedCar == selected;
            bool unlocked = meta.IsCarUnlocked(selected);
            if (carInfo != null) carInfo.text = $"{selected.DisplayName}\nСкорость {selected.GetStat(StatId.Speed,20):0} м/с\nПрочность {selected.GetStat(StatId.MaxHealth,100):0}\nБак {selected.GetStat(StatId.FuelCapacity,100):0}\nМасса {selected.GetStat(StatId.Mass,1200):0} кг";
            if (buyCarLabel != null)
            {
                if (!unlocked && !owned)
                {
                    int reqStage = selected.GetRequiredCampaignLevel() - 1;
                    buyCarLabel.text = $"🔒 ПРОЙДИТЕ ЭТАП {reqStage}";
                }
                else
                {
                    buyCarLabel.text = equipped ? "ВЫБРАН ДЛЯ ЗАЕЗДА" : owned ? "ВЫБРАТЬ АВТОМОБИЛЬ" : $"КУПИТЬ • {selected.Price} МОНЕТ";
                }
            }
            if (buyCarButton != null) buyCarButton.interactable = !equipped && (owned || (unlocked && meta.Coins >= selected.Price));

            if (carButtons != null)
            {
                for (int i = 0; i < carButtons.Length && i < data.Cars.Count; i++)
                {
                    if (carButtons[i] == null) continue;
                    var c = data.Cars[i];
                    if (c == null) continue;
                    bool cOwned = meta.OwnsCar(c.Id);
                    bool cUnlocked = meta.IsCarUnlocked(c);
                    bool cEquipped = meta.SelectedCar != null && meta.SelectedCar.Id == c.Id;
                    string prefix = cEquipped ? "★ " : (cOwned ? "✔ " : (cUnlocked ? "💰 " : "🔒 "));
                    var txt = carButtons[i].GetComponentInChildren<Text>();
                    if (txt != null) txt.text = $"{prefix}{c.DisplayName}";
                }
            }

            for (int i = 0; upgradeButtons != null && i < upgradeButtons.Length && i < data.Upgrades.Count; i++)
            {
                var track = data.Upgrades[i];
                if (track == null) continue;
                int level = meta.GetUpgradeLevel(selected.Id, track);
                bool canAfford = meta.Coins >= track.GetCost(level);
                bool isMax = level >= track.MaxLevel;
                if (upgradeLabels != null && i < upgradeLabels.Length && upgradeLabels[i] != null)
                {
                    if (!owned)
                    {
                        upgradeLabels[i].text = $"{track.DisplayName}   {level}/{track.MaxLevel}\nАвтомобиль не куплен";
                    }
                    else if (isMax)
                    {
                        upgradeLabels[i].text = $"{track.DisplayName}   {level}/{track.MaxLevel}\nМАКСИМУМ";
                    }
                    else
                    {
                        upgradeLabels[i].text = $"{track.DisplayName}   {level}/{track.MaxLevel}\nБонус +{track.GetBonus(level):0.##}   •   УЛУЧШИТЬ {track.GetCost(level)}";
                    }
                }
                if (upgradeButtons[i] != null)
                {
                    upgradeButtons[i].interactable = owned && !isMax && canAfford;
                }
            }
        }

        // Persistent Button.onClick targets remain visible and editable in Inspector.
        public void Navigate(int action)
        {
            if (IsMainMenu && HandleJourneyAction(action)) { Refresh(); FocusMainMenu(); return; }
            switch (action)
            {
                case 0:
                    Time.timeScale = 1f;
                    if (garage != null)
                    {
                        garage.StartRun();
                    }
                    else
                    {
                        int sector = CampaignMapModal.SelectedStartSector;
                        if (sector <= 1)
                        {
                            StartBunkerPrologue();
                        }
                        else
                        {
                            LoadStage(sector);
                        }
                    }
                    break;
                case 1:
                    if (run != null && (RogueDrive.Gameplay.Hub.GarageDepartureCheckpoint.OwnsCurrentRun||RogueDrive.Gameplay.Hub.JourneyCheckpoint.OwnsCurrentRun)) run.LoadGarage();
                    else { Time.timeScale = 1f; SceneTransitionManager.SwitchScene("GarageScene"); }
                    break;
                case 2: settingsOpen = true; LoadSettings(); graphicsSettingsPanel?.Open(); break;
                case 3: aboutOpen = true; break;
                case 4:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
                case 5: Time.timeScale = 1f; SceneTransitionManager.SwitchScene("MainMenuScene"); break;
                case 6: campaignOpen = true; break;
                case 7: settingsOpen = aboutOpen = campaignOpen = false; graphicsSettingsPanel?.Close(); break;
                case 8: SaveSettings(); settingsOpen = false; graphicsSettingsPanel?.Close(); break;
                case 9: pause?.PauseGame(); break;
                case 10: pause?.ResumeGame(); break;
                case 11: run?.Restart(); break;
                case 12: levelUp?.Reroll(); break;
                case 13: NextStage(); break;
            }
            Refresh();
            FocusMainMenu();
        }

        bool HandleJourneyAction(int action)
        {
            if (journeyLoading) return true;
            switch (action)
            {
                case 0:
                    newJourneyOpen = true;
                    return true;
                case 1:
                    if (canPrepareJourney) journeyLoading = RogueDrive.Gameplay.Hub.GarageDepartureCheckpoint.RequestRestore(false);
                    return true;
                case 6:
                    if (canContinueJourney) journeyLoading = RogueDrive.Gameplay.Hub.GarageDepartureCheckpoint.ContinueJourney();
                    return true;
                case 7:
                    newJourneyOpen = false;
                    return false;
                case 14:
                    if (!newJourneyOpen || !Application.CanStreamedLevelBeLoaded("GarageScene")) return true;
                    journeyLoading = true;
                    RogueDrive.Gameplay.Hub.GarageDepartureCheckpoint.ClearJourney();
                    SaveService.ResetToNew(catalog != null ? catalog.Upgrades : null, catalog != null ? catalog.Cars : null);
                    CampaignMapModal.SelectedStartSector = 1;
                    StartBunkerPrologue();
                    return true;
                case 15:
                    if (coopLobbyModal != null)
                    {
                        coopLobbyModal.Open();
                    }
                    return true;
                default: return false;
            }
        }

        public void NextStage()
        {
            Time.timeScale = 1f;
            int nextStage = (run != null ? run.CurrentStageIndex : 1) + 1;
            if (nextStage > 4) nextStage = 1;
            CampaignMapModal.SelectedStartSector = nextStage;
            LoadStage(nextStage);
        }

        public static void StartBunkerPrologue()
        {
            Time.timeScale = 1f;
            RogueDrive.Gameplay.Hub.GaragePrologueManager.ForcePrologueAwakening = true;
            PlayerPrefs.DeleteKey("BunkerPrologueSeen_V1");
            PlayerPrefs.DeleteKey("GaragePrologueDone");
            PlayerPrefs.Save();
            SceneTransitionManager.SwitchScene("GarageScene");
        }

        public static void LoadStage(int sector)
        {
            Time.timeScale = 1f;
            string sceneName = sector switch
            {
                1 => "Stage1_Outskirts",
                2 => "Stage2_Wasteland",
                3 => "Stage3_Industrial",
                4 => "Stage4_Citadel",
                _ => "Stage1_Outskirts"
            };

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneTransitionManager.SwitchScene(sceneName);
            }
            else
            {
                SceneTransitionManager.SwitchScene("Stage1_Outskirts");
            }
        }

        public void SelectSector(int sector)
        {
            if (sector < 1 || sector > 5 || (sector > 1 && (progress == null || progress.Data.HighestCampaignLevel < sector))) return;
            CampaignMapModal.SelectedStartSector = sector;
            campaignOpen = false; Time.timeScale = 1f;
            LoadStage(sector);
        }
        public void SelectOffer(int index) => levelUp?.Choose(index);
        public void LoadSettings()
        {
            if (masterSlider == null) return;
            masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", defaults != null ? defaults.MasterVolume : .85f);
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", defaults != null ? defaults.MusicVolume : .75f);
            effectsSlider.value = PlayerPrefs.GetFloat("SfxVolume", defaults != null ? defaults.EffectsVolume : .9f);
            steeringSlider.value = PlayerPrefs.GetFloat("SteerSensitivity", defaults != null ? defaults.SteeringSensitivity : 1f);
        }
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", masterSlider.value); PlayerPrefs.SetFloat("MusicVolume", musicSlider.value);
            PlayerPrefs.SetFloat("SfxVolume", effectsSlider.value); PlayerPrefs.SetFloat("SteerSensitivity", steeringSlider.value);
            PlayerPrefs.Save(); AudioListener.volume = masterSlider.value;
        }
        private void CreateGraphicsSettingsControls()
        {
            if (settingsPanel == null || graphicsSettingsPanel != null) return;
            graphicsSettingsPanel = GraphicsSettingsPanel.Create(settingsPanel, () =>
            {
                settingsOpen = false;
                Refresh();
                FocusMainMenu();
            });
        }
        static void Fill(Slider slider, float value, float max) { if (slider != null) slider.SetValueWithoutNotify(max > 0 ? Mathf.Clamp01(value / max) : 0); }
        static void Set(GameObject target, bool visible) { if (target != null && target.activeSelf != visible) target.SetActive(visible); }
    }
}
