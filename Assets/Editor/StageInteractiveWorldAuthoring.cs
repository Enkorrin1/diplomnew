using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Editor
{
    public static class StageInteractiveWorldAuthoring
    {
        [MenuItem("RogueDrive/World/Apply Interactive Stashes, Physics Props & Destructibles")]
        public static void ApplyToActiveScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[InteractiveWorld] Остановите Play Mode перед настройкой сцены.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var stageWorld = GameObject.Find("Stage_World");
            if (stageWorld == null)
            {
                Debug.LogWarning("[InteractiveWorld] Stage_World не найден в активной сцене " + scene.name);
                return;
            }

            int reactivatedCanisters = 0;
            int configuredProps = 0;
            int configuredStashes = 0;
            int configuredDestructibles = 0;

            // =========================================================================
            // 1. Активация и настройка всех канистр и полезных физических предметов
            // =========================================================================
            var allCanisters = stageWorld.GetComponentsInChildren<BunkerAssemblyItem>(true)
                .Concat(stageWorld.GetComponentsInChildren<FluidContainer>(true).Select(f => f.GetComponent<BunkerAssemblyItem>()))
                .Where(c => c != null).Distinct().ToArray();

            foreach (var can in allCanisters)
            {
                if (!can.gameObject.activeSelf)
                {
                    can.gameObject.SetActive(true);
                    reactivatedCanisters++;
                }

                // Гарантируем Rigidbody и коллайдер
                var rb = can.GetComponent<Rigidbody>();
                if (rb == null) rb = can.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.mass = can.name.Contains("Water") ? 8f : 6f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                var col = can.GetComponent<Collider>();
                if (col == null)
                {
                    var box = can.gameObject.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.35f, 0.45f, 0.25f);
                    box.center = new Vector3(0f, 0.22f, 0f);
                }

                if (can.GetComponent<GarageItemUse>() == null) can.gameObject.AddComponent<GarageItemUse>();

                var prop = can.GetComponent<PhysicsProp>();
                if (prop == null)
                {
                    prop = can.gameObject.AddComponent<PhysicsProp>();
                    prop.Configure(can.name.Contains("Water") ? "Канистра с водой" : can.name.Contains("Oil") ? "Канистра с маслом" : "Канистра бензина");
                    configuredProps++;
                }
            }

            // Настройка кружки в лагере (Forest_Camp_Episode/mug)
            var camp = GameObject.Find("Stage_World/AuthoredStageWorld/Forest_Camp_Episode");
            if (camp != null)
            {
                var mug = camp.transform.Find("Mug");
                if (mug != null)
                {
                    var rb = mug.GetComponent<Rigidbody>() ?? mug.gameObject.AddComponent<Rigidbody>();
                    rb.mass = 0.5f;
                    var col = mug.GetComponent<Collider>();
                    if (col == null)
                    {
                        var box = mug.gameObject.AddComponent<BoxCollider>();
                        box.size = new Vector3(0.2f, 0.2f, 0.2f);
                        box.center = Vector3.zero;
                    }
                    var prop = mug.GetComponent<PhysicsProp>() ?? mug.gameObject.AddComponent<PhysicsProp>();
                    prop.Configure("Эмалированная кружка");
                    prop.SetPocketSized(true);

                    var func = mug.GetComponent<GarageItemFunction>() ?? mug.gameObject.AddComponent<GarageItemFunction>();
                    func.Configure(GarageItemFunction.ItemKind.Mug);
                    if (mug.GetComponent<GarageItemUse>() == null) mug.gameObject.AddComponent<GarageItemUse>();
                    configuredProps++;
                }
            }

            // =========================================================================
            // 2. Интерактивные тайники (Рюкзаки, коробки припасов, багажники машин)
            // =========================================================================
            var allTransforms = stageWorld.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                string nameLower = t.name.ToLower();

                // Рюкзаки
                if (nameLower.Contains("backpack"))
                {
                    t.gameObject.SetActive(true);
                    var col = t.GetComponent<Collider>();
                    if (col == null)
                    {
                        var box = t.gameObject.AddComponent<BoxCollider>();
                        box.size = new Vector3(0.55f, 0.65f, 0.4f);
                        box.center = new Vector3(0f, 0.3f, 0f);
                    }
                    var stash = t.GetComponent<RoadsideScavengePoint>() ?? t.gameObject.AddComponent<RoadsideScavengePoint>();
                    stash.Configure(RoadsideScavengePoint.ScavengeType.Backpack, "Рюкзак сталкера");
                    configuredStashes++;
                }
                // Открытые картонные коробки и ящики припасов
                else if (nameLower.Contains("cardboardbox") || nameLower.Contains("supplycrate"))
                {
                    t.gameObject.SetActive(true);
                    var col = t.GetComponent<Collider>();
                    if (col == null)
                    {
                        var box = t.gameObject.AddComponent<BoxCollider>();
                        box.size = new Vector3(0.6f, 0.5f, 0.6f);
                        box.center = new Vector3(0f, 0.25f, 0f);
                    }
                    var stash = t.GetComponent<RoadsideScavengePoint>() ?? t.gameObject.AddComponent<RoadsideScavengePoint>();
                    stash.Configure(RoadsideScavengePoint.ScavengeType.CardboardBox, "Ящик с припасами");
                    configuredStashes++;
                }
                // Брошенные легковые автомобили и пикапы на локациях
                else if (nameLower.StartsWith("vehicle_pick up truck") || nameLower.StartsWith("vehicle_car") || nameLower.StartsWith("n van"))
                {
                    // Добавляем точку обыска багажника, если еще нет
                    if (t.Find("TrunkScavengeSpot") == null && t.GetComponent<RoadsideScavengePoint>() == null)
                    {
                        GameObject trunkSpot = new GameObject("TrunkScavengeSpot");
                        trunkSpot.transform.SetParent(t, false);
                        // Багажник обычно сзади по Z
                        trunkSpot.transform.localPosition = new Vector3(0f, 0.8f, -2.1f);
                        var box = trunkSpot.AddComponent<BoxCollider>();
                        box.size = new Vector3(1.6f, 1.2f, 1.2f);
                        var stash = trunkSpot.AddComponent<RoadsideScavengePoint>();
                        stash.Configure(RoadsideScavengePoint.ScavengeType.AbandonedCarTrunk, "Брошенный авто");
                        configuredStashes++;
                    }
                }
            }

            // =========================================================================
            // 3. Физическая разрушаемость препятствий на трассе
            // =========================================================================
            foreach (var t in allTransforms)
            {
                string nameLower = t.name.ToLower();

                // Деревянные поддоны
                if (nameLower.Contains("pallet") && !nameLower.Contains("shelffull"))
                {
                    if (t.GetComponent<DestructibleTrackObstacle>() == null)
                    {
                        var col = t.GetComponent<Collider>();
                        if (col == null)
                        {
                            var box = t.gameObject.AddComponent<BoxCollider>();
                            box.size = new Vector3(1.2f, 0.25f, 1.2f);
                        }
                        var dest = t.gameObject.AddComponent<DestructibleTrackObstacle>();
                        dest.Configure(DestructibleTrackObstacle.ObstacleType.WoodPallet, false);
                        configuredDestructibles++;
                    }
                }
                // Мусорные контейнеры / урны
                else if (nameLower.Contains("dustbin") || nameLower.Contains("trashbin"))
                {
                    if (t.GetComponent<DestructibleTrackObstacle>() == null)
                    {
                        var col = t.GetComponent<Collider>();
                        if (col == null)
                        {
                            var box = t.gameObject.AddComponent<BoxCollider>();
                            box.size = new Vector3(0.5f, 0.9f, 0.5f);
                        }
                        var dest = t.gameObject.AddComponent<DestructibleTrackObstacle>();
                        dest.Configure(DestructibleTrackObstacle.ObstacleType.TrashBin, false);
                        configuredDestructibles++;
                    }
                }
                // Бочки на дороге и обочине
                else if (nameLower.Contains("barrelfbx") || nameLower.Contains("bottleneckbarrel"))
                {
                    // Проверяем, что это не интерактивная канистра с жидкостью
                    if (t.GetComponent<FluidContainer>() == null && t.GetComponent<DestructibleTrackObstacle>() == null)
                    {
                        var col = t.GetComponent<Collider>();
                        if (col == null)
                        {
                            var capsule = t.gameObject.AddComponent<CapsuleCollider>();
                            capsule.radius = 0.35f;
                            capsule.height = 1.1f;
                        }
                        var dest = t.gameObject.AddComponent<DestructibleTrackObstacle>();
                        bool isExplosive = nameLower.Contains("bottleneck") || Random.value < 0.2f;
                        dest.Configure(DestructibleTrackObstacle.ObstacleType.MetalBarrel, isExplosive);
                        configuredDestructibles++;
                    }
                }
            }

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[InteractiveWorld] Успешно завершено! Канистр активировано: {reactivatedCanisters}, " +
                      $"Физических предметов настроено: {configuredProps}, Тайников: {configuredStashes}, Разрушаемых препятствий: {configuredDestructibles}.");
        }
    }
}
