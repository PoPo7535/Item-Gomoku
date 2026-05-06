using System;
using System.Collections.Generic;
using EnhancedUI.EnhancedScroller;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ChatType
{
    Host,
    Client,
    Notice
}
public class ChatData
{
    public string name;
    public string msg;
    public Vector2 size;
    public ChatType type;
}

public class ChatScroller : NetworkBehaviour,IEnhancedScrollerDelegate, IPlayerLeft
{
    [SerializeField] private EnhancedScroller scroller;
    [SerializeField] private EnhancedScrollerCellView myTextCellViewPrefab;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button chatButton;
    [SerializeField] private TMP_Text helperText;
    private readonly List<ChatData> _data = new();
    private float _totalCellSize = 0;
    private float _oldScrollPosition = 0;
    private string _clientNickName = string.Empty;
    
    private Vector2 CalTextSize(string text)
    {
        helperText.text = text;
        return helperText.GetPreferredValues(text, helperText.rectTransform.rect.width, 0);
    }
    public void Start()
    {
        scroller.Delegate = this;
        chatButton.onClick.AddListener(SendChatEvent);
        inputField.onSubmit.AddListener(_ =>
        {
            SendChatEvent();
        });
    }

    private void SendChatEvent()
    {
        if (inputField.text == string.Empty)
            return;
        var tpye = Object.HasStateAuthority ? ChatType.Host : ChatType.Client;
        Rpc_SendChat(App.I.nickName, inputField.text, tpye);
        inputField.text = "";
        inputField.ActivateInputField();
    }
    public override void Spawned()
    {
        SendChat("Space",string.Empty, ChatType.Host); // 필수
        Rpc_SendChat(App.I.nickName, $"[{App.I.nickName}] 님이 입장하셨습니다.", ChatType.Notice);
    }


    private void SendChat(string name, string msg, ChatType type)
    {
        scroller.ClearAll();
        _oldScrollPosition = scroller.ScrollPosition;
        scroller.ScrollPosition = 0;
        var chatData = new ChatData
        {
            name = name, 
            msg = msg, 
            type = type
        };
        chatData.size = CalTextSize(ChatCell.GetMsg(chatData));
        _data.Add(chatData);

        ResizeScroller();

        scroller.JumpToDataIndex(
            _data.Count - 1, 
            1f, 
            1f,
            tweenType: EnhancedScroller.TweenType.easeInOutSine,
            tweenTime: 0.5f, 
            jumpComplete: ResetSpacer);
    }
    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void Rpc_SendChat(string name, string msg, ChatType type)
    {
        if(Object.HasStateAuthority && type == ChatType.Notice)
            _clientNickName = name;
        SendChat(name, msg, type);
    }
    private void ResetSpacer()
    {
        _data[0].size = new Vector2(_data[0].size.x,Mathf.Max(scroller.ScrollRectSize - _totalCellSize, 0));
        scroller.ReloadData(1.0f);
    }

    private void ResizeScroller()
    {
        var scrollRectSize = scroller.ScrollRectSize;

        var offset = _oldScrollPosition - scroller.ScrollSize;

        var rectTransform = scroller.GetComponent<RectTransform>();
        var size = rectTransform.sizeDelta;

        rectTransform.sizeDelta = new Vector2(size.x, float.MaxValue);

        _totalCellSize = scroller.padding.top + scroller.padding.bottom;
        for (var i = 1; i < _data.Count; i++)
            _totalCellSize += _data[i].size.y + (i < _data.Count - 1 ? scroller.spacing : 0);

        _data[0].size = new Vector2(_data[0].size.x, scrollRectSize);

        rectTransform.sizeDelta = size;

        scroller.ReloadData();

        scroller.ScrollPosition = (_totalCellSize - _data[^1].size.y) + offset;
    }


    public int GetNumberOfCells(EnhancedScroller scroller)
    {
        return _data.Count;
    }

    public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
    {
        return _data[dataIndex].size.y;
    }

    public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
    {
        ChatCell cellView;

        if (dataIndex == 0)
        {
            cellView = scroller.GetCellView(myTextCellViewPrefab) as ChatCell;
            cellView.name = "Space";
            cellView.SetSpace();
        }
        else
        {
            cellView = scroller.GetCellView(myTextCellViewPrefab) as ChatCell;
            cellView.name = "Cell " + dataIndex;
            cellView.SetCell(_data[dataIndex]);
        }

        return cellView;
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (App.I.Runner.LocalPlayer == player)
            return;
        SendChat(string.Empty, $"[{_clientNickName}] 님이 퇴장하셨습니다.", ChatType.Notice);
        _clientNickName = string.Empty;
    }
}
