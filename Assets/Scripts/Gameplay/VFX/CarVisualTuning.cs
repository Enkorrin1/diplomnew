using RogueDrive.Meta;
using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Модульный визуальный тюнинг автомобиля:
    /// — Броня кузова (Hull): силовой кенгурятник, защитные решетки на окнах, бронеплиты на дверях.
    /// — Вооружение (Armament): массивный ствол автопушки + лазерный целеуказатель.
    /// — Колеса (Tires): угрожающие шипы на ступицах дисков.
    /// Синхронизируется с MetaProgress и отображается как в гараже, так и на трассе.
    /// </summary>
    public sealed class CarVisualTuning : MonoBehaviour
    {
        private Transform tuningRoot;
        private LineRenderer laserSight;

        public void RefreshTuning(string carId, MetaProgress progress)
        {
            if (string.IsNullOrEmpty(carId)) carId = "light";

            // Очищаем старые детали
            Transform oldRoot = transform.Find("VisualTuning_Modules");
            if (oldRoot != null)
            {
                if (Application.isPlaying) Destroy(oldRoot.gameObject);
                else DestroyImmediate(oldRoot.gameObject);
            }

            GameObject rootObj = new GameObject("VisualTuning_Modules");
            rootObj.transform.SetParent(transform, false);
            tuningRoot = rootObj.transform;

            if (progress == null) return;

            int hullLevel = GetLevel(progress, carId, "hull");
            int armamentLevel = GetLevel(progress, carId, "armament");
            int tiresLevel = GetLevel(progress, carId, "tires");

            // 1. БРОНЯ КУЗОВА (HULL)
            if (hullLevel >= 1)
            {
                AttachBullbar(hullLevel);
            }
            if (hullLevel >= 3)
            {
                AttachWindowGrates();
            }
            if (hullLevel >= 5)
            {
                AttachSideArmorPlates();
            }

            // 2. ВООРУЖЕНИЕ (ARMAMENT)
            if (armamentLevel >= 1)
            {
                AttachTurretEnhancements(armamentLevel);
            }

            // 3. КОЛЕСНЫЕ ШИПЫ (TIRES)
            if (tiresLevel >= 1)
            {
                AttachWheelSpikes();
            }
        }

        private int GetLevel(MetaProgress progress, string carId, string trackIdKeyword)
        {
            if (progress == null || progress.Data == null || progress.Data.Upgrades == null) return 0;

            // Ищем уровень по ключевому слову в ID улучшения
            for (int i = 0; i < progress.Data.Upgrades.Count; i++)
            {
                var entry = progress.Data.Upgrades[i];
                if ((string.IsNullOrEmpty(entry.CarId) || entry.CarId == carId) && entry.TrackId != null && entry.TrackId.ToLower().Contains(trackIdKeyword))
                {
                    return entry.Level;
                }
            }
            return 0;
        }

        private void AttachBullbar(int level)
        {
            Transform barRoot = new GameObject("Bullbar_Bumper").transform;
            barRoot.SetParent(tuningRoot, false);

            Color steelColor = new Color(0.20f, 0.22f, 0.25f);
            Color hazardColor = new Color(0.9f, 0.7f, 0.1f);

            // Основная нижняя труба кенгурятника
            CreateMeshPart("MainTube", new Vector3(0f, 0.42f, 2.2f), new Vector3(1.9f, 0.12f, 0.12f), steelColor, barRoot);

            // Верхняя защитная дуга
            CreateMeshPart("UpperGuard", new Vector3(0f, 0.82f, 2.15f), new Vector3(1.6f, 0.10f, 0.10f), steelColor, barRoot);

            // Вертикальные распорки
            CreateMeshPart("Strut_L", new Vector3(-0.65f, 0.62f, 2.18f), new Vector3(0.08f, 0.45f, 0.08f), steelColor, barRoot);
            CreateMeshPart("Strut_R", new Vector3(0.65f, 0.62f, 2.18f), new Vector3(0.08f, 0.45f, 0.08f), steelColor, barRoot);

            // Желтые предупредительные пластины тарана
            if (level >= 2)
            {
                CreateMeshPart("PlowPlate_L", new Vector3(-0.55f, 0.40f, 2.24f), new Vector3(0.45f, 0.28f, 0.05f), hazardColor, barRoot);
                CreateMeshPart("PlowPlate_R", new Vector3(0.55f, 0.40f, 2.24f), new Vector3(0.45f, 0.28f, 0.05f), hazardColor, barRoot);
            }
        }

        private void AttachWindowGrates()
        {
            Transform gratesRoot = new GameObject("Window_ArmorGrates").transform;
            gratesRoot.SetParent(tuningRoot, false);

            Color grateColor = new Color(0.16f, 0.18f, 0.20f);

            // Решетка лобового стекла
            CreateMeshPart("WindshieldGrate_1", new Vector3(0f, 1.15f, 0.75f), new Vector3(1.5f, 0.04f, 0.04f), grateColor, gratesRoot);
            CreateMeshPart("WindshieldGrate_2", new Vector3(0f, 1.30f, 0.55f), new Vector3(1.4f, 0.04f, 0.04f), grateColor, gratesRoot);
            CreateMeshPart("WindshieldGrate_3", new Vector3(0f, 1.45f, 0.35f), new Vector3(1.3f, 0.04f, 0.04f), grateColor, gratesRoot);

            // Боковые решетки
            CreateMeshPart("SideGrate_L", new Vector3(-0.95f, 1.18f, -0.1f), new Vector3(0.04f, 0.25f, 0.8f), grateColor, gratesRoot);
            CreateMeshPart("SideGrate_R", new Vector3(0.95f, 1.18f, -0.1f), new Vector3(0.04f, 0.25f, 0.8f), grateColor, gratesRoot);
        }

        private void AttachSideArmorPlates()
        {
            Transform armorRoot = new GameObject("Door_ArmorPlates").transform;
            armorRoot.SetParent(tuningRoot, false);

            Color armorPlateColor = new Color(0.24f, 0.26f, 0.30f);

            // Бронелисты на бортах дверей
            CreateMeshPart("Plate_L", new Vector3(-1.02f, 0.65f, 0.1f), new Vector3(0.06f, 0.45f, 1.5f), armorPlateColor, armorRoot);
            CreateMeshPart("Plate_R", new Vector3(1.02f, 0.65f, 0.1f), new Vector3(0.06f, 0.45f, 1.5f), armorPlateColor, armorRoot);
        }

        private void AttachTurretEnhancements(int level)
        {
            Transform roofSocket = transform.Find("Socket_Roof") ?? transform;
            Transform turretEnhance = new GameObject("Turret_HeavyBarrel").transform;
            turretEnhance.SetParent(roofSocket, false);

            Color gunMetal = new Color(0.12f, 0.13f, 0.15f);

            // Спаренный пламегаситель
            CreateMeshPart("HeavyMuzzle_L", new Vector3(-0.08f, 0.12f, 0.95f), new Vector3(0.08f, 0.08f, 0.35f), gunMetal, turretEnhance);
            CreateMeshPart("HeavyMuzzle_R", new Vector3(0.08f, 0.12f, 0.95f), new Vector3(0.08f, 0.08f, 0.35f), gunMetal, turretEnhance);

            // Лазерный целеуказатель
            if (level >= 2)
            {
                GameObject laserObj = new GameObject("LaserSight_Beam");
                laserObj.transform.SetParent(turretEnhance, false);
                laserObj.transform.localPosition = new Vector3(0.14f, 0.10f, 0.6f);

                laserSight = laserObj.AddComponent<LineRenderer>();
                laserSight.useWorldSpace = false;
                laserSight.startWidth = 0.035f;
                laserSight.endWidth = 0.015f;
                laserSight.positionCount = 2;
                laserSight.SetPosition(0, Vector3.zero);
                laserSight.SetPosition(1, Vector3.forward * 30f);

                var laserMat = new Material(Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Standard"));
                laserSight.sharedMaterial = laserMat;
                laserSight.startColor = new Color(1f, 0.1f, 0.1f, 0.85f);
                laserSight.endColor = new Color(1f, 0.1f, 0.1f, 0.1f);
            }
        }

        private void AttachWheelSpikes()
        {
            Transform spikesRoot = new GameObject("Wheel_Spikes").transform;
            spikesRoot.SetParent(tuningRoot, false);

            Color spikeColor = new Color(0.75f, 0.75f, 0.8f);

            Vector3[] wheelPositions = new[]
            {
                new Vector3(-0.98f, 0.35f, 1.4f),  // Переднее левое
                new Vector3(0.98f, 0.35f, 1.4f),   // Переднее правое
                new Vector3(-0.98f, 0.35f, -1.3f), // Заднее левое
                new Vector3(0.98f, 0.35f, -1.3f)   // Заднее правое
            };

            for (int i = 0; i < wheelPositions.Length; i++)
            {
                float outward = wheelPositions[i].x > 0 ? 1f : -1f;
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spike.name = $"Spike_Wheel_{i}";
                spike.transform.SetParent(spikesRoot, false);
                spike.transform.localPosition = wheelPositions[i] + Vector3.right * (outward * 0.12f);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                spike.transform.localScale = new Vector3(0.06f, 0.15f, 0.06f);

                var r = spike.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = new Material(Shader.Find("Standard")) { color = spikeColor };

                var col = spike.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }

        private static GameObject CreateMeshPart(string name, Vector3 pos, Vector3 scale, Color color, Transform parent)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = pos;
            part.transform.localScale = scale;

            var col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = part.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
            }

            return part;
        }
    }
}
