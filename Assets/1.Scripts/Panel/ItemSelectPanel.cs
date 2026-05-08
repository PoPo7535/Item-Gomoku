using System;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class ItemSelectPanel : NetworkBehaviour
{
    [SerializeField] private CanvasGroup cg;
    [FormerlySerializedAs("itemPanel")]
    [Header("아이템")]
    [SerializeField] private ItemUsePanel _itemUsePanel;
    [SerializeField] private ItemToggle[] itemToggles;
    [SerializeField] private ItemToggle[] activeItemToggles;
    [SerializeField] private GomokuItem[] itemSO;
    private const int SelectMaxCount = 3;
    private int _currentSelectCount = 0;
    
    [Header("타이머")]
    [SerializeField] private Slider timerSlider;
    [SerializeField] private TMP_Text timerText;
    
    [Header("UI")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemInfoText;
    [Networked] private TickTimer Timer { get; set; }
    public float timeLimit = 30f;
    [Networked, OnChangedRender(nameof(TryActiveCg))] private NetworkBool ClientIsSelect { get; set; }
    [Networked, OnChangedRender(nameof(TryActiveCg))] private NetworkBool HostIsSelect { get; set; }
    private void TryActiveCg()
    {
        if (ClientIsSelect && HostIsSelect)
        {
            cg.interactable = true;
            ActiveCg(false);
            Timer = TickTimer.None;
            GomokuManager.I.StartGame();
        }
    }

    [Space]
    [SerializeField] private Button okBtn;
    
    private void Start()
    {
        SetItemToggles();
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
            _timeOut = false;
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
        foreach (var itemToggle in activeItemToggles)
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

    private bool _timeOut = false;
    private void CheckTimeOut()
    {
        if (_timeOut)
            return;
        while (_currentSelectCount < SelectMaxCount)
        {
            var randomValue = Random.Range(0, activeItemToggles.Length - 1);
            if (false == activeItemToggles[randomValue].toggle.isOn)
                activeItemToggles[randomValue].toggle.isOn = true;
            _timeOut = true;
        }

        if (_timeOut)
            okBtn.onClick.Invoke();
    }
    
    private void SetToggleEvent()
    {
        okBtn.interactable = false;
        foreach (var item in activeItemToggles)
        {
            item.toggle.onValueChanged.AddListener((isOn) =>
            {
                var block = item.toggle.colors;
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
                foreach (var item in activeItemToggles)
                    item.toggle.interactable = true;
                okBtn.interactable = false;
            }
            else
            {
                foreach (var item in activeItemToggles)
                    item.toggle.interactable = item.toggle.isOn;
                okBtn.interactable = true;
            }
        }
    }
    private void SetButtonEvent()
    {
        okBtn.onClick.AddListener(()=>
        {
            _timeOut = true;
            cg.interactable = false;
            var items = GetSelectItem();
            _itemUsePanel.Set(items);
            if (false == Object.HasStateAuthority)
                Rpc_Ready();
            else
                HostIsSelect = true;
        });
    }

    private void SetItemToggles()
    {
        for (var i = 0; i < itemToggles.Length; i++)
        {
            if (i < itemSO.Length)
            {
                var i1 = i;
                itemToggles[i].toggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn)
                    {
                        itemNameText.text = itemSO[i1].itemName;   
                        itemInfoText.text = itemSO[i1].description;   
                    }
                    else
                    {
                        itemNameText.text = string.Empty;   
                        itemInfoText.text = string.Empty;   
                    }

                });
                itemToggles[i].Set(itemSO[i]);
                
            }
            else
            {
                itemToggles[i].gameObject.SetActive(false);
            }
        }

        activeItemToggles = itemToggles.Where(item => item.gameObject.activeSelf).ToArray();
    }
}
