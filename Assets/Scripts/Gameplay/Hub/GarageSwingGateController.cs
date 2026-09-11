using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Контроллер двустворчатых распашных гермоворот бункера.
    /// Требует включенного дизель-генератора (питания сети).
    /// При активации плавно распахивает левую и правую створки наружу в стороны.
    /// </summary>
    public sealed class GarageSwingGateController : MonoBehaviour, IGarageInteractable
    {
        [Header("Door Transforms (Створки ворот)")]
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;

        [Header("Swing Angles (Углы распахивания)")]
        [SerializeField] private float leftOpenAngle = -95f;
        [SerializeField] private float rightOpenAngle = 95f;
        [SerializeField] private float openSpeed = 45f; // градусов в секунду

        [Header("Status")]
        [SerializeField] private bool isOpen = false;
        private bool isAnimating = false;

        public bool IsOpen => isOpen;
        public void OpenGates() => OpenGate();

        private Quaternion leftClosedRot;
        private Quaternion rightClosedRot;
        private Quaternion leftTargetRot;
        private Quaternion rightTargetRot;

        private void Start()
        {
            EnsureDoorsExist();

            if (leftDoor != null)
            {
                leftClosedRot = leftDoor.localRotation;
                leftTargetRot = leftClosedRot * Quaternion.Euler(0f, leftOpenAngle, 0f);
            }

            if (rightDoor != null)
            {
                rightClosedRot = rightDoor.localRotation;
                rightTargetRot = rightClosedRot * Quaternion.Euler(0f, rightOpenAngle, 0f);
            }

            // Если в сессии ворота уже были открыты
            if (GaragePrologueManager.Instance != null && GaragePrologueManager.Instance.IsGateOpen)
            {
                isOpen = true;
                if (leftDoor != null) leftDoor.localRotation = leftTargetRot;
                if (rightDoor != null) rightDoor.localRotation = rightTargetRot;
            }
        }

        public string GetPromptText()
        {
            bool hasPower = GaragePrologueManager.Instance == null || GaragePrologueManager.Instance.IsPowerOn;
            if (!hasPower)
            {
                return "Ворота обесточены! [Включите генератор питания]";
            }

            if (isOpen)
            {
                return "Гермоворота открыты (Путь на поверхность свободен)";
            }

            return "[E] Открыть двустворчатые гермоворота";
        }

        public bool CanInteract()
        {
            bool hasPower = GaragePrologueManager.Instance == null || GaragePrologueManager.Instance.IsPowerOn;
            return hasPower && !isOpen && !isAnimating;
        }

        public void Interact(GaragePlayerController player)
        {
            if (!CanInteract()) return;

            OpenGate();
        }

        public void OpenGate()
        {
            if (isOpen || isAnimating) return;

            isAnimating = true;

            if (GaragePrologueManager.Instance != null)
            {
                GaragePrologueManager.Instance.OpenGate();
            }

            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayGateOpen();
            }
        }

        private void Update()
        {
            if (isAnimating)
            {
                float step = openSpeed * Time.deltaTime;
                bool leftDone = true;
                bool rightDone = true;

                if (leftDoor != null)
                {
                    leftDoor.localRotation = Quaternion.RotateTowards(leftDoor.localRotation, leftTargetRot, step);
                    leftDone = Quaternion.Angle(leftDoor.localRotation, leftTargetRot) < 0.5f;
                }

                if (rightDoor != null)
                {
                    rightDoor.localRotation = Quaternion.RotateTowards(rightDoor.localRotation, rightTargetRot, step);
                    rightDone = Quaternion.Angle(rightDoor.localRotation, rightTargetRot) < 0.5f;
                }

                if (leftDone && rightDone)
                {
                    isAnimating = false;
                    isOpen = true;
                }
            }
        }

        /// <summary>
        /// Создает временные заглушки створок ворот, если 3D-модели еще не назначены.
        /// Пользователь сможет легко заменить их своими моделями из Blender!
        /// </summary>
        private void EnsureDoorsExist()
        {
            if (leftDoor == null)
            {
                Transform foundLeft = transform.Find("Gate_Door_Left");
                if (foundLeft != null) leftDoor = foundLeft;
                else leftDoor = CreateDoorPlaceholder("Gate_Door_Left", new Vector3(-3.2f, 0f, 0f), true);
            }

            if (rightDoor == null)
            {
                Transform foundRight = transform.Find("Gate_Door_Right");
                if (foundRight != null) rightDoor = foundRight;
                else rightDoor = CreateDoorPlaceholder("Gate_Door_Right", new Vector3(3.2f, 0f, 0f), false);
            }
        }

        private Transform CreateDoorPlaceholder(string doorName, Vector3 hingePos, bool isLeft)
        {
            // Корневой объект в точке петли (Origin)
            GameObject hingeObj = new GameObject(doorName);
            hingeObj.transform.SetParent(transform, false);
            hingeObj.transform.localPosition = hingePos;

            // Сама створка ворот смещена от петли к центру
            GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshObj.name = "Placeholder_BlastDoorMesh";
            meshObj.transform.SetParent(hingeObj.transform, false);

            float width = 3.2f;
            float height = 5.0f;
            float thickness = 0.35f;

            // Центр геометрии смещен на половину ширины от петли
            float xOffset = isLeft ? (width * 0.5f) : (-width * 0.5f);
            meshObj.transform.localPosition = new Vector3(xOffset, height * 0.5f, 0f);
            meshObj.transform.localScale = new Vector3(width, height, thickness);

            var r = meshObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = new Material(Shader.Find("Standard"))
                {
                    color = new Color(0.25f, 0.28f, 0.32f) // броневая сталь
                };
            }

            // Желто-черная предупреждающая полоса
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "HazardStripe";
            stripe.transform.SetParent(meshObj.transform, false);
            stripe.transform.localPosition = new Vector3(0f, -0.4f, 0.52f);
            stripe.transform.localScale = new Vector3(0.95f, 0.15f, 0.05f);
            var sr = stripe.GetComponent<Renderer>();
            if (sr != null)
            {
                sr.sharedMaterial = new Material(Shader.Find("Standard"))
                {
                    color = new Color(0.9f, 0.75f, 0.1f) // яркий hazard-желтый
                };
            }

            return hingeObj.transform;
        }
    }
}
