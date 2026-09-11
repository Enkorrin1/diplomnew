using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RogueDrive.Modifiers;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Данные одного визита в казино: откуда брать барабан, как применять результат
    /// и что показывать про гарантию синергии. Собирается координатором сессии.
    /// </summary>
    public sealed class CasinoVisit
    {
        public string StopName;
        public int Tokens;

        /// <summary>Кандидаты вращения под выбранную ставку; первый элемент — выпавший результат.</summary>
        public Func<CasinoBet, IReadOnlyList<ModifierDefinition>> DrawReel;

        /// <summary>Есть ли под ставку хотя бы один кандидат (жетоны здесь не учитываются).</summary>
        public Func<CasinoBet, bool> BetAvailable;

        /// <summary>Применение выпавшего модификатора.</summary>
        public Action<ModifierDefinition> ApplyResult;

        public BuildState Build;
        public SynergyResolver Synergies;
        public IOfferGenerator Generator;
    }

    /// <summary>
    /// Экран придорожного казино. Работает с панелью, размещённой в UI_Canvas сцены
    /// (меню «RogueDrive/Казино/Создать панель казино»), ничего не строит в рантайме.
    /// Ставит заезд на паузу и по очереди «прокручивает» накопленные жетоны: перед
    /// каждым вращением игрок выбирает ставку (обычная, «Редкий+» за два жетона,
    /// «Синергия» за три), барабан пробегает по кандидатам из генератора и
    /// останавливается на выпавшем модификаторе, который применяется автоматически.
    /// Без панели в сцене работает «вслепую»: бафы выдаются сразу с уведомлением в HUD.
    /// </summary>
    public sealed class BuffCasinoView : MonoBehaviour
    {
        public static BuffCasinoView Instance { get; private set; }

        public event Action Closed;

        [Header("Reel Timing (unscaled seconds)")]
        [SerializeField, Min(0.3f)] private float spinDuration = 2.2f;
        [SerializeField, Min(0.2f)] private float resultHold = 1.6f;
        [SerializeField, Min(0.05f)] private float startTick = 0.06f;
        [SerializeField, Min(0.05f)] private float endTick = 0.32f;

        [Header("Scene UI (authored)")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text reelPrev;
        [SerializeField] private Text reelCurrent;
        [SerializeField] private Text reelNext;
        [SerializeField] private Text resultTitle;
        [SerializeField] private Text resultBody;
        [SerializeField] private Text footerText;
        [SerializeField] private Image reelFrame;
        [SerializeField] private Image resultPanel;
        [SerializeField] private Button continueButton;

        [Header("Bet Row (authored)")]
        [SerializeField] private GameObject betRoot;
        [SerializeField] private Button[] betButtons = new Button[3];
        [SerializeField] private Text[] betLabels = new Text[3];
        [SerializeField] private Text betHint;

        bool isVisible;
        bool skipRequested;
        bool continueRequested;
        int requestedBet = -1;
        Coroutine routine;

        public bool IsVisible => isVisible;
        public bool HasSceneUI => panelRoot != null && reelCurrent != null && resultTitle != null;
        public bool HasBetUI => betRoot != null && betButtons != null && betButtons.Length == 3 && betButtons[0] != null;

        /// <summary>Корневая панель в сцене (для редакторского обновления разметки).</summary>
        public Transform PanelTransform => titleText != null ? titleText.transform.parent : null;

        /// <summary>Привязка панели сцены (вызывается редакторской командой создания панели).</summary>
        public void BindSceneUI(GameObject root, Text title, Text subtitle,
                                Text prev, Text current, Text next,
                                Text resTitle, Text resBody, Text footer,
                                Image frame, Image resPanel, Button button)
        {
            panelRoot = root;
            titleText = title;
            subtitleText = subtitle;
            reelPrev = prev;
            reelCurrent = current;
            reelNext = next;
            resultTitle = resTitle;
            resultBody = resBody;
            footerText = footer;
            reelFrame = frame;
            resultPanel = resPanel;
            continueButton = button;
        }

        /// <summary>Привязка ряда ставок (вызывается редакторской командой создания/обновления панели).</summary>
        public void BindBetUI(GameObject root, Button[] buttons, Text[] labels, Text hint)
        {
            betRoot = root;
            betButtons = buttons;
            betLabels = labels;
            betHint = hint;
        }

        private void Awake()
        {
            Instance = this;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!isVisible) return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                skipRequested = true;
                continueRequested = true;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) requestedBet = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) requestedBet = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) requestedBet = 2;
        }

        /// <summary>Обработчик кнопки «Продолжить путь» (подключён в сцене как persistent listener).</summary>
        public void OnContinuePressed()
        {
            skipRequested = true;
            continueRequested = true;
        }

        /// <summary>Обработчики кнопок ставок (подключены в сцене как persistent listeners).</summary>
        public void OnBetStandardPressed() => requestedBet = 0;
        public void OnBetRarePressed() => requestedBet = 1;
        public void OnBetSynergyPressed() => requestedBet = 2;

        public void Show(CasinoVisit visit)
        {
            if (isVisible || visit == null || visit.Tokens <= 0) return;

            if (!HasSceneUI)
            {
                RunHeadless(visit);
                return;
            }

            panelRoot.SetActive(true);
            isVisible = true;
            skipRequested = false;
            continueRequested = false;
            requestedBet = -1;

            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Time.timeScale = 0f;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(SpinSequence(visit));
        }

        public void Hide()
        {
            if (!isVisible) return;
            isVisible = false;

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            if (panelRoot != null) panelRoot.SetActive(false);

            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Time.timeScale = 1f;
            Closed?.Invoke();
        }

        /// <summary>Резервный режим без панели в сцене: мгновенная выдача обычными спинами с уведомлением в HUD.</summary>
        void RunHeadless(CasinoVisit visit)
        {
            Debug.LogWarning("[BuffCasinoView] Панель казино не размещена в сцене — бафы выдаются без анимации. " +
                             "Выполните «RogueDrive/Казино/Создать панель казино в UI_Canvas».");

            var names = new List<string>();
            for (int i = 0; i < visit.Tokens; i++)
            {
                IReadOnlyList<ModifierDefinition> candidates = visit.DrawReel != null ? visit.DrawReel(CasinoBet.Standard) : null;
                if (candidates == null || candidates.Count == 0) break;
                visit.ApplyResult?.Invoke(candidates[0]);
                names.Add(candidates[0].DisplayName);
            }

            PrototypeHud.Instance?.ShowBiomeNotification(
                visit.StopName.ToUpperInvariant(),
                names.Count > 0 ? "ВЫПАЛО: " + string.Join(", ", names) : "ПУЛ МОДИФИКАТОРОВ ИСЧЕРПАН",
                new Color(1f, 0.3f, 0.85f));

            Closed?.Invoke();
        }

        IEnumerator SpinSequence(CasinoVisit visit)
        {
            if (titleText != null) titleText.text = visit.StopName.ToUpperInvariant();
            if (resultPanel != null) resultPanel.gameObject.SetActive(false);
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (betRoot != null) betRoot.SetActive(false);
            SetText(footerText, "");
            SetText(reelPrev, ""); SetText(reelNext, "");
            SetText(reelCurrent, "· · ·");

            yield return new WaitForSecondsRealtime(0.35f);

            var reel = new List<ModifierDefinition>();
            int tokens = visit.Tokens;
            int spin = 0;

            while (tokens > 0)
            {
                spin++;
                if (resultPanel != null) resultPanel.gameObject.SetActive(false);
                UpdateSubtitle(visit, tokens, spin);

                // 1. Ставка
                CasinoBet bet = CasinoBet.Standard;
                bool anyExtraBet = false;
                for (int b = 1; b <= 2; b++)
                    if (BetAffordable(visit, (CasinoBet)b, tokens)) anyExtraBet = true;

                if (anyExtraBet)
                {
                    yield return ChooseBet(visit, tokens, result => bet = result);
                }

                tokens -= CasinoBetRules.Cost(bet);
                UpdateSubtitle(visit, tokens, spin);
                if (betRoot != null) betRoot.SetActive(false);
                SetText(footerText, bet == CasinoBet.Standard
                    ? "[ПРОБЕЛ] — остановить барабан"
                    : $"СТАВКА «{CasinoBetRules.DisplayName(bet).ToUpperInvariant()}»    •    [ПРОБЕЛ] — остановить барабан");

                // 2. Барабан
                IReadOnlyList<ModifierDefinition> candidates = visit.DrawReel != null ? visit.DrawReel(bet) : null;
                if (candidates == null || candidates.Count == 0)
                {
                    SetText(reelCurrent, "ПУЛ МОДИФИКАТОРОВ ИСЧЕРПАН");
                    reelCurrent.color = Color.gray;
                    SetText(reelPrev, ""); SetText(reelNext, "");
                    yield return new WaitForSecondsRealtime(1.2f);
                    break;
                }

                ModifierDefinition result = candidates[0];

                // Визуальный барабан: результат перемешан среди кандидатов
                reel.Clear();
                for (int i = 0; i < candidates.Count; i++) reel.Add(candidates[i]);
                for (int i = reel.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (reel[i], reel[j]) = (reel[j], reel[i]);
                }
                while (reel.Count < 3)
                {
                    // Дублируем, чтобы соседние строки барабана были заполнены
                    int n = reel.Count;
                    for (int i = 0; i < n; i++) reel.Add(reel[i]);
                }
                int resultIndex = reel.IndexOf(result);

                skipRequested = false;
                float elapsed = 0f;
                int cursor = UnityEngine.Random.Range(0, reel.Count);
                float tickTimer = 0f;

                while (elapsed < spinDuration && !skipRequested)
                {
                    float t = elapsed / spinDuration;
                    float tick = Mathf.Lerp(startTick, endTick, t * t);
                    tickTimer += Time.unscaledDeltaTime;
                    elapsed += Time.unscaledDeltaTime;

                    if (tickTimer >= tick)
                    {
                        tickTimer = 0f;
                        cursor = (cursor + 1) % reel.Count;
                        RenderReel(reel, cursor, visit.Build, false);
                        RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
                    }

                    yield return null;
                }

                // Довести барабан до результата
                if (!skipRequested)
                {
                    int steps = ((resultIndex - cursor) % reel.Count + reel.Count) % reel.Count;
                    for (int s = 0; s < steps; s++)
                    {
                        cursor = (cursor + 1) % reel.Count;
                        RenderReel(reel, cursor, visit.Build, false);
                        RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
                        yield return new WaitForSecondsRealtime(endTick);
                    }
                }
                cursor = resultIndex;
                RenderReel(reel, cursor, visit.Build, true);

                // 3. Применение выпавшего модификатора
                int levelBefore = visit.Build != null ? visit.Build.GetLevel(result.Id) : 0;
                bool closesSynergy = visit.Synergies != null && visit.Build != null && visit.Synergies.WouldActivate(visit.Build, result.Id);
                bool pityFired = visit.Generator != null
                              && visit.Generator.PityThreshold > 0
                              && bet != CasinoBet.SynergyHunt
                              && closesSynergy
                              && visit.Generator.PicksSinceLastSynergy >= visit.Generator.PityThreshold;

                visit.ApplyResult?.Invoke(result);

                ShowResult(result, levelBefore + 1, closesSynergy, bet, pityFired);
                if (result.Rarity == Rarity.Epic || closesSynergy)
                    RogueDrive.Audio.AudioManager.Instance?.PlayLevelUp();
                else
                    RogueDrive.Audio.AudioManager.Instance?.PlayCoin();

                SetText(footerText, tokens > 0 ? "[ПРОБЕЛ] — следующий жетон" : "");
                skipRequested = false;
                float hold = 0f;
                while (hold < resultHold && !skipRequested)
                {
                    hold += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // Завершение: ждём подтверждения
            SetText(subtitleText, "ЖЕТОНЫ ПОТРАЧЕНЫ");
            SetText(footerText, "[ПРОБЕЛ] / [ENTER] — ПРОДОЛЖИТЬ ПУТЬ");
            if (betRoot != null) betRoot.SetActive(false);
            if (continueButton != null) continueButton.gameObject.SetActive(true);
            continueRequested = false;
            yield return null;
            while (!continueRequested)
                yield return null;

            routine = null;
            Hide();
        }

        /// <summary>Фаза выбора ставки: кнопки в панели или клавиши 1/2/3; пробел — обычная ставка.</summary>
        IEnumerator ChooseBet(CasinoVisit visit, int tokens, Action<CasinoBet> onChosen)
        {
            requestedBet = -1;
            skipRequested = false;

            bool[] affordable = new bool[3];
            for (int b = 0; b < 3; b++)
                affordable[b] = BetAffordable(visit, (CasinoBet)b, tokens);

            if (HasBetUI)
            {
                betRoot.SetActive(true);
                for (int b = 0; b < 3; b++)
                {
                    if (betButtons[b] != null) betButtons[b].interactable = affordable[b];
                    if (betLabels != null && b < betLabels.Length && betLabels[b] != null)
                    {
                        string cost = CasinoBetRules.Cost((CasinoBet)b) == 1 ? "1 жетон" : CasinoBetRules.Cost((CasinoBet)b) + " жетона";
                        betLabels[b].text = $"[{b + 1}] {CasinoBetRules.DisplayName((CasinoBet)b).ToUpperInvariant()}\n{cost}";
                        betLabels[b].color = affordable[b] ? new Color(0.08f, 0.05f, 0.1f) : new Color(0.35f, 0.3f, 0.38f);
                    }
                }
                SetText(betHint, BetHintText(affordable));
            }

            SetText(reelCurrent, "ВЫБЕРИТЕ СТАВКУ");
            reelCurrent.color = new Color(0.85f, 0.65f, 0.15f);
            SetText(reelPrev, ""); SetText(reelNext, "");
            SetText(footerText, HasBetUI
                ? "[1] обычный   [2] редкий+ (2 жетона)   [3] синергия (3 жетона)   •   [ПРОБЕЛ] — обычный"
                : "[1] обычный   [2] редкий+ (2 жетона)   [3] синергия (3 жетона)");

            while (true)
            {
                if (skipRequested)
                {
                    skipRequested = false;
                    onChosen(CasinoBet.Standard);
                    yield break;
                }

                if (requestedBet >= 0)
                {
                    int chosen = requestedBet;
                    requestedBet = -1;

                    if (chosen < 3 && affordable[chosen])
                    {
                        onChosen((CasinoBet)chosen);
                        yield break;
                    }

                    RogueDrive.Audio.AudioManager.Instance?.PlaySwitchClick();
                }

                yield return null;
            }
        }

        static bool BetAffordable(CasinoVisit visit, CasinoBet bet, int tokens)
        {
            if (tokens < CasinoBetRules.Cost(bet))
                return false;

            if (bet == CasinoBet.Standard)
                return true;

            return visit.BetAvailable == null || visit.BetAvailable(bet);
        }

        static string BetHintText(bool[] affordable)
        {
            if (!affordable[1] && !affordable[2])
                return "Редких кандидатов и незамкнутых синергий в пуле нет — доступен только обычный спин";
            if (!affordable[2])
                return "Синергия: нет кандидата, который замкнул бы связку, либо не хватает жетонов";
            if (!affordable[1])
                return "Редкий+: редких кандидатов в пуле не осталось";
            return "Ставка сужает пул, из которого выбирает слот-машина";
        }

        void UpdateSubtitle(CasinoVisit visit, int tokens, int spin)
        {
            string pity = "";
            if (visit.Generator != null && visit.Generator.PityThreshold > 0)
            {
                int left = Mathf.Max(0, visit.Generator.PityThreshold - visit.Generator.PicksSinceLastSynergy);
                pity = left == 0
                    ? "    •    ★ ГАРАНТИЯ СИНЕРГИИ АКТИВНА"
                    : $"    •    до гарантии синергии: {left}";
            }

            SetText(subtitleText, $"ЖЕТОНОВ: {tokens}    •    ВРАЩЕНИЕ {spin}{pity}");
        }

        void RenderReel(List<ModifierDefinition> reel, int cursor, BuildState build, bool landed)
        {
            int n = reel.Count;
            ModifierDefinition prev = reel[(cursor - 1 + n) % n];
            ModifierDefinition cur = reel[cursor];
            ModifierDefinition next = reel[(cursor + 1) % n];

            SetText(reelPrev, Label(prev, build));
            SetText(reelNext, Label(next, build));
            SetText(reelCurrent, Label(cur, build));

            Color rc = GetRarityColor(cur.Rarity);
            reelCurrent.color = rc;
            if (reelPrev != null) reelPrev.color = new Color(0.55f, 0.55f, 0.6f, 0.7f);
            if (reelNext != null) reelNext.color = new Color(0.55f, 0.55f, 0.6f, 0.7f);
            if (reelFrame != null)
                reelFrame.color = landed ? new Color(rc.r, rc.g, rc.b, 0.95f) : new Color(0.85f, 0.65f, 0.15f, 0.9f);
        }

        static string Label(ModifierDefinition def, BuildState build)
        {
            int lvl = build != null ? build.GetLevel(def.Id) : 0;
            return lvl > 0 ? $"{def.DisplayName}  (улучшение → ур. {lvl + 1})" : def.DisplayName;
        }

        void ShowResult(ModifierDefinition def, int newLevel, bool closesSynergy, CasinoBet bet, bool pityFired)
        {
            Color rc = GetRarityColor(def.Rarity);
            if (resultPanel != null)
            {
                resultPanel.gameObject.SetActive(true);
                resultPanel.color = new Color(rc.r * 0.25f, rc.g * 0.25f, rc.b * 0.25f, 0.92f);
            }
            resultTitle.color = rc;
            resultTitle.text = newLevel > 1
                ? $"УЛУЧШЕНО: {def.DisplayName.ToUpperInvariant()}"
                : $"ВЫПАЛО: {def.DisplayName.ToUpperInvariant()}";

            string extra = "";
            if (closesSynergy) extra += "\n\n★ СОБРАНА СИНЕРГИЯ! ★";
            if (pityFired) extra += "\n(сработала гарантия от невезения)";
            else if (bet != CasinoBet.Standard) extra += $"\n(ставка «{CasinoBetRules.DisplayName(bet)}»)";

            SetText(resultBody, $"{RarityName(def.Rarity)} • {CategoryName(def.Category)} • Уровень {newLevel}\n{def.Description}" + extra);
        }

        static void SetText(Text t, string value)
        {
            if (t != null) t.text = value;
        }

        static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "Редкий";
                case Rarity.Epic: return "Эпический";
                default: return "Обычный";
            }
        }

        static string CategoryName(ModifierCategory c)
        {
            switch (c)
            {
                case ModifierCategory.Elemental: return "Стихия";
                case ModifierCategory.Defensive: return "Защита";
                default: return "Атака";
            }
        }

        public static Color GetRarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.25f, 0.7f, 1f);
                case Rarity.Epic: return new Color(0.95f, 0.4f, 1f);
                default: return new Color(0.9f, 0.9f, 0.9f);
            }
        }
    }
}
