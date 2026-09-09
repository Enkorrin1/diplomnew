#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RogueDrive.Modifiers;
using RogueDrive.Meta;

namespace RogueDrive.EditorTools
{
    [InitializeOnLoad]
    public static class CarCatalogAssetBinder
    {
        static CarCatalogAssetBinder()
        {
            EditorApplication.delayCall += BindAllCars;
        }

        [MenuItem("RogueDrive/Cars/Bind 3D Car Models to Catalog")]
        public static void BindAllCars()
        {
            BindCar("Assets/Content/Cars/light.asset", "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Classic Car_9.prefab");
            BindCar("Assets/Content/Cars/truck.asset", "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/N Van_10.prefab");
            BindCar("Assets/Content/Cars/suv.asset", "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Pick Up_11.prefab");
            BindCar("Assets/Content/Cars/armored.asset", "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Military Vehicle_3.prefab");

            EnsureSportAndPoliceCars();
            AssetDatabase.SaveAssets();
        }

        static void BindCar(string assetPath, string prefabPath)
        {
            CarDefinition car = AssetDatabase.LoadAssetAtPath<CarDefinition>(assetPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (car != null && prefab != null)
            {
                if (car.Prefab != prefab)
                {
                    car.Prefab = prefab;
                    EditorUtility.SetDirty(car);
                    Debug.Log($"[CarCatalogAssetBinder] Привязана 3D-модель {prefab.name} к авто {car.DisplayName}");
                }
            }
        }

        static void EnsureSportAndPoliceCars()
        {
            GarageCatalog catalog = AssetDatabase.LoadAssetAtPath<GarageCatalog>("Assets/Content/GarageCatalog.asset");
            if (catalog == null) return;

            string sportPath = "Assets/Content/Cars/sport.asset";
            CarDefinition sport = AssetDatabase.LoadAssetAtPath<CarDefinition>(sportPath);
            if (sport == null)
            {
                sport = ScriptableObject.CreateInstance<CarDefinition>();
                sport.Id = "sport";
                sport.DisplayName = "Фантом (Спорткар)";
                sport.Description = "Сверхскоростной гоночный болид с максимальной скоростью и турбо-ускорением.";
                sport.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Sport Car_39.prefab");
                sport.Price = 450;
                sport.BodyColor = new Color(0.95f, 0.15f, 0.15f);
                sport.BaseStats = new[]
                {
                    new StatValue(StatId.Damage, 15f),
                    new StatValue(StatId.FireRate, 3.5f),
                    new StatValue(StatId.MaxHealth, 85f),
                    new StatValue(StatId.FuelCapacity, 90f),
                    new StatValue(StatId.FuelDrain, 1.25f),
                    new StatValue(StatId.Speed, 32f),
                    new StatValue(StatId.Mass, 1050f),
                    new StatValue(StatId.PickupRadius, 4.5f)
                };
                sport.Sockets = new[] { new SocketCapacity { Type = SocketType.Roof, Count = 1 } };
                AssetDatabase.CreateAsset(sport, sportPath);
            }
            else
            {
                sport.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Sport Car_39.prefab");
                EditorUtility.SetDirty(sport);
            }

            string policePath = "Assets/Content/Cars/police.asset";
            CarDefinition police = AssetDatabase.LoadAssetAtPath<CarDefinition>(policePath);
            if (police == null)
            {
                police = ScriptableObject.CreateInstance<CarDefinition>();
                police.Id = "police";
                police.DisplayName = "Шериф (Перехватчик)";
                police.Description = "Полицейский перехватчик с усиленным тараном и скорострельной турелью.";
                police.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Police Car N_4.prefab");
                police.Price = 300;
                police.BodyColor = new Color(0.12f, 0.25f, 0.75f);
                police.BaseStats = new[]
                {
                    new StatValue(StatId.Damage, 14f),
                    new StatValue(StatId.FireRate, 3f),
                    new StatValue(StatId.MaxHealth, 120f),
                    new StatValue(StatId.FuelCapacity, 110f),
                    new StatValue(StatId.FuelDrain, 1.05f),
                    new StatValue(StatId.Speed, 27f),
                    new StatValue(StatId.Mass, 1400f),
                    new StatValue(StatId.PickupRadius, 5f)
                };
                police.Sockets = new[] { new SocketCapacity { Type = SocketType.Roof, Count = 1 } };
                AssetDatabase.CreateAsset(police, policePath);
            }
            else
            {
                police.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Police Car N_4.prefab");
                EditorUtility.SetDirty(police);
            }

            // Добавляем в каталог
            SerializedObject catSo = new SerializedObject(catalog);
            SerializedProperty carsProp = catSo.FindProperty("_cars");
            bool hasSport = false;
            bool hasPolice = false;
            for (int i = 0; i < carsProp.arraySize; i++)
            {
                var elem = carsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (elem == sport) hasSport = true;
                if (elem == police) hasPolice = true;
            }
            if (!hasPolice)
            {
                carsProp.InsertArrayElementAtIndex(carsProp.arraySize);
                carsProp.GetArrayElementAtIndex(carsProp.arraySize - 1).objectReferenceValue = police;
            }
            if (!hasSport)
            {
                carsProp.InsertArrayElementAtIndex(carsProp.arraySize);
                carsProp.GetArrayElementAtIndex(carsProp.arraySize - 1).objectReferenceValue = sport;
            }
            catSo.ApplyModifiedProperties();
        }
    }
}
#endif
