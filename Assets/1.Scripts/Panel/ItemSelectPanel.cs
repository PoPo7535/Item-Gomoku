using UnityEngine;
using UnityEngine.UI;

public class ItemSelectPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private ItemToggle itemPrefab;
    [SerializeField] private GomokuItem[] itemScriptableObjects;
    [SerializeField] private Transform itemParent;
    [SerializeField, ReadOnly] private Toggle[] toggles;
    [SerializeField] private Button okBtn;
    private const int SelectMaxCount = 3;
    private int _currentSelectCount = 0;
    
    private void Start()
    {
        IntItems();
        SetToggleEvent();
        SetButtonEvent();
    }
    public void ActiveCg(bool isActive) => cg.ActiveCG(isActive);

    private void IntItems()
    {
        toggles = new Toggle[itemScriptableObjects.Length];
        for (var i = 0; i < itemScriptableObjects.Length; i++)
        {
            var itemScriptable = itemScriptableObjects[i];
            var itemToggle = Instantiate(itemPrefab, itemParent);
            toggles[i] = itemToggle.toggle;
            itemToggle.Set(itemScriptable.sprite);
        }
    }

    private void SetButtonEvent()
    {
        okBtn.onClick.AddListener(() =>
        {
            ActiveCg(false);
        });
    }
    
    private void SetToggleEvent()
    {
        okBtn.interactable = false;
        foreach (var toggle in toggles)
        {
            toggle.onValueChanged.AddListener((isOn) =>
            {
                if (isOn)
                    ++_currentSelectCount;
                else
                    --_currentSelectCount;
                _currentSelectCount.Log();
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
