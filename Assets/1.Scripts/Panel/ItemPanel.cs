using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ItemPanel : MonoBehaviour
{
    [SerializeField] private ItemToggle _itemPrefab;
    [SerializeField] private ToggleGroup _toggleGroup;
    [SerializeField] private Transform bg;
    public void Set(GomokuItem[] items)
    {
        foreach (var item in items)
        {
            var itemObj = Instantiate(_itemPrefab, bg);
            itemObj.Set(item);
            itemObj.toggle.group = _toggleGroup;
            itemObj.toggle.onValueChanged.AddListener((isOn) =>
            {
                
            });
        }
    }
}
