using RogueDrive.Meta;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Показывает один набор колёс для текущего уровня прокачки. Колёса живут
    /// только под четырьмя WheelMounts, поэтому новый tier никогда не накладывается
    /// на предыдущий или на штатную геометрию модели автомобиля.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class CarWheelUpgradeVisuals : MonoBehaviour
    {
        [SerializeField] private GameObject[] tierPrefabs;
        [SerializeField] private Vector3 wheelScale = Vector3.one * 0.46f;
        [SerializeField] private Vector3 frontLeftPosition = new Vector3(-0.95f, 0.35f, 1.25f);
        [SerializeField] private Vector3 frontRightPosition = new Vector3(0.95f, 0.35f, 1.25f);
        [SerializeField] private Vector3 rearLeftPosition = new Vector3(-0.95f, 0.35f, -1.25f);
        [SerializeField] private Vector3 rearRightPosition = new Vector3(0.95f, 0.35f, -1.25f);

        Transform mountsRoot;
        int appliedLevel = -1;
        readonly Dictionary<Transform, Vector3> nativeWheelBasePositions = new Dictionary<Transform, Vector3>();
        sealed class WheelPose
        {
            public Transform Visual;
            public Quaternion Rotation;
            public Vector3 Center;
            public Vector3 MeshCenter;
            public Vector3 Axle;
            public Vector3 SteeringAxis;
            public bool Front;
            public Vector3 SuspensionOffset;
        }
        readonly List<WheelPose> wheelPoses = new List<WheelPose>();
        float spin;
        float steeringAngle;

        private void LateUpdate()
        {
            var car = GetComponent<ArcadeCarController>();
            if (car == null) return;
            steeringAngle = Mathf.MoveTowards(steeringAngle, car.SteeringAngle, 140f * Time.deltaTime);
            spin = (spin + car.SpeedMps * Time.deltaTime / 0.35f * Mathf.Rad2Deg) % 360f;
            foreach (var pose in wheelPoses)
            {
                if (pose.Visual == null || !pose.Visual.gameObject.activeSelf) continue;
                Quaternion steer = Quaternion.AngleAxis(pose.Front ? steeringAngle : 0f, pose.SteeringAxis);
                Quaternion rotation = steer * Quaternion.AngleAxis(spin, pose.Axle) * pose.Rotation;
                pose.Visual.localRotation = rotation;
                pose.Visual.localPosition = pose.Center + pose.SuspensionOffset - rotation * pose.MeshCenter;
            }
        }

        public int SuspensionWheelCount => wheelPoses.Count;

        public bool GetSuspensionWheel(int index, out Vector3 center, out float radius)
        {
            center = Vector3.zero;
            radius = 0.35f;
            if (index < 0 || index >= wheelPoses.Count) return false;
            var pose = wheelPoses[index];
            if (pose.Visual == null || !pose.Visual.gameObject.activeSelf) return false;
            Transform parent = pose.Visual.parent;
            center = parent.TransformPoint(pose.Center);
            MeshFilter original = parent.GetComponent<MeshFilter>();
            if (original != null && original.sharedMesh != null)
                radius = Mathf.Max(0.1f, parent.TransformVector(Vector3.up * original.sharedMesh.bounds.extents.y).magnitude);
            return true;
        }

        public void SetSuspensionWheelCenter(int index, Vector3 worldCenter)
        {
            var pose = wheelPoses[index];
            if (pose.Visual == null) return;
            pose.SuspensionOffset = pose.Visual.parent.InverseTransformPoint(worldCenter) - pose.Center;
        }

        public void Configure(GameObject[] prefabs)
        {
            tierPrefabs = prefabs;
            RefreshForCurrentProgress();
        }

        private void OnEnable()
        {
            SaveService.OnProgressUpdated += OnProgressUpdated;
            if (GetComponent<ArcadeCarController>() == null)
                RefreshForCurrentProgress();
        }

        private void OnDisable()
        {
            SaveService.OnProgressUpdated -= OnProgressUpdated;
        }

        void OnProgressUpdated(MetaProgress _) => RefreshForCurrentProgress();

        public void RefreshForCurrentProgress()
        {
            int level = SaveService.GetActiveProgress().Data.GetUpgradeLevel("tires");
            ApplyLevel(level);
        }

        /// <summary>
        /// Нужен, когда контроллер заезда заменяет временный кузов выбранным
        /// prefab'ом: заново считывает его реальную колёсную базу.
        /// </summary>
        public void RebuildForCurrentCarModel()
        {
            RemoveGeneratedWheelOverlays();
            wheelPoses.Clear();
            appliedLevel = -1;
            RefreshForCurrentProgress();
        }

        void RemoveGeneratedWheelOverlays()
        {
            if (mountsRoot == null)
                mountsRoot = transform.Find("WheelMounts");
            if (mountsRoot == null)
                return;

            mountsRoot.gameObject.SetActive(false);
            mountsRoot.SetParent(null);
            if (Application.isPlaying)
                Destroy(mountsRoot.gameObject);
            else
                DestroyImmediate(mountsRoot.gameObject);
            mountsRoot = null;
        }

        // CarVisualEnhancer создаёт этот объект только как fallback. При наличии
        // штатных колёс он становится лишним вторым набором на кузове.
        void RemoveLegacyProceduralWheels()
        {
            Transform legacyRoot = transform.Find("Wheels");
            if (legacyRoot == null || legacyRoot.Find("Wheel_FL") == null || legacyRoot.Find("Wheel_FR") == null)
                return;

            legacyRoot.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(legacyRoot.gameObject);
            else
                DestroyImmediate(legacyRoot.gameObject);
        }

        public void ApplyLevel(int upgradeLevel)
        {
            if (tierPrefabs == null || tierPrefabs.Length == 0)
                return;

            int clampedLevel = Mathf.Clamp(upgradeLevel, 0, tierPrefabs.Length - 1);
            if (clampedLevel == appliedLevel && mountsRoot != null)
                return;

            EnsureMounts();
            HideBuiltInWheelRenderers();
            ClearMountedWheels();

            GameObject prefab = tierPrefabs[clampedLevel];
            if (prefab == null)
                return;

            if (TryCreateWheelVisualsOnBuiltInRoots(prefab))
            {
                RemoveGeneratedWheelOverlays();
                RemoveLegacyProceduralWheels();
                appliedLevel = clampedLevel;
                return;
            }

            for (int i = 0; i < mountsRoot.childCount; i++)
            {
                Transform mount = mountsRoot.GetChild(i);
                GameObject wheel = Instantiate(prefab, mount);
                wheel.name = "WheelVisual";
                wheel.transform.localPosition = Vector3.zero;
                // wheel-pack уже экспортирован осью вращения вдоль X (оси колеса).
                // Дополнительный поворот на 90° разворачивал протектор к камере.
                wheel.transform.localRotation = Quaternion.identity;
                wheel.transform.localScale = wheelScale;

                foreach (Collider collider in wheel.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            }

            appliedLevel = clampedLevel;
        }

        /// <summary>Смещает только визуальные колёса, оставляя их на земле при подъёме кузова.</summary>
        public void SetMountHeightOffset(float localYOffset)
        {
            if (SetNativeWheelHeightOffset(localYOffset))
                return;

            EnsureMounts();
            mountsRoot.localPosition = new Vector3(0f, localYOffset, 0f);
        }

        bool SetNativeWheelHeightOffset(float localYOffset)
        {
            bool found = false;
            Transform model = transform.Find("RealCarModel_3D") ?? transform;
            Transform[] candidates = model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                Transform candidate = candidates[i];
                string name = candidate.name.ToUpperInvariant();
                bool isWheel = (name.Contains("TIRE") || name.Contains("WHEEL")) &&
                    (name.Contains("FL") || name.Contains("FR") || name.Contains("RL") || name.Contains("RR") || name.Contains("BL") || name.Contains("BR"));
                if (!isWheel || (mountsRoot != null && candidate.IsChildOf(mountsRoot)))
                    continue;

                if (!nativeWheelBasePositions.ContainsKey(candidate))
                    nativeWheelBasePositions[candidate] = candidate.localPosition;
                candidate.localPosition = nativeWheelBasePositions[candidate] + Vector3.up * localYOffset;
                found = true;
            }
            return found;
        }

        void EnsureMounts()
        {
            if (mountsRoot != null)
                return;

            mountsRoot = transform.Find("WheelMounts");
            if (mountsRoot == null)
            {
                mountsRoot = new GameObject("WheelMounts").transform;
                mountsRoot.SetParent(transform, false);
                if (!TryGetBuiltInWheelPositions(out Vector3 fl, out Vector3 fr, out Vector3 rl, out Vector3 rr))
                {
                    fl = frontLeftPosition;
                    fr = frontRightPosition;
                    rl = rearLeftPosition;
                    rr = rearRightPosition;
                }

                CreateMount("Wheel_FL", fl);
                CreateMount("Wheel_FR", fr);
                CreateMount("Wheel_RL", rl);
                CreateMount("Wheel_RR", rr);
            }
        }

        // У каждого импортированного автомобиля своя колёсная база. Используем
        // штатные шины только как точки привязки, затем скрываем их рендеры.
        bool TryGetBuiltInWheelPositions(out Vector3 fl, out Vector3 fr, out Vector3 rl, out Vector3 rr)
        {
            fl = fr = rl = rr = Vector3.zero;
            bool foundFl = false;
            bool foundFr = false;
            bool foundRl = false;
            bool foundRr = false;

            Transform model = transform.Find("RealCarModel_3D") ?? transform;
            Transform[] candidates = model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                Transform candidate = candidates[i];
                if (candidate == transform || candidate.IsChildOf(mountsRoot))
                    continue;

                string name = candidate.name.ToUpperInvariant();
                if (!name.Contains("TIRE") && !name.Contains("WHEEL"))
                    continue;

                Vector3 position = transform.InverseTransformPoint(candidate.position);
                if (!foundFl && (name.Contains("FL") || name.Contains("FRONT_L"))) { fl = position; foundFl = true; }
                else if (!foundFr && (name.Contains("FR") || name.Contains("FRONT_R"))) { fr = position; foundFr = true; }
                else if (!foundRl && (name.Contains("RL") || name.Contains("BL") || name.Contains("REAR_L") || name.Contains("BACK_L"))) { rl = position; foundRl = true; }
                else if (!foundRr && (name.Contains("RR") || name.Contains("BR") || name.Contains("REAR_R") || name.Contains("BACK_R"))) { rr = position; foundRr = true; }
            }

            return foundFl && foundFr && foundRl && foundRr;
        }

        bool TryCreateWheelVisualsOnBuiltInRoots(GameObject sourcePrefab)
        {
            MeshFilter sourceFilter = sourcePrefab.GetComponentInChildren<MeshFilter>(true);
            Renderer sourceRenderer = sourcePrefab.GetComponentInChildren<Renderer>(true);
            if (sourceFilter == null || sourceFilter.sharedMesh == null || sourceRenderer == null)
                return false;

            Transform fl = null;
            Transform fr = null;
            Transform rl = null;
            Transform rr = null;
            Transform model = transform.Find("RealCarModel_3D") ?? transform;
            Transform[] candidates = model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                Transform candidate = candidates[i];
                if (candidate == transform || (mountsRoot != null && candidate.IsChildOf(mountsRoot)))
                    continue;

                string name = candidate.name.ToUpperInvariant();
                if (!name.Contains("TIRE") && !name.Contains("WHEEL"))
                    continue;

                if (fl == null && (name.Contains("FL") || name.Contains("FRONT_L"))) fl = candidate;
                else if (fr == null && (name.Contains("FR") || name.Contains("FRONT_R"))) fr = candidate;
                else if (rl == null && (name.Contains("RL") || name.Contains("BL") || name.Contains("REAR_L") || name.Contains("BACK_L"))) rl = candidate;
                else if (rr == null && (name.Contains("RR") || name.Contains("BR") || name.Contains("REAR_R") || name.Contains("BACK_R"))) rr = candidate;
            }

            if (fl == null || fr == null || rl == null || rr == null)
                return false;

            return CreateFittedWheelVisual(fl, sourcePrefab, sourceFilter, sourceRenderer) &&
                   CreateFittedWheelVisual(fr, sourcePrefab, sourceFilter, sourceRenderer) &&
                   CreateFittedWheelVisual(rl, sourcePrefab, sourceFilter, sourceRenderer) &&
                   CreateFittedWheelVisual(rr, sourcePrefab, sourceFilter, sourceRenderer);
        }

        bool CreateFittedWheelVisual(Transform wheelRoot, GameObject sourcePrefab, MeshFilter sourceFilter, Renderer sourceRenderer)
        {
            MeshFilter targetFilter = wheelRoot.GetComponentInChildren<MeshFilter>(true);
            Renderer targetRenderer = wheelRoot.GetComponentInChildren<Renderer>(true);
            if (targetFilter == null || targetRenderer == null)
                return false;

            Transform oldVisual = targetFilter.transform.Find("TierWheelVisual");
            if (oldVisual != null)
            {
                oldVisual.gameObject.SetActive(false);
                Destroy(oldVisual.gameObject);
            }

            Quaternion rotation = FindBestWheelRotation(sourceFilter.sharedMesh.bounds.size, targetFilter.sharedMesh.bounds.size);
            if (wheelRoot.localPosition.x < 0f)
                rotation = Quaternion.Euler(0f, 180f, 0f) * rotation;
            Vector3 rotatedSize = RotatedBoundsSize(sourceFilter.sharedMesh.bounds.size, rotation);
            Vector3 targetSize = targetFilter.sharedMesh.bounds.size;
            Vector3 scale = new Vector3(
                SafeRatio(targetSize.x, rotatedSize.x),
                SafeRatio(targetSize.y, rotatedSize.y),
                SafeRatio(targetSize.z, rotatedSize.z));
            // Unity applies local scale before rotation. Convert target-axis
            // scale back into the source mesh's axes.
            scale = RotatedBoundsSize(scale, Quaternion.Inverse(rotation));

            GameObject visual = Instantiate(sourcePrefab, targetFilter.transform);
            visual.name = "TierWheelVisual";
            visual.transform.localRotation = rotation;
            visual.transform.localScale = scale;
            visual.transform.localPosition = targetFilter.sharedMesh.bounds.center -
                rotation * Vector3.Scale(sourceFilter.sharedMesh.bounds.center, scale);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            targetRenderer.enabled = false;
            wheelPoses.RemoveAll(p => p.Visual == null || !p.Visual.gameObject.activeSelf);
            wheelPoses.Add(new WheelPose
            {
                Visual = visual.transform,
                Rotation = rotation,
                Center = targetFilter.sharedMesh.bounds.center,
                MeshCenter = Vector3.Scale(sourceFilter.sharedMesh.bounds.center, scale),
                Axle = targetFilter.transform.InverseTransformDirection(transform.right).normalized,
                SteeringAxis = targetFilter.transform.InverseTransformDirection(transform.up).normalized,
                Front = wheelRoot.name.ToUpperInvariant().Contains("FL") ||
                        wheelRoot.name.ToUpperInvariant().Contains("FR")
            });
            return true;
        }

        static float SafeRatio(float target, float source) => source > 0.0001f ? Mathf.Clamp(target / source, 0.03f, 15f) : 1f;

        static Quaternion FindBestWheelRotation(Vector3 sourceSize, Vector3 targetSize)
        {
            Quaternion[] candidates =
            {
                Quaternion.identity,
                Quaternion.Euler(0f, 0f, 90f),
                Quaternion.Euler(0f, 90f, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                Quaternion.Euler(90f, 90f, 0f),
                Quaternion.Euler(90f, 0f, 90f)
            };
            Quaternion best = Quaternion.identity;
            float bestScore = float.MaxValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                Vector3 size = RotatedBoundsSize(sourceSize, candidates[i]);
                float sx = SafeRatio(targetSize.x, size.x);
                float sy = SafeRatio(targetSize.y, size.y);
                float sz = SafeRatio(targetSize.z, size.z);
                float score = Mathf.Abs(sx - sy) + Mathf.Abs(sy - sz) + Mathf.Abs(sz - sx);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidates[i];
                }
            }
            return best;
        }

        static Vector3 RotatedBoundsSize(Vector3 size, Quaternion rotation)
        {
            Vector3 x = rotation * Vector3.right * size.x;
            Vector3 y = rotation * Vector3.up * size.y;
            Vector3 z = rotation * Vector3.forward * size.z;
            return new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        }

        void CreateMount(string mountName, Vector3 localPosition)
        {
            Transform mount = new GameObject(mountName).transform;
            mount.SetParent(mountsRoot, false);
            mount.localPosition = localPosition;
        }

        void ClearMountedWheels()
        {
            for (int i = 0; i < mountsRoot.childCount; i++)
            {
                Transform mount = mountsRoot.GetChild(i);
                for (int childIndex = mount.childCount - 1; childIndex >= 0; childIndex--)
                {
                    GameObject previousWheel = mount.GetChild(childIndex).gameObject;
                    // Destroy выполняется в конце кадра, поэтому скрываем прежний tier сразу.
                    previousWheel.SetActive(false);
                    Destroy(previousWheel);
                }
            }
        }

        void HideBuiltInWheelRenderers()
        {
            // У импортированных моделей MeshRenderer обычно лежит глубже объекта
            // "... Tire". Отключаем все рендеры этого подграфа, а не только
            // рендер с подходящим именем — штатный диск не останется под новым.
            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform wheelRoot = allTransforms[i];
                if (wheelRoot == transform || wheelRoot.IsChildOf(mountsRoot))
                    continue;

                string objectName = wheelRoot.name.ToUpperInvariant();
                bool looksLikeWheel = objectName.Contains("TIRE") ||
                                      (objectName.Contains("WHEEL") && objectName != "WHEELS");
                if (!looksLikeWheel)
                    continue;

                Renderer[] wheelRenderers = wheelRoot.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < wheelRenderers.Length; rendererIndex++)
                {
                    Renderer renderer = wheelRenderers[rendererIndex];
                    if (!renderer.transform.IsChildOf(mountsRoot))
                        renderer.enabled = false;
                }
            }
        }
    }
}
