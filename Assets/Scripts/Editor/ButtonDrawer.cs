#if UNITY_EDITOR
using System.Reflection;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MonoBehaviour), true)]
public class ButtonDrawer : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var targetObject = target as MonoBehaviour;
        var type = targetObject.GetType();
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var method in methods)
        {
            var attributes = method.GetCustomAttributes(typeof(ButtonAttribute), true);
            if (attributes.Length <= 0) 
                continue;
            var buttonName = method.Name;
            if (GUILayout.Button(buttonName))
                method.Invoke(targetObject, null);
        }
    }
}
#endif
