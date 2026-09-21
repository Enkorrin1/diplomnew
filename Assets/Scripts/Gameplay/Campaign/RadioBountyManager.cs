using System;
using System.Collections;
using RogueDrive.Audio;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Combat;
using RogueDrive.Gameplay.Track;
using UnityEngine;
using UnityEngine.UI;

namespace RogueDrive.Gameplay.Campaign
{
    public enum BountyType
    {
        SpeedDemon = 0,         // Поддерживать скорость >= 20 м/с (72 км/ч)
        RammingCarnage = 1,     // Убийства тараном бампера
        DriftMaster = 2,        // Набрать время заноса
        TurretSharpshooter = 3, // Убийства турелью
        BarrelDemolition = 4    // Детонация бочек
    }

    /// <summary>
    /// Менеджер динамических радио-контрактов на ходу (In-Run Radio Bounties).
    /// Периодически принимает оперативные вызовы от диспетчера Маяка с временными задачами:
    /// «Форсаж», «Таранный каток», «Мастер заноса», «Снайпер турели», «Подрывник».
    /// Награждает мгновенными бонусами монет и нитро, повышая адреналин и реиграбельность.
    /// </summary>
    public sealed class RadioBountyManager : MonoBehaviour
    {
        public static RadioBountyManager Instance { get; private set; }

        public static event Action<BountyType, bool> BountyCompleted;

        [Header("Dispatch Settings")]
        [SerializeField, Min(10f)] private float initialDelay = 25f;
        [SerializeField, Min(30f)] private float minIntervalBetweenBounties = 55f;
        [SerializeField, Min(40f)] private float maxIntervalBetweenBounties = 80f;

        [Header("Active Bounty State")]
        [SerializeField] private bool isContractActive = false;
        [SerializeField] private BountyType activeType = BountyType.SpeedDemon;
        [SerializeField] private float timeRemaining = 0f;
        [SerializeField] private float currentProgress = 0f;
        [SerializeField] private float targetProgress = 10f;
        [SerializeField] private string contractTitle = string.Empty;

        private ArcadeCarController car;
        private GameRunController run;

        private float dispatchTimer;
        private int contractCounter = 1;

        // UI Widget
        private GameObject bountyPanelRoot;
        private Text titleText;
        private Text objectiveText;
        private Text timerText;
        private Image timerProgressBar;

        public bool IsContractActive => isContractActive;
        public BountyType ActiveType => activeType;
        public float TimeRemaining => timeRemaining;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            BuildBountyUI();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            UnsubscribeEvents();
        }

        private void Start()
        {
            car = FindFirstObjectByType<ArcadeCarController>();
            run = FindFirstObjectByType<GameRunController>();

            dispatchTimer = initialDelay;
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            EnemyBase.AnyEnemyKilled += OnEnemyKilled;
            ExplosiveBarrel.BarrelExploded += OnBarrelExploded;
        }

        private void UnsubscribeEvents()
        {
            EnemyBase.AnyEnemyKilled -= OnEnemyKilled;
            ExplosiveBarrel.BarrelExploded -= OnBarrelExploded;
        }

        private void Update()
        {
            if (run == null)
            {
                run = FindFirstObjectByType<GameRunController>();
                if (run == null) return;
            }
            if (car == null)
            {
                car = FindFirstObjectByType<ArcadeCarController>();
            }

            if (run.IsGameOver) return;

            float dt = Time.deltaTime;

            if (!isContractActive)
            {
                dispatchTimer -= dt;
                if (dispatchTimer <= 0f)
                {
                    StartRandomBounty();
                }
            }
            else
            {
                timeRemaining -= dt;

                // Специфическая покадровая логика контрактов
                ProcessActiveBounty(dt);

                UpdateBountyUI();

                if (currentProgress >= targetProgress)
                {
                    CompleteBounty(true);
                }
                else if (timeRemaining <= 0f)
                {
                    CompleteBounty(false);
                }
            }
        }

