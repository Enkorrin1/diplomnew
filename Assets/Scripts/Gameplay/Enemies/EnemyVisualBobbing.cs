using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Процедурная анимация бега/ковыляния для зомби и мутантов.
    /// Работает с уже импортированными ригами и не требует отдельных animation clips:
    /// раскачивает корпус и двигает кости рук и ног только во время фактического движения.
    /// </summary>
    public sealed class EnemyVisualBobbing : MonoBehaviour
    {
        [Header("Bobbing Settings")]
        [SerializeField] private float stepFrequency = 8.5f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float tiltAngle = 5.5f;
        [SerializeField, Range(0f, 45f)] private float legSwingAngle = 20f;
        [SerializeField, Range(0f, 45f)] private float armSwingAngle = 16f;

        private Transform visualTransform;
        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot;
        private EnemyBase enemyBase;
        private float randomSeed;
        private Vector3 previousWorldPosition;
        private float motionWeight;

        private BonePose leftArm;
        private BonePose rightArm;
        private BonePose leftLeg;
        private BonePose rightLeg;
        private BonePose torso;

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
            ResolveVisual();
            randomSeed = Random.Range(0f, 100f);
            previousWorldPosition = transform.position;
        }

        private void Start()
        {
            if (visualTransform == null || visualTransform == transform)
            {
                ResolveVisual();
            }
        }

        private void ResolveVisual()
        {
            Transform bodyChild = transform.Find("VisualModel") ?? transform.Find("Body") ?? transform.Find("Visual") ?? transform.Find("VisualBody");
            visualTransform = bodyChild != null ? bodyChild : transform;

            initialLocalPos = visualTransform.localPosition;
            initialLocalRot = visualTransform.localRotation;

            leftArm = FindBone("upper_arm.L", "upper_arm.l", "arm_stretch.l", "shoulder.L", "shoulder.l");
            rightArm = FindBone("upper_arm.R", "upper_arm.r", "arm_stretch.r", "shoulder.R", "shoulder.r");
            leftLeg = FindBone("thigh.L", "thigh.l", "thigh_stretch.l", "leg_stretch.l");
            rightLeg = FindBone("thigh.R", "thigh.r", "thigh_stretch.r", "leg_stretch.r");
            torso = FindBone("spine", "spine.001", "spine_01.x");
        }

        private void Update()
        {
            if (visualTransform == null) return;
            if (enemyBase != null && enemyBase.IsDead) return;

            float distanceMoved = Vector3.Distance(transform.position, previousWorldPosition);
            previousWorldPosition = transform.position;
            float worldSpeed = Time.deltaTime > 0f ? distanceMoved / Time.deltaTime : 0f;
            float targetMotionWeight = worldSpeed > 0.05f ? 1f : 0f;
            motionWeight = Mathf.MoveTowards(motionWeight, targetMotionWeight, Time.deltaTime * 8f);

            float t = (Time.time + randomSeed) * stepFrequency;

            // Вертикальное покачивание (шаг)
            float verticalOffset = Mathf.Abs(Mathf.Sin(t)) * bobHeight * motionWeight;

            // Боковое покачивание (переваливание с ноги на ногу)
            float sideTilt = Mathf.Sin(t * 0.5f) * tiltAngle * motionWeight;

            visualTransform.localPosition = initialLocalPos + new Vector3(0f, verticalOffset, 0f);
            visualTransform.localRotation = initialLocalRot * Quaternion.Euler(0f, 0f, sideTilt);

            float step = Mathf.Sin(t) * motionWeight;
            leftLeg.ApplyRotation(step * legSwingAngle, 0f, 0f);
            rightLeg.ApplyRotation(-step * legSwingAngle, 0f, 0f);
            leftArm.ApplyRotation(-step * armSwingAngle, 0f, 0f);
            rightArm.ApplyRotation(step * armSwingAngle, 0f, 0f);
            torso.ApplyRotation(0f, 0f, -sideTilt * 0.35f);
        }

        private void OnDisable()
        {
            RestoreInitialPose();
        }

        private void RestoreInitialPose()
        {
            if (visualTransform != null)
            {
                visualTransform.localPosition = initialLocalPos;
                visualTransform.localRotation = initialLocalRot;
            }

            leftArm.RestoreRotation();
            rightArm.RestoreRotation();
            leftLeg.RestoreRotation();
            rightLeg.RestoreRotation();
            torso.RestoreRotation();
        }

        private BonePose FindBone(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform bone = FindDeepChild(visualTransform, names[i]);
                if (bone != null)
                {
                    return new BonePose(bone);
                }
            }

            return default;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null) return null;
            if (root.name == childName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDeepChild(root.GetChild(i), childName);
                if (result != null) return result;
            }

            return null;
        }

        private readonly struct BonePose
        {
            private readonly Transform transform;
            private readonly Quaternion initialRotation;

            public BonePose(Transform transform)
            {
                this.transform = transform;
                initialRotation = transform.localRotation;
            }

            public void ApplyRotation(float x, float y, float z)
            {
                if (transform != null)
                {
                    transform.localRotation = initialRotation * Quaternion.Euler(x, y, z);
                }
            }

            public void RestoreRotation()
            {
                if (transform != null)
                {
                    transform.localRotation = initialRotation;
                }
            }
        }
    }
}
