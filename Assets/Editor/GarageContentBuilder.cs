#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using RogueDrive.Meta;
using RogueDrive.Modifiers;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    public static class GarageContentBuilder
    {
        const string CarsDir = "Assets/Content/Cars";
        const string UpgradesDir = "Assets/Content/Upgrades";
        const string CatalogPath = "Assets/Content/GarageCatalog.asset";

        [MenuItem("RogueDrive/Настроить автопарк и улучшения (Гараж)")]
        public static void BuildAllGarageContent()
        {
            EnsureDirectories();

            List<CarDefinition> cars = BuildCars();
            List<UpgradeTrack> upgrades = BuildUpgrades();

            GarageCatalog catalog = AssetDatabase.LoadAssetAtPath<GarageCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GarageCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.EditorSetData(cars, upgrades, LoadWheelUpgradePrefabs());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GarageContentBuilder] Успешно настроено: {cars.Count} автомобилей, {upgrades.Count} веток прокачки. Каталог: {CatalogPath}");
        }

        static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Content"))
                AssetDatabase.CreateFolder("Assets", "Content");
            if (!AssetDatabase.IsValidFolder(CarsDir))
                AssetDatabase.CreateFolder("Assets/Content", "Cars");
            if (!AssetDatabase.IsValidFolder(UpgradesDir))
                AssetDatabase.CreateFolder("Assets/Content", "Upgrades");
        }

        static List<CarDefinition> BuildCars()
        {
            var list = new List<CarDefinition>();

            // 1. Седан «Скиталец» (Легковая)
            list.Add(CreateOrUpdateCar("light", "Скиталец (Седан)",
                "Стартовый сбалансированный автомобиль выживших. Отличная управляемость и маневренность.",
                0, new Color(0.12f, 0.58f, 0.95f),
                new[]
                {
                    new StatValue(StatId.Damage, 10f),
                    new StatValue(StatId.FireRate, 2f),
                    new StatValue(StatId.MaxHealth, 100f),
                    new StatValue(StatId.FuelCapacity, 100f),
                    new StatValue(StatId.FuelDrain, 1f),
                    new StatValue(StatId.Speed, 24f),
                    new StatValue(StatId.Mass, 1200f),
                    new StatValue(StatId.PickupRadius, 4.5f)
                },
                new[]
                {
                    new SocketCapacity { Type = SocketType.Roof, Count = 1 },
                    new SocketCapacity { Type = SocketType.Hood, Count = 1 }
                }));

            // 2. Фургон «Транзит»
            list.Add(CreateOrUpdateCar("truck", "Транзит (Фургон)",
                "Вместительный экспедиционный фургон с гигантским топливным баком и усиленным каркасом.",
                150, new Color(0.92f, 0.55f, 0.15f),
                new[]
                {
                    new StatValue(StatId.Damage, 12f),
                    new StatValue(StatId.FireRate, 2f),
                    new StatValue(StatId.MaxHealth, 160f),
                    new StatValue(StatId.FuelCapacity, 170f),
                    new StatValue(StatId.FuelDrain, 1.15f),
                    new StatValue(StatId.Speed, 20f),
                    new StatValue(StatId.Mass, 1800f),
                    new StatValue(StatId.PickupRadius, 5f)
                },
                new[]
                {
                    new SocketCapacity { Type = SocketType.Roof, Count = 1 },
                    new SocketCapacity { Type = SocketType.Hood, Count = 1 },
                    new SocketCapacity { Type = SocketType.Side, Count = 2 }
                }));

            // 3. Внедорожник «Мародёр»
            list.Add(CreateOrUpdateCar("suv", "Мародёр (Джип)",
                "Тяжелый постапокалиптический внедорожник со стальным кенгурятником для сокрушительного тарана.",
                400, new Color(0.35f, 0.72f, 0.3f),
                new[]
                {
                    new StatValue(StatId.Damage, 15f),
                    new StatValue(StatId.FireRate, 2.2f),
                    new StatValue(StatId.MaxHealth, 200f),
                    new StatValue(StatId.FuelCapacity, 140f),
                    new StatValue(StatId.FuelDrain, 1.3f),
                    new StatValue(StatId.Speed, 22f),
                    new StatValue(StatId.Mass, 2400f),
                    new StatValue(StatId.PickupRadius, 5.5f)
                },
                new[]
                {
                    new SocketCapacity { Type = SocketType.Roof, Count = 1 },
                    new SocketCapacity { Type = SocketType.Hood, Count = 1 },
                    new SocketCapacity { Type = SocketType.Bumper, Count = 1 },
                    new SocketCapacity { Type = SocketType.Side, Count = 2 }
                }));

            // 4. Броневик «Бастион»
            list.Add(CreateOrUpdateCar("armored", "Бастион (Броневик)",
                "Сверхтяжелая военная бронемашина. 5 сокетов вооружения, колоссальная броня и неудержимая масса.",
                900, new Color(0.48f, 0.45f, 0.52f),
                new[]
                {
                    new StatValue(StatId.Damage, 18f),
                    new StatValue(StatId.FireRate, 2.5f),
                    new StatValue(StatId.MaxHealth, 300f),
                    new StatValue(StatId.FuelCapacity, 200f),
                    new StatValue(StatId.FuelDrain, 1.5f),
                    new StatValue(StatId.Speed, 18f),
                    new StatValue(StatId.Mass, 3500f),
                    new StatValue(StatId.PickupRadius, 6f)
                },
                new[]
                {
                    new SocketCapacity { Type = SocketType.Roof, Count = 2 },
                    new SocketCapacity { Type = SocketType.Hood, Count = 1 },
                    new SocketCapacity { Type = SocketType.Bumper, Count = 1 },
                    new SocketCapacity { Type = SocketType.Side, Count = 2 },
                    new SocketCapacity { Type = SocketType.Exhaust, Count = 1 }
                }));

            return list;
        }

        static CarDefinition CreateOrUpdateCar(string id, string name, string desc, int price, Color bodyColor, StatValue[] stats, SocketCapacity[] sockets)
        {
            string path = $"{CarsDir}/{id}.asset";
            CarDefinition car = AssetDatabase.LoadAssetAtPath<CarDefinition>(path);
            if (car == null)
            {
                car = ScriptableObject.CreateInstance<CarDefinition>();
                AssetDatabase.CreateAsset(car, path);
            }

            car.Id = id;
            car.DisplayName = name;
            car.Description = desc;
            car.Price = price;
            car.BodyColor = bodyColor;
            car.BaseStats = stats;
            car.Sockets = sockets;

            EditorUtility.SetDirty(car);
            return car;
        }

        static List<UpgradeTrack> BuildUpgrades()
        {
            var list = new List<UpgradeTrack>();

            // 1. Двигатель
            list.Add(CreateOrUpdateUpgrade("engine", "Двигатель",
                "Увеличивает максимальную скорость и приёмистость автомобиля.",
                StatId.Speed, 2.5f, 5, 50, 1.4f));

            // 2. Трансмиссия / КПП
            list.Add(CreateOrUpdateUpgrade("transmission", "Трансмиссия",
                "Оптимизирует крутящий момент и увеличивает скорострельность турелей.",
                StatId.FireRate, 0.4f, 5, 60, 1.45f));

            // 3. Топливный бак
            list.Add(CreateOrUpdateUpgrade("tank", "Топливный бак",
                "Увеличивает объем запаса бензина, позволяя уехать дальше за один заезд.",
                StatId.FuelCapacity, 25f, 5, 40, 1.35f));

            // 4. Броня кузова
            list.Add(CreateOrUpdateUpgrade("hull", "Броня кузова",
                "Повышает максимальный запас прочности (HP) и устойчивость к столкновениям.",
                StatId.MaxHealth, 30f, 5, 50, 1.4f));

            // 5. Шины: сцепление и комплект дисков на модели автомобиля.
            list.Add(CreateOrUpdateUpgrade("tires", "Колёса",
                "Новый протектор повышает сцепление с дорогой и меняет комплект колёс.",
                StatId.Grip, 0.018f, 5, 35, 1.35f));

            // 6. Подвеска: дорожный просвет и контроль кузова.
            list.Add(CreateOrUpdateUpgrade("suspension", "Подвеска",
                "Поднимает клиренс и уменьшает раскачку кузова на неровностях.",
                StatId.Suspension, 0.06f, 5, 45, 1.38f));

            // 7. Орудийная турель
            list.Add(CreateOrUpdateUpgrade("armament", "Оружейный узел",
                "Модернизирует калибр и кинетическую энергию снарядов авто-турели.",
                StatId.Damage, 5f, 5, 75, 1.5f));

            return list;
        }

        static UpgradeTrack CreateOrUpdateUpgrade(string id, string name, string desc, StatId target, float valPerLvl, int maxLvl, int baseCost, float growth)
        {
            string path = $"{UpgradesDir}/{id}.asset";
            UpgradeTrack track = AssetDatabase.LoadAssetAtPath<UpgradeTrack>(path);
            if (track == null)
            {
                track = ScriptableObject.CreateInstance<UpgradeTrack>();
                AssetDatabase.CreateAsset(track, path);
            }

            track.Id = id;
            track.DisplayName = name;
            track.Description = desc;
            track.Target = target;
            track.ValuePerLevel = valPerLvl;
            track.MaxLevel = maxLvl;
            track.BaseCost = baseCost;
            track.CostGrowth = growth;

            EditorUtility.SetDirty(track);
            return track;
        }

        static GameObject[] LoadWheelUpgradePrefabs()
        {
            string[] paths =
            {
                "Assets/wheel/Prefabs/wheel_01.prefab",
                "Assets/wheel/Prefabs/wheel_03.prefab",
                "Assets/wheel/Prefabs/wheel_05.prefab",
                "Assets/wheel/Prefabs/wheel_07.prefab",
                "Assets/wheel/Prefabs/wheel_09.prefab",
                "Assets/wheel/Prefabs/wheel_12.prefab"
            };

            GameObject[] prefabs = new GameObject[paths.Length];
            for (int i = 0; i < paths.Length; i++)
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            return prefabs;
        }
    }
}
#endif
