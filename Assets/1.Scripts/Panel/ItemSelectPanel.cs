using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSelectPanel : NetworkBehaviour
{
    [SerializeField] private CanvasGroup cg;
    private ItemToggle[] _toggles;
    [Header("아이템")]
    [SerializeField] private ItemPanel itemPanel;
    [SerializeField] private GomokuItem[] itemSO;
    [SerializeField] private ItemToggle itemPrefab;
    [SerializeField] private Transform itemParent;
    
    [Header("타이머")]
    [SerializeField] private Slider timerSlider;
    [SerializeField] private TMP_Text timerText;
    [Networked] public TickTimer Timer { get; set; }
    public float timeLimit = 30f;
    
    [Space]
    [SerializeField] private Button okBtn;
    private const int SelectMaxCount = 3;
    private int _currentSelectCount = 0;
    
    private void Start()
    {
        IntItems();
        SetToggleEvent();
        SetButtonEvent();
    }
    public void LateUpdate()
    {
        var time = App.I.TickTimerRemainingTime(Timer);
        timerText.text = $"{time:0.0}";
        timerSlider.value = time/ timeLimit;
    }

    public void ActiveCg(bool isActive)
    {
        cg.ActiveCG(isActive);
        Timer = isActive ? TickTimer.CreateFromSeconds(App.I.Runner, timeLimit) : TickTimer.None;
    }

    private GomokuItem[] GetSelectItem()
    {
        var items = new GomokuItem[SelectMaxCount];
        var count = 0;
        foreach (var itemToggle in _toggles)
        {
            if (itemToggle.toggle.isOn)
            {
                items[count] = itemToggle.gomokuItem;
                itemToggle.toggle.isOn = false;
                ++count;
            }
        }

        return items;
    }
    
    private void IntItems()
    {
        _toggles = new ItemToggle[itemSO.Length];
        for (var i = 0; i < itemSO.Length; i++)
        {
            var itemToggle = Instantiate(itemPrefab, itemParent);
            _toggles[i] = itemToggle;
            itemToggle.Set(itemSO[i]);
        }
    }

    private void SetButtonEvent()
    {
        okBtn.onClick.AddListener(() =>
        {
            ActiveCg(false);
            GomokuManager.I.StartGame();
            var items = GetSelectItem();
            items.Length.Log();
            itemPanel.Set(items);
        });
    }
    
    private void SetToggleEvent()
    {
        okBtn.interactable = false;
        foreach (var item in _toggles)
        {
            item.toggle.onValueChanged.AddListener((isOn) =>
            {
                var block = item.toggle.colors;
                block.normalColor = isOn ? new Color32(150, 150, 150, 255) : Color.white;
                item.toggle.colors = block;
                if (isOn)
                    ++_currentSelectCount;
                else
                    --_currentSelectCount;
                if (_currentSelectCount > SelectMaxCount)
                {
                    item.toggle.isOn = false;
                    return;
                }
                ActiveInteractable(_currentSelectCount != SelectMaxCount);
            });
        }

        void ActiveInteractable(bool active)
        {
            if (active)
            {
                foreach (var item in _toggles)
                    item.toggle.interactable = true;
                okBtn.interactable = false;
            }
            else
            {
                foreach (var item in _toggles)
                    item.toggle.interactable = item.toggle.isOn;
                okBtn.interactable = true;
            }
        }
    }
}
