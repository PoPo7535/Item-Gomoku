using System;
using EnhancedUI.EnhancedScroller;
using TMPro;
using UnityEngine;

public class ChatCell : EnhancedScrollerCellView
{
    public RectTransform rectTransform;
    public TMP_Text chat;

    private readonly string _hostColor = "AB7477";
    private readonly string _clinerColor = "939EAE";
    private readonly string _noticeColor = "3C3C3C";
    public void SetCell(ChatData chatData)
    {
        string colorCode = chatData.type switch
        {
            ChatType.Host => _hostColor,
            ChatType.Client => _clinerColor,
            ChatType.Notice => _noticeColor,
            _ => throw new ArgumentOutOfRangeException()
        };
        if (chatData.type != ChatType.Notice)
        {
            chat.alignment = TextAlignmentOptions.Left;
            chat.text = $"<color=#{colorCode}>{chatData.name}</color> : {chatData.msg}";
        }
        else
        {
            chat.alignment = TextAlignmentOptions.Center;
            chat.text = $"<color=#{colorCode}>- {chatData.msg} -</color>";
        }
    }

    public static string GetMsg(ChatData chatData)
    {        
        if (chatData.type != ChatType.Notice)
            return $"{chatData.name} : {chatData.msg}";
        else
            return $"- {chatData.msg} -";
    }

    public void SetSpace()
    {
        chat.text = string.Empty;
    }
}
