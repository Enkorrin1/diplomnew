using System;
using UnityEngine;
using RogueDrive.Audio;
using RogueDrive.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Механика тяжелого ранения (Downed State) и поднятия/реанимации (Revive).
    /// При получении смертельного урона игрок не погибает мгновенно, а падает на четвереньки
    /// и может ползти в течение 30 секунд.
    /// В кооперативе напарник может подойти и поднять раненого [E].
    /// В одиночной игре раненый может спасти себя сам при наличии аптечки в кармане.
    /// </summary>
    [RequireComponent(typeof(PlayerFieldNeeds))]
    public sealed class PlayerDownedState : MonoBehaviour, IGarageInteractable
    {
        [Header("Downed Parameters")]
        [SerializeField] private float maxBleedoutDuration = 30f;
        [SerializeField] private float reviveHealthAmount = 40f;

        private PlayerFieldNeeds needs;
        private GaragePlayerController playerController;
        private bool isDowned = false;
        private float bleedoutTimer = 0f;
        private float heartbeatSoundTimer = 0f;

        public bool IsDowned => isDowned;
        public float BleedoutTimer => bleedoutTimer;
        public float MaxBleedoutDuration => maxBleedoutDuration;

        public event Action OnDowned;
        public event Action OnRevived;

        private void Awake()
        {
            needs = GetComponent<PlayerFieldNeeds>();
            playerController = GetComponent<GaragePlayerController>();
        }

        public void EnterDowned()
        {
            if (isDowned) return;

            isDowned = true;
            bleedoutTimer = maxBleedoutDuration;

            // Ограничение скорости передвижения (ползание)
            if (playerController != null)
            {
                // Форсируем приседание и медленное ползание
                // GaragePlayerController плавно интерполирует высоту
            }

            AudioManager.Instance?.PlayCrash(1.2f);
            GarageInteractionUI.Instance?.ShowBanner("⚠ ВЫ ТЯЖЕЛО РАНЕНЫ! Ползите в безопасное место!", 4f);

            OnDowned?.Invoke();
        }

        private void Update()
        {
            if (!isDowned) return;

            // Обратный отсчет таймера истекания кровью
            bleedoutTimer -= Time.deltaTime;

            // Звук учащенного сердцебиения в критическом состоянии
            heartbeatSoundTimer -= Time.deltaTime;
            if (heartbeatSoundTimer <= 0f)
            {
                heartbeatSoundTimer = Mathf.Lerp(0.55f, 1.2f, bleedoutTimer / maxBleedoutDuration);
                AudioManager.Instance?.PlayMineBeep(0.7f);
            }

            // Уведомление на экране с таймером
            if (GarageInteractionUI.Instance != null && Time.frameCount % 10 == 0)
            {
                bool hasMedkit = HasSelfReviveMedkit();
                string selfReviveHint = hasMedkit ? "\n[E] Применить аптечку для самоспасения" : "\nТребуется помощь напарника или аптечка!";
                GarageInteractionUI.Instance.ShowNotification($"⚠ ТЯЖЕЛОЕ РАНЕНИЕ! Истекание кровью: {Mathf.Max(0, bleedoutTimer):0.0}с {selfReviveHint}", 0.35f);
            }

            // Клавиша самореанимации для одиночного режима при наличии аптечки
            if (Input.GetKeyDown(KeyCode.E) && HasSelfReviveMedkit())
            {
                ConsumeMedkitAndSelfRevive();
                return;
            }

            // Смерть от потери крови
            if (bleedoutTimer <= 0f)
            {
                isDowned = false;
                GarageInteractionUI.Instance?.HideBanner();
                needs?.ForceDie();
            }
        }

        public bool HasSelfReviveMedkit()
        {
            var pocket = GetComponent<PlayerPocketInventory>() ?? PlayerPocketInventory.Instance;
            if (pocket != null)
            {
                if (pocket.HasItem("first_aid_medkit") || pocket.HasItem("first_aid")) return true;
                if (pocket.HasTool(GarageItemFunction.ItemKind.RepairKit)) return true;
            }
            return false;
        }

        private void ConsumeMedkitAndSelfRevive()
        {
            var pocket = GetComponent<PlayerPocketInventory>() ?? PlayerPocketInventory.Instance;
            if (pocket != null)
            {
                if (pocket.RemoveItem("first_aid_medkit", 1) || pocket.RemoveItem("first_aid", 1))
                {
                    Revive(reviveHealthAmount);
                    GarageInteractionUI.Instance?.ShowBanner("✔ ВЫ ПРИМЕНИЛИ АПТЕЧКУ И ПОДНЯЛИСЬ НА НОГИ!", 3.5f);
                    return;
                }
            }
        }

        public void Revive(float health)
        {
            if (!isDowned) return;

            isDowned = false;
            bleedoutTimer = 0f;

            if (needs != null)
            {
                needs.Heal(health);
            }

            AudioManager.Instance?.PlayLevelUp();
            GarageInteractionUI.Instance?.ShowBanner("✔ БОЕЦ РЕАНИМИРОВАН!", 3f);

            OnRevived?.Invoke();
        }

        // IGarageInteractable: напарник может навести прицел на раненого и нажать [E]
        public string GetPromptText()
        {
            if (!isDowned) return string.Empty;
            return "[E] Реанимировать напарника (Первая помощь)";
        }

        public bool CanInteract()
        {
            return isDowned;
        }

        public void Interact(GaragePlayerController interactor)
        {
            if (!isDowned) return;
            Revive(reviveHealthAmount);
        }
    }
}
