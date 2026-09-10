#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RogueDrive.Gameplay;

namespace RogueDrive.EditorTools
{
    [InitializeOnLoad]
    public static class Enemy3DAssetBinder
    {
        static Enemy3DAssetBinder()
        {
            // Rebinding is an explicit menu action: preserve authored prefab edits.
        }

        [MenuItem("RogueDrive/Enemies/Bind 3D Enemy Models to Prefabs")]
        public static void BindAllEnemies()
        {
            BindEnemy(
                "Assets/Prefabs/WalkerZombie.prefab",
                "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Zombie.prefab",
                "Assets/AlexMakes3D/Polygon style/Halloween pack/Props/Prefabs/Kitchen cleaver.prefab",
                new Vector3(0f, -0.5f, 0f),
                Vector3.one,
                1.8f, 0.45f
            );

            BindEnemy(
                "Assets/Prefabs/RunnerMutant.prefab",
                "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Evil_Clown.prefab",
                "Assets/AlexMakes3D/Polygon style/Halloween pack/Props/Prefabs/Сlown hammer.prefab",
                new Vector3(0f, -0.5f, 0f),
                new Vector3(1.05f, 1.05f, 1.05f),
                1.7f, 0.42f
            );

            BindEnemy(
                "Assets/Prefabs/ArmoredBrute.prefab",
                "Assets/AlexMakes3D/Polygon style/Halloween pack/Characters/Prefabs/Pumpkinhead.prefab",
                "Assets/AlexMakes3D/Polygon style/Halloween pack/Props/Prefabs/Pitchfork.prefab",
                new Vector3(0f, -0.65f, 0f),
                new Vector3(1.35f, 1.35f, 1.35f),
                2.2f, 0.65f
            );

            BindEnemy(
                "Assets/Prefabs/AcidSpitter.prefab",
                "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_Pacient_04.prefab",
                null,
                new Vector3(0f, -0.5f, 0f),
                Vector3.one,
                1.8f, 0.45f
            );

            AssetDatabase.SaveAssets();
        }

        static void BindEnemy(string prefabPath, string modelPath, string weaponPath, Vector3 offset, Vector3 scale, float colHeight, float colRadius)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject weaponPrefab = !string.IsNullOrEmpty(weaponPath) ? AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath) : null;

            EnemyBase enemy = prefab.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                SerializedObject so = new SerializedObject(enemy);
                SerializedProperty modelProp = so.FindProperty("visualModelPrefab");
                SerializedProperty offsetProp = so.FindProperty("visualModelOffset");
                SerializedProperty scaleProp = so.FindProperty("visualModelScale");
                SerializedProperty weaponProp = so.FindProperty("weaponPropPrefab");

                if (modelProp != null && modelPrefab != null) modelProp.objectReferenceValue = modelPrefab;
                if (offsetProp != null) offsetProp.vector3Value = offset;
                if (scaleProp != null) scaleProp.vector3Value = scale;
                if (weaponProp != null && weaponPrefab != null) weaponProp.objectReferenceValue = weaponPrefab;

                so.ApplyModifiedProperties();
            }

            // Настраиваем коллайдер
            CapsuleCollider cc = prefab.GetComponent<CapsuleCollider>();
            if (cc != null)
            {
                SerializedObject ccSo = new SerializedObject(cc);
                ccSo.FindProperty("m_Height").floatValue = colHeight;
                ccSo.FindProperty("m_Radius").floatValue = colRadius;
                ccSo.FindProperty("m_Center").vector3Value = new Vector3(0f, colHeight * 0.5f - 0.5f, 0f);
                ccSo.ApplyModifiedProperties();
            }

            // Отключаем примитивный MeshRenderer в префабе
            MeshRenderer mr = prefab.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                SerializedObject mrSo = new SerializedObject(mr);
                mrSo.FindProperty("m_Enabled").boolValue = false;
                mrSo.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(prefab);
            Debug.Log($"[Enemy3DAssetBinder] Привязана 3D-модель {(modelPrefab != null ? modelPrefab.name : "null")} к врагу {prefab.name}");
        }
    }
}
#endif
