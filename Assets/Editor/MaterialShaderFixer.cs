#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    [InitializeOnLoad]
    public static class MaterialShaderFixer
    {
        static MaterialShaderFixer()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                FixAllMaterials();
                FixSceneCarAndTurret();
            };
        }

        [MenuItem("RogueDrive/Graphics/Fix All Material Shaders")]
        public static void FixAllMaterials()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Shader standardShader = Shader.Find("Standard");
            if (standardShader == null) return;

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int fixedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                bool isBroken = mat.shader == null 
                             || mat.shader.name == "Hidden/InternalErrorShader" 
                             || mat.shader.name.Contains("Error") 
                             || mat.shader.name.StartsWith("Universal Render Pipeline");

                if (isBroken)
                {
                    Texture baseTex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                    if (baseTex == null && mat.HasProperty("_MainTex"))
                    {
                        baseTex = mat.GetTexture("_MainTex");
                    }

                    Color baseColor = Color.white;
                    if (mat.HasProperty("_BaseColor"))
                    {
                        baseColor = mat.GetColor("_BaseColor");
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        baseColor = mat.GetColor("_Color");
                    }

                    mat.shader = standardShader;

                    if (baseTex != null)
                    {
                        mat.mainTexture = baseTex;
                    }
                    mat.color = baseColor;

                    EditorUtility.SetDirty(mat);
                    fixedCount++;
                    Debug.Log($"[MaterialShaderFixer] Исправлен фиолетовый материал: {mat.name} ({path}) на Standard");
                }
            }

            if (fixedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[MaterialShaderFixer] Успешно исправлено {fixedCount} фиолетовых материалов.");
            }
        }

        [MenuItem("RogueDrive/Graphics/Fix Scene Car and Turret Models")]
        public static void FixSceneCarAndTurret()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("[MaterialShaderFixer] Пропуск изменения сцены во время Play Mode.");
                return;
            }

            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            string prototypePath = "Assets/Scenes/Stage1_Outskirts.unity";

            if (activeScene.path != prototypePath)
            {
                if (System.IO.File.Exists(prototypePath))
                {
                    activeScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(prototypePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                }
            }

            GameObject playerCar = GameObject.Find("Player Car");
            if (playerCar == null) return;

            bool modified = false;

            // 1. Обновляем визуальную модель машинки на 3D Classic Car_9
            Transform visualBody = playerCar.transform.Find("VisualBody");
            if (visualBody != null)
            {
                string[] primitives = { "Chassis", "Cabin", "FrontBumper", "Headlight_L", "Headlight_R", "Wheel_1", "Wheel_2", "Wheel_3", "Wheel_4" };
                foreach (string prim in primitives)
                {
                    Transform t = visualBody.Find(prim);
                    if (t != null)
                    {
                        Object.DestroyImmediate(t.gameObject);
                        modified = true;
                    }
                }

                Transform existing3D = visualBody.Find("Classic Car_9") ?? visualBody.Find("RealCarModel_3D");
                if (existing3D == null)
                {
                    GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Classic Car_9.prefab");
                    if (carPrefab != null)
                    {
                        GameObject carInstance = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, visualBody);
                        carInstance.name = "Classic Car_9";
                        carInstance.transform.localPosition = Vector3.zero;
                        carInstance.transform.localRotation = Quaternion.identity;
                        modified = true;
                    }
                }
            }

            // 2. Исправляем турель на крыше
            Transform socketRoof = playerCar.transform.Find("Socket_Roof");
            if (socketRoof != null)
            {
                socketRoof.localPosition = new Vector3(0f, 1.25f, -0.1f);
                Transform turret = socketRoof.Find("AutoTurret_Roof");
                if (turret != null)
                {
                    Transform baseT = turret.Find("TurretBase");
                    if (baseT != null)
                    {
                        baseT.localScale = new Vector3(0.35f, 0.04f, 0.35f);
                        baseT.localPosition = new Vector3(0f, 0.02f, 0f);
                        modified = true;
                    }

                    Transform swivel = turret.Find("TurretSwivel");
                    if (swivel != null)
                    {
                        MeshRenderer mr = swivel.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            Object.DestroyImmediate(mr);
                            modified = true;
                        }
                        MeshFilter mf = swivel.GetComponent<MeshFilter>();
                        if (mf != null)
                        {
                            Object.DestroyImmediate(mf);
                            modified = true;
                        }

                        if (swivel.Find("AR_Turret_Gun") == null)
                        {
                            GameObject gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Low Poly AR Weapon Pack 1/Prefabs/Weapons/AR_A_1.prefab");
                            if (gunPrefab != null)
                            {
                                GameObject gun = (GameObject)PrefabUtility.InstantiatePrefab(gunPrefab, swivel);
                                gun.name = "AR_Turret_Gun";
                                gun.transform.localPosition = new Vector3(0f, 0.05f, 0.15f);
                                gun.transform.localRotation = Quaternion.identity;
                                gun.transform.localScale = Vector3.one * 1.6f;
                                Collider[] cols = gun.GetComponentsInChildren<Collider>(true);
                                for (int i = 0; i < cols.Length; i++) Object.DestroyImmediate(cols[i]);
                                modified = true;
                            }
                        }
                    }
                }
            }

            // Отложенный вызов может попасть в момент переключения в Play Mode.
            // Повторная проверка предотвращает попытку сохранить runtime-сцену.
            if (modified && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
                Debug.Log("[MaterialShaderFixer] Сцена RogueDrivePrototype успешно обновлена: установлена 3D модель Classic Car_9 и скрыты примитивы!");
            }
        }
    }
}
#endif
