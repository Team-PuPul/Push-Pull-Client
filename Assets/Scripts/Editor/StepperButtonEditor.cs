using UnityEditor;
using UnityEngine;

// StepperButton 전용 인스펙터.
// UIButtonEditor는 UIButton의 하드코딩된 필드만 그리므로,
// 서브클래스인 StepperButton의 onPrev/onNext가 표시되지 않는다.
// 더 구체적인 타입 매칭으로 이 에디터가 우선 적용되며,
// 스테퍼 이벤트를 추가로 그린 뒤 UIButton 공통 인스펙터를 이어서 그린다.
[CustomEditor(typeof(StepperButton), true)]
[CanEditMultipleObjects]
public class StepperButtonEditor : UIButtonEditor
{
    SerializedProperty onPrevProp;
    SerializedProperty onNextProp;

    protected override void OnEnable()
    {
        base.OnEnable();
        // 실제 StepperButton 클래스의 변수명과 일치해야 합니다.
        onPrevProp = serializedObject.FindProperty("onPrev");
        onNextProp = serializedObject.FindProperty("onNext");
    }

    public override void OnInspectorGUI()
    {
        // 1. 스테퍼 전용 이벤트 (좌/우)
        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stepper Events", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(onPrevProp, new GUIContent("On Prev (←)"));
        EditorGUILayout.PropertyField(onNextProp, new GUIContent("On Next (→)"));
        serializedObject.ApplyModifiedProperties();

        // 2. UIButton 공통 인스펙터 (사운드 / 버튼 타입 / Base Button)
        base.OnInspectorGUI();
    }
}
