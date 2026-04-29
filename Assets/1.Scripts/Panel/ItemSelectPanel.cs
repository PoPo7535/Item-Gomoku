using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSelectPanel : NetworkBehaviour
{
    [SerializeField] private CanvasGroup cg;
    [Header("아이템")]
    [SerializeField] private ItemPanel itemPanel;
    [SerializeField] private GomokuItem[] itemSO;
    [SerializeField] private ItemToggle itemPrefab;
    private ItemToggle[] _toggles;
    [SerializeField] private Transform itemParent;
    private const int SelectMaxCount = 3;
    private int _currentSelectCount = 0;
    
    [Header("타이머")]
    [SerializeField] private Slider timerSlider;
    [SerializeField] private TMP_Text timerText;
    [Networked] private TickTimer Timer { get; set; }
    public float timeLimit = 30f;
    [Networked, OnChangedRender(nameof(TryActiveCg))] private NetworkBool ClientIsSelect { get; set; }
    [Networked, OnChangedRender(nameof(TryActiveCg))] private NetworkBool HostIsSelect { get; set; }
    private void TryActiveCg()
    {
        if (ClientIsSelect && HostIsSelect)
        {
            ActiveCg(false);
            GomokuManager.I.StartGame();
        }
    }

    [Space]
    [SerializeField] private Button okBtn;
    
    private void Start()
    {
        SetItems();
        SetToggleEvent();
        SetButtonEvent();
    }
   
    public void LateUpdate()
    {
        if (Timer.Expired(App.I.Runner))
        {
            CheckTimeOut();
            return;
        }

        var time = App.I.TickTimerRemainingTime(Timer);
        timerText.text = $"{time:0.0}";
        timerSlider.value = time / timeLimit;
    }
    public void ActiveCg(bool isActive)
    {
        if (isActive)
        {
            ClientIsSelect = false;
            HostIsSelect = false;
        }
        cg.ActiveCG(isActive);
        Timer = isActive ? TickTimer.CreateFromSeconds(App.I.Runner, timeLimit) : TickTimer.None;
    }
    [Rpc(RpcSources.All,RpcTargets.All,HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void Rpc_Ready()
    {
        ClientIsSelect = true;
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

    private void CheckTimeOut()
    {
        var check = false;
        while (_currentSelectCount < SelectMaxCount)
        {
            var randomValue = Random.Range(0, _toggles.Length - 1);
            if (false == _toggles[randomValue].toggle.isOn)
            {
                _toggles[randomValue].toggle.isOn = true;
            }

            check = true;
        }

        if (check)
            okBtn.onClick.Invoke();
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
    private void SetButtonEvent()
    {
        okBtn.onClick.AddListener(()=>
        {
            var items = GetSelectItem();
            itemPanel.Set(items);
            if (false == Object.HasStateAuthority)
                Rpc_Ready();
            else
                HostIsSelect = true;
            Timer = TickTimer.None;
        });
    }

    private void SetItems()
    {
        _toggles = new ItemToggle[itemSO.Length];
        for (var i = 0; i < itemSO.Length; i++)
        {
            var itemToggle = Instantiate(itemPrefab, itemParent);
            _toggles[i] = itemToggle;
            itemToggle.Set(itemSO[i]);
        }
    }
}
