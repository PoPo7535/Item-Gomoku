using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SerializableDic<,>.Pair), true)]
public class DictionaryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var keyProp = property.FindPropertyRelative("key");
        var valueProp = property.FindPropertyRelative("value");

        position.height = EditorGUIUtility.singleLineHeight;

        var spacing = 4f;
        var keyWidth = position.width * 0.45f;
        var valueWidth = position.width * 0.55f - spacing;

        var keyRect = new Rect(position.x, position.y, keyWidth, position.height);
        var valueRect = new Rect(position.x + keyWidth + spacing, position.y, valueWidth, position.height);

        EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
        EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
