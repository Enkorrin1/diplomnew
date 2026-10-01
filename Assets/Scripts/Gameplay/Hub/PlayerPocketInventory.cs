using System;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    [Serializable]
    public class PocketPhysicalEntry
    {
        public PhysicsProp item;
        public Vector3 scale;
        public float viewScale;
    }
    [System.Serializable]
    public struct PocketSlotData
    {
        public string id;
        public string displayName;
        public int count;
        public Sprite icon;
        public PhysicsProp worldItem;
        public Vector3 worldScale;
        public float viewScale;
        public System.Collections.Generic.List<PocketPhysicalEntry> reserves;
        public GameObject largeItem;
        public BunkerAssemblyItemType legacyType;
        public GameObject WorldObject => worldItem != null ? worldItem.gameObject : largeItem;

        public bool IsEmpty => string.IsNullOrEmpty(id) || count <= 0;

        public static PocketSlotData Empty => new PocketSlotData
        {
            id = string.Empty,
            displayName = string.Empty,
            count = 0,
            icon = null
        };
    }

    /// <summary>
    /// Карманный инвентарь быстрого доступа (Hotbar) на 5 слотов [1] [2] [3] [4] [5].
    /// Предназначен для мелких предметов: ключи зажигания, гаечные ключи, отвертки,
    /// фонарик, расходники, детали, документы.
    /// Переключается цифровыми клавишами 1..5 или колесиком мыши.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class PlayerPocketInventory : MonoBehaviour
    {
        private static PlayerPocketInventory instance;
        public static PlayerPocketInventory Instance
        {
            get
            {
                if(instance==null)instance=FindFirstObjectByType<PlayerPocketInventory>() ?? FindFirstObjectByType<PlayerPocketInventory>(FindObjectsInactive.Include);
                return instance;
            }
            private set=>instance=value;
        }

        public const int HotbarCount = 5;
        public const int SlotCount = 20;

        public event Action<int, PocketSlotData> OnSlotUpdated;
        public event Action<int> OnActiveSlotChanged;

        [Header("Slots State")]
        [SerializeField] private PocketSlotData[] slots = new PocketSlotData[SlotCount];
        [SerializeField] private int activeSlotIndex = 0;

        public int ActiveSlotIndex => activeSlotIndex;

        public void EnsureSlots()
        {
            if (slots == null || slots.Length != SlotCount)
            {
                System.Array.Resize(ref slots, SlotCount);
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            EnsureSlots();
        }

        private void OnEnable()
        {
            activeSlotIndex = Mathf.Clamp(activeSlotIndex, 0, HotbarCount - 1);
            if (weaponSocket != null)
            {
                weaponSocket.gameObject.SetActive(true);
            }
            RefreshPocketVisual();
            UpdateWeaponVisual();
        }
        private void OnDisable()
        {
            HideVisuals();
        }
        private void Start()
        {
            PlayerFieldNeeds.For(GetComponent<GaragePlayerController>());
            if (GetComponent<RogueDrive.UI.InventoryWindowUI>() == null) gameObject.AddComponent<RogueDrive.UI.InventoryWindowUI>();
            // Убеждаемся, что UI инвентаря инициализирован
            if (PocketInventoryUI.Instance == null)
            {
                var uiObj = new GameObject("PocketInventoryUI");
                uiObj.AddComponent<PocketInventoryUI>();
            }

            // Оповещаем подписчиков о начальном состоянии
            OnActiveSlotChanged?.Invoke(activeSlotIndex);
            for (int i = 0; i < SlotCount; i++)
            {
                OnSlotUpdated?.Invoke(i, slots[i]);
            }
            UpdateWeaponVisual();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [Header("Weapon Viewmodel")]
        [SerializeField] private Transform weaponSocket;
        [SerializeField] private GameObject activeWeaponModel;
        private Coroutine swingCoroutine;
        private Vector3 defaultSocketLocalPos = new Vector3(0.28f, -0.25f, 0.46f);
        private Quaternion defaultSocketLocalRot = Quaternion.identity;

        private void Update()
        {
            if (RogueDrive.UI.InventoryWindowUI.BlockGameplayInput) return;
            var player = GetComponent<GaragePlayerController>();
            if ((player != null && player.IsMovementLocked) || Cursor.lockState != CursorLockMode.Locked) return;
            HandleQuickSlotInput();
            HandleMeleeAttack();
        }

        private void HandleMeleeAttack()
        {
            var active = GetActiveItem();
            if (active.IsEmpty || (!active.id.Contains("axe") && !active.id.Contains("weapon"))) return;

            // Если в руках занят двуручный слот (колесо/канистра) или открыто меню, не бьем
            if (PlayerHandsInventory.Instance != null && PlayerHandsInventory.Instance.HasItem) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            if (Input.GetMouseButtonDown(0) && swingCoroutine == null)
            {
                swingCoroutine = StartCoroutine(PerformAxeSwing());
            }
        }

        private System.Collections.IEnumerator PerformAxeSwing()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayImpact();
            }

            if (weaponSocket != null)
            {
                Quaternion startRot = defaultSocketLocalRot;
                Quaternion swingRot = Quaternion.Euler(42f, -28f, 22f);
                Vector3 startPos = defaultSocketLocalPos;
                Vector3 swingPos = defaultSocketLocalPos + new Vector3(-0.12f, -0.06f, 0.14f);

                float elapsed = 0f;
                float duration = 0.11f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    weaponSocket.localPosition = Vector3.Lerp(startPos, swingPos, t);
                    weaponSocket.localRotation = Quaternion.Slerp(startRot, swingRot, t);
                    yield return null;
                }

                // Проверяем попадание лучом вперед
                var player = GetComponent<GaragePlayerController>();
                Camera cam = player != null ? player.PlayerCamera : Camera.main;
                if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 2.5f))
                {
                    var rb = hit.rigidbody;
                    if (rb != null && !rb.isKinematic)
                    {
                        rb.AddForceAtPosition(cam.transform.forward * 240f, hit.point, ForceMode.Impulse);
                    }
                }

                elapsed = 0f;
                float recoverDuration = 0.20f;
                while (elapsed < recoverDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / recoverDuration;
                    weaponSocket.localPosition = Vector3.Lerp(swingPos, startPos, t);
                    weaponSocket.localRotation = Quaternion.Slerp(swingRot, startRot, t);
                    yield return null;
                }

                weaponSocket.localPosition = defaultSocketLocalPos;
                weaponSocket.localRotation = defaultSocketLocalRot;
            }

            swingCoroutine = null;
        }

        private void EnsureWeaponSocket()
        {
            if (weaponSocket != null) return;

            var player = GetComponent<GaragePlayerController>() ?? FindFirstObjectByType<GaragePlayerController>();
            Camera cam = player != null ? player.PlayerCamera : Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();

            if (cam != null)
            {
                Transform existing = cam.transform.Find("WeaponHoldSocket");
                if (existing != null)
                {
                    weaponSocket = existing;
                }
                else
                {
                    var go = new GameObject("WeaponHoldSocket");
                    go.transform.SetParent(cam.transform, false);
                    go.transform.localPosition = defaultSocketLocalPos;
                    go.transform.localRotation = defaultSocketLocalRot;
                    weaponSocket = go.transform;
                }
            }
        }

        private void ConfigureAxeTransform(Transform axeTransform)
        {
            if (axeTransform == null) return;
            // Рукоять направлена вверх и вперед-влево из правой руки игрока
            Vector3 handleDir = new Vector3(-0.16f, 0.84f, 0.52f).normalized;
            // Острие лезвия направлено вперед и слегка вниз к прицелу
            Vector3 bladeDir = new Vector3(-0.06f, -0.38f, 0.92f).normalized;
            // В меше Axe.fbx: -Z идет вдоль рукояти к топорищу, -Y идет к острию лезвия
            Quaternion targetRot = Quaternion.LookRotation(-handleDir, -bladeDir);

            axeTransform.localRotation = targetRot;
            // Смещаем пивот из лезвия вниз по рукояти на 35 см (в точку хвата ладонью)
            axeTransform.localPosition = 0.35f * handleDir;
            axeTransform.localScale = Vector3.one * 0.85f;
        }

        private void UpdateWeaponVisual()
        {
            EnsureWeaponSocket();
            if (weaponSocket == null) return;

            var active = GetActiveItem();
            var player = GetComponent<GaragePlayerController>();
            bool driving = GarageDriveOutController.Instance != null && GarageDriveOutController.Instance.IsDriving;
            bool visible = !driving && (player == null || !player.IsMovementLocked);
            bool isAxe = visible && !active.IsEmpty && active.worldItem == null && (active.id.Contains("axe") || active.id == "weapon_axe") &&
                (PlayerHandsInventory.Instance == null || !PlayerHandsInventory.Instance.HasItem);

            if (activeWeaponModel != null)
            {
                activeWeaponModel.SetActive(isAxe);
                if (isAxe)
                {
                    ConfigureAxeTransform(activeWeaponModel.transform);
                }
            }
            else if (isAxe)
            {
                // Ищем или создаем модель топора в сокете
                Transform childAxe = weaponSocket.Find("Axe_ViewModel");
                if (childAxe != null)
                {
                    activeWeaponModel = childAxe.gameObject;
                    activeWeaponModel.SetActive(true);
                    ConfigureAxeTransform(childAxe);
                }
                else
                {
                    // Ищем префаб топора в проекте
                    GameObject axePrefab = null;
#if UNITY_EDITOR
                    axePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Downloads/AlexMakes3D/Polygon style/Halloween pack/Props/Prefabs/Axe.prefab");
#endif
                    if (axePrefab != null)
                    {
                        var spawned = Instantiate(axePrefab, weaponSocket);
                        spawned.name = "Axe_ViewModel";
                        ConfigureAxeTransform(spawned.transform);
                        var col = spawned.GetComponent<Collider>();
                        if (col != null) Destroy(col);
                        activeWeaponModel = spawned;
                    }
                }
            }
        }

        private void HandleQuickSlotInput()
        {
            for (int i=0;i<HotbarCount;i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))
            {
                if (Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)) MoveSlot(activeSlotIndex,i);
                else SelectSlot(i);
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0.05f)
            {
                int prev = (activeSlotIndex - 1 + HotbarCount) % HotbarCount;
                SelectSlot(prev);
            }
            else if (scroll < -0.05f)
            {
                int next = (activeSlotIndex + 1) % HotbarCount;
                SelectSlot(next);
            }
        }

        public void SelectSlot(int index)
        {
            if (index < 0 || index >= HotbarCount) return;
            if (activeSlotIndex == index) return;

            activeSlotIndex = index;
            OnActiveSlotChanged?.Invoke(activeSlotIndex);
            UpdateWeaponVisual();
            RefreshPocketVisual();
        }

        public bool TryAddItem(string id, string displayName, int count = 1, Sprite icon = null)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return false;

            // 1. Проверяем, есть ли уже этот предмет в каком-либо слоте (стакование)
            for (int i = 0; i < SlotCount; i++)
            {
                if (!slots[i].IsEmpty && slots[i].id == id && slots[i].worldItem == null)
                {
                    slots[i].count += count;
                    OnSlotUpdated?.Invoke(i, slots[i]);
                    return true;
                }
            }

            // 2. Ищем первый свободный слот
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i] = new PocketSlotData
                    {
                        id = id,
                        displayName = displayName,
                        count = count,
                        icon = icon
                    };
                    OnSlotUpdated?.Invoke(i, slots[i]);

                    // Автоматически переключаем фокус на новый предмет, если текущий слот пуст
                    if (slots[activeSlotIndex].IsEmpty)
                    {
                        SelectSlot(i);
                    }
                    else
                    {
                        UpdateWeaponVisual();
                    }

                    return true;
                }
            }

            // Инвентарь переполнен
            return false;
        }

        public bool RemoveItem(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return false;

            for (int i = 0; i < SlotCount; i++)
            {
                if (!slots[i].IsEmpty && slots[i].id == id)
                {
                    if (slots[i].worldItem != null)
                    {
                        int amount=Mathf.Min(count,slots[i].count);
                        for(int n=0;n<amount;n++){var item=slots[i].worldItem;RemovePhysicalHead(i);if(item!=null)Destroy(item.gameObject);}
                        return true;
                    }
                    if (slots[i].count > count)
                    {
                        slots[i].count -= count;
                        OnSlotUpdated?.Invoke(i, slots[i]);
                        UpdateWeaponVisual();
                        return true;
                    }
                    else
                    {
                        ClearSlot(i);
                        return true;
                    }
                }
            }

            return false;
        }

        public bool HasItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty && slots[i].id == id)
                {
                    return true;
                }
            }

            return false;
        }

        public int GetItemCount(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            EnsureSlots();
            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty && slots[i].id == id)
                {
                    total += slots[i].count;
                }
            }
            return total;
        }

        public PocketSlotData GetSlot(int index)
        {
            EnsureSlots();
            if (index < 0 || index >= slots.Length) return PocketSlotData.Empty;
            return slots[index];
        }

        public PocketSlotData GetActiveItem()
        {
            return GetSlot(activeSlotIndex);
        }

        public void ClearSlot(int index)
        {
            EnsureSlots();
            if (index < 0 || index >= slots.Length) return;
            if (slots[index].worldItem != null) Destroy(slots[index].worldItem.gameObject);
            if (slots[index].reserves != null) foreach(var entry in slots[index].reserves) if(entry.item!=null)Destroy(entry.item.gameObject);
            slots[index] = PocketSlotData.Empty;
            OnSlotUpdated?.Invoke(index, slots[index]);
            UpdateWeaponVisual();
            RefreshPocketVisual();
        }
    }
}


