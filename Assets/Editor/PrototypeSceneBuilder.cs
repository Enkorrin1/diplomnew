#if UNITY_EDITOR
using System.IO;
using RogueDrive.Gameplay;
using RogueDrive.Modifiers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.EditorTools
{
    /// <summary>
    /// Создает полноценную готовую рабочую сцену со всеми подсистемами:
    /// физический автомобиль, 360° авто-турель, префабы врагов, опыт,
    /// экран выбора 1 из 3 бафов и процедурный генератор трассы.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string SceneFolder = "Assets/Scenes";
        const string PrefabFolder = "Assets/Prefabs";
        const string ScenePath = SceneFolder + "/RogueDrivePrototype.unity";

        [MenuItem("RogueDrive/Создать прототип заезда")]
        public static void CreatePrototype()
        {
            EnsureFolders();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.32f);

            // 1. Создаем необходимые базовые префабы
            GameObject projectilePrefab = CreateProjectilePrefab();
            GameObject acidProjectilePrefab = CreateAcidProjectilePrefab();
            GameObject xpGemPrefab = CreateExperienceGemPrefab();
            GameObject coinPrefab = CreateCoinPickupPrefab();
            GameObject walkerPrefab = CreateWalkerPrefab(xpGemPrefab, coinPrefab);
            GameObject runnerPrefab = CreateRunnerPrefab(xpGemPrefab, coinPrefab);
            GameObject brutePrefab = CreateBrutePrefab(xpGemPrefab, coinPrefab);
            GameObject spitterPrefab = CreateSpitterPrefab(xpGemPrefab, acidProjectilePrefab, coinPrefab);

            // 2. Игровой корень
            GameObject gameRoot = new GameObject("RogueDrivePrototype");
            GameplayPool pool = gameRoot.AddComponent<GameplayPool>();
            GameRunController run = gameRoot.AddComponent<GameRunController>();
            RunExperienceManager exp = gameRoot.AddComponent<RunExperienceManager>();
            LevelUpView levelUp = gameRoot.AddComponent<LevelUpView>();

            CreateLight();
            CreateInitialRoad();

            // 3. Автомобиль игрока и турель
            ArcadeCarController car = CreateArcadeCar(run);
            AutoTurret turret = CreateRoofTurret(car, projectilePrefab);
            CreateCamera(car.transform);
            CreateInitialObstacles();

            // 4. Процедурный генератор трассы и врагов
            ProceduralTrackGenerator trackGen = gameRoot.AddComponent<ProceduralTrackGenerator>();
            ConfigureTrackGenerator(trackGen, car.transform, new[] { walkerPrefab, runnerPrefab, brutePrefab, spitterPrefab });

            // 5. Координатор сессии модификаторов
            GameSessionCoordinator coordinator = gameRoot.AddComponent<GameSessionCoordinator>();
            ConfigureSessionCoordinator(coordinator, car, turret, run, exp, levelUp);

            // 6. HUD
            PrototypeHud hud = gameRoot.AddComponent<PrototypeHud>();
            hud.Configure(run, car);
            SerializedObject hudSo = new SerializedObject(hud);
            hudSo.FindProperty("run").objectReferenceValue = run;
            hudSo.FindProperty("car").objectReferenceValue = car;
            hudSo.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"Полноценный прототип заезда успешно создан и сохранён: {ScenePath}");
        }

        static void CreateLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        static void CreateInitialRoad()
        {
            const float roadWidth = 24f;
            const float roadLength = 150f;
            const float roadZ = roadLength * 0.5f;

            GameObject road = CreatePrimitive(PrimitiveType.Cube, "Initial_Road", new Vector3(0f, -0.2f, roadZ), new Vector3(roadWidth, 0.4f, roadLength), new Color(0.13f, 0.14f, 0.18f));
            road.isStatic = true;

            float guardrailX = (roadWidth * 0.5f) + 0.3f;
            GameObject leftWall = CreatePrimitive(PrimitiveType.Cube, "Left_Guardrail", new Vector3(-guardrailX, 0.6f, roadZ), new Vector3(0.35f, 1.2f, roadLength), new Color(0.36f, 0.4f, 0.46f));
            GameObject rightWall = CreatePrimitive(PrimitiveType.Cube, "Right_Guardrail", new Vector3(guardrailX, 0.6f, roadZ), new Vector3(0.35f, 1.2f, roadLength), new Color(0.36f, 0.4f, 0.46f));
            leftWall.isStatic = true;
            rightWall.isStatic = true;
        }

        static ArcadeCarController CreateArcadeCar(GameRunController run)
        {
            GameObject car = new GameObject("Player Car");
            car.transform.position = new Vector3(0f, 0.65f, 5f);

            BoxCollider collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.8f, 0.9f, 3.6f);
            collider.center = new Vector3(0f, 0.45f, 0f);

            PhysicsMaterial carPhysMat = new PhysicsMaterial("ArcadeCarPhysMat")
            {
                dynamicFriction = 0.05f,
                staticFriction = 0.05f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f
            };
            collider.sharedMaterial = carPhysMat;

            Rigidbody body = car.AddComponent<Rigidbody>();
            body.mass = 1200f;
            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            ArcadeCarController controller = car.AddComponent<ArcadeCarController>();
            controller.Configure(run);
            SerializedObject carSo = new SerializedObject(controller);
            carSo.FindProperty("run").objectReferenceValue = run;
            carSo.ApplyModifiedProperties();

            car.AddComponent<CarAuraController>();

            GameObject visualBody = new GameObject("VisualBody");
            visualBody.transform.SetParent(car.transform, false);

            CreateCarPart(visualBody.transform, "Chassis", new Vector3(0f, 0.35f, 0f), new Vector3(1.75f, 0.55f, 3.5f), new Color(0.08f, 0.55f, 0.95f));
            CreateCarPart(visualBody.transform, "Cabin", new Vector3(0f, 0.85f, -0.2f), new Vector3(1.35f, 0.55f, 1.6f), new Color(0.68f, 0.88f, 1f));
            CreateCarPart(visualBody.transform, "FrontBumper", new Vector3(0f, 0.25f, 1.75f), new Vector3(1.82f, 0.35f, 0.25f), new Color(0.2f, 0.2f, 0.25f));

            // Фары и колеса
            CreateCarPart(visualBody.transform, "Headlight_L", new Vector3(-0.65f, 0.35f, 1.78f), new Vector3(0.3f, 0.18f, 0.1f), Color.yellow);
            CreateCarPart(visualBody.transform, "Headlight_R", new Vector3(0.65f, 0.35f, 1.78f), new Vector3(0.3f, 0.18f, 0.1f), Color.yellow);

            Vector3[] wheelOffsets =
            {
                new Vector3(-0.92f, 0.25f, 1.15f),
                new Vector3(0.92f, 0.25f, 1.15f),
                new Vector3(-0.92f, 0.25f, -1.15f),
                new Vector3(0.92f, 0.25f, -1.15f)
            };
            for (int i = 0; i < wheelOffsets.Length; i++)
            {
                CreateCarPart(visualBody.transform, $"Wheel_{i + 1}", wheelOffsets[i], new Vector3(0.22f, 0.55f, 0.55f), new Color(0.1f, 0.1f, 0.12f));
            }

            // Настройка SocketRegistry
            SocketRegistry socketRegistry = car.AddComponent<SocketRegistry>();
            SetupCarSockets(car.transform, socketRegistry);

            return controller;
        }

        static AutoTurret CreateRoofTurret(ArcadeCarController car, GameObject projPrefab)
        {
            Transform socketRoof = car.transform.Find("Socket_Roof");
            GameObject turretObj = new GameObject("AutoTurret_Roof");
            turretObj.transform.SetParent(socketRoof != null ? socketRoof : car.transform, false);
            turretObj.transform.localPosition = Vector3.zero;

            // База турели
            GameObject baseObj = CreatePrimitive(PrimitiveType.Cylinder, "TurretBase", new Vector3(0f, 0.1f, 0f), new Vector3(0.6f, 0.15f, 0.6f), new Color(0.25f, 0.28f, 0.35f));
            Collider baseCol = baseObj.GetComponent<Collider>();
            if (baseCol != null) Object.DestroyImmediate(baseCol);
            baseObj.transform.SetParent(turretObj.transform, false);

            // Поворотная голова
            GameObject swivelObj = CreatePrimitive(PrimitiveType.Cube, "TurretSwivel", new Vector3(0f, 0.28f, 0f), new Vector3(0.45f, 0.3f, 0.55f), new Color(0.85f, 0.35f, 0.15f));
            Collider swivelCol = swivelObj.GetComponent<Collider>();
            if (swivelCol != null) Object.DestroyImmediate(swivelCol);
            swivelObj.transform.SetParent(turretObj.transform, false);

            // 3D Модель штурмовой винтовки на поворотной турели
            GameObject weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Low Poly AR Weapon Pack 1/Prefabs/Weapons/AR_A_1.prefab");
            if (weaponPrefab != null)
            {
                GameObject gunObj = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, swivelObj.transform);
                gunObj.name = "AR_Turret_Gun";
                gunObj.transform.localPosition = new Vector3(0f, 0.05f, 0.15f);
                gunObj.transform.localRotation = Quaternion.identity;
                gunObj.transform.localScale = Vector3.one * 1.6f;
            }
            else
            {
                // Резервный ствол, если ассет не найден
                GameObject barrelObj = CreatePrimitive(PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.28f, 0.45f), new Vector3(0.15f, 0.45f, 0.15f), new Color(0.15f, 0.15f, 0.18f));
                Collider barrelCol = barrelObj.GetComponent<Collider>();
                if (barrelCol != null) Object.DestroyImmediate(barrelCol);
                barrelObj.transform.SetParent(swivelObj.transform, false);
                barrelObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // Точка вылета
            GameObject muzzleObj = new GameObject("Muzzle");
            muzzleObj.transform.SetParent(swivelObj.transform, false);
            muzzleObj.transform.localPosition = weaponPrefab != null ? new Vector3(0f, 0.12f, 1.4f) : new Vector3(0f, 0.28f, 0.72f);

            AutoTurret turret = turretObj.AddComponent<AutoTurret>();

            SerializedObject so = new SerializedObject(turret);
            so.FindProperty("swivelTransform").objectReferenceValue = swivelObj.transform;
            SerializedProperty muzzlesProp = so.FindProperty("muzzleTransforms");
            muzzlesProp.arraySize = 1;
            muzzlesProp.GetArrayElementAtIndex(0).objectReferenceValue = muzzleObj.transform;
            so.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
            so.ApplyModifiedProperties();

            return turret;
        }

        static void SetupCarSockets(Transform carTransform, SocketRegistry registry)
        {
            Transform socketRoof = CreateSocketAnchor(carTransform, "Socket_Roof", new Vector3(0f, 1.18f, -0.2f));
            Transform socketHood = CreateSocketAnchor(carTransform, "Socket_Hood", new Vector3(0f, 0.68f, 1.1f));
            Transform socketBumper = CreateSocketAnchor(carTransform, "Socket_Bumper", new Vector3(0f, 0.32f, 1.85f));
            Transform socketExhaust = CreateSocketAnchor(carTransform, "Socket_Exhaust", new Vector3(0f, 0.2f, -1.82f));
            Transform socketSideL = CreateSocketAnchor(carTransform, "Socket_Side_L", new Vector3(-0.92f, 0.45f, 0f));
            Transform socketSideR = CreateSocketAnchor(carTransform, "Socket_Side_R", new Vector3(0.92f, 0.45f, 0f));

            SerializedObject so = new SerializedObject(registry);
            SerializedProperty slotsProp = so.FindProperty("_slots");

            var socketConfigs = new (SocketType type, Transform anchor)[]
            {
                (SocketType.Roof, socketRoof),
                (SocketType.Hood, socketHood),
                (SocketType.Bumper, socketBumper),
                (SocketType.Exhaust, socketExhaust),
                (SocketType.Side, socketSideL),
                (SocketType.Side, socketSideR)
            };

            slotsProp.arraySize = socketConfigs.Length;
            for (int i = 0; i < socketConfigs.Length; i++)
            {
                SerializedProperty element = slotsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Type").enumValueIndex = (int)socketConfigs[i].type;
                element.FindPropertyRelative("Anchor").objectReferenceValue = socketConfigs[i].anchor;
            }
            so.ApplyModifiedProperties();
        }

        static Transform CreateSocketAnchor(Transform parent, string name, Vector3 localPosition)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            return anchor.transform;
        }

        static void CreateCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = target.position + new Vector3(0f, 6.5f, -10.5f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
            camera.fieldOfView = 60f;

            ArcadeCameraFollow follow = cameraObject.AddComponent<ArcadeCameraFollow>();
            follow.Configure(target);
            SerializedObject so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = target;
            so.ApplyModifiedProperties();
        }

        static void CreateInitialObstacles()
        {
            float[] obstacleX = { -6f, 4f, -1f, 5f, -5f, 2f };
            for (int i = 0; i < obstacleX.Length; i++)
            {
                float z = 30f + i * 20f;
                bool isBarrel = (i % 2 == 0);
                Color color = isBarrel ? new Color(1f, 0.45f, 0.1f) : new Color(0.92f, 0.22f, 0.18f);
                string name = isBarrel ? $"Explosive_Barrel_{i + 1}" : $"Barrier_{i + 1}";
                Vector3 scale = isBarrel ? new Vector3(1.3f, 1.6f, 1.3f) : new Vector3(2.4f, 1.4f, 1.4f);

                GameObject obstacle = CreatePrimitive(PrimitiveType.Cube, name, new Vector3(obstacleX[i], scale.y * 0.5f, z), scale, color);
                BoxCollider collider = obstacle.GetComponent<BoxCollider>();
                collider.isTrigger = true;
                obstacle.AddComponent<TrackObstacle>();
            }
        }

        static void ConfigureTrackGenerator(ProceduralTrackGenerator gen, Transform car, GameObject[] enemies)
        {
            SerializedObject so = new SerializedObject(gen);
            so.FindProperty("targetCar").objectReferenceValue = car;
            SerializedProperty enemyProp = so.FindProperty("enemyPrefabs");
            enemyProp.arraySize = enemies.Length;
            for (int i = 0; i < enemies.Length; i++)
            {
                enemyProp.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            }
            so.ApplyModifiedProperties();
        }

        static void ConfigureSessionCoordinator(GameSessionCoordinator coord, ArcadeCarController car, AutoTurret turret, GameRunController run, RunExperienceManager exp, LevelUpView levelUp)
        {
            SerializedObject so = new SerializedObject(coord);
            so.FindProperty("carController").objectReferenceValue = car;
            so.FindProperty("turret").objectReferenceValue = turret;
            so.FindProperty("runController").objectReferenceValue = run;
            so.FindProperty("experienceManager").objectReferenceValue = exp;
            so.FindProperty("levelUpView").objectReferenceValue = levelUp;

            ModifierCatalog catalog = AssetDatabase.LoadAssetAtPath<ModifierCatalog>("Assets/Content/ModifierCatalog.asset");
            CarDefinition carDef = AssetDatabase.LoadAssetAtPath<CarDefinition>("Assets/Content/Cars/light.asset");
            WeightingConfig weighting = AssetDatabase.LoadAssetAtPath<WeightingConfig>("Assets/Content/Simulation/Weighting_Moderate.asset");

            if (catalog != null) so.FindProperty("modifierCatalog").objectReferenceValue = catalog;
            if (carDef != null) so.FindProperty("carDefinition").objectReferenceValue = carDef;
            if (weighting != null) so.FindProperty("weightingConfig").objectReferenceValue = weighting;

            so.ApplyModifiedProperties();
        }

        // --- Создание базовых префабов ---------------------------------------

        static GameObject CreateProjectilePrefab()
        {
            string path = $"{PrefabFolder}/BulletProjectile.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BulletProjectile";
            go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            SphereCollider sc = go.GetComponent<SphereCollider>();
            sc.isTrigger = true;

            Renderer r = go.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            mat.color = new Color(1f, 0.85f, 0.2f);
            r.sharedMaterial = mat;

            go.AddComponent<Projectile>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject CreateAcidProjectilePrefab()
        {
            string path = $"{PrefabFolder}/AcidProjectile.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AcidProjectile";
            go.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            SphereCollider sc = go.GetComponent<SphereCollider>();
            sc.isTrigger = true;

            Renderer r = go.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            mat.color = new Color(0.2f, 0.95f, 0.1f);
            r.sharedMaterial = mat;

            go.AddComponent<AcidProjectile>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject CreateExperienceGemPrefab()
        {
            string path = $"{PrefabFolder}/ExperienceGem.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ExperienceGem";
            go.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            BoxCollider bc = go.GetComponent<BoxCollider>();
            bc.isTrigger = true;

            Renderer r = go.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            mat.color = new Color(0.15f, 0.95f, 1f);
            r.sharedMaterial = mat;

            go.AddComponent<ExperienceGem>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject CreateWalkerPrefab(GameObject xpGem, GameObject coinPrefab)
        {
            string path = $"{PrefabFolder}/WalkerZombie.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                SetDropsOnEnemy(existing.GetComponent<WalkerZombie>(), xpGem, coinPrefab, 1);
                return existing;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "WalkerZombie";
            go.transform.localScale = new Vector3(0.9f, 1.3f, 0.9f);
            CapsuleCollider cc = go.GetComponent<CapsuleCollider>();
            cc.isTrigger = true;

            Renderer r = go.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.28f, 0.6f, 0.32f);
            r.sharedMaterial = mat;

            WalkerZombie walker = go.AddComponent<WalkerZombie>();
            SetDropsOnEnemy(walker, xpGem, coinPrefab, 1);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject CreateRunnerPrefab(GameObject xpGem, GameObject coinPrefab)
        {
            string path = $"{PrefabFolder}/RunnerMutant.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                SetDropsOnEnemy(existing.GetComponent<RunnerMutant>(), xpGem, coinPrefab, 2);
                return existing;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "RunnerMutant";
            go.transform.localScale = new Vector3(0.75f, 1.1f, 0.75f);
            CapsuleCollider cc = go.GetComponent<CapsuleCollider>();
            cc.isTrigger = true;

            Renderer r = go.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.95f, 0.48f, 0.12f);
            r.sharedMaterial = mat;

            RunnerMutant runner = go.AddComponent<RunnerMutant>();
            SetDropsOnEnemy(runner, xpGem, coinPrefab, 2);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject CreateBrutePrefab(GameObject xpGem, GameObject coinPrefab)
        {
            string path = $"{PrefabFolder}/ArmoredBrute.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                SetDropsOnEnemy(existing.GetComponent<ArmoredBrute>(), xpGem, coinPrefab, 5);
                return existing;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ArmoredBrute";
            go.transform.localScale = new Vector3(1.8f, 2.2f, 1.6f);
            BoxCollider bc = go.GetComponent<BoxCollider>();
            bc.isTrigger = true;

            Renderer r = go.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.55f, 0.15f, 0.18f);
            r.sharedMaterial = mat;

            ArmoredBrute brute = go.AddComponent<ArmoredBrute>();
            SetDropsOnEnemy(brute, xpGem, coinPrefab, 5);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject CreateSpitterPrefab(GameObject xpGem, GameObject acidProj, GameObject coinPrefab)
        {
            string path = $"{PrefabFolder}/AcidSpitter.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                SetDropsOnEnemy(existing.GetComponent<AcidSpitter>(), xpGem, coinPrefab, 3);
                return existing;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "AcidSpitter";
            go.transform.localScale = new Vector3(1.0f, 1.2f, 1.0f);
            CapsuleCollider cc = go.GetComponent<CapsuleCollider>();
            if (cc != null) cc.isTrigger = true;

            Renderer r = go.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.6f, 0.2f, 0.8f);
            r.sharedMaterial = mat;

            AcidSpitter spitter = go.AddComponent<AcidSpitter>();
            SetDropsOnEnemy(spitter, xpGem, coinPrefab, 3);

            SerializedObject so = new SerializedObject(spitter);
            so.FindProperty("acidProjectilePrefab").objectReferenceValue = acidProj;
            so.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject CreateCoinPickupPrefab()
        {
            string path = $"{PrefabFolder}/CoinPickup.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "CoinPickup";
            go.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Collider col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            Renderer r = go.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            mat.color = new Color(1f, 0.85f, 0.1f);
            r.sharedMaterial = mat;

            go.AddComponent<CoinPickup>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void SetDropsOnEnemy(EnemyBase enemy, GameObject gem, GameObject coin, int coinReward)
        {
            if (enemy == null) return;
            SerializedObject so = new SerializedObject(enemy);
            so.FindProperty("xpGemPrefab").objectReferenceValue = gem;
            so.FindProperty("coinPrefab").objectReferenceValue = coin;
            so.FindProperty("coinReward").intValue = coinReward;
            so.ApplyModifiedProperties();
        }

        static GameObject CreatePrimitive(PrimitiveType type, string objectName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject result = GameObject.CreatePrimitive(type);
            result.name = objectName;
            result.transform.position = position;
            result.transform.localScale = scale;

            Renderer renderer = result.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(renderer.sharedMaterial);
                material.color = color;
                renderer.sharedMaterial = material;
            }

            return result;
        }

        static void CreateCarPart(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject part = CreatePrimitive(PrimitiveType.Cube, objectName, Vector3.zero, localScale, color);
            Collider col = part.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
    }
}
#endif
