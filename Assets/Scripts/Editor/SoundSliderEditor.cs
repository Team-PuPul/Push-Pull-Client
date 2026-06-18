using UnityEditor;
using UnityEngine;

// SoundSlider 전용 인스펙터.
// UIButtonEditor는 UIButton의 하드코딩된 필드만 그리므로,
// 서브클래스인 SoundSlider의 soundSlider가 표시되지 않는다.
// 더 구체적인 타입 매칭으로 이 에디터가 우선 적용되며,
// 슬라이더 참조를 추가로 그린 뒤 UIButton 공통 인스펙터를 이어서 그린다.
[CustomEditor(typeof(SoundSlider), true)]
[CanEditMultipleObjects]
public class SoundSliderEditor : UIButtonEditor
{
    SerializedProperty soundSliderProp;

    protected override void OnEnable()
    {
        base.OnEnable();
        // 실제 SoundSlider 클래스의 변수명과 일치해야 합니다.
        soundSliderProp = serializedObject.FindProperty("soundSlider");
    }

    public override void OnInspectorGUI()
    {
        // 1. 사운드 슬라이더 전용 참조
        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sound Slider", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(soundSliderProp, new GUIContent("Sound Slider"));
        serializedObject.ApplyModifiedProperties();

        // 2. UIButton 공통 인스펙터 (사운드 / 버튼 타입 / Base Button)
        base.OnInspectorGUI();
    }
}
