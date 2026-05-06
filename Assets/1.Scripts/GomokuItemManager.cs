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
    /// <summary>
    /// 아이템 ui 선택시 호출될함수
    /// </summary>
    public void SelectItem(GomokuItem item)
    {
        if (item == null)
        {
            CurrentSelectedItem = null;
            if (_test != null) _test.text = $"아이템 선택 해제 : {CurrentSelectedItem}";
            return; // 여기서 함수를 끝냄
        }

   
        if (CurrentSelectedItem != null && CurrentSelectedItem.name == item.name)
        {
            CurrentSelectedItem = null;
            if (_test != null) _test.text = $"아이템 선택 해제 : {CurrentSelectedItem}";
            return;
        }

        CurrentSelectedItem = item;
        _test.text = $"아이템 선택  : {CurrentSelectedItem}";
    }
    /// <summary>
    /// 아이템 사용시 호출될 함수
    /// </summary>
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
                GomokuManager.I.RPC_UseDoubleMarkerItem(); // 미완
                _test.text = $"아이템 사용 : {CurrentSelectedItem.name}";
                success = true;
                break;

            case "가짜 돌":
                // 아이템 효과
                success = true;
                break;
            case "착수 숨김":
                GomokuManager.I.RPC_UseHideMoveItem(); // 완성
                _test.text = $"아이템 사용 : {CurrentSelectedItem.name}";
                success = true;
                break;
            case "돌 바꾸기":
                // 아이템 효과
                success = true;
                break;
            case "타이머 감소":
                GomokuManager.I.RPC_UseTimerReductionItem();// 완성
                _test.text = $"아이템 사용 : {CurrentSelectedItem.name}";
                success = true;
                break;
            case "투명 돌":
                // 아이템 효과
                success = true;
                break;
        }

        if (success)
        {
            CurrentSelectedItem = null;                    
        }


        return success;
    }
    // 상태초기화
    public void ResetSelection()
    {
        CurrentSelectedItem = null;
        _test.text = "";
    }
    // 아이템 사용후 ui 제거
    public void ConsumeItemUI()
    {
        if (CurrentSelectedItem == null) return;

        // UI 제거 (버튼 끄기 or Destroy)

        CurrentSelectedItem = null;

    }

}