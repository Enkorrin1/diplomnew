using System;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Универсальный интерактивный физический предмет (ящик, инструмент, канистра, кружка, декор, лут на заправках и трассе).
    /// Позволяет брать в руки любой 3D-объект на [E], носить его с покачиванием перед камерой
    /// и бросать/ставить на [G] / [Q] с реалистичной физикой и звуками удара.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("RogueDrive/Physics Prop")]
    public class PhysicsProp : MonoBehaviour, IGarageInteractable
    {
        [Header("Prop Identification")]
        [SerializeField] private string propName = "Предмет";
        [SerializeField] private bool pocketSized;
        public bool PocketSized => pocketSized;
        [SerializeField] private Sprite inventoryIcon;
        public Sprite InventoryIcon => inventoryIcon;
        public void SetInventoryIcon(Sprite value) => inventoryIcon = value;
        public string StackKey => (name.Contains("Bolt") || name.Contains("BatterySmall") || name.Contains("BatteryLarge"))
            ? name.Replace("_Portable", "").Replace("(Clone)", "").Trim() : "";
        public int StackLimit => name.Contains("Bolt") ? 20 : 8;
        public void SetPocketSized(bool value) => pocketSized = value;
        [SerializeField] private float throwForceMultiplier = 1.0f;

        [Header("Hold Positioning")]
        [SerializeField] private Vector3 holdOffset = Vector3.zero;
        [SerializeField] private Vector3 holdEuler = Vector3.zero;

        [Header("Sound Feedback")]
        [SerializeField] private float minImpactVelocity = 1.2f;

        private Rigidbody rb;
        private Collider[] colliders;
        private bool isHeld = false;
        private float lastImpactTime = 0f;

        public string PropName => propName;
        public bool IsHeld => isHeld;
        public Vector3 HoldOffset => holdOffset;
        public Vector3 HoldEuler => holdEuler;
        public float ThrowForceMultiplier => throwForceMultiplier;
        public void Configure(string title) => propName = title;

        private void Awake()
        {
            var function=GarageItemFunction.Ensure(gameObject);
            if(function!=null && GetComponent<GarageItemUse>()==null)gameObject.AddComponent<GarageItemUse>();
            rb = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();

            // Настройка качественной физики
            if (rb != null)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }

        public string GetPromptText()
        {
            if (isHeld) return string.Empty;
            var functional=GetComponent<GarageItemFunction>();
            string suffix=functional!=null && functional.WorldUsable?"  |  [F] "+functional.ActionLabel:"";
            if (pocketSized) return $"[E] Убрать в слот: {propName}"+suffix;

            var storage = GetComponent<GaragePortableContainer>();
            if (storage != null) return storage.Prompt;

            var inv = BunkerPlayerInventory.Instance;
            if (inv != null && inv.HasItem)
            {
                return $"[E] Взять {propName} (Руки заняты: положите {inv.GetHeldItemDisplayName()})";
            }

            return $"[E] Взять в руки: {propName}"+suffix;
        }

        public bool CanInteract()
        {
            if (isHeld) return false;
            if (pocketSized) return true;
            if (GetComponent<GaragePortableContainer>() != null) return true;
            var inv = BunkerPlayerInventory.Instance;
            return inv == null || !inv.HasItem;
        }

        public void Interact(GaragePlayerController player)
        {
            if (isHeld) return;
            if (pocketSized)
            {
                var pocket = PlayerPocketInventory.Instance;
                if (pocket != null && !pocket.TryStorePhysical(this))
                    GarageInteractionUI.Instance?.ShowNotification("Инвентарь заполнен. Освободите ячейку.", 3f);
                return;
            }
            var storage = GetComponent<GaragePortableContainer>();
            if (storage != null && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                storage.StorePocket(PlayerPocketInventory.Instance);return;
            }
            if (storage != null && PlayerHandsInventory.Instance != null && PlayerHandsInventory.Instance.HasItem)
            {
                storage.Store(PlayerHandsInventory.Instance);
                return;
            }

            var inv = BunkerPlayerInventory.Instance;
            if (inv == null && player != null)
            {
                inv = player.gameObject.AddComponent<BunkerPlayerInventory>();
            }

            if (inv != null)
            {
                inv.HoldPhysicsProp(this);
            }
        }

        public void OnPickedUp()
        {
            isHeld = true;

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
            isHeld = false;

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

        private void OnCollisionEnter(Collision collision)
        {
            if (isHeld) return;

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

}
