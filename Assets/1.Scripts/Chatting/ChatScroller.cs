using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EnhancedUI.EnhancedScroller;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

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

public class ChatScroller : MonoBehaviour,IEnhancedScrollerDelegate
{
    [SerializeField] private TMP_InputField _inputField;
    public EnhancedScrollerCellView myTextCellViewPrefab;
    public EnhancedScrollerCellView spacerCellViewPrefab;
    private readonly List<ChatData> _data = new();
    private float _totalCellSize = 0;
    private float _oldScrollPosition = 0;
    public EnhancedScroller scroller;
    public TMP_Text myText;
    public Vector2 CalTextSize(string text)
    {
        myText.text = text;
        return myText.GetPreferredValues(text, myText.rectTransform.rect.width, 0);
    }
    public void Start()
    {
        scroller.Delegate = this;
    }

    private void SendMsg(ChatData chatData)
    {
        scroller.ClearAll();

        _oldScrollPosition = scroller.ScrollPosition;
        scroller.ScrollPosition = 0;
        
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
            cellView = scroller.GetCellView(spacerCellViewPrefab) as ChatCell;
            cellView.name = "Space";
            cellView.SetSpace();
        }
        else
        {
            cellView = scroller.GetCellView(myTextCellViewPrefab) as ChatCell;
            cellView.name = "Cell " + dataIndex.ToString();
            cellView.SetCell(_data[dataIndex]);
        }

        return cellView;
    }
}
