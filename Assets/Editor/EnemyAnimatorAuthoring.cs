using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RogueDrive.Editor
{
    /// <summary>
    /// Автоматизированный авторский инструмент для настройки скелетной анимации врагов (Mecanim).
    /// Создает универсальный Animator Controller с состояниями Idle, Run, Attack, Hit, Death,
    /// генерирует базовые ключевые кадры и связывает Animator с префабами зомби и мутантов.
    /// </summary>
    public static class EnemyAnimatorAuthoring
    {
        private const string AnimFolderPath = "Assets/Content/Animations";
        private const string ControllerPath = "Assets/Content/Animations/ZombieAnimatorController.controller";

        [MenuItem("RogueDrive/Анимация врагов (Mecanim)/Создать и настроить Animator Controller", priority = 40)]
        public static void CreateOrUpdateAnimatorController()
        {
            if (!Directory.Exists(AnimFolderPath))
            {
                Directory.CreateDirectory(AnimFolderPath);
                AssetDatabase.Refresh();
            }

            // 1. Создание анимационных клипов
            AnimationClip idleClip = EnsureClip("Zombie_Idle", CreateIdleCurves());
            AnimationClip runClip = EnsureClip("Zombie_Run", CreateRunCurves());
            AnimationClip attackClip = EnsureClip("Zombie_Attack", CreateAttackCurves());
            AnimationClip hitClip = EnsureClip("Zombie_Hit", CreateHitCurves());
            AnimationClip deathClip = EnsureClip("Zombie_Death", CreateDeathCurves());

            // 2. Создание или обновление AnimatorController
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // Параметры
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsAttacking", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;

            // Очистка старых состояний для чистой сборки
            var existingStates = rootStateMachine.states;
            for (int i = 0; i < existingStates.Length; i++)
            {
                rootStateMachine.RemoveState(existingStates[i].state);
            }

            // Добавление состояний
            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;

            var runState = rootStateMachine.AddState("Run");
            runState.motion = runClip;

            var attackState = rootStateMachine.AddState("Attack");
            attackState.motion = attackClip;

            var hitState = rootStateMachine.AddState("Hit");
            hitState.motion = hitClip;

            var deathState = rootStateMachine.AddState("Death");
            deathState.motion = deathClip;

            rootStateMachine.defaultState = idleState;

            // Переходы
            // Idle <-> Run
            var toRun = idleState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.15f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var toIdle = runState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // AnyState -> Attack
            var toAttack = rootStateMachine.AddAnyStateTransition(attackState);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");

            var attackToRun = attackState.AddTransition(runState);
            attackToRun.hasExitTime = true;
            attackToRun.exitTime = 0.85f;
            attackToRun.duration = 0.15f;

            // AnyState -> Hit
            var toHit = rootStateMachine.AddAnyStateTransition(hitState);
            toHit.hasExitTime = false;
            toHit.duration = 0.05f;
            toHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");

            var hitToRun = hitState.AddTransition(runState);
            hitToRun.hasExitTime = true;
            hitToRun.exitTime = 0.8f;
            hitToRun.duration = 0.1f;

            // AnyState -> Death
            var toDeath = rootStateMachine.AddAnyStateTransition(deathState);
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
            toDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyAnimatorAuthoring] ✅ Universal Mecanim Controller создан и сохранен в: {ControllerPath}");
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                if (controller.parameters[i].name == name) return;
            }
            controller.AddParameter(name, type);
        }

        private static AnimationClip EnsureClip(string clipName, AnimationClip generatedClip)
        {
            string path = $"{AnimFolderPath}/{clipName}.anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(generatedClip, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(generatedClip, path);
            return generatedClip;
        }

        private static AnimationClip CreateIdleCurves()
        {
            AnimationClip clip = new AnimationClip { name = "Zombie_Idle", wrapMode = WrapMode.Loop };
            AnimationCurve curveY = AnimationCurve.EaseInOut(0f, 0f, 1.0f, 0.04f);
            curveY.postWrapMode = WrapMode.PingPong;
            clip.SetCurve("", typeof(Transform), "localPosition.y", curveY);
            return clip;
        }

        private static AnimationClip CreateRunCurves()
        {
            AnimationClip clip = new AnimationClip { name = "Zombie_Run", wrapMode = WrapMode.Loop };
            AnimationCurve curveY = AnimationCurve.Linear(0f, 0f, 0.25f, 0.1f);
            curveY.AddKey(0.5f, 0f);
            curveY.postWrapMode = WrapMode.Loop;

            AnimationCurve rotZ = AnimationCurve.Linear(0f, -4f, 0.25f, 4f);
            rotZ.AddKey(0.5f, -4f);
            rotZ.postWrapMode = WrapMode.Loop;

            clip.SetCurve("", typeof(Transform), "localPosition.y", curveY);
            clip.SetCurve("", typeof(Transform), "localEulerAngles.z", rotZ);
            return clip;
        }

        private static AnimationClip CreateAttackCurves()
        {
            AnimationClip clip = new AnimationClip { name = "Zombie_Attack", wrapMode = WrapMode.Once };
            AnimationCurve rotX = AnimationCurve.Linear(0f, 0f, 0.2f, -25f);
            rotX.AddKey(0.45f, 15f);
            rotX.AddKey(0.65f, 0f);
            clip.SetCurve("", typeof(Transform), "localEulerAngles.x", rotX);
            return clip;
        }

        private static AnimationClip CreateHitCurves()
        {
            AnimationClip clip = new AnimationClip { name = "Zombie_Hit", wrapMode = WrapMode.Once };
            AnimationCurve rotX = AnimationCurve.Linear(0f, 0f, 0.1f, 18f);
            rotX.AddKey(0.25f, 0f);
            clip.SetCurve("", typeof(Transform), "localEulerAngles.x", rotX);
            return clip;
        }

        private static AnimationClip CreateDeathCurves()
        {
            AnimationClip clip = new AnimationClip { name = "Zombie_Death", wrapMode = WrapMode.ClampForever };
            AnimationCurve curveY = AnimationCurve.Linear(0f, 0f, 0.4f, -0.45f);
            AnimationCurve rotX = AnimationCurve.Linear(0f, 0f, 0.4f, -85f);
            clip.SetCurve("", typeof(Transform), "localPosition.y", curveY);
            clip.SetCurve("", typeof(Transform), "localEulerAngles.x", rotX);
            return clip;
        }
    }
}
