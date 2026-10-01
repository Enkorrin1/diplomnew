using UnityEngine;

namespace RogueDrive.Gameplay.Track
{
    /// <summary>
    /// Зеркало заднего вида: камера за машиной рендерит в текстуру, панель зеркала в UI.
    /// Камера и панель лежат в сцене; рендер раз в несколько кадров и без теней.
    /// </summary>
    public sealed class StormRearMirror : MonoBehaviour
    {
        [SerializeField] Camera mirrorCamera;
        [SerializeField] GameObject mirrorPanel;
        [SerializeField] Vector3 offset = new Vector3(0, 1.45f, -2.4f);
        [SerializeField] Vector3 lookEuler = new Vector3(-5, 180, 0);
        [SerializeField, Range(1, 4)] int renderEveryNthFrame = 2;

        ArcadeCarController car;
        GameRunController run;
        Hub.GaragePlayerController pedestrian;
        float nextSearch;

        void Awake() { mirrorCamera.enabled = false; mirrorPanel.SetActive(false); }

        void LateUpdate()
        {
            if ((car == null || run == null || pedestrian == null) && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                if (car == null) car = FindFirstObjectByType<ArcadeCarController>();
                if (run == null) run = FindFirstObjectByType<GameRunController>();
                if (pedestrian == null) pedestrian = FindFirstObjectByType<Hub.GaragePlayerController>(FindObjectsInactive.Include);
            }
            bool driving = car != null && car.isActiveAndEnabled && Time.timeScale > 0
                           && (run == null || !run.IsGameOver)
                           && (pedestrian == null || !pedestrian.gameObject.activeInHierarchy);
            if (mirrorPanel.activeSelf != driving) mirrorPanel.SetActive(driving);
            if (!driving || Time.frameCount % renderEveryNthFrame != 0) return;

            var t = car.transform;
            mirrorCamera.transform.SetPositionAndRotation(t.TransformPoint(offset), t.rotation * Quaternion.Euler(lookEuler));
            var shadows = QualitySettings.shadows;
            QualitySettings.shadows = ShadowQuality.Disable;
            mirrorCamera.Render();
            QualitySettings.shadows = shadows;
        }
    }
}
