using System;
using System.Collections;
using RogueDrive.Audio;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Интерактивный двухстоечный автомобильный подъемник на станции СТО (База 1).
    /// Игрок нажимает на рычаг/пульт управления [E], после чего подъемник плавно
    /// опускает поднятый пикап на землю с характерным звуком стравливания гидравлики.
    /// </summary>
    public sealed class CarLiftStation : MonoBehaviour, IGarageInteractable
    {
        public event Action LiftFullyLowered;

        [Header("Components")]
        [SerializeField] private Transform leftPost;
        [SerializeField] private Transform rightPost;
        [SerializeField] private Transform liftArmsRoot;
        [SerializeField] private Transform carAnchor;

        [Header("Positions")]
        [SerializeField] private float raisedHeight = 1.8f;
        [SerializeField] private float loweredHeight = 0.05f;
        [SerializeField] private float lowerDuration = 3.5f;

        [Header("State")]
        [SerializeField] private bool isLowered = false;
        private bool isMoving = false;

        public bool IsLowered => isLowered;

        private void Start()
        {
            if (liftArmsRoot == null)
            {
                BuildPlaceholderLift();
            }
            UpdateLiftPosition(isLowered ? loweredHeight : raisedHeight);
        }

        public string GetPromptText()
        {
            if (isMoving) return "Подъемник опускается...";
            if (isLowered) return "✓ Автомобильный подъемник опущен (Пикап на полу)";
            return "[E] Потянуть рычаг: Опустить гидравлический подъемник";
        }

        public bool CanInteract()
        {
            return !isLowered && !isMoving;
        }

        public void Interact(GaragePlayerController player)
        {
            if (!CanInteract()) return;
            StartCoroutine(LowerLiftRoutine());
        }

        private IEnumerator LowerLiftRoutine()
        {
            isMoving = true;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGateOpen();
            }

            float elapsed = 0f;
            float startY = liftArmsRoot != null ? liftArmsRoot.localPosition.y : raisedHeight;

            while (elapsed < lowerDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / lowerDuration);
                float curY = Mathf.Lerp(startY, loweredHeight, t);
                UpdateLiftPosition(curY);
                yield return null;
            }

            UpdateLiftPosition(loweredHeight);
            isMoving = false;
            isLowered = true;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayImpact(0.8f);
            }

            LiftFullyLowered?.Invoke();
        }

        private void UpdateLiftPosition(float y)
        {
            if (liftArmsRoot != null)
            {
                Vector3 pos = liftArmsRoot.localPosition;
                pos.y = y;
                liftArmsRoot.localPosition = pos;
            }
        }

        public void BuildPlaceholderLift()
        {
            Material postMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = new Color(0.18f, 0.35f, 0.65f) // Синий индустриальный металл СТО
            };

            Material armsMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = new Color(0.95f, 0.75f, 0.1f) // Предупреждающий желтый
            };

            // Левая колонна
            GameObject lp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lp.name = "LiftPost_Left";
            lp.transform.SetParent(transform, false);
            lp.transform.localPosition = new Vector3(-2.2f, 2.2f, 0f);
            lp.transform.localScale = new Vector3(0.5f, 4.4f, 0.6f);
            lp.GetComponent<Renderer>().sharedMaterial = postMat;
            leftPost = lp.transform;

            // Правая колонна
            GameObject rp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rp.name = "LiftPost_Right";
            rp.transform.SetParent(transform, false);
            rp.transform.localPosition = new Vector3(2.2f, 2.2f, 0f);
            rp.transform.localScale = new Vector3(0.5f, 4.4f, 0.6f);
            rp.GetComponent<Renderer>().sharedMaterial = postMat;
            rightPost = rp.transform;

            // Подвижная каретка с лапами (LiftArmsRoot)
            GameObject arms = new GameObject("Lift_Arms_Root");
            arms.transform.SetParent(transform, false);
            arms.transform.localPosition = new Vector3(0f, raisedHeight, 0f);
            liftArmsRoot = arms.transform;

            // Балка левая и правая
            CreateBeam(arms.transform, "Arm_Left", new Vector3(-1.8f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 3.2f), armsMat);
            CreateBeam(arms.transform, "Arm_Right", new Vector3(1.8f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 3.2f), armsMat);
            CreateBeam(arms.transform, "Crossbeam", new Vector3(0f, 0.15f, 0f), new Vector3(4.0f, 0.25f, 0.4f), armsMat);

            // Точка крепления машины
            GameObject anchor = new GameObject("Car_Placement_Anchor");
            anchor.transform.SetParent(arms.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            carAnchor = anchor.transform;

            // Пульт управления на левой колонне
            GameObject leverBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leverBox.name = "Lift_Control_LeverBox";
            leverBox.transform.SetParent(lp.transform, false);
            leverBox.transform.localPosition = new Vector3(0.55f, -0.2f, 0f);
            leverBox.transform.localScale = new Vector3(0.5f, 0.2f, 0.5f);
            leverBox.GetComponent<Renderer>().sharedMaterial = armsMat;

            // Навешиваем BoxCollider для лучевого взаимодействия
            var col = GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(-1.9f, 1.4f, 0f);
            col.size = new Vector3(1.2f, 2.2f, 1.2f);
        }

        public Transform GetCarAnchor()
        {
            if (carAnchor == null && liftArmsRoot != null) return liftArmsRoot;
            return carAnchor != null ? carAnchor : transform;
        }

        private void CreateBeam(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
