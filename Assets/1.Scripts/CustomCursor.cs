using UnityEngine;
using Utility;

public class CustomCursor : Singleton<CustomCursor>
{
    public Transform cursorImageTransform;
    void Start()
    {
        Cursor.visible = false;
    }

    void Update()
    {
        if (cursorImageTransform != null)
        {
            cursorImageTransform.position = Input.mousePosition;
        }
    }
}
