using System.Collections.Generic;
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

    public void Start()
    {
        GomokuManager.I.PlayEvents.Add((isPlay) =>
        {
            if (false == isPlay)
                ActivePanel(false);
        });
    }

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
                _toggles[i].toggle.onValueChanged.RemoveListener(ToggleAction); 
                _toggles[i].toggle.onValueChanged.RemoveAllListeners(); // 전에 등록되어있는 이벤트 싹다제거
                _toggles[i].toggle.onValueChanged.AddListener(ToggleAction);
                void ToggleAction(bool isOn)
                {
                    if (isOn)
                    {
                        GomokuItemManager.I.SelectItem(item);
                    }
                    else if (GomokuItemManager.I.CurrentSelectedItem == item)
                    {
                        GomokuItemManager.I.SelectItem(null);
                    }
                }
            }
            else
            {
                _toggles[i].gameObject.SetActive(false);
            }
        }
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
    /// <summary>
    /// 특정 아이템 찾아 끄기 
    /// </summary>
    public void HideUsedItem(GomokuItem item)
    {
        if (item == null) return;

        foreach (var t in _toggles)
        {
            
            if (t.gameObject.activeSelf && t.gomokuItem == item)
            {
                t.gameObject.SetActive(false); // UI에서 아예 안 보이게 처리
                break;
            }
        }
    }
}
