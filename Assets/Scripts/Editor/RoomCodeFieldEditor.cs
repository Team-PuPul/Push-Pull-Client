using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;

// RoomCodeField 전용 커스텀 에디터.
// TMP_InputField에는 [CustomEditor(typeof(TMP_InputField), true)]로 등록된 TMP_InputFieldEditor가 있어
// 파생 클래스인 RoomCodeField에도 그대로 적용된다. 그 결과 RoomCodeField에서 추가한
// startButton 필드가 인스펙터에 표시되지 않으므로, 전용 에디터로 해당 필드를 노출한다.
[CustomEditor(typeof(RoomCodeField), true)]
[CanEditMultipleObjects]
public class RoomCodeFieldEditor : TMP_InputFieldEditor
{
    // 실제 RoomCodeField 클래스의 변수명과 일치해야 한다.
    private SerializedProperty startButtonProp;

    protected override void OnEnable()
    {
        base.OnEnable();
        startButtonProp = serializedObject.FindProperty("startButton");
    }

    public override void OnInspectorGUI()
    {
        // 1. RoomCodeField 전용 설정
        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Room Code Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(startButtonProp, new GUIContent("Start Button"));
        EditorGUILayout.HelpBox("유효한 방 코드가 입력되면 상호작용이 활성화될 시작 버튼입니다.", MessageType.None);
        serializedObject.ApplyModifiedProperties();

        // 2. 기존 TMP_InputField의 속성들
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Base Input Field Settings", EditorStyles.boldLabel);
        base.OnInspectorGUI();
    }
}
