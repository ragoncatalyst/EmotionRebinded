using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(SButton))]
public class SButtonEditor : ButtonEditor
{
    SerializedProperty onButtonDown;
    SerializedProperty onButtonUp;

    protected override void OnEnable()
    {
        base.OnEnable();
        onButtonDown = serializedObject.FindProperty("OnButtonDown");
        onButtonUp = serializedObject.FindProperty("OnButtonUp");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pointer Events", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(onButtonDown);
        EditorGUILayout.PropertyField(onButtonUp);

        serializedObject.ApplyModifiedProperties();
    }
}