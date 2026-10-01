using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Универсальная интерактивная деталь автомобиля или полевой расходник (колесо, АКБ, канистра, монтировка).
    /// Игрок подходит, берет деталь в руки на [E], видит её перед камерой,
    /// может бросить/положить на [G]/[Q], либо смонтировать на автомобиль на соответствующей точке сборки.
    /// Используется как в гараже/бункере, так и на дорогах, блокпостах и заправках.
    /// </summary>
    [AddComponentMenu("RogueDrive/Car Part Item")]
    public class CarPartItem : MonoBehaviour, IGarageInteractable
    {
        [Header("Item Configuration")]
        [SerializeField] private BunkerAssemblyItemType itemType = BunkerAssemblyItemType.Wheel;
        [SerializeField] private string customDisplayName;

        [Header("Hold Positioning")]
        [SerializeField] private Vector3 holdOffset = Vector3.zero;
        [SerializeField] private Vector3 holdEuler = Vector3.zero;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject visualRoot;

        [Header("Sound Feedback")]
        [SerializeField] private float minImpactVelocity = 1.2f;

        private Rigidbody rb;
        private Collider[] colliders;
        private bool isPickedUp = false;
        private float lastImpactTime = 0f;

        public BunkerAssemblyItemType ItemType => itemType;
        public bool IsPickedUp => isPickedUp;
        public string CustomDisplayName => customDisplayName;
        public Vector3 HoldOffset => holdOffset;
        public Vector3 HoldEuler => holdEuler;

        public void Configure(BunkerAssemblyItemType type, string displayName = null)
        {
            itemType = type;
            customDisplayName = displayName;
        }

        private void Awake()
        {
            if (visualRoot == null)
            {
                visualRoot = gameObject;
            }

            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            colliders = GetComponentsInChildren<Collider>();

            // Отключаем лишние точечные источники света, так как подсветка теперь контурная
            var childLights = GetComponentsInChildren<Light>(true);
            for (int i = 0; i < childLights.Length; i++)
            {
                if (childLights[i] != null) childLights[i].enabled = false;
            }
        }

        public string GetPromptText()
        {
            if (isPickedUp) return string.Empty;

            var inv = BunkerPlayerInventory.Instance;
            if (inv != null && inv.HasItem)
            {
                return $"[E] Взять {GetDisplayName()} (Руки заняты: сначала положите {inv.GetHeldItemDisplayName()})";
            }

            return $"[E] Взять в руки: {GetDisplayName()}";
        }

        public bool CanInteract()
        {
            if (isPickedUp) return false;
            var inv = BunkerPlayerInventory.Instance;
            return inv == null || !inv.HasItem;
        }

        public void Interact(GaragePlayerController player)
        {
            if (isPickedUp) return;

            var inv = BunkerPlayerInventory.Instance;
            if (inv == null && player != null)
            {
                inv = player.gameObject.AddComponent<BunkerPlayerInventory>();
            }

            if (inv != null)
            {
                inv.HoldAssemblyItem(this);
            }
        }

        public string GetDisplayName()
        {
            var fluidContainer = GetComponent<FluidContainer>();
            if (fluidContainer != null)
            {
                return fluidContainer.GetDefaultDisplayName();
            }

            if (!string.IsNullOrEmpty(customDisplayName))
            {
                return customDisplayName;
            }

            return itemType switch
            {
                BunkerAssemblyItemType.Wheel => "Колесо со ступичным креплением",
                BunkerAssemblyItemType.Battery => "Силовой аккумулятор 12V",
                BunkerAssemblyItemType.FuelCanister => "Канистра топлива (15 л)",
                BunkerAssemblyItemType.WaterCanister => "Канистра чистой воды (10 л)",
                BunkerAssemblyItemType.Crowbar => "Стальная монтировка",
                _ => "Предмет сборки"
            };
        }

        public void OnPickedUp()
        {
            isPickedUp = true;

            if (colliders == null || colliders.Length == 0)
            {
                colliders = GetComponentsInChildren<Collider>();
            }

            foreach (var col in colliders)
            {
                if (col != null) col.enabled = false;
            }

            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.detectCollisions = false;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySwitchClick();
            }
        }

        public void OnDropped(Vector3 velocity, Vector3 angularVelocity)
        {
            isPickedUp = false;

            if (colliders == null || colliders.Length == 0)
            {
                colliders = GetComponentsInChildren<Collider>();
            }

            foreach (var col in colliders)
            {
                if (col != null) col.enabled = true;
            }

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.detectCollisions = true;
                rb.linearVelocity = velocity;
                rb.angularVelocity = angularVelocity;
            }
        }

        public void OnAssembled()
        {
            isPickedUp = true;
            gameObject.SetActive(false);
        }

        public void ResetItem()
        {
            isPickedUp = false;
            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
            }
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isPickedUp) return;

            if (collision.relativeVelocity.magnitude >= minImpactVelocity)
            {
                if (Time.time - lastImpactTime > 0.2f)
                {
                    lastImpactTime = Time.time;
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayImpact();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Псевдоним класса для обратной совместимости со старыми сценами бункера.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RogueDrive/Bunker Assembly Item (Legacy)")]
    public class BunkerAssemblyItem : CarPartItem
    {
    }
}
