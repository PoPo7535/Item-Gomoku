using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ItemSelectPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private GameObject itemPrefab;
    [SerializeField, ReadOnly] private Toggle[] toggles;
    [SerializeField] private Button okBtn;
    private const int SelectMaxCount = 3;
    private int _currentSelectCount = 0;
    
    private void Start()
    {
        SetToggleEvent();
        SetButtonEvent();
    }

    public void ActiveCg(bool isActive) => cg.ActiveCG(isActive);

    private void SetButtonEvent()
    {
        okBtn.onClick.AddListener(() =>
        {
            ActiveCg(false);
        });
    }
    
    private void SetToggleEvent()
    {
        foreach (var toggle in toggles)
        {
            toggle.onValueChanged.AddListener((isOn) =>
            {
                if (isOn)
                    ++_currentSelectCount;
                else
                    --_currentSelectCount;
                if (_currentSelectCount > SelectMaxCount)
                {
                    toggle.isOn = false;
                    return;
                }
                ActiveInteractable(_currentSelectCount != SelectMaxCount);
            });
        }

        void ActiveInteractable(bool active)
        {
            if (active)
            {
                foreach (var toggle in toggles)
                    toggle.interactable = true;
                okBtn.interactable = false;
            }
            else
            {
                foreach (var toggle in toggles)
                    toggle.interactable = toggle.isOn;
                okBtn.interactable = true;
            }
        }
    }
}
