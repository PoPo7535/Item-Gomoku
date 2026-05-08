using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ItemUsePanel : MonoBehaviour
{
    [SerializeField] private RectTransform gameView;
    [SerializeField] private ToggleGroup _toggleGroup;
    [SerializeField] private Transform bg;
    [SerializeField] private List<ItemToggle> _toggles = new();
    private List<ItemToggle> _activeToggles = new();
    public void Set(GomokuItem[] items)
    {
        ActivePanel(true);
        for (var i = 0; i < _toggles.Count; i++)
        {
            if (i < items.Length)
            {
                var item = items[i];
                _toggles[i].Set(item);
                _toggles[i].toggle.group = _toggleGroup;
                _toggles[i].toggle.gameObject.SetActive(true);
                _toggles[i].toggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn)
                    {
                        GomokuItemManager.I.SelectItem(item);
                    }
                    else if (GomokuItemManager.I.CurrentSelectedItem == item)
                    {
                        GomokuItemManager.I.SelectItem(null);
                    }
                });
            }
            else
            {
                _toggles[i].gameObject.SetActive(false);
            }
        }
        _activeToggles = _toggles.Where(toggle => toggle.toggle.isOn).ToList();
    }

    private void ActivePanel(bool isActive)
    {
        var rect = transform as RectTransform;
        rect.DOAnchorPosY(isActive ? 100 : -100, 0.5f);
        gameView.DOAnchorPosY(isActive ? 80 : 0, 0.5f);
    }
    /// <summary>
    /// 턴 바낄때 토글 초기화
    /// </summary>
    public void ClearAllToggles()
    {
        foreach (var t in _toggles)
        {
            t.toggle.isOn = false; 
        }
    }
}
