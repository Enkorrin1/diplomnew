using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEngine;

namespace RogueDrive.Editor
{
    [CustomEditor(typeof(FluidContainer), true)]
    [CanEditMultipleObjects]
    public sealed class BunkerFluidContainerEditor : UnityEditor.Editor
    {
        private SerializedProperty fluidTypeProp;
        private SerializedProperty maxCapacityProp;
        private SerializedProperty currentLitersProp;
        private SerializedProperty emptyMassProp;
        private SerializedProperty fluidDensityProp;
        private SerializedProperty keepEmptyProp;

        private void OnEnable()
        {
            fluidTypeProp = serializedObject.FindProperty("fluidType");
            maxCapacityProp = serializedObject.FindProperty("maxCapacityLiters");
            currentLitersProp = serializedObject.FindProperty("currentLiters");
            emptyMassProp = serializedObject.FindProperty("emptyMassKg");
            fluidDensityProp = serializedObject.FindProperty("fluidDensity");
            keepEmptyProp = serializedObject.FindProperty("keepEmptyCanisterInHand");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var container = (BunkerFluidContainer)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Параметры жидкости канистры", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(fluidTypeProp, new GUIContent("Тип жидкости"));
            EditorGUILayout.PropertyField(maxCapacityProp, new GUIContent("Вместимость (л)"));
            
            // Слайдер текущего объема в пределах вместимости
            float maxCap = Mathf.Max(0.1f, maxCapacityProp.floatValue);
            currentLitersProp.floatValue = EditorGUILayout.Slider(
                new GUIContent("Залитый объем (л)"), 
                currentLitersProp.floatValue, 
                0f, 
                maxCap);

            // Визуальный индикатор наполненности
            float fillPct = maxCap > 0f ? Mathf.Clamp01(currentLitersProp.floatValue / maxCap) : 0f;
            Rect progressRect = GUILayoutUtility.GetRect(18, 22, "TextField");
            string progressText = $"{container.GetFluidName()}: {currentLitersProp.floatValue:F1} / {maxCap:F0} л ({fillPct * 100f:F0}%)";
            EditorGUI.ProgressBar(progressRect, fillPct, progressText);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Физический вес (кг)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(emptyMassProp, new GUIContent("Вес пустой канистры (кг)"));
            EditorGUILayout.PropertyField(fluidDensityProp, new GUIContent("Плотность (кг/л)"));

            // Информационный блок общего веса
            float totalMass = emptyMassProp.floatValue + (currentLitersProp.floatValue * fluidDensityProp.floatValue);
            EditorGUILayout.HelpBox(
                $"⚖ Общий физический вес Rigidbody: {totalMass:F2} кг\n" +
                $"• Пустая тара: {emptyMassProp.floatValue:F1} кг\n" +
                $"• Масса жидкости: {currentLitersProp.floatValue * fluidDensityProp.floatValue:F2} кг", 
                MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Поведение при заправке", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(keepEmptyProp, new GUIContent("Оставлять пустую в руках"));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Быстрое заполнение:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("100% (Полная)", GUILayout.Height(24)))
            {
                Undo.RecordObject(container, "Fill Max");
                container.FillMax();
                serializedObject.Update();
            }

            if (GUILayout.Button("50% (Половина)", GUILayout.Height(24)))
            {
                Undo.RecordObject(container, "Fill Half");
                container.FillHalf();
                serializedObject.Update();
            }

            if (GUILayout.Button("0% (Пустая)", GUILayout.Height(24)))
            {
                Undo.RecordObject(container, "Drain All");
                container.DrainAll();
                serializedObject.Update();
            }

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            // Синхронизируем массу в Rigidbody при любых изменениях в инспекторе
            if (GUI.changed)
            {
                container.UpdatePhysicalMass();
                EditorUtility.SetDirty(container);
            }
        }
    }
}
