using UnityEngine;

namespace RogueDrive.Gameplay.Coop
{
    public enum CoopBunkerActionType
    {
        Generator,
        WheelMount,
        BatteryMount,
        FuelRefill,
        CollectKeys,
        OpenGates
    }

    /// <summary>
    /// Interactive workstation/hotspot in the Co-op Bunker.
    /// Handled via on-foot raycast from CoopPlayer.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CoopBunkerInteractable : MonoBehaviour
    {
        [SerializeField] private CoopBunkerActionType actionType;
        [SerializeField] private string customPrompt;
        [SerializeField] private GameObject visualToHideOnDone;
        [SerializeField] private GameObject visualToShowOnDone;

        public CoopBunkerActionType ActionType => actionType;
        public static Vector3 InteractionPoint(Collider collider, Vector3 eye)
        {
            return collider is MeshCollider mesh && !mesh.convex
                ? collider.bounds.ClosestPoint(eye) : collider.ClosestPoint(eye);
        }
        private void Start() => Awake();

        private void Awake()
        {
            var session = CoopSession.Instance;
            if (session == null || session.Manager == null || !session.Manager.IsListening) return;
            // Preparation stations are authored world fixtures, not the future shared
            // cargo system. Local rigidbody simulation must not move them independently.
            var parentBody = GetComponentInParent<Rigidbody>();
            if (parentBody != null) parentBody.isKinematic = true;
            foreach (var body in GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        }

        public string PromptText
        {
            get
            {
                if (!string.IsNullOrEmpty(customPrompt)) return customPrompt;
                return actionType switch
                {
                    CoopBunkerActionType.Generator => "[E] Запустить генератор бункера",
                    CoopBunkerActionType.WheelMount => "[E] Смонтировать переднее колесо",
                    CoopBunkerActionType.BatteryMount => "[E] Установить аккумулятор в отсек",
                    CoopBunkerActionType.FuelRefill => "[E] Залить топливо в бензобак (+10 л)",
                    CoopBunkerActionType.CollectKeys => "[E] Забрать ключи зажигания и припасы",
                    CoopBunkerActionType.OpenGates => "[E] Открыть гермоворота на пульте",
                    _ => "[E] Взаимодействовать"
                };
            }
        }

        public bool CanInteract()
        {
            var qm = CoopQuestManager.Instance;
            if (qm == null) return false;

            return actionType switch
            {
                CoopBunkerActionType.Generator => !qm.QuestGeneratorRunning.Value,
                CoopBunkerActionType.WheelMount => !qm.QuestWheelMounted.Value,
                CoopBunkerActionType.BatteryMount => !qm.QuestBatteryMounted.Value,
                CoopBunkerActionType.FuelRefill => qm.QuestFuelLiters.Value < CoopQuestManager.RequiredFuel,
                CoopBunkerActionType.CollectKeys => !qm.QuestKeyCollected.Value,
                CoopBunkerActionType.OpenGates => !qm.QuestGatesOpened.Value,
                _ => false
            };
        }

        public void Interact(CoopPlayer player)
        {
            var qm = CoopQuestManager.Instance;
            if (qm == null || !CanInteract()) return;

            switch (actionType)
            {
                case CoopBunkerActionType.Generator:
                    qm.StartGeneratorServerRpc();
                    break;
                case CoopBunkerActionType.WheelMount:
                    qm.MountWheelServerRpc();
                    break;
                case CoopBunkerActionType.BatteryMount:
                    qm.MountBatteryServerRpc();
                    break;
                case CoopBunkerActionType.FuelRefill:
                    qm.AddFuelServerRpc(10f);
                    break;
                case CoopBunkerActionType.CollectKeys:
                    qm.CollectKeyServerRpc();
                    break;
                case CoopBunkerActionType.OpenGates:
                    qm.OpenGatesServerRpc();
                    break;
            }

            // Presentation follows the authoritative quest state in Update.
        }

        private void Update()
        {
            var qm = CoopQuestManager.Instance;
            if (qm == null) return;

            bool isDone = actionType switch
            {
                CoopBunkerActionType.Generator => qm.QuestGeneratorRunning.Value,
                CoopBunkerActionType.WheelMount => qm.QuestWheelMounted.Value,
                CoopBunkerActionType.BatteryMount => qm.QuestBatteryMounted.Value,
                CoopBunkerActionType.FuelRefill => qm.QuestFuelLiters.Value >= CoopQuestManager.RequiredFuel,
                CoopBunkerActionType.CollectKeys => qm.QuestKeyCollected.Value,
                CoopBunkerActionType.OpenGates => qm.QuestGatesOpened.Value,
                _ => false
            };

            if (visualToHideOnDone != null && visualToHideOnDone.activeSelf == isDone)
            {
                visualToHideOnDone.SetActive(!isDone);
            }

            if (visualToShowOnDone != null && visualToShowOnDone.activeSelf != isDone)
            {
                visualToShowOnDone.SetActive(isDone);
            }
        }
    }
}
