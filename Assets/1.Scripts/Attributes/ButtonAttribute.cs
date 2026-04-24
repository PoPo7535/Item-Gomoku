using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Method)]
public class ButtonAttribute : PropertyAttribute
{
    public string MethodName { get; private set; }

    public ButtonAttribute(string methodName = null)
    {
        MethodName = methodName;
    }
}