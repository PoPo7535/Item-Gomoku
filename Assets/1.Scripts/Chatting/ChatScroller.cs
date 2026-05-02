using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PolyAndCode.UI;
using UnityEngine;

public enum ChatType
{
    Host,
    Client,
    Notice
}
public struct ChatInfo
{
    public string Name;
    public string Msg;
    public ChatType Type;
}

public class ChatScroller : MonoBehaviour, IRecyclableScrollRectDataSource
{
    [SerializeField] private RecyclableScrollRect _recyclableScrollRect;
    private readonly List<ChatInfo> _contactList = new();

    public void Awake()
    {
        _recyclableScrollRect.DataSource = this;
    }

    public async void Start()
    {
        await new WaitForSecondsRealtime(1);
        _contactList.Add(new ChatInfo()
        {
            Name = "AA",
            Msg = "12345667ASDFG",
            Type = ChatType.Host
        });
        await new WaitForSecondsRealtime(1);
        _recyclableScrollRect.ReloadData();

        _contactList.Add(new ChatInfo()
        {
            Name = "BB",
            Msg = "12345667ASDFG222222222222222222222222222",
            Type = ChatType.Client
        });
        await new WaitForSecondsRealtime(1);
        _recyclableScrollRect.ReloadData();

        _contactList.Add(new ChatInfo()
        {
            Name = "AA",
            Msg = "12345667ASDFG",
            Type = ChatType.Notice
        });
        _recyclableScrollRect.ReloadData();
    }

    public int GetItemCount() => _contactList.Count;
    

    public void SetCell(ICell cell, int index)
    {
        var item = cell as ChatCell;
        item.SetCell(_contactList[index]);
    }
}