        private void StartRandomBounty()
        {
            isContractActive = true;
            contractCounter++;

            int roll = UnityEngine.Random.Range(0, 5);
            activeType = (BountyType)roll;
            currentProgress = 0f;

            switch (activeType)
            {
                case BountyType.SpeedDemon:
                    timeRemaining = 16f;
                    targetProgress = 10f; // 10 секунд на высокой скорости
                    contractTitle = "«ФОРСАЖ: ПРЕДЕЛ СКОРОСТИ»";
                    break;

                case BountyType.RammingCarnage:
                    timeRemaining = 26f;
                    targetProgress = 8f; // 8 зомби сбить бампером
                    contractTitle = "«ТАРАННЫЙ КАТОК»";
                    break;

                case BountyType.DriftMaster:
                    timeRemaining = 28f;
                    targetProgress = 3.5f; // 3.5 сек заноса
                    contractTitle = "«МАСТЕР ЗАНОСА»";
                    break;

                case BountyType.TurretSharpshooter:
                    timeRemaining = 25f;
                    targetProgress = 16f; // 16 врагов из турели
                    contractTitle = "«СНАЙПЕР ТУРЕЛИ»";
                    break;

                case BountyType.BarrelDemolition:
                    timeRemaining = 30f;
                    targetProgress = 2f; // 2 бочки
                    contractTitle = "«ВЗРЫВНОЙ САПЁР»";
                    break;
            }

            if (bountyPanelRoot != null)
                bountyPanelRoot.SetActive(true);

            AudioManager.Instance?.PlayCoin();
            AudioManager.Instance?.PlaySiren(0.6f);
        }

        private void ProcessActiveBounty(float dt)
        {
            if (car == null) return;

            if (activeType == BountyType.SpeedDemon)
            {
                // Скорость выше 20 м/с (~72 км/ч)
                if (Mathf.Abs(car.SpeedMps) >= 19.5f)
                {
                    currentProgress += dt;
                }
            }
            else if (activeType == BountyType.DriftMaster)
            {
                // Управляемый дрифт
                if (car.IsDrifting)
                {
                    currentProgress += dt;
                }
            }
        }

        private void OnEnemyKilled(EnemyBase enemy)
        {
            if (!isContractActive) return;

            if (activeType == BountyType.TurretSharpshooter)
            {
                currentProgress += 1f;
            }
            else if (activeType == BountyType.RammingCarnage)
            {
                if (car != null && Mathf.Abs(car.SpeedMps) >= 11f)
                {
                    currentProgress += 1f;
                }
            }
        }

        private void OnBarrelExploded(ExplosiveBarrel barrel)
        {
            if (!isContractActive) return;

            if (activeType == BountyType.BarrelDemolition)
            {
                currentProgress += 1f;
            }
        }

        private void CompleteBounty(bool success)
        {
            isContractActive = false;
            dispatchTimer = UnityEngine.Random.Range(minIntervalBetweenBounties, maxIntervalBetweenBounties);

            if (success)
            {
                int rewardCoins = 45;
                float rewardNitro = 50f;

                if (run != null)
                {
                    run.AddCoins(rewardCoins);
                    run.AddNitro(rewardNitro);
                }

                AudioManager.Instance?.PlayFanfare();
                ComboScoreSystem.Instance?.TriggerBanner($"🏆 КОНТРАКТ ВЫПОЛНЕН! +{rewardCoins} МОНЕТ", new Color(1f, 0.85f, 0.2f));
            }
            else
            {
                AudioManager.Instance?.PlayMineBeep(0.7f);
            }

            if (bountyPanelRoot != null)
                bountyPanelRoot.SetActive(false);

            BountyCompleted?.Invoke(activeType, success);
        }

