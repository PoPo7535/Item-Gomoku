using System;
using TMPro;
using UnityEngine;
using Utility;
public class GomokuItemManager : MonoBehaviour
{   
    public static GomokuItemManager I;
    public GomokuItem CurrentSelectedItem; // 선택된 아이템 여기에 담자
    public TMP_Text _test;
    
    private void Awake()
    {
        I = this;
    }
    
    public void SelectItem(GomokuItem item)
    {
        if (item == null)
        {
            CurrentSelectedItem = null;
            if (_test != null) _test.text = $"아이템 선택 해제 : {CurrentSelectedItem}";
            return; // 여기서 함수를 끝냄
        }

        // 2. 그 다음 기존 로직 수행
        if (CurrentSelectedItem != null && CurrentSelectedItem.name == item.name)
        {
            CurrentSelectedItem = null;
            if (_test != null) _test.text = $"아이템 선택 해제 : {CurrentSelectedItem}";
            return;
        }

        CurrentSelectedItem = item;
        _test.text = $"아이템 선택  : {CurrentSelectedItem}";
    }

    public bool TryUseItem(int x, int z) // 실제 아이템 사용 
    {
        if (CurrentSelectedItem == null)
            return false;

        if (!GomokuManager.I.IsMyTurn()) // 자기턴 아닐때 사용방지 사실없어도되긴함
        {
            Debug.Log("내 턴 아님");
            return false;
        }

        bool success = false;
   
        switch (CurrentSelectedItem.name)
        {
            case "더블 표시":
                GomokuManager.I.RemoveStoneProcess(x, z);
                _test.text = $"아이템 사용 : {CurrentSelectedItem.name}";
                success = true;
                break;

            case "가짜 돌":
                // 아이템 효과
                success = true;
                break;

            case "착수 숨김":
                GomokuManager.I.RPC_UseHideMoveItem();
                _test.text = $"아이템 사용 : {CurrentSelectedItem.name}";
                success = true;
                break;
            case "돌 바꾸기":
                // 아이템 효과
                success = true;
                break;
            case "타이머 감소":
                // 아이템 효과
                success = true;
                break;
            case "투명 돌":
                // 아이템 효과
                success = true;
                break;
        }

        if (success)

            CurrentSelectedItem = null;

        return success;
    }
    // 상태초기화
    public void ResetSelection()
    {
        CurrentSelectedItem = null;
        // _test.text = "";
    }

}