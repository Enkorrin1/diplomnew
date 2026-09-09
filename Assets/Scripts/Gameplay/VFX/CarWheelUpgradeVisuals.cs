using RogueDrive.Meta;
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

        public void Configure(GameObject[] prefabs)
        {
            tierPrefabs = prefabs;
            RefreshForCurrentProgress();
        }

        private void OnEnable()
        {
            SaveService.OnProgressUpdated += OnProgressUpdated;
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

            for (int i = 0; i < mountsRoot.childCount; i++)
            {
                Transform mount = mountsRoot.GetChild(i);
                GameObject wheel = Instantiate(prefab, mount);
                wheel.name = "WheelVisual";
                wheel.transform.localPosition = Vector3.zero;
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = wheelScale;

                foreach (Collider collider in wheel.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            }

            appliedLevel = clampedLevel;
        }

        /// <summary>Смещает только визуальные колёса, оставляя их на земле при подъёме кузова.</summary>
        public void SetMountHeightOffset(float localYOffset)
        {
            EnsureMounts();
            mountsRoot.localPosition = new Vector3(0f, localYOffset, 0f);
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

            Transform[] candidates = GetComponentsInChildren<Transform>(true);
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
