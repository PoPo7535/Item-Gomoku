using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ItemPanel : MonoBehaviour
{
    [SerializeField] private ItemToggle _itemPrefab;
    [SerializeField] private ToggleGroup _toggleGroup;
    [SerializeField] private Transform bg;
    private List<Toggle> _toggles = new List<Toggle>();
    public void Set(GomokuItem[] items)
    {
        foreach (var item in items)
        {
            var itemObj = Instantiate(_itemPrefab, bg);
            itemObj.Set(item);
            itemObj.toggle.group = _toggleGroup;
            _toggles.Add(itemObj.toggle);
            itemObj.toggle.onValueChanged.AddListener((isOn) =>
            {
                if (isOn)
                {
                    GomokuItemManager.I.SelectItem(item);
                    "선택".Log();    
                }
                else if (GomokuItemManager.I.CurrentSelectedItem == item)
                {
                    GomokuItemManager.I.SelectItem(null);
                    "해제".Log();
                }
                
            });
        }
    }
    // 턴이 바뀔 때 호출할 함수
    public void ClearAllToggles()
    {
        foreach (var t in _toggles)
        {
            t.isOn = false; 
        }
    }
}
