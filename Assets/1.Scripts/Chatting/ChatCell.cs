using System;
using EnhancedUI.EnhancedScroller;
using TMPro;
using UnityEngine;

public class ChatCell : EnhancedScrollerCellView
{
    public RectTransform rectTransform;
    public TMP_Text chat;


    public void SetCell(ChatData chatData)
    {
        chat.text = GetMsg(chatData);
        // var size = chat.GetPreferredValues(chat.text, chat.rectTransform.rect.width, 0);
        // rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, size.y);
    }

    public static string GetMsg(ChatData chatData)
    {
        return $"{chatData.name} : {chatData.msg}";
    }

    public void SetSpace()
    {
        chat.text = string.Empty;
    }
}
