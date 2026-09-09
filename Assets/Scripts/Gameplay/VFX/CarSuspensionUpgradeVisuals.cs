using RogueDrive.Meta;
using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Применяет отдельную прокачку подвески. В гараже поднимает кузов относительно
    /// колёс; в заезде передаёт клиренс контроллеру физики и уменьшает крен.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class CarSuspensionUpgradeVisuals : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float clearancePerLevel = 0.06f;

        Vector3 baseLocalPosition;
        Transform visualBody;
        Vector3 baseVisualBodyPosition;
        bool initialized;

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
            int level = SaveService.GetActiveProgress().Data.GetUpgradeLevel("suspension");
            ApplyLevel(level);
        }

        public void ApplyLevel(int level)
        {
            if (!initialized)
            {
                baseLocalPosition = transform.localPosition;
                visualBody = transform.Find("RealCarModel_3D") ?? transform.Find("VisualBody");
                if (visualBody != null)
                    baseVisualBodyPosition = visualBody.localPosition;
                initialized = true;
            }

            float clearance = Mathf.Max(0, level) * clearancePerLevel;
            CarWheelUpgradeVisuals wheels = GetComponent<CarWheelUpgradeVisuals>();
            ArcadeCarController controller = GetComponent<ArcadeCarController>();

            if (controller != null)
            {
                controller.ApplySuspensionUpgrade(level, clearance);
                if (visualBody != null)
                    visualBody.localPosition = baseVisualBodyPosition + Vector3.up * clearance;
            }
            else
            {
                // Preview-модель целиком поднимается, а WheelMounts компенсируют сдвиг.
                transform.localPosition = baseLocalPosition + Vector3.up * clearance;
                wheels?.SetMountHeightOffset(-clearance);
            }
        }
    }
}
