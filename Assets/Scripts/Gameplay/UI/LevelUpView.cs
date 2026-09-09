using System;
using System.Collections.Generic;
using UnityEngine;
using RogueDrive.Modifiers;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Экран выбора модификатора «1 из 3» при повышении уровня заезда.
    /// Ставит заезд на паузу, отображает карточки предложенных бафов,
    /// подсвечивает синергии и передает выбор в ModifierService.
    /// </summary>
    public sealed class LevelUpView : MonoBehaviour
    {
        public static LevelUpView Instance { get; private set; }

        public event Action<ModifierDefinition> OfferSelected;
        public event Action RerollRequested;

        bool isVisible;
        IReadOnlyList<ModifierDefinition> currentOffers;
        BuildState currentBuild;
        SynergyResolver synergyResolver;

        int remainingRerolls = 1;

        GUIStyle titleStyle;
        GUIStyle cardTitleStyle;
        GUIStyle cardDescStyle;
        GUIStyle rarityStyle;
        GUIStyle synergyBadgeStyle;
        GUIStyle buttonStyle;

        Texture2D overlayTex;
        Texture2D cardBgTex;
        Texture2D synergyCardBgTex;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (overlayTex != null) Destroy(overlayTex);
            if (cardBgTex != null) Destroy(cardBgTex);
            if (synergyCardBgTex != null) Destroy(synergyCardBgTex);
        }

        public void Show(IReadOnlyList<ModifierDefinition> offers, BuildState build, SynergyResolver synergies)
        {
            currentOffers = offers;
            currentBuild = build;
            synergyResolver = synergies;
            isVisible = true;

            Time.timeScale = 0f; // Пауза игрового процесса
        }

        public void Hide()
        {
            isVisible = false;
            Time.timeScale = 1f; // Возобновление заезда
        }

        void OnGUI()
        {
            if (!isVisible || currentOffers == null || currentOffers.Count == 0)
                return;

            EnsureStyles();

            // 1. Полупрозрачный темный фон
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayTex);

            // 2. Заголовок экрана
            float topY = Screen.height * 0.12f;
            GUI.Label(new Rect(0, topY, Screen.width, 40f), "НОВЫЙ УРОВЕНЬ ЗАЕЗДА!", titleStyle);

            // 3. Вычисление позиций 3 карточек
            int cardCount = currentOffers.Count;
            float cardWidth = Mathf.Min(260f, (Screen.width - 80f) / cardCount);
            float cardHeight = 360f;
            float spacing = 24f;
            float totalWidth = (cardWidth * cardCount) + (spacing * (cardCount - 1));
            float startX = (Screen.width - totalWidth) * 0.5f;
            float cardY = (Screen.height - cardHeight) * 0.5f;

            for (int i = 0; i < cardCount; i++)
            {
                ModifierDefinition def = currentOffers[i];
                float x = startX + i * (cardWidth + spacing);
                Rect cardRect = new Rect(x, cardY, cardWidth, cardHeight);

                DrawCard(cardRect, def);
            }

            // 4. Кнопка бесплатного / рекламного реролла снизу
            if (remainingRerolls > 0)
            {
                float rerollW = 280f;
                float rerollH = 40f;
                Rect rerollRect = new Rect((Screen.width - rerollW) * 0.5f, cardY + cardHeight + 25f, rerollW, rerollH);

                if (GUI.Button(rerollRect, $"ОБНОВИТЬ КАРТЫ (ОСТАЛОСЬ: {remainingRerolls})", buttonStyle))
                {
                    remainingRerolls--;
                    RerollRequested?.Invoke();
                }
            }
        }

        void DrawCard(Rect rect, ModifierDefinition def)
        {
            bool closesSynergy = CheckClosesSynergy(def);
            Texture2D bg = closesSynergy ? synergyCardBgTex : cardBgTex;
            GUI.DrawTexture(rect, bg);

            float pad = 14f;
            float innerW = rect.width - pad * 2f;
            float curY = rect.y + 16f;

            // Редкость (цветной бейдж)
            Color rarityCol = GetRarityColor(def.Rarity);
            GUI.contentColor = rarityCol;
            GUI.Label(new Rect(rect.x + pad, curY, innerW, 20f), $"{def.Rarity.ToString().ToUpper()} • {def.Category}", rarityStyle);
            GUI.contentColor = Color.white;
            curY += 26f;

            // Название модификатора
            GUI.Label(new Rect(rect.x + pad, curY, innerW, 36f), def.DisplayName, cardTitleStyle);
            curY += 40f;

            // Уровень (текущий -> будущий)
            int curLevel = currentBuild != null ? currentBuild.GetLevel(def.Id) : 0;
            string levelText = curLevel > 0 ? $"Уровень: {curLevel} ➔ {curLevel + 1}" : "Новый модификатор";
            if (def.RequiresSocket)
            {
                levelText += $"\nСокет: {def.RequiredSocket}";
            }
            GUI.Label(new Rect(rect.x + pad, curY, innerW, 34f), levelText, cardDescStyle);
            curY += 40f;

            // Бейдж синергии
            if (closesSynergy)
            {
                GUI.Label(new Rect(rect.x + pad, curY, innerW, 28f), "⚡ СОБИРАЕТ СИНЕРГИЮ!", synergyBadgeStyle);
                curY += 32f;
            }

            // Описание эффекта
            GUI.Label(new Rect(rect.x + pad, curY, innerW, 110f), def.Description, cardDescStyle);

            // Кнопка выбора внизу карточки
            float btnH = 42f;
            Rect btnRect = new Rect(rect.x + pad, rect.y + rect.height - btnH - 16f, innerW, btnH);
            if (GUI.Button(btnRect, "ВЫБРАТЬ", buttonStyle))
            {
                SelectOffer(def);
            }
        }

        bool CheckClosesSynergy(ModifierDefinition candidate)
        {
            if (candidate == null || currentBuild == null || synergyResolver == null)
                return false;

            return synergyResolver.WouldActivate(currentBuild, candidate.Id);
        }

        void SelectOffer(ModifierDefinition def)
        {
            Hide();
            OfferSelected?.Invoke(def);
        }

        Color GetRarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.25f, 0.7f, 1f);
                case Rarity.Epic: return new Color(0.95f, 0.4f, 1f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.82f, 0.2f) }
            };

            cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            cardDescStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.9f, 0.94f) }
            };

            rarityStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            synergyBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.88f, 0.1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            overlayTex = MakeTex(new Color(0.04f, 0.05f, 0.08f, 0.88f));
            cardBgTex = MakeTex(new Color(0.14f, 0.16f, 0.22f, 0.96f));
            synergyCardBgTex = MakeTex(new Color(0.22f, 0.2f, 0.14f, 0.98f));
        }

        Texture2D MakeTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}
