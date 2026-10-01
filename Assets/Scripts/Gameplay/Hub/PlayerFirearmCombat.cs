using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RogueDrive.Audio;
using RogueDrive.UI;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Контроллер стрельбы из огнестрельного оружия от первого лица.
    /// Поддерживает:
    /// — Плавное прицеливание (ПКМ, ADS zoom);
    /// — Выстрел лучом (ЛКМ), отдачу, вспышку дула, звук выстрела;
    /// — Систему боеприпасов с реальным расходом патронов из инвентаря (9мм патроны);
    /// — Постоянный тактический HUD оружия (магазин, запас, индикатор перезарядки);
    /// — Эффекты попадания пуль (брызги крови по зомби, искры и рикошет по поверхностям);
    /// — Привлечение бродячих зомби шумом выстрела в радиусе 40 метров.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class PlayerFirearmCombat : MonoBehaviour
    {
        private GaragePlayerController player;
        private PlayerPocketInventory pocket;
        private Camera playerCamera;

        private FirearmWeapon currentWeapon;
        private float nextFireTime;
        private float reloadEndTime;
        private float reloadStartTime;
        private bool isReloading;
        private bool isAiming;
        [SerializeField] private bool starterLoadoutIssued;

        // Отдача и визуал оружия
        private float recoilAngle = 0f;
        private Vector3 recoilOffset = Vector3.zero;
        private float originalFov = 68f;
        private Light muzzleFlashLight;

        // Firearm HUD Elements
        private Canvas hudCanvas;
        private GameObject hudRoot;
        private Text weaponNameText;
        private Text ammoCounterText;
        private Text ammoReserveText;
        private Text reloadPromptText;
        private Image reloadProgressBar;

        public bool IsAiming => isAiming;
        public bool IsReloading => isReloading;

        private void OnEnable()
        {
            if (player == null) player = GetComponent<GaragePlayerController>();
            if (pocket == null) pocket = GetComponent<PlayerPocketInventory>();
            EnsureStarterFirearm();
        }

        private void OnDisable()
        {
            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }
            isAiming = false;
            isReloading = false;
        }

        private void Start()
        {
            player = GetComponent<GaragePlayerController>();
            pocket = GetComponent<PlayerPocketInventory>();
            if (player != null) playerCamera = player.PlayerCamera;
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null) originalFov = playerCamera.fieldOfView;

            EnsureMuzzleLight();
            EnsureStarterFirearm();
            BuildFirearmHUD();
        }

        private void EnsureMuzzleLight()
        {
            if (muzzleFlashLight == null && playerCamera != null)
            {
                var flashObj = new GameObject("MuzzleFlashLight");
                flashObj.transform.SetParent(playerCamera.transform, false);
                flashObj.transform.localPosition = new Vector3(0.2f, -0.1f, 0.4f);
                muzzleFlashLight = flashObj.AddComponent<Light>();
                muzzleFlashLight.type = LightType.Point;
                muzzleFlashLight.range = 5.5f;
                muzzleFlashLight.intensity = 3.0f;
                muzzleFlashLight.color = new Color(1f, 0.85f, 0.45f);
                muzzleFlashLight.enabled = false;
            }
        }

        public void EnsureStarterFirearm()
        {
            if (starterLoadoutIssued || GarageDepartureCheckpoint.HasPendingRestore
                || GarageDepartureCheckpoint.Restoring || JourneyCheckpoint.HasPendingRestore || JourneyCheckpoint.Restoring)
                return;
            if (pocket == null) pocket = GetComponent<PlayerPocketInventory>();
            if (pocket == null) return;
            // Grant once per expedition, even after the gun is sold or ammo runs out.
            starterLoadoutIssued = true;
            if (pocket.GetItemCount("ammo_9mm") == 0)
                pocket.TryAddItem("ammo_9mm", "Патроны 9мм", 21);

            if (GetComponentInChildren<FirearmWeapon>(true) != null) return;

            var prefab = Resources.Load<GameObject>("Combat/Pistol");
            if (prefab == null)
            {
                prefab = AssetDatabaseLoadFallback();
            }

            if (prefab != null)
            {
                var spawned = Instantiate(prefab, transform.position + transform.forward, Quaternion.identity);
                var prop = spawned.GetComponent<PhysicsProp>() ?? spawned.AddComponent<PhysicsProp>();
                prop.Configure("Пистолет 9мм");
                prop.SetPocketSized(true);

                var weapon = spawned.GetComponent<FirearmWeapon>() ?? spawned.AddComponent<FirearmWeapon>();
                weapon.SetAmmo(7);

                var identity = spawned.GetComponent<GarageCheckpointItem>() ?? spawned.AddComponent<GarageCheckpointItem>();
                if (string.IsNullOrEmpty(identity.id)) identity.id = "combat_pistol";

                // If the pocket is full, leave this single issued weapon nearby.
                if (!pocket.TryStorePhysical(prop)) prop.OnDropped(Vector3.zero, Vector3.zero);
            }
        }

        public void MarkStarterLoadoutIssued() => starterLoadoutIssued = true;

        private GameObject AssetDatabaseLoadFallback()
        {
#if UNITY_EDITOR
            // Return an asset, not a second live weapon left outside the inventory.
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Downloads/ithappy/Apocalypse_Free/Prefabs/Props/Pistol1.prefab");
#else
            return null;
#endif
        }

        private bool CanAct()
        {
            bool inGarageHub = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Garage");
            return isActiveAndEnabled && player != null && player.isActiveAndEnabled
                && !player.IsMovementLocked && playerCamera != null
                && GetComponent<PlayerFieldNeeds>()?.IsDead != true && Time.timeScale > 0f
                && !InventoryWindowUI.BlockGameplayInput
                && (Cursor.lockState == CursorLockMode.Locked || Application.isEditor)
                && GetComponent<PlayerHandsInventory>()?.HasItem != true
                && (!inGarageHub || WorkshopServiceZone.Find(transform.position)?.Safe != true);
        }

        private void Update()
        {
            if (pocket == null) pocket = GetComponent<PlayerPocketInventory>();
            currentWeapon = pocket?.ActivePhysical != null ? pocket.ActivePhysical.GetComponent<FirearmWeapon>() : null;

            bool canShoot = CanAct() && currentWeapon != null;

            // Обновляем тактический HUD боеприпасов
            UpdateFirearmHUD(canShoot);

            if (!canShoot)
            {
                isAiming = false;
                isReloading = false;
                if (playerCamera != null && Mathf.Abs(playerCamera.fieldOfView - originalFov) > 0.1f)
                {
                    playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, originalFov, Time.deltaTime * 10f);
                }
                return;
            }

            // 1. Прицеливание (ПКМ)
            isAiming = Input.GetMouseButton(1);
            float targetFov = isAiming ? 46f : originalFov;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * 12f);

            // 2. Обработка перезарядки
            if (isReloading)
            {
                if (Time.time >= reloadEndTime)
                {
                    CompleteReload();
                }
                return;
            }

            // 3. Выстрел (ЛКМ)
            if (Input.GetMouseButtonDown(0))
            {
                if (Time.time >= nextFireTime)
                {
                    TryFire();
                }
            }

            // 4. Перезарядка (R)
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (currentWeapon.CurrentAmmo < currentWeapon.MaxMagazine)
                {
                    StartReload();
                }
            }
        }

        private void TryFire()
        {
            if (currentWeapon == null) return;

            if (!currentWeapon.TryConsumeAmmo())
            {
                // Щелчок бойка (патроны кончились)
                AudioManager.Instance?.PlaySwitchClick();
                GarageInteractionUI.Instance?.ShowNotification("Магазин пуст! Нажмите [R] для перезарядки", 1.2f);
                StartReload();
                return;
            }

            nextFireTime = Time.time + currentWeapon.FireRate;

            // Звук выстрела
            AudioManager.Instance?.PlayShoot(0.12f);

            // Физическая отдача
            recoilAngle += isAiming ? 3.5f : 6.5f;
            recoilOffset += new Vector3(0f, isAiming ? 0.025f : 0.045f, -0.07f);

            // Вспышка дульного пламени
            if (muzzleFlashLight != null)
            {
                StopAllCoroutines();
                StartCoroutine(MuzzleFlashRoutine());
            }

            // Луч попадания из центра прицела
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, currentWeapon.Range, ~0, QueryTriggerInteraction.Ignore))
            {
                // Проверка на зомби
                var zombie = hit.collider.GetComponentInParent<EncounterZombie>();
                if (zombie != null && !zombie.IsDead)
                {
                    bool headshot = hit.point.y > (zombie.transform.position.y + 1.35f);
                    float damage = currentWeapon.Damage * (headshot ? 2.0f : 1.0f);
                    zombie.TakeDamage(damage);
                    SpawnImpactEffect(hit, true, headshot);

                    if (headshot)
                    {
                        GarageInteractionUI.Instance?.ShowNotification("💥 ТОЧНО В ГОЛОВУ! КРИТИЧЕСКИЙ УРОН", 1.1f);
                    }
                }
                else
                {
                    // Проверка разрушаемых препятствий или других целей
                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(currentWeapon.Damage);
                    }
                    SpawnImpactEffect(hit, false, false);
                }
            }

            // Шум выстрела привлекает зомби в радиусе 40 метров
            Collider[] alertColliders = Physics.OverlapSphere(transform.position, 40f);
            for (int i = 0; i < alertColliders.Length; i++)
            {
                var z = alertColliders[i].GetComponentInParent<EncounterZombie>();
                if (z != null)
                {
                    z.AlertToSound(transform.position);
                }
            }
        }

        private void SpawnImpactEffect(RaycastHit hit, bool isZombie, bool headshot)
        {
            if (isZombie)
            {
                AudioManager.Instance?.PlayImpact(headshot ? 1.0f : 0.7f);
                // Эффект всплеска крови
                GameObject blood = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blood.name = "BloodSplatter_FX";
                blood.transform.position = hit.point;
                blood.transform.localScale = Vector3.one * (headshot ? 0.35f : 0.22f);
                Destroy(blood.GetComponent<Collider>());
                var r = blood.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.65f, 0.05f, 0.05f, 0.95f);
                    r.sharedMaterial = mat;
                }
                Destroy(blood, 0.22f);
            }
            else
            {
                AudioManager.Instance?.PlayRicochet();
                // Эффект искр и пыли
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "BulletSpark_FX";
                spark.transform.position = hit.point + hit.normal * 0.05f;
                spark.transform.localScale = Vector3.one * 0.12f;
                Destroy(spark.GetComponent<Collider>());
                var r = spark.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(1f, 0.9f, 0.4f, 1f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.2f) * 2f);
                    r.sharedMaterial = mat;
                }
                Destroy(spark, 0.14f);
            }
        }

        private void StartReload()
        {
            if (isReloading || currentWeapon == null) return;

            int reserveAmmo = pocket != null ? pocket.GetItemCount("ammo_9mm") : 0;
            if (reserveAmmo <= 0)
            {
                AudioManager.Instance?.PlaySwitchClick();
                GarageInteractionUI.Instance?.ShowNotification("Нет патронов 9мм! Обыщите рюкзаки и коробки на остановках.", 2.2f);
                return;
            }

            isReloading = true;
            reloadStartTime = Time.time;
            reloadEndTime = Time.time + currentWeapon.ReloadDuration;
            AudioManager.Instance?.PlayKeysJingle();
        }

        private void CompleteReload()
        {
            isReloading = false;
            if (currentWeapon == null) return;

            int needed = currentWeapon.MaxMagazine - currentWeapon.CurrentAmmo;
            int reserveAmmo = pocket != null ? pocket.GetItemCount("ammo_9mm") : 0;
            int loaded = Mathf.Min(needed, reserveAmmo);

            if (loaded > 0)
            {
                pocket.RemoveItem("ammo_9mm", loaded);
                currentWeapon.Reload(loaded);
                AudioManager.Instance?.PlaySwitchClick();
                int remaining = pocket.GetItemCount("ammo_9mm");
                GarageInteractionUI.Instance?.ShowNotification($"Пистолет заряжен: {currentWeapon.CurrentAmmo}/{currentWeapon.MaxMagazine} (В запасе: {remaining})", 1.8f);
            }
        }

        private IEnumerator MuzzleFlashRoutine()
        {
            muzzleFlashLight.enabled = true;
            yield return new WaitForSeconds(0.045f);
            muzzleFlashLight.enabled = false;
        }

        private void BuildFirearmHUD()
        {
            if (hudCanvas != null) return;

            var canvasObj = new GameObject("FirearmCombat_HUD");
            hudCanvas = canvasObj.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 35;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                                Resources.GetBuiltinResource<Font>("Arial.ttf");

            hudRoot = new GameObject("WeaponPanel");
            hudRoot.transform.SetParent(canvasObj.transform, false);

            var rootRect = hudRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-28f, 28f);
            rootRect.sizeDelta = new Vector2(240f, 90f);

            var bgImg = hudRoot.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.08f, 0.10f, 0.85f);

            // Название оружия
            var nameObj = new GameObject("WeaponTitle");
            nameObj.transform.SetParent(hudRoot.transform, false);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -8f);
            nameRect.sizeDelta = new Vector2(220f, 20f);
            weaponNameText = nameObj.AddComponent<Text>();
            weaponNameText.font = standardFont;
            weaponNameText.fontSize = 12;
            weaponNameText.alignment = TextAnchor.MiddleCenter;
            weaponNameText.color = new Color(0.7f, 0.75f, 0.8f, 0.9f);
            weaponNameText.text = "ПИСТОЛЕТ 9ММ";

            // Счётчик патронов
            var ammoObj = new GameObject("AmmoCount");
            ammoObj.transform.SetParent(hudRoot.transform, false);
            var ammoRect = ammoObj.AddComponent<RectTransform>();
            ammoRect.anchorMin = new Vector2(0f, 0f);
            ammoRect.anchorMax = new Vector2(0.55f, 0.75f);
            ammoRect.offsetMin = Vector2.zero;
            ammoRect.offsetMax = Vector2.zero;
            ammoCounterText = ammoObj.AddComponent<Text>();
            ammoCounterText.font = standardFont;
            ammoCounterText.fontSize = 28;
            ammoCounterText.alignment = TextAnchor.MiddleCenter;
            ammoCounterText.color = Color.white;
            ammoCounterText.text = "7 / 7";

            // Запас патронов
            var reserveObj = new GameObject("AmmoReserve");
            reserveObj.transform.SetParent(hudRoot.transform, false);
            var reserveRect = reserveObj.AddComponent<RectTransform>();
            reserveRect.anchorMin = new Vector2(0.55f, 0.35f);
            reserveRect.anchorMax = new Vector2(1f, 0.75f);
            reserveRect.offsetMin = Vector2.zero;
            reserveRect.offsetMax = Vector2.zero;
            ammoReserveText = reserveObj.AddComponent<Text>();
            ammoReserveText.font = standardFont;
            ammoReserveText.fontSize = 13;
            ammoReserveText.alignment = TextAnchor.MiddleLeft;
            ammoReserveText.color = new Color(0.35f, 0.85f, 1f, 1f);
            ammoReserveText.text = "ЗАПАС: 21";

            // Подсказка перезарядки
            var promptObj = new GameObject("ReloadPrompt");
            promptObj.transform.SetParent(hudRoot.transform, false);
            var promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0f, 0f);
            promptRect.anchorMax = new Vector2(1f, 0.35f);
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;
            reloadPromptText = promptObj.AddComponent<Text>();
            reloadPromptText.font = standardFont;
            reloadPromptText.fontSize = 11;
            reloadPromptText.alignment = TextAnchor.MiddleCenter;
            reloadPromptText.color = new Color(0.9f, 0.7f, 0.3f, 1f);
            reloadPromptText.text = "[ПКМ] Прицел  ·  [R] Зарядить";

            hudRoot.SetActive(false);
        }

        private void UpdateFirearmHUD(bool visible)
        {
            if (hudRoot == null) return;

            if (!visible)
            {
                if (hudRoot.activeSelf) hudRoot.SetActive(false);
                return;
            }

            if (!hudRoot.activeSelf) hudRoot.SetActive(true);

            if (currentWeapon != null)
            {
                int current = currentWeapon.CurrentAmmo;
                int max = currentWeapon.MaxMagazine;
                int reserve = pocket != null ? pocket.GetItemCount("ammo_9mm") : 0;

                // Основной счетчик
                if (ammoCounterText != null)
                {
                    string colorTag = current == 0 ? "<color=#FF4444>" : (current <= 2 ? "<color=#FFAA00>" : "<color=#FFFFFF>");
                    ammoCounterText.text = $"{colorTag}<b>{current}</b></color> <color=#777777>/</color> <size=20>{max}</size>";
                }

                // Запас в карманах
                if (ammoReserveText != null)
                {
                    ammoReserveText.text = $"ЗАПАС: {reserve}";
                    ammoReserveText.color = reserve > 0 ? new Color(0.35f, 0.85f, 1f, 1f) : new Color(0.7f, 0.3f, 0.3f, 0.8f);
                }

                // Подсказка статуса
                if (reloadPromptText != null)
                {
                    if (isReloading)
                    {
                        float progress = Mathf.Clamp01((Time.time - reloadStartTime) / currentWeapon.ReloadDuration);
                        reloadPromptText.text = $"<color=#FFCC00>ПЕРЕЗАРЯДКА... {Mathf.RoundToInt(progress * 100f)}%</color>";
                    }
                    else if (current == 0)
                    {
                        float pulse = Mathf.PingPong(Time.time * 4f, 1f);
                        reloadPromptText.text = pulse > 0.5f ? "<color=#FF3333><b>[R] МАГАЗИН ПУСТ!</b></color>" : "<color=#FFAA33><b>[R] ПЕРЕЗАРЯДИТЬ</b></color>";
                    }
                    else if (current <= 2)
                    {
                        reloadPromptText.text = "<color=#FFAA00>[R] ПЕРЕЗАРЯДИТЬ</color>";
                    }
                    else
                    {
                        reloadPromptText.text = isAiming ? "<color=#55FF77>ПРИЦЕЛИВАНИЕ</color>" : "[ПКМ] Прицел  ·  [R] Зарядить";
                    }
                }
            }
        }

        private void LateUpdate()
        {
            // Плавное гашение отдачи
            recoilAngle = Mathf.Lerp(recoilAngle, 0f, Time.deltaTime * 14f);
            recoilOffset = Vector3.Lerp(recoilOffset, Vector3.zero, Time.deltaTime * 14f);

            if (currentWeapon == null || !currentWeapon.gameObject.activeInHierarchy) return;

            // Позиционирование пистолета в руках (от бедра и при прицеливании)
            // В WeaponHoldSocket (который находится в (0.28, -0.25, 0.46) камеры):
            // Для положения от бедра: помещаем пистолет в (0.22, -0.16, 0.42) камеры
            Vector3 hipPos = new Vector3(-0.06f, 0.09f, -0.04f);
            // Для прицеливания (ADS): выравниваем мушку и целик ровно по центру камеры: X = 0, Y = -0.11, Z = 0.36
            Vector3 aimPos = new Vector3(-0.28f, 0.14f, -0.10f);
            Vector3 targetLocalPos = (isAiming ? aimPos : hipPos) + recoilOffset;

            // У 3D-модели Pistol1 ствол направлен вдоль локальной оси -X.
            // Для стрельбы строго вперед (+Z) необходим базовый поворот +90 градусов по Y.
            // От бедра добавляем легкий естественный наклон к точке прицеливания.
            Quaternion aimRot = Quaternion.Euler(-recoilAngle, 90f, 0f);
            Quaternion hipRot = Quaternion.Euler(-recoilAngle - 2f, 90f - 3f, 4f);
            Quaternion targetLocalRot = isAiming ? aimRot : hipRot;

            currentWeapon.transform.localPosition = Vector3.Lerp(currentWeapon.transform.localPosition, targetLocalPos, Time.deltaTime * 18f);
            currentWeapon.transform.localRotation = Quaternion.Slerp(currentWeapon.transform.localRotation, targetLocalRot, Time.deltaTime * 18f);
        }

        private void OnDestroy()
        {
            if (hudCanvas != null) Destroy(hudCanvas.gameObject);
        }
    }
}
