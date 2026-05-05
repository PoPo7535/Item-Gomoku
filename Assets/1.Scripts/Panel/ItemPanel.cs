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
                if (isOn) // 클릭 시  선택된 아이템 정보 넘김
                {
                    GomokuItemManager.I.SelectItem(item);    

                }else if (GomokuItemManager.I.CurrentSelectedItem == item)// 한번더 클릭시 해제
                {
                    GomokuItemManager.I.SelectItem(null);
                }
                
            });
        }
    }
}
