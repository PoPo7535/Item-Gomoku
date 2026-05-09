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
    /// 모든 아이템 버튼의 클릭 가능 여부를 설정
    /// </summary>
    public void SetInteractable(bool isInteractable)
    {
        foreach (var t in _toggles)
        {
            if (t.toggle != null)
            {
                t.toggle.interactable = isInteractable;
            }
        }
    }

    public void ClearAllToggles()
    {
        _toggleGroup.SetAllTogglesOff(); // 토글그룹에 속한 모든 토글을 꺼버림
    }
}
