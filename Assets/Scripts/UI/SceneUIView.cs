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
        [SerializeField] private GameObject mainPanel, garagePanel, hudPanel, settingsPanel, aboutPanel, campaignPanel, pausePanel, resultsPanel, levelPanel, touchPanel;
        [SerializeField] private Text wallet, carInfo, buyCarLabel, resultText, hudText, bannerText, bossText, xpText, comboText;
        [SerializeField] private Button buyCarButton;
        [SerializeField] private Button[] carButtons, upgradeButtons, sectorButtons, offerButtons;
        [SerializeField] private Text[] upgradeLabels, offerLabels;
        [SerializeField] private Button rerollButton;
        [SerializeField] private Slider healthBar, fuelBar, nitroBar, xpBar, bossBar;
        [SerializeField] private Slider masterSlider, musicSlider, effectsSlider, steeringSlider;
        [SerializeField] private Text settingsValues;
        [SerializeField] private GameSettingsDefaults defaults;
        private bool settingsOpen, aboutOpen, campaignOpen;
        private MetaProgress progress;
        public bool BlocksBackgroundInput => settingsOpen || aboutOpen || campaignOpen;

        void Start()
        {
            ResolveMissingReferences();
            progress = SaveService.GetActiveProgress(catalog != null ? catalog.Upgrades : null, catalog != null ? catalog.Cars : null);
            LoadSettings();
            if (masterSlider != null) AudioListener.volume = masterSlider.value;
            Refresh();
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
        }

        void Update() => Refresh();

        void Refresh()
        {
            if (run == null || pause == null || combo == null) ResolveMissingReferences();
            bool choosing = levelUp != null && levelUp.IsVisible;
            bool paused = pause != null && pause.IsPaused;
            bool ended = run != null && run.IsGameOver;
            if (run != null && !paused) settingsOpen = false;
            Set(settingsPanel, settingsOpen);
            Set(aboutPanel, aboutOpen);
            Set(campaignPanel, campaignOpen);
            Set(pausePanel, paused && !settingsOpen && !choosing && !ended);
            Set(resultsPanel, ended && !campaignOpen);
            Set(levelPanel, choosing && !ended);
            Set(hudPanel, run != null && !ended && !choosing && !paused);
            Set(touchPanel, run != null && !ended && !choosing && !paused && Application.isMobilePlatform);
            if (wallet != null && progress != null) wallet.text = $"МОНЕТЫ  {progress.Coins}     РЕКОРД  {progress.Data.BestEndlessDistance:0} м";
            if (settingsValues != null && masterSlider != null)
                settingsValues.text = $"{masterSlider.value:P0}\n{musicSlider.value:P0}\n{effectsSlider.value:P0}\n{steeringSlider.value:0.0}×";
            if (sectorButtons != null && progress != null)
                for (int i = 0; i < sectorButtons.Length; i++) if (sectorButtons[i] != null) sectorButtons[i].interactable = progress.Data.HighestCampaignLevel >= i + 1 || i == 0;
            RefreshGarage();
            if (run != null)
            {
                Fill(healthBar, run.Health, run.MaxHealth); Fill(fuelBar, run.Fuel, run.MaxFuel); Fill(nitroBar, run.Nitro, run.MaxNitro);
                float targetDist = run.StageTargetDistance;
                float stageProgress = Mathf.Clamp01(run.Distance / targetDist);
                string stageTitle = $"ЭТАП {run.CurrentStageIndex}: {run.Distance:0} / {targetDist:0} м ({stageProgress * 100:0}%)";
                if (hudText != null) hudText.text = $"{stageTitle}\nHP {run.Health:0}/{run.MaxHealth:0}    ТОПЛИВО {run.Fuel:0}/{run.MaxFuel:0}    НИТРО {run.Nitro:0}%\n+{run.CoinsCollected} монет     {(car != null ? car.SpeedKmh : 0):0} км/ч" + (run.IsOutOfFuel ? "\nБак пуст — движение по инерции" : "");

                if (resultText != null)
                {
                    if (run.IsStageVictory)
                    {
                        string nextInfo = run.CurrentStageIndex < 4
                            ? $"\n\n★ ОТКРЫТ ЭТАП {run.CurrentStageIndex + 1} И НОВЫЙ АВТОМОБИЛЬ В ГАРАЖЕ! ★"
                            : "\n\n★ ВСЯ КАМПАНИЯ ПРОЙДЕНА! ОТКРЫТ РЕЖИМ ENDLESS! ★";
                        resultText.text = $"🏆 ЭТАП {run.CurrentStageIndex} ПРОЙДЕН!\n\n{run.EndReason}\nДистанция: {run.Distance:0} м\nЗаработано за этап: +{run.CoinsCollected} монет\nБаланс: {progress?.Coins ?? 0}{nextInfo}";
                    }
                    else
                    {
                        float pct = Mathf.Clamp01(run.Distance / run.StageTargetDistance) * 100f;
                        resultText.text = $"ЗАЕЗД ЗАВЕРШЁН\n\n{run.EndReason}\nПройдено: {run.Distance:0} м из {run.StageTargetDistance:0} м ({pct:0}%)\nЗаработано: +{run.CoinsCollected} монет\nБаланс: {progress?.Coins ?? 0}";
                    }
                }
            }
            if (bannerText != null) bannerText.text = hud != null ? hud.Banner : "";
            if (comboText != null) comboText.text = combo != null ? combo.Banner : "";
            var boss = hud != null ? hud.ActiveBoss : null;
            if (bossBar != null) { bossBar.gameObject.SetActive(boss != null && !boss.IsDead); if (boss != null) Fill(bossBar, boss.CurrentHealth, boss.MaxHealth); }
            if (bossText != null) bossText.text = boss != null && !boss.IsDead ? $"{boss.BossTitle} — {boss.Phase}\n{boss.CurrentHealth:0}/{boss.MaxHealth:0}" : "";
            if (experience != null) { Fill(xpBar, experience.CurrentXp, experience.RequiredXp); if (xpText != null) xpText.text = $"УРОВЕНЬ {experience.CurrentLevel}    ОПЫТ {experience.CurrentXp:0}/{experience.RequiredXp:0}"; }
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
            switch (action)
            {
                case 0: Time.timeScale = 1f; if (garage != null) garage.StartRun(); else LoadStage(CampaignMapModal.SelectedStartSector); break;
                case 1: Time.timeScale = 1f; SceneTransitionManager.SwitchScene("GarageScene"); break;
                case 2: settingsOpen = true; LoadSettings(); break;
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
                case 7: settingsOpen = aboutOpen = campaignOpen = false; break;
                case 8: SaveSettings(); settingsOpen = false; break;
                case 9: pause?.PauseGame(); break;
                case 10: pause?.ResumeGame(); break;
                case 11: run?.Restart(); break;
                case 12: levelUp?.Reroll(); break;
                case 13: NextStage(); break;
            }
            Refresh();
        }

        public void NextStage()
        {
            Time.timeScale = 1f;
            int nextStage = (run != null ? run.CurrentStageIndex : 1) + 1;
            if (nextStage > 4) nextStage = 1;
            CampaignMapModal.SelectedStartSector = nextStage;
            LoadStage(nextStage);
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
                _ => "RogueDrivePrototype"
            };

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneTransitionManager.SwitchScene(sceneName);
            }
            else
            {
                SceneTransitionManager.SwitchScene("RogueDrivePrototype");
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
        static void Fill(Slider slider, float value, float max) { if (slider != null) slider.SetValueWithoutNotify(max > 0 ? Mathf.Clamp01(value / max) : 0); }
        static void Set(GameObject target, bool visible) { if (target != null && target.activeSelf != visible) target.SetActive(visible); }
    }
}
