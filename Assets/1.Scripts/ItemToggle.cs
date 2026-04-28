using UnityEngine;
using UnityEngine.UI;

public class ItemToggle : MonoBehaviour
{
    public Toggle toggle;
    [SerializeField] private Image image;
    [HideInInspector] public GomokuItem gomokuItem;
    public void Set(GomokuItem gomokuItem)
    {
        this.gomokuItem = gomokuItem;
        image.sprite = this.gomokuItem.sprite;
    }
}
