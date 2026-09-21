using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Контроллер скелетной анимации врагов (Mecanim Animator).
    /// Управляет состояниями аниматора: Idle, Run, Attack, Hit, Die.
    /// Синхронизируется с поведением EnemyBase и автоматически активирует
    /// процедурный резерв (EnemyVisualBobbing), если у модели отсутствует контроллер анимаций.
    /// </summary>
    public sealed class EnemyMecanimController : MonoBehaviour
    {
        [Header("Animator Components")]
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private bool useMecanimIfAvailable = true;

        [Header("Parameter Names")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string attackParam = "IsAttacking";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string dieTrigger = "Die";

        private EnemyBase enemyBase;
        private EnemyVisualBobbing proceduralBobbing;

        private int speedHash;
        private int attackHash;
        private int hitHash;
        private int dieHash;

        private bool hasValidAnimator;
        private float attackTimer;

        public bool HasValidAnimator => hasValidAnimator;

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
            proceduralBobbing = GetComponent<EnemyVisualBobbing>();

            speedHash = Animator.StringToHash(speedParam);
            attackHash = Animator.StringToHash(attackParam);
            hitHash = Animator.StringToHash(hitTrigger);
            dieHash = Animator.StringToHash(dieTrigger);

            ResolveAnimator();
        }

        private void Start()
        {
            if (targetAnimator == null)
                ResolveAnimator();
        }

        private void Update()
        {
            if (attackTimer > 0f)
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f && hasValidAnimator)
                {
                    targetAnimator.SetBool(attackHash, false);
                }
            }
        }

        public void ResolveAnimator()
        {
            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>();
            }

            hasValidAnimator = useMecanimIfAvailable && targetAnimator != null && targetAnimator.runtimeAnimatorController != null;

            if (proceduralBobbing != null)
            {
                // Если Mecanim активен — отключаем процедурное покачивание костей, чтобы избежать конфликтов с позами
                proceduralBobbing.enabled = !hasValidAnimator;
            }
        }

        public void SetSpeed(float speed)
        {
            if (!hasValidAnimator) return;

            targetAnimator.SetFloat(speedHash, speed);
        }

        public void TriggerAttack(float duration = 0.65f)
        {
            if (!hasValidAnimator) return;

            targetAnimator.SetBool(attackHash, true);
            attackTimer = duration;
        }

        public void TriggerHit()
        {
            if (!hasValidAnimator) return;

            targetAnimator.SetTrigger(hitHash);
        }

        public void TriggerDeath()
        {
            if (!hasValidAnimator) return;

            targetAnimator.SetTrigger(dieHash);
            targetAnimator.SetBool(attackHash, false);
        }

        public void SetAnimator(Animator animator)
        {
            targetAnimator = animator;
            ResolveAnimator();
        }
    }
}
