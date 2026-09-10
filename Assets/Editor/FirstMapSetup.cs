#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using RogueDrive.Gameplay;

namespace RogueDrive.EditorTools
{
    [InitializeOnLoad]
    public sealed class FirstMapSetup : IPreprocessBuildWithReport
    {
        const string Path = "Assets/Resources/FirstMapAssets.asset";
        public int callbackOrder => 0;

        static FirstMapSetup() => EditorApplication.delayCall += EnsureAssets;

        static void EnsureAssets()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && AssetDatabase.LoadAssetAtPath<FirstMapAssets>(Path) == null) Build();
        }

        public void OnPreprocessBuild(BuildReport report) => EnsureAssets();

        [MenuItem("RogueDrive/First Map/Bind Existing Art")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var data = AssetDatabase.LoadAssetAtPath<FirstMapAssets>(Path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<FirstMapAssets>();
                AssetDatabase.CreateAsset(data, Path);
            }
            data.garage = Load("Assets/Cartoon Buildings/Prefabs/Garage.prefab");
            data.gasStation = Load("Assets/Cartoon Buildings/Prefabs/Gas_station.prefab");
            data.station = Load("Assets/Cartoon Buildings/Prefabs/Station.prefab");
            data.container = Load("Assets/FREE Low Poly Shipping Container/Prefabs/Low Poly Shipping Container.prefab");
            data.lamp = Load("Assets/FastMesh/Prefabs/Road/LampPost-1.prefab");
            data.sign = Load("Assets/FastMesh/Prefabs/Road/DirectionalSign.prefab");
            data.cone = Load("Assets/FastMesh/Prefabs/Road/TrafficCones.prefab");
            data.barrier = Load("Assets/FastMesh/Prefabs/Road/RoadBarrier-1.prefab");
            const string cars = "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/";
            data.sedan = Load(cars + "Hatchback Car_15.prefab");
            data.van = Load(cars + "N Van_10.prefab");
            data.police = Load(cars + "Police Car N_4.prefab");
            data.military = Load(cars + "Military Vehicle_3.prefab");
            data.pallet = Load("Assets/GarageAssetPack/Prefabs/WoodenPallet.prefab");
            data.barrel = Load("Assets/GarageAssetPack/Prefabs/Barrelfbx.prefab");
            data.tent = Load("Assets/Prefabs/LowPolyTent.prefab");
            data.rocks = new GameObject[5];
            for (int i = 0; i < 5; i++) data.rocks[i] = Load($"Assets/Low Poly Stones/Prefabs/ST_Stone{i + 1}.prefab");
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("First map: 20 existing art assets bound for Editor and player builds.");
        }

        static GameObject Load(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new BuildFailedException("Missing first-map asset: " + path);
            return prefab;
        }
    }
}
#endif
