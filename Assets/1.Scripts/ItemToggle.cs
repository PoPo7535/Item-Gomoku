using UnityEngine;
using UnityEngine.UI;

public class ItemToggle : MonoBehaviour
{
    public Toggle toggle;
    [SerializeField] private Image image;

    public void Set(Sprite sprite)
    {
        image.sprite = sprite;
    }
}
