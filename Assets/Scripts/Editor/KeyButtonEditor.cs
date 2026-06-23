using UnityEditor;
using UnityEngine;

// KeyButton 전용 인스펙터.
// UIButtonEditor(=ButtonEditor 상속)를 그대로 재사용하면서 키 리바인딩 설정 섹션만 추가한다.
// 정확한 타입(KeyButton)을 지정했으므로 부모의 editForChildClasses 에디터보다 우선 적용된다.
[CustomEditor(typeof(KeyButton))]
[CanEditMultipleObjects]
public class KeyButtonEditor : UIButtonEditor
{
    SerializedProperty actionReferenceProp;
    SerializedProperty controlSchemeProp;
    SerializedProperty keySpriteSOProp;
    SerializedProperty keyIconProp;
    SerializedProperty keyLabelProp;
    SerializedProperty waitingSpriteProp;
    SerializedProperty waitingTextProp;

    protected override void OnEnable()
    {
        base.OnEnable();
        // 실제 KeyButton 클래스의 변수명과 일치해야 합니다.
        actionReferenceProp = serializedObject.FindProperty("actionReference");
        controlSchemeProp = serializedObject.FindProperty("controlScheme");
        keySpriteSOProp = serializedObject.FindProperty("keySpriteSO");
        keyIconProp = serializedObject.FindProperty("keyIcon");
        waitingSpriteProp = serializedObject.FindProperty("waitingSprite");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 키 리바인딩 설정 (KeyButton 전용)
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Key Rebinding Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(actionReferenceProp, new GUIContent("Action"));
        EditorGUILayout.PropertyField(controlSchemeProp, new GUIContent("Control Scheme"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Key Display", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(keySpriteSOProp, new GUIContent("Key Sprite SO"));
        EditorGUILayout.PropertyField(keyIconProp, new GUIContent("Key Icon"));
        EditorGUILayout.PropertyField(waitingSpriteProp, new GUIContent("Waiting Sprite"));

        serializedObject.ApplyModifiedProperties();

        // 나머지(사운드/버튼 로직/기본 Button 속성)는 UIButtonEditor가 그대로 그린다.
        base.OnInspectorGUI();
    }
}
