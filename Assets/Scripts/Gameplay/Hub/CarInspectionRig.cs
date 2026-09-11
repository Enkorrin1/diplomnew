using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Автоматически генерирует и настраивает 3D-зоны инспекции вокруг автомобиля в гараже.
    /// Вешается на объект автомобиля или точку подиума машины.
    /// Каждая зона снабжена невидимым триггер-коллайдером для луча прицела.
    /// </summary>
    public sealed class CarInspectionRig : MonoBehaviour
    {
        [Header("Hotspot Settings")]
        [SerializeField] private bool autoGenerateHotspots = true;

        private void Start()
        {
            if (autoGenerateHotspots)
            {
                SetupHotspots();
            }
        }

        public void SetupHotspots()
        {
            // 1. Капот (Двигатель)
            EnsureHotspot("Hotspot_Engine", new Vector3(0f, 0.75f, 1.4f), new Vector3(1.6f, 0.7f, 1.3f),
                "engine", "ДВИГАТЕЛЬ V8", "+Макс. скорость и разгон");

            // 2. Крыша (Вооружение)
            EnsureHotspot("Hotspot_Turret", new Vector3(0f, 1.65f, 0.1f), new Vector3(1.3f, 0.8f, 1.3f),
                "armament", "АВТОПУШКА КРЫШИ", "+Урон и скорострельность");

            // 3. Колеса и подвеска (Правый и левый борт)
            EnsureHotspot("Hotspot_Tires", new Vector3(1.15f, 0.45f, 0f), new Vector3(0.65f, 0.8f, 3.2f),
                "tires", "КОЛЕСА И ШИПЫ", "+Сцепление и шипы на ступицах");

            // 4. Бензобак (Задняя часть)
            EnsureHotspot("Hotspot_Tank", new Vector3(0f, 0.85f, -1.8f), new Vector3(1.4f, 0.8f, 1.0f),
                "tank", "ТОПЛИВНЫЙ БАК", "+Запас хода и емкость бака");

            // 5. Кузов и силовой кенгурятник (Передний бампер / Левый борт)
            EnsureHotspot("Hotspot_Hull", new Vector3(0f, 0.45f, 2.3f), new Vector3(2.0f, 0.7f, 0.8f),
                "hull", "БРОНЯ КОРПУСА", "+Защита от тарана и кенгурятник");
        }

        private void EnsureHotspot(string nodeName, Vector3 localPos, Vector3 boxSize,
            string keyword, string displayName, string effectDesc)
        {
            Transform existing = transform.Find(nodeName);
            GameObject spotObj;

            if (existing == null)
            {
                spotObj = new GameObject(nodeName);
                spotObj.transform.SetParent(transform, false);
                spotObj.transform.localPosition = localPos;

                BoxCollider box = spotObj.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = boxSize;

                var hotspot = spotObj.AddComponent<CarInspectionHotspot>();
                hotspot.Configure(keyword, displayName, effectDesc);
            }
            else
            {
                spotObj = existing.gameObject;
                var hotspot = spotObj.GetComponent<CarInspectionHotspot>();
                if (hotspot == null) hotspot = spotObj.AddComponent<CarInspectionHotspot>();
                hotspot.Configure(keyword, displayName, effectDesc);
            }
        }
    }
}