        private void UpdateBountyUI()
        {
            if (bountyPanelRoot == null) return;

            if (titleText != null)
                titleText.text = $"📻 МАЯК: {contractTitle}";

            if (objectiveText != null)
            {
                if (activeType == BountyType.SpeedDemon || activeType == BountyType.DriftMaster)
                {
                    objectiveText.text = $"ПРОГРЕСС: {currentProgress:F1}с / {targetProgress:F1}с";
                }
                else
                {
                    objectiveText.text = $"ЦЕЛЬ: {Mathf.Min(currentProgress, targetProgress)} / {targetProgress}";
                }
            }

            if (timerText != null)
            {
                timerText.text = $"ОСТАЛОСЬ: {Mathf.Max(0f, timeRemaining):F1}с";
                timerText.color = timeRemaining < 5f ? Color.red : Color.white;
            }

            if (timerProgressBar != null)
            {
                timerProgressBar.fillAmount = Mathf.Clamp01(currentProgress / Mathf.Max(1f, targetProgress));
            }
        }

        private void BuildBountyUI()
        {
            if (bountyPanelRoot != null) return;

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            bountyPanelRoot = new GameObject("RadioBountyPanel", typeof(RectTransform), typeof(Image));
            bountyPanelRoot.transform.SetParent(canvas.transform, false);

            RectTransform rt = bountyPanelRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.68f, 0.80f);
            rt.anchorMax = new Vector2(0.98f, 0.92f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = bountyPanelRoot.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.16f, 0.92f);

            // 1. Заголовок
            GameObject titleObj = new GameObject("BountyTitle", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(bountyPanelRoot.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.05f, 0.65f);
            titleRt.anchorMax = new Vector2(0.95f, 0.95f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            titleText = titleObj.GetComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 17;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = new Color(1f, 0.82f, 0.2f);
            titleText.fontStyle = FontStyle.Bold;

            // 2. Цель
            GameObject objObj = new GameObject("BountyObjective", typeof(RectTransform), typeof(Text));
            objObj.transform.SetParent(bountyPanelRoot.transform, false);
            RectTransform objRt = objObj.GetComponent<RectTransform>();
            objRt.anchorMin = new Vector2(0.05f, 0.32f);
            objRt.anchorMax = new Vector2(0.65f, 0.65f);
            objRt.offsetMin = Vector2.zero;
            objRt.offsetMax = Vector2.zero;

            objectiveText = objObj.GetComponent<Text>();
            objectiveText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            objectiveText.fontSize = 15;
            objectiveText.alignment = TextAnchor.MiddleLeft;
            objectiveText.color = Color.white;

            // 3. Таймер
            GameObject timerObj = new GameObject("BountyTimer", typeof(RectTransform), typeof(Text));
            timerObj.transform.SetParent(bountyPanelRoot.transform, false);
            RectTransform timerRt = timerObj.GetComponent<RectTransform>();
            timerRt.anchorMin = new Vector2(0.65f, 0.32f);
            timerRt.anchorMax = new Vector2(0.95f, 0.65f);
            timerRt.offsetMin = Vector2.zero;
            timerRt.offsetMax = Vector2.zero;

            timerText = timerObj.GetComponent<Text>();
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            timerText.fontSize = 15;
            timerText.alignment = TextAnchor.MiddleRight;
            timerText.color = Color.white;

            // 4. Прогресс-бар
            GameObject barBgObj = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBgObj.transform.SetParent(bountyPanelRoot.transform, false);
            RectTransform barBgRt = barBgObj.GetComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0.05f, 0.08f);
            barBgRt.anchorMax = new Vector2(0.95f, 0.22f);
            barBgRt.offsetMin = Vector2.zero;
            barBgRt.offsetMax = Vector2.zero;

            Image barBg = barBgObj.GetComponent<Image>();
            barBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            GameObject barFillObj = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFillObj.transform.SetParent(barBgObj.transform, false);
            RectTransform fillRt = barFillObj.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            timerProgressBar = barFillObj.GetComponent<Image>();
            timerProgressBar.color = new Color(0.25f, 0.85f, 0.35f);
            timerProgressBar.type = Image.Type.Filled;
            timerProgressBar.fillMethod = Image.FillMethod.Horizontal;
            timerProgressBar.fillOrigin = (int)Image.OriginHorizontal.Left;
            timerProgressBar.fillAmount = 0f;

            bountyPanelRoot.SetActive(false);
        }
    }
}
