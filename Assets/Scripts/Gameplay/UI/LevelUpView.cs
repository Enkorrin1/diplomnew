using System;
using System.Collections.Generic;
using UnityEngine;
using RogueDrive.Modifiers;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Р­РєСЂР°РЅ РІС‹Р±РѕСЂР° РјРѕРґРёС„РёРєР°С‚РѕСЂР° В«1 РёР· 3В» РїСЂРё РїРѕРІС‹С€РµРЅРёРё СѓСЂРѕРІРЅСЏ Р·Р°РµР·РґР°.
    /// РЎС‚Р°РІРёС‚ Р·Р°РµР·Рґ РЅР° РїР°СѓР·Сѓ, РѕС‚РѕР±СЂР°Р¶Р°РµС‚ РєР°СЂС‚РѕС‡РєРё РїСЂРµРґР»РѕР¶РµРЅРЅС‹С… Р±Р°С„РѕРІ,
    /// РїРѕРґСЃРІРµС‡РёРІР°РµС‚ СЃРёРЅРµСЂРіРёРё Рё РїРµСЂРµРґР°РµС‚ РІС‹Р±РѕСЂ РІ ModifierService.
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


        public bool IsVisible => isVisible;
        [SerializeField] private bool useSceneUI = true;
        public bool UseSceneUI => useSceneUI;
        public IReadOnlyList<ModifierDefinition> Offers => currentOffers;
        public int RemainingRerolls => remainingRerolls;
        public int OfferLevel(int index) => currentOffers != null && index < currentOffers.Count && currentBuild != null ? currentBuild.GetLevel(currentOffers[index].Id) : 0;
        public bool OfferCompletesSynergy(int index) => currentOffers != null && index < currentOffers.Count && CheckClosesSynergy(currentOffers[index]);
        public void Choose(int index)
        {
            if (isVisible && currentOffers != null && index >= 0 && index < currentOffers.Count) SelectOffer(currentOffers[index]);
        }
        public void Reroll()
        {
            if (!isVisible || remainingRerolls <= 0) return;
            remainingRerolls--;
            RerollRequested?.Invoke();
        }

        private void Awake()
        {
            Instance = this;
            useSceneUI = true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(IReadOnlyList<ModifierDefinition> offers, BuildState build, SynergyResolver synergies)
        {
            currentOffers = offers;
            currentBuild = build;
            synergyResolver = synergies;
            isVisible = true;

            // PC: РѕСЃРІРѕР±РѕР¶РґР°РµРј РєСѓСЂСЃРѕСЂ РґР»СЏ РІС‹Р±РѕСЂР° РјРѕРґРёС„РёРєР°С‚РѕСЂРѕРІ
            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Time.timeScale = 0f; // РџР°СѓР·Р° РёРіСЂРѕРІРѕРіРѕ РїСЂРѕС†РµСЃСЃР°
        }

        public void Hide()
        {
            isVisible = false;

            // PC: Р±Р»РѕРєРёСЂСѓРµРј РєСѓСЂСЃРѕСЂ РѕР±СЂР°С‚РЅРѕ РїСЂРё РІРѕР·РѕР±РЅРѕРІР»РµРЅРёРё Р·Р°РµР·РґР°
            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Time.timeScale = 1f; // Р’РѕР·РѕР±РЅРѕРІР»РµРЅРёРµ Р·Р°РµР·РґР°
        }

        private void Update()
        {
            if (!isVisible) return;

            // PC: РіРѕСЂСЏС‡РёРµ РєР»Р°РІРёС€Рё РІС‹Р±РѕСЂР° РјРѕРґРёС„РёРєР°С‚РѕСЂРѕРІ [1], [2], [3] Рё СЂРµСЂРѕР»Р»Р° [R]
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                Choose(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                Choose(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                Choose(2);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                Reroll();
            }
        }

        private bool CheckClosesSynergy(ModifierDefinition candidate)
        {
            if (candidate == null || currentBuild == null || synergyResolver == null)
                return false;

            return synergyResolver.WouldActivate(currentBuild, candidate.Id);
        }

        private void SelectOffer(ModifierDefinition def)
        {
            Hide();
            OfferSelected?.Invoke(def);
        }

        public Color GetRarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.25f, 0.7f, 1f);
                case Rarity.Epic: return new Color(0.95f, 0.4f, 1f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
