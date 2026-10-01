using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDrive.Editor
{
    /// <summary>
    /// Редакторская утилита быстрой настройки любых 3D-моделей в сцене бункера / гаража:
    /// в 1 клик добавляет и подгоняет BoxCollider, настраивает Rigidbody
    /// и вешает интерактивный компонент (Колесо, АКБ, Канистра, Монтировка или свободный физический предмет).
    /// </summary>
    public static class BunkerItemSetupUtility
    {
        private const string MenuRoot = "GameObject/RogueDrive/";
        private const string TopMenuRoot = "RogueDrive/Настроить предмет в сцене/";

        #region Context Menu Actions

        [MenuItem(MenuRoot + "Сделать деталью: Колесо (Wheel)", false, 20)]
        [MenuItem(TopMenuRoot + "Деталь: Колесо (Wheel)", false, 20)]
        private static void SetupWheel()
        {
            SetupSelectedAsAssemblyItem(BunkerAssemblyItemType.Wheel, "Колесо со ступичным креплением", 12f);
        }

        [MenuItem(MenuRoot + "Сделать деталью: Аккумулятор (Battery)", false, 21)]
        [MenuItem(TopMenuRoot + "Деталь: Аккумулятор (Battery)", false, 21)]
        private static void SetupBattery()
        {
            SetupSelectedAsAssemblyItem(BunkerAssemblyItemType.Battery, "Силовой аккумулятор 12V", 14f);
        }

        [MenuItem(MenuRoot + "Сделать деталью: Канистра бензина (15 л из 20 л)", false, 22)]
        [MenuItem(TopMenuRoot + "Канистра бензина (15 л из 20 л)", false, 22)]
        private static void SetupFuel15L()
        {
            SetupSelectedCanister(BunkerFluidType.Gasoline, 20f, 15f);
        }

        [MenuItem(MenuRoot + "Сделать деталью: Канистра бензина (20 л - Полная)", false, 23)]
        [MenuItem(TopMenuRoot + "Канистра бензина (20 л - Полная)", false, 23)]
        private static void SetupFuel20L()
        {
            SetupSelectedCanister(BunkerFluidType.Gasoline, 20f, 20f);
        }

        [MenuItem(MenuRoot + "Сделать деталью: Пустая канистра (0 из 20 л)", false, 24)]
        [MenuItem(TopMenuRoot + "Пустая канистра (0 из 20 л)", false, 24)]
        private static void SetupFuelEmpty()
        {
            SetupSelectedCanister(BunkerFluidType.Empty, 20f, 0f);
        }

        [MenuItem(MenuRoot + "Сделать деталью: Канистра с водой (10 л)", false, 25)]
        [MenuItem(TopMenuRoot + "Канистра с водой (10 л)", false, 25)]
        private static void SetupWater10L()
        {
            SetupSelectedCanister(BunkerFluidType.Water, 10f, 10f);
        }

        [MenuItem(MenuRoot + "Сделать деталью: Монтировка (Crowbar)", false, 26)]
        [MenuItem(TopMenuRoot + "Деталь: Монтировка (Crowbar)", false, 26)]
        private static void SetupCrowbar()
        {
            SetupSelectedAsAssemblyItem(BunkerAssemblyItemType.Crowbar, "Стальная монтировка", 3f);
        }

        [MenuItem(MenuRoot + "Сделать физическим предметом (Physics Prop)", false, 30)]
        [MenuItem(TopMenuRoot + "Любой физический предмет (Physics Prop)", false, 30)]
        private static void SetupPhysicsProp()
        {
            var targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0) return;

            foreach (var go in targets)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Setup Physics Prop");

                EnsureCollider(go);
                var rb = EnsureRigidbody(go, 5f);

                // Убираем взаимоисключающий скрипт сборки
                var assemblyItem = go.GetComponent<BunkerAssemblyItem>();
                if (assemblyItem != null) Undo.DestroyObjectImmediate(assemblyItem);

                var prop = go.GetComponent<BunkerPhysicsProp>();
                if (prop == null)
                {
                    prop = Undo.AddComponent<BunkerPhysicsProp>(go);
                }

                EditorUtility.SetDirty(go);
                if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);

                Debug.Log($"<color=#55FF88>[RogueDrive]</color> Объект <b>'{go.name}'</b> настроен как физический предмет (можно брать на [E] и бросать на [G]).");
            }

            ShowNotificationMessage($"Настроено физических предметов: {targets.Length}");
        }

        [MenuItem(MenuRoot + "Сделать карманным предметом (Pocket Item)", false, 32)]
        [MenuItem(TopMenuRoot + "Мелкий карманный предмет (Pocket Item)", false, 32)]
        private static void SetupPocketItem()
        {
            var targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0) return;

            foreach (var go in targets)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Setup Pocket Item");

                var col = EnsureCollider(go);
                if (col != null) col.isTrigger = true;

                // Убираем взаимоисключающие скрипты
                var assemblyItem = go.GetComponent<BunkerAssemblyItem>();
                if (assemblyItem != null) Undo.DestroyObjectImmediate(assemblyItem);
                var prop = go.GetComponent<BunkerPhysicsProp>();
                if (prop != null) Undo.DestroyObjectImmediate(prop);

                var pickup = go.GetComponent<PocketItemPickup>();
                if (pickup == null)
                {
                    pickup = Undo.AddComponent<PocketItemPickup>(go);
                    pickup.Configure(go.name.ToLower().Replace(" ", "_"), go.name, 1);
                }

                EditorUtility.SetDirty(go);
                if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);

                Debug.Log($"<color=#55FF88>[RogueDrive]</color> Объект <b>'{go.name}'</b> настроен как карманный предмет (подбирается в пояс 1..5 на [E]).");
            }

            ShowNotificationMessage($"Настроено карманных предметов: {targets.Length}");
        }

        [MenuItem(MenuRoot + "Подогнать BoxCollider по размеру модели", false, 40)]
        [MenuItem(TopMenuRoot + "Подогнать BoxCollider по размеру модели", false, 40)]
        private static void FitColliderAction()
        {
            var targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0) return;

            foreach (var go in targets)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Fit BoxCollider");
                FitBoxCollider(go);
                EditorUtility.SetDirty(go);
                if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
            }

            ShowNotificationMessage($"Коллайдеры подогнаны для {targets.Length} объектов");
        }

        #endregion

        #region Validation

        [MenuItem(MenuRoot + "Сделать деталью: Колесо (Wheel)", true)]
        [MenuItem(MenuRoot + "Сделать деталью: Аккумулятор (Battery)", true)]
        [MenuItem(MenuRoot + "Сделать деталью: Канистра бензина (15 л из 20 л)", true)]
        [MenuItem(MenuRoot + "Сделать деталью: Канистра бензина (20 л - Полная)", true)]
        [MenuItem(MenuRoot + "Сделать деталью: Пустая канистра (0 из 20 л)", true)]
        [MenuItem(MenuRoot + "Сделать деталью: Канистра с водой (10 л)", true)]
        [MenuItem(MenuRoot + "Сделать деталью: Монтировка (Crowbar)", true)]
        [MenuItem(MenuRoot + "Сделать физическим предметом (Physics Prop)", true)]
        [MenuItem(MenuRoot + "Сделать карманным предметом (Pocket Item)", true)]
        [MenuItem(MenuRoot + "Подогнать BoxCollider по размеру модели", true)]
        private static bool ValidateSelection()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        #endregion

        #region Setup Helpers

        private static void SetupSelectedAsAssemblyItem(BunkerAssemblyItemType type, string displayName, float defaultMass)
        {
            var targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0) return;

            foreach (var go in targets)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, $"Setup {type}");

                EnsureCollider(go);
                EnsureRigidbody(go, defaultMass);

                // Убираем взаимоисключающий скрипт обычного пропа
                var prop = go.GetComponent<BunkerPhysicsProp>();
                if (prop != null) Undo.DestroyObjectImmediate(prop);

                var assemblyItem = go.GetComponent<BunkerAssemblyItem>();
                if (assemblyItem == null)
                {
                    assemblyItem = Undo.AddComponent<BunkerAssemblyItem>(go);
                }

                assemblyItem.Configure(type, displayName);

                EditorUtility.SetDirty(go);
                if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);

                Debug.Log($"<color=#55FF88>[RogueDrive]</color> Объект <b>'{go.name}'</b> успешно настроен как деталь сборки: <b>{displayName}</b> (масса: {defaultMass} кг).");
            }

            ShowNotificationMessage($"Объектов настроено: {targets.Length} ({displayName})");
        }

        private static void SetupSelectedCanister(BunkerFluidType fluidType, float capacity, float currentLiters)
        {
            var targets = Selection.gameObjects;
            if (targets == null || targets.Length == 0) return;

            foreach (var go in targets)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Setup Canister");

                EnsureCollider(go);

                // Убираем взаимоисключающий скрипт обычного пропа
                var prop = go.GetComponent<BunkerPhysicsProp>();
                if (prop != null) Undo.DestroyObjectImmediate(prop);

                var assemblyItem = go.GetComponent<BunkerAssemblyItem>();
                if (assemblyItem == null)
                {
                    assemblyItem = Undo.AddComponent<BunkerAssemblyItem>(go);
                }

                var itemType = fluidType == BunkerFluidType.Water
                    ? BunkerAssemblyItemType.WaterCanister
                    : BunkerAssemblyItemType.FuelCanister;

                assemblyItem.Configure(itemType);

                var container = go.GetComponent<BunkerFluidContainer>();
                if (container == null)
                {
                    container = Undo.AddComponent<BunkerFluidContainer>(go);
                }

                container.Configure(fluidType, capacity, currentLiters);
                EnsureRigidbody(go, container.TotalMass);

                EditorUtility.SetDirty(go);
                if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);

                Debug.Log($"<color=#55FF88>[RogueDrive]</color> Канистра <b>'{go.name}'</b> успешно настроена: <b>{container.GetDefaultDisplayName()}</b> (Вес: {container.TotalMass:F2} кг).");
            }

            ShowNotificationMessage($"Канистр настроено: {targets.Length}");
        }

        public static Collider EnsureCollider(GameObject go)
        {
            // Проверяем, есть ли уже коллайдер на самом объекте или его детях
            var existingColliders = go.GetComponentsInChildren<Collider>();
            if (existingColliders.Length > 0)
            {
                // Если есть MeshCollider, PhysX требует включенный Convex для динамических Rigidbody!
                foreach (var col in existingColliders)
                {
                    if (col is MeshCollider mc && !mc.convex)
                    {
                        Undo.RecordObject(mc, "Enable Convex");
                        mc.convex = true;
                    }
                }
                return existingColliders[0];
            }

            // Коллайдеров нет — рассчитываем аккуратный BoxCollider по Renderer'ам
            return FitBoxCollider(go);
        }

        public static BoxCollider FitBoxCollider(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                var fallbackBox = go.GetComponent<BoxCollider>() ?? Undo.AddComponent<BoxCollider>(go);
                fallbackBox.size = Vector3.one * 0.5f;
                fallbackBox.center = Vector3.zero;
                return fallbackBox;
            }

            // Считаем совокупный мировой Bounds по всем видимым мешам
            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }

            var box = go.GetComponent<BoxCollider>() ?? Undo.AddComponent<BoxCollider>(go);
            Undo.RecordObject(box, "Adjust BoxCollider");

            // Преобразуем координаты мирового бокса в локальные для корневого объекта
            box.center = go.transform.InverseTransformPoint(worldBounds.center);

            Vector3 lossy = go.transform.lossyScale;
            box.size = new Vector3(
                Mathf.Abs(lossy.x) > 0.0001f ? worldBounds.size.x / Mathf.Abs(lossy.x) : 0.5f,
                Mathf.Abs(lossy.y) > 0.0001f ? worldBounds.size.y / Mathf.Abs(lossy.y) : 0.5f,
                Mathf.Abs(lossy.z) > 0.0001f ? worldBounds.size.z / Mathf.Abs(lossy.z) : 0.5f
            );
            return box;
        }

        public static Rigidbody EnsureRigidbody(GameObject go, float mass)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody>(go);
            }

            Undo.RecordObject(rb, "Configure Rigidbody");
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            return rb;
        }

        private static void ShowNotificationMessage(string message)
        {
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent(message));
            }
        }

        #endregion
    }
}
