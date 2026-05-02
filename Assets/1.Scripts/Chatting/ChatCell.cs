using PolyAndCode.UI;
using TMPro;
using UnityEngine;

public class ChatCell : MonoBehaviour, ICell
{
    public RectTransform rectTransform;
    public TMP_Text chat;


    public void SetCell(ChatInfo chatInfo)
    {
        chat.text = $"{chatInfo.Name} : {chatInfo.Msg}";
        var size = chat.GetPreferredValues(chat.text, chat.rectTransform.rect.width, 0);
        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, size.y);
    }
}
