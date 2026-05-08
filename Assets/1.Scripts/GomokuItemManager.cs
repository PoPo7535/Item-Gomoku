using System;
using TMPro;
using UnityEngine;
using Utility;

public class GomokuItemManager : MonoBehaviour
{   
    public static GomokuItemManager I;
    public GomokuItem CurrentSelectedItem; // 선택된 아이템 담기
    public TMP_Text _test;
    
    private void Awake()
    {
        I = this;
    }

    /// <summary>
    /// 아이템 UI 선택 시 호출될 함수
    /// </summary>
    public void SelectItem(GomokuItem item)
    {
        if (item == null)
        {
            ResetSelection();
            return;
        }

        // [수정] 이름 대신 Enum(type)으로 비교하여 중복 선택 시 해제
        if (CurrentSelectedItem != null && CurrentSelectedItem.type == item.type)
        {
            ResetSelection();
            if (_test != null) _test.text = "아이템 선택 해제";
            return;
        }

        CurrentSelectedItem = item;
        if (_test != null) _test.text = $"아이템 선택 : {CurrentSelectedItem.itemName}";
    }

    /// <summary>
    /// 아이템 사용 시 호출될 함수
    /// </summary>
    public bool TryUseItem(int x, int z) 
    {
        if (CurrentSelectedItem == null)
            return false;

        if (!GomokuManager.I.IsMyTurn())
        {
            Debug.Log("내 턴 아님");
            return false;
        }

        bool success = false;
   
        // [수정] itemName 대신 Enum 타입으로 분기 처리
        switch (CurrentSelectedItem.type)
        {
            case ItemType.DoubleShow: // 더블 표시
                GomokuManager.I.RPC_UseDoubleMarkerItem();
                success = true;
                break;

            case ItemType.FakeStone: // 가짜 돌
                // 
                success = true; 
                break;

            case ItemType.HideStone: // 착수 숨김 
                GomokuManager.I.RPC_UseHideMoveItem();
                success = true;
                break;

            case ItemType.SwapStone: // 돌 바꾸기
                GomokuManager.I.RPC_UseStoneSwapItem();
                success = true;
                break;

            case ItemType.TimerDecreasing: // 타이머 감소
                GomokuManager.I.RPC_UseTimerReductionItem();
                success = true;
                break;

            case ItemType.TransparentStone: // 투명 돌
                //
                success = true;
                break;

            case ItemType.Detect: // 간파하기
                // 필요시 추가 로직 작성
                success = true;
                break;
        }

        if (success)
        {
            if (_test != null) _test.text = $"아이템 사용 완료 : {CurrentSelectedItem.itemName}";
            CurrentSelectedItem = null;                    
        }

        return success;
    }

    // 상태 초기화
    public void ResetSelection()
    {
        CurrentSelectedItem = null;
        if (_test != null) _test.text = "";
    }

    // 아이템 사용 후 UI 제거 로직 (필요시 구현)
    public void ConsumeItemUI()
    {
        if (CurrentSelectedItem == null) return;
        // UI 버튼 비활성화 등의 처리
        CurrentSelectedItem = null;
    }
}