using System;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Универсальный инвентарь персонажа от первого лица для удержания крупногабаритных предметов в руках:
    /// колеса, аккумуляторы, канистры с жидкостью, монтировки, ящики и лут.
    /// Управляет физическим сокетом удержания перед камерой, плавным покачиванием (sway/bobbing),
    /// броском на [G], аккуратной постановкой на [Q] и синхронизацией с PhysX без лагов.
    /// Работает во всех игровых локациях (бункер, заправки, открытая трасса, форпосты).
    /// </summary>
    [AddComponentMenu("RogueDrive/Player Hands Inventory")]
    public class PlayerHandsInventory : MonoBehaviour
    {
        private static PlayerHandsInventory instance;
        public static PlayerHandsInventory Instance
        {
            get
            {
                if(instance==null)instance=FindFirstObjectByType<PlayerHandsInventory>() ?? FindFirstObjectByType<PlayerHandsInventory>(FindObjectsInactive.Include);
                return instance;
            }
            protected set=>instance=value;
        }

        public event Action<BunkerAssemblyItemType> HeldItemChanged;

        [Header("Hold Socket Configuration")]
        [SerializeField] private Transform holdSocket;
        [SerializeField] private Vector3 baseHoldOffset = new Vector3(0.28f, -0.28f, 0.58f);
        [SerializeField] private Vector3 baseHoldEuler = new Vector3(10f, -20f, 5f);
        [SerializeField] private float throwForce = 6.5f;
        [SerializeField] private float dropForce = 0.8f;
        [SerializeField] private float swaySmoothing = 14f;

        [Header("Current Held State")]
        [SerializeField] private BunkerAssemblyItemType heldAssemblyType = BunkerAssemblyItemType.None;
        [SerializeField] private GameObject heldObject;

        private CarPartItem heldAssemblyItem;
        private PhysicsProp heldPhysicsProp;
        private Rigidbody[] heldRigidbodies;
        private Collider[] heldColliders;
        private Transform originalParent;
        private Vector3 originalLocalScale = Vector3.one;
        private Vector3 originalWorldScale = Vector3.one;
        private float propCarryScale = 1f;

        private Camera playerCamera;
        private Vector3 currentSwayOffset;
        private Quaternion currentSwayRotation = Quaternion.identity;

        public BunkerAssemblyItemType HeldItem => heldAssemblyType;
        public bool HasItem => heldAssemblyType != BunkerAssemblyItemType.None || heldObject != null;
        public GameObject HeldGameObject => heldObject;
        public Vector3 HeldWorldScale => originalWorldScale;

        protected virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            EnsureHoldSocket();
        }

        protected virtual void Start()
        {
            EnsureHoldSocket();
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureHoldSocket()
        {
            if (holdSocket != null && playerCamera != null) return;

            var player = GetComponent<GaragePlayerController>();
            if (player == null)
            {
                player = FindFirstObjectByType<GaragePlayerController>();
            }

            if (player != null && player.PlayerCamera != null)
            {
                playerCamera = player.PlayerCamera;
            }
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>() ?? Camera.main ?? FindFirstObjectByType<Camera>();
            }

            if (playerCamera != null)
            {
                var existingSocket = playerCamera.transform.Find("ItemHoldSocket");
                if (existingSocket != null)
                {
                    holdSocket = existingSocket;
                }
                else
                {
                    var socketObj = new GameObject("ItemHoldSocket");
                    socketObj.transform.SetParent(playerCamera.transform, false);
                    socketObj.transform.localPosition = baseHoldOffset;
                    socketObj.transform.localRotation = Quaternion.Euler(baseHoldEuler);
                    socketObj.transform.localScale = Vector3.one;
                    holdSocket = socketObj.transform;
                }
            }
        }

        private void Update()
        {
            if (RogueDrive.UI.InventoryWindowUI.BlockGameplayInput) return;
            var player = GetComponent<GaragePlayerController>();
            if ((player != null && player.IsMovementLocked) || Cursor.lockState != CursorLockMode.Locked) return;
            if (Input.GetKeyDown(KeyCode.R))
            {
                if(heldObject!=null)heldObject.GetComponent<GarageItemFunction>()?.ReplaceBattery();
                else PlayerPocketInventory.Instance?.ReplaceSelectedBattery();
            }
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (heldObject != null) { heldObject.GetComponent<GarageItemUse>()?.Use(player); UpdateUIHint(); }
                else
                {
                    var target = GetComponentInChildren<GarageInteractionRaycaster>()?.CurrentTarget as Component;
                    var container = target != null ? target.GetComponent<GaragePortableContainer>() : null;
                    var function=target!=null?target.GetComponent<GarageItemFunction>():null;
                    var active=PlayerPocketInventory.Instance?.ActivePhysical;
                    var activeKind=active!=null?active.GetComponent<GarageItemFunction>():null;
                    if(function!=null && function.WorldUsable && !(activeKind!=null && (activeKind.Kind==GarageItemFunction.ItemKind.Mug || activeKind.Kind==GarageItemFunction.ItemKind.Battery))) function.Use(player);
                    else if (container != null) container.Take(this);
                    else PlayerPocketInventory.Instance?.UseActivePhysical();
                }
            }
            if (!HasItem)
            {
                if (Input.GetKeyDown(KeyCode.G)) PlayerPocketInventory.Instance?.DropActivePhysical(true);
                else if (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1)) PlayerPocketInventory.Instance?.DropActivePhysical(false);
                return;
            }

            HandleInput();
            HandleSway();
        }

        public static void GetDefaultItemHoldSettings(BunkerAssemblyItemType type, GameObject obj, out Vector3 offset, out Vector3 euler, out Vector3 scale)
        {
            bool isCanister = type == BunkerAssemblyItemType.FuelCanister || 
                              type == BunkerAssemblyItemType.WaterCanister ||
                              (obj != null && (obj.name.Contains("Jerrycan") || obj.GetComponentInChildren<FluidContainer>() != null));

            bool isWheel = type == BunkerAssemblyItemType.Wheel ||
                           (obj != null && (obj.name.Contains("Wheel") || obj.name.Contains("Tire")));

            bool isBattery = type == BunkerAssemblyItemType.Battery ||
                            (obj != null && obj.name.Contains("Battery"));

            bool isCrowbar = obj != null && (obj.name.IndexOf("Crowbar", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            obj.name.IndexOf("Wrench", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            obj.GetComponentInChildren<MeleeWeapon>() != null);

            bool isCrate = obj != null && (obj.name.IndexOf("Crate", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          obj.name.IndexOf("Box", System.StringComparison.OrdinalIgnoreCase) >= 0);

            bool isBarrel = obj != null && obj.name.IndexOf("Barrel", System.StringComparison.OrdinalIgnoreCase) >= 0;

            bool isFirearm = obj != null && (obj.name.IndexOf("Pistol", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            obj.GetComponentInChildren<FirearmWeapon>() != null);

            if (isCanister)
            {
                // Канистра: опускаем ниже уровня глаз, сдвигаем вправо, поворачиваем узкой гранью вперед-вверх, компактный аккуратный масштаб
                offset = new Vector3(0.02f, 0.08f, 0.15f);
                euler = new Vector3(15f, 75f, -10f);
                scale = new Vector3(0.52f, 0.52f, 0.52f);
            }
            else if (isWheel)
            {
                // Колесо: приподнимаем выше (y = -0.06f), поворачиваем диском в поле зрения игрока
                offset = new Vector3(0.04f, -0.06f, 0.06f);
                euler = new Vector3(45f, -15f, 10f);
                scale = new Vector3(0.50f, 0.50f, 0.50f);
            }
            else if (isBattery)
            {
                // Аккумулятор: компактный блок в правой руке клеммами вверх
                offset = new Vector3(0.04f, -0.10f, 0.02f);
                euler = new Vector3(5f, -10f, 0f);
                scale = new Vector3(0.68f, 0.68f, 0.68f);
            }
            else if (isFirearm)
            {
                // Пистолет / огнестрел в руках (компенсация -X оси модели Pistol1)
                offset = new Vector3(-0.06f, 0.09f, -0.04f);
                euler = new Vector3(-2f, 87f, 4f);
                scale = Vector3.one;
            }
            else if (isCrowbar)
            {
                // Монтировка / инструмент: рукоять в руке, жало направлено вперед-вверх
                offset = new Vector3(-0.04f, -0.05f, 0.05f);
                euler = new Vector3(25f, -20f, 15f);
                scale = Vector3.one;
            }
            else if (isCrate)
            {
                // Ящик: опускаем вниз, масштабируем чтобы не перекрывать обзор
                offset = new Vector3(-0.06f, -0.22f, 0.10f);
                euler = new Vector3(8f, 12f, -4f);
                scale = new Vector3(0.55f, 0.55f, 0.55f);
            }
            else if (isBarrel)
            {
                // Бочка
                offset = new Vector3(-0.05f, -0.24f, 0.10f);
                euler = new Vector3(10f, 10f, 0f);
                scale = new Vector3(0.45f, 0.45f, 0.45f);
            }
            else
            {
                float autoScale = 1.0f;
                if (obj != null)
                {
                    var rend = obj.GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        float maxDim = Mathf.Max(rend.bounds.size.x, rend.bounds.size.y, rend.bounds.size.z);
                        if (maxDim > 0.6f) autoScale = Mathf.Clamp(0.55f / maxDim, 0.3f, 1.0f);
                    }
                }
                offset = new Vector3(-0.04f, -0.10f, 0.04f);
                euler = new Vector3(12f, -12f, 4f);
                scale = Vector3.one * autoScale;
            }
        }

        private void LateUpdate()
        {
            if (!HasItem || heldObject == null || holdSocket == null) return;

            GetDefaultItemHoldSettings(heldAssemblyType, heldObject, out Vector3 defOffset, out Vector3 defEuler, out Vector3 defScale);

            Vector3 customOffset = defOffset +
                (heldAssemblyItem != null && heldAssemblyItem.HoldOffset != Vector3.zero ? heldAssemblyItem.HoldOffset : Vector3.zero) +
                (heldPhysicsProp != null ? heldPhysicsProp.HoldOffset : Vector3.zero);

            Vector3 customEuler = defEuler +
                (heldAssemblyItem != null && heldAssemblyItem.HoldEuler != Vector3.zero ? heldAssemblyItem.HoldEuler : Vector3.zero) +
                (heldPhysicsProp != null ? heldPhysicsProp.HoldEuler : Vector3.zero);

            Vector3 targetWorldPos = holdSocket.TransformPoint(customOffset);
            Quaternion targetWorldRot = holdSocket.rotation * Quaternion.Euler(customEuler);

            heldObject.transform.position = targetWorldPos;
            heldObject.transform.rotation = targetWorldRot;
            heldObject.transform.localScale = Vector3.Scale(originalWorldScale, defScale) * propCarryScale;

            if (heldRigidbodies != null)
            {
                for (int i = 0; i < heldRigidbodies.Length; i++)
                {
                    if (heldRigidbodies[i] != null)
                    {
                        heldRigidbodies[i].position = targetWorldPos;
                        heldRigidbodies[i].rotation = targetWorldRot;
                    }
                }
            }
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                DropItem(throwForward: true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1))
            {
                DropItem(throwForward: false);
                return;
            }
        }

        private void HandleSway()
        {
            if (holdSocket == null || playerCamera == null) return;

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            Vector3 targetSway = new Vector3(-mouseX * 0.02f, -mouseY * 0.02f, 0f);
            currentSwayOffset = Vector3.Lerp(currentSwayOffset, targetSway, Time.deltaTime * swaySmoothing);

            Quaternion targetRot = Quaternion.Euler(-mouseY * 2.5f, mouseX * 2.5f, -mouseX * 3f);
            currentSwayRotation = Quaternion.Slerp(currentSwayRotation, targetRot, Time.deltaTime * swaySmoothing);

            holdSocket.localPosition = baseHoldOffset + currentSwayOffset;
            holdSocket.localRotation = Quaternion.Euler(baseHoldEuler) * currentSwayRotation;
        }

        public bool HoldAssemblyItem(CarPartItem item)
        {
            if (item == null) return false;

            if (HasItem)
            {
                ShowNotification($"Руки уже заняты: {GetHeldItemDisplayName()}! Сначала положите [G].");
                return false;
            }

            EnsureHoldSocket();

            heldAssemblyItem = item;
            heldAssemblyType = item.ItemType;
            heldPhysicsProp = null;
            heldObject = item.gameObject;

            AttachObjectToSocket(heldObject.transform, item.HoldOffset, item.HoldEuler);
            item.OnPickedUp();

            HeldItemChanged?.Invoke(heldAssemblyType);
            UpdateUIHint();

            return true;
        }

        public bool HoldPhysicsProp(PhysicsProp prop)
        {
            if (prop == null) return false;

            if (HasItem)
            {
                ShowNotification($"Руки уже заняты: {GetHeldItemDisplayName()}! Сначала положите [G].");
                return false;
            }

            EnsureHoldSocket();

            heldAssemblyItem = null;
            heldAssemblyType = BunkerAssemblyItemType.None;
            heldPhysicsProp = prop;
            heldObject = prop.gameObject;

            AttachObjectToSocket(heldObject.transform, prop.HoldOffset, prop.HoldEuler);
            prop.OnPickedUp();

            HeldItemChanged?.Invoke(BunkerAssemblyItemType.None);
            UpdateUIHint();

            return true;
        }

        public bool TryHoldItem(BunkerAssemblyItemType type, out GameObject spawnedObject)
        {
            spawnedObject = null;
            if (type == BunkerAssemblyItemType.None) return false;
            if (HasItem) return false;

            EnsureHoldSocket();

            GameObject itemObj = null;
            switch (type)
            {
                case BunkerAssemblyItemType.Wheel:
                {
                    itemObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    itemObj.name = "Item_Held_Wheel";
                    itemObj.transform.localScale = new Vector3(0.65f, 0.25f, 0.65f);
                    itemObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    SetPrimitiveColor(itemObj, new Color(0.12f, 0.12f, 0.14f));
                    break;
                }
                case BunkerAssemblyItemType.Battery:
                {
                    itemObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    itemObj.name = "Item_Held_Battery";
                    itemObj.transform.localScale = new Vector3(0.32f, 0.24f, 0.22f);
                    SetPrimitiveColor(itemObj, new Color(0.18f, 0.22f, 0.30f));
                    break;
                }
                case BunkerAssemblyItemType.FuelCanister:
                {
                    itemObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    itemObj.name = "Item_Held_FuelCanister";
                    itemObj.transform.localScale = new Vector3(0.35f, 0.5f, 0.4f);
                    SetPrimitiveColor(itemObj, new Color(0.75f, 0.18f, 0.14f));
                    var container = itemObj.AddComponent<FluidContainer>();
                    container.Configure(BunkerFluidType.Gasoline, 20f, 15f);
                    break;
                }
                case BunkerAssemblyItemType.WaterCanister:
                {
                    itemObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    itemObj.name = "Item_Held_WaterCanister";
                    itemObj.transform.localScale = new Vector3(0.35f, 0.5f, 0.4f);
                    SetPrimitiveColor(itemObj, new Color(0.2f, 0.55f, 0.85f));
                    var container = itemObj.AddComponent<FluidContainer>();
                    container.Configure(BunkerFluidType.Water, 20f, 10f);
                    break;
                }
                case BunkerAssemblyItemType.Crowbar:
                {
                    itemObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    itemObj.name = "Item_Held_Crowbar";
                    itemObj.transform.localScale = new Vector3(0.04f, 0.5f, 0.04f);
                    itemObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    SetPrimitiveColor(itemObj, new Color(0.7f, 0.15f, 0.12f));
                    break;
                }
                default:
                {
                    itemObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    itemObj.name = $"Item_Held_{type}";
                    itemObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                    SetPrimitiveColor(itemObj, Color.gray);
                    break;
                }
            }

            if (itemObj == null) return false;

            var rb = itemObj.GetComponent<Rigidbody>();
            if (rb == null) rb = itemObj.AddComponent<Rigidbody>();
            rb.mass = 4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var assemblyItem = itemObj.AddComponent<CarPartItem>();
            assemblyItem.Configure(type, GetItemDisplayName(type));

            if (!HoldAssemblyItem(assemblyItem))
            {
                Destroy(itemObj);
                return false;
            }

            spawnedObject = itemObj;
            return true;
        }

        public bool TryHoldItem(BunkerAssemblyItemType type)
        {
            return TryHoldItem(type, out _);
        }

        private static void SetPrimitiveColor(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Diffuse");
            if (shader != null)
            {
                var mat = new Material(shader) { color = color };
                renderer.material = mat;
            }
        }

        private void AttachObjectToSocket(Transform objTransform, Vector3 customOffset, Vector3 customEuler)
        {
            originalParent = objTransform.parent;
            originalLocalScale = objTransform.localScale;
            originalWorldScale = objTransform.lossyScale;
            propCarryScale = 1f;
            if (heldPhysicsProp != null)
            {
                var renderers = objTransform.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    propCarryScale = Mathf.Min(1f, .6f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z));
                }
            }

            heldRigidbodies = objTransform.GetComponentsInChildren<Rigidbody>();
            if (heldRigidbodies != null)
            {
                foreach (var rb in heldRigidbodies)
                {
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
                }
            }

            heldColliders = objTransform.GetComponentsInChildren<Collider>();
            if (heldColliders != null)
            {
                foreach (var col in heldColliders)
                {
                    if (col != null) col.enabled = false;
                }
            }

            objTransform.SetParent(holdSocket, true);
            objTransform.localPosition = customOffset;
            objTransform.localRotation = Quaternion.Euler(customEuler);
            objTransform.localScale = originalLocalScale;
        }

        public void DropItem(bool throwForward = false, bool forStorage = false)
        {
            if (!HasItem || heldObject == null) return;

            Transform objTransform = heldObject.transform;
            Vector3 heldPosition = objTransform.position;
            Quaternion heldRotation = objTransform.rotation;
            Vector3 heldScale = objTransform.localScale;
            objTransform.SetParent(originalParent != null ? originalParent : null, true);
            objTransform.localScale = originalParent != null ? originalLocalScale : originalWorldScale;

            // Restore the world size before finding clearance for release.
            var renderers = heldObject.GetComponentsInChildren<Renderer>();
            if (!forStorage && playerCamera != null && renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var centerOffset = bounds.center - objTransform.position;
                var origin = playerCamera.transform.position;
                var forward = playerCamera.transform.forward;
                float reach = Mathf.Max(1.1f, bounds.extents.magnitude + .6f);
                var desiredCenter = origin + forward * reach;
                if (Physics.Raycast(origin, forward, out var surface, reach + .5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float support = Vector3.Dot(new Vector3(Mathf.Abs(surface.normal.x), Mathf.Abs(surface.normal.y), Mathf.Abs(surface.normal.z)), bounds.extents);
                    desiredCenter = surface.point + surface.normal * (support + .03f);
                }
                objTransform.position = desiredCenter - centerOffset;
                Physics.SyncTransforms();
                // Reject release inside furniture or the player instead of letting PhysX eject it.
                var overlaps = Physics.OverlapBox(desiredCenter, bounds.extents * .96f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (Array.Exists(overlaps, c => !c.transform.IsChildOf(objTransform)))
                {
                    objTransform.SetParent(holdSocket, true);
                    objTransform.SetPositionAndRotation(heldPosition, heldRotation);
                    objTransform.localScale = heldScale;
                    ShowNotification("Недостаточно места. Наведитесь на свободную поверхность.");
                    return;
                }
            }

            Vector3 throwDir = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            Vector3 throwVelocity = throwForward
                ? (throwDir * throwForce + Vector3.up * 1.5f)
                : (Vector3.down * dropForce + transform.forward * 0.4f);

            Vector3 angularVel = throwForward
                ? new Vector3(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f))
                : Vector3.zero;

            if (heldColliders != null)
            {
                foreach (var col in heldColliders)
                {
                    if (col != null) col.enabled = true;
                }
            }

            if (heldRigidbodies != null)
            {
                foreach (var rb in heldRigidbodies)
                {
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.interpolation = RigidbodyInterpolation.Interpolate;
                        rb.detectCollisions = true;
                        rb.linearVelocity = throwVelocity;
                        rb.angularVelocity = angularVel;
                    }
                }
            }

            if (heldAssemblyItem != null)
            {
                heldAssemblyItem.OnDropped(throwVelocity, angularVel);
            }
            else if (heldPhysicsProp != null)
            {
                heldPhysicsProp.OnDropped(throwVelocity, angularVel);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayImpact();
            }

            heldAssemblyType = BunkerAssemblyItemType.None;
            heldAssemblyItem = null;
            heldPhysicsProp = null;
            heldObject = null;
            heldRigidbodies = null;
            heldColliders = null;

            HeldItemChanged?.Invoke(BunkerAssemblyItemType.None);

            if (GarageInteractionUI.Instance != null)
            {
                GarageInteractionUI.Instance.HideHeldHint();
            }
        }

        public void ConsumeHeldItem()
        {
            if (!HasItem || heldObject == null) return;

            if (heldAssemblyItem != null)
            {
                heldAssemblyItem.OnAssembled();
            }
            else
            {
                heldObject.SetActive(false);
            }

            heldAssemblyType = BunkerAssemblyItemType.None;
            heldAssemblyItem = null;
            heldPhysicsProp = null;
            heldObject = null;
            heldRigidbodies = null;
            heldColliders = null;

            HeldItemChanged?.Invoke(BunkerAssemblyItemType.None);

            if (GarageInteractionUI.Instance != null)
            {
                GarageInteractionUI.Instance.HideHeldHint();
            }
        }

        public string GetHeldItemDisplayName()
        {
            if (heldPhysicsProp != null)
            {
                var container = heldPhysicsProp.GetComponent<FluidContainer>();
                if (container != null)
                {
                    return container.GetDefaultDisplayName();
                }
                return heldPhysicsProp.PropName;
            }

            if (heldAssemblyItem != null)
            {
                return heldAssemblyItem.GetDisplayName();
            }

            if (heldObject != null)
            {
                var container = heldObject.GetComponent<FluidContainer>();
                if (container != null)
                {
                    return container.GetDefaultDisplayName();
                }
                return heldObject.name;
            }

            if (heldAssemblyType != BunkerAssemblyItemType.None)
            {
                return GetItemDisplayName(heldAssemblyType);
            }

            return "Ничего";
        }

        private void UpdateUIHint()
        {
            if (GarageInteractionUI.Instance != null && HasItem)
            {
                string name = GetHeldItemDisplayName();
                GarageInteractionUI.Instance.ShowHeldHint(
                    $"{name}  |  [G] Бросить  |  [Q] Положить" +
                    (heldObject.GetComponent<GarageItemUse>() != null ? "  |  [F] " + heldObject.GetComponent<GarageItemUse>().ActionLabel : ""));
            }
        }

        private void ShowNotification(string msg)
        {
            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.ShowNotification(msg, 3.5f);
            }
        }

        public static string GetItemDisplayName(BunkerAssemblyItemType type)
        {
            switch (type)
            {
                case BunkerAssemblyItemType.Wheel:
                    return "Колесо со ступичным креплением";
                case BunkerAssemblyItemType.Battery:
                    return "Силовой аккумулятор 12V";
                case BunkerAssemblyItemType.FuelCanister:
                    return "Канистра бензина (15 л)";
                case BunkerAssemblyItemType.WaterCanister:
                    return "Канистра воды для радиатора (10 л)";
                case BunkerAssemblyItemType.OilCanister: return "Моторное масло";
                case BunkerAssemblyItemType.Engine: return "Двигатель";
                case BunkerAssemblyItemType.Radiator: return "Радиатор";
                case BunkerAssemblyItemType.Crowbar:
                    return "Стальная монтировка";
                case BunkerAssemblyItemType.Axe:
                    return "Пожарный топор";
                default:
                    return "Ничего";
            }
        }
    }

    /// <summary>
    /// Псевдоним класса для обратной совместимости со старыми сценами бункера.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RogueDrive/Bunker Player Inventory (Legacy)")]
    public class BunkerPlayerInventory : PlayerHandsInventory
    {
        public static new BunkerPlayerInventory Instance =>
            PlayerHandsInventory.Instance as BunkerPlayerInventory ?? FindFirstObjectByType<BunkerPlayerInventory>();

        protected override void Awake()
        {
            base.Awake();
        }
    }
}

