using System;
using TMPro;
using UnityEngine;
using Utility;
public enum InputMode
{
    Normal,         // 일반 착수
    UseTransparent, // 투명돌
    UseFakeStone,   // 가짜돌
    UseDetect       // 간파
}
public class GomokuItemManager : MonoBehaviour
{   
    public static GomokuItemManager I;
    public GomokuItem CurrentSelectedItem; // 선택된 아이템 담기
    public TMP_Text _test;
    // 현재 선턱된 아이템 모드 
    public InputMode CurrentMode = InputMode.Normal;
    // 이번 턴에 이미 아이템을 사용했는지 여부
    private bool _hasUsedItemInTurn = false;
    private void Awake()
    {
        I = this;
    }

    /// <summary>
    /// 아이템 UI 선택 시 호출될 함수
    /// </summary>
    public void SelectItem(GomokuItem item)
    {   
        if (_hasUsedItemInTurn) return;
        // 처음 부를 때 null 이면초기화
        if (item == null)
        {
            ResetSelection();
            return;
        }

        // 중복 선택 시 해제
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
            if (_hasUsedItemInTurn) return false;
            if (CurrentSelectedItem == null) return false;
            if (!GomokuManager.I.IsMyTurn()) return false;

            // 에러 방지: 아이템 정보를 미리 변수에 저장해둡니다.
            string usedItemName = CurrentSelectedItem.itemName;
            ItemType usedType = CurrentSelectedItem.type;

            bool success = false;
   
        // [수정] itemName 대신 Enum 타입으로 분기 처리
        switch (CurrentSelectedItem.type)
        {
            case ItemType.DoubleShow: // 더블 표시
                GomokuManager.I.RPC_UseDoubleMarkerItem(); // 완성
                success = true;
                break;

            case ItemType.FakeStone: // 가짜 돌
                CurrentMode = InputMode.UseFakeStone; // 완성
                success = true; 
                break;

            case ItemType.HideStone: // 착수 숨김 
                GomokuManager.I.RPC_UseHideMoveItem(); // 완성 
                success = true;
                break;

            case ItemType.SwapStone: // 돌 바꾸기
                GomokuManager.I.RPC_UseStoneSwapItem(GomokuManager.I.MyColor); // 완성 
                success = true;
                break;

            case ItemType.TimerDecreasing: // 타이머 감소
                GomokuManager.I.RPC_UseTimerReductionItem(); // 완성 
                success = true;
                break;

            case ItemType.TransparentStone: // 투명 돌
                CurrentMode = InputMode.UseTransparent; // 완성
                success = true;
                break;

            case ItemType.Detect: // 간파하기
                CurrentMode = InputMode.UseDetect; // 완성
                success = true;
                break;
            default:
                CurrentMode = InputMode.Normal;
                break;
        }

        if (success)
        {   
            _hasUsedItemInTurn = true;

            // 로그 출력을 가장 먼저 수행 (CurrentSelectedItem이 null이 되기 전)
            if (_test != null) _test.text = $"아이템 사용 완료 : {usedItemName}";

            // 즉시 발동형 아이템 처리
            if (usedType != ItemType.FakeStone && 
                usedType != ItemType.TransparentStone && 
                usedType != ItemType.Detect)
            {
                ConsumeItemUI(); 
            }
            else
            {
                // 좌표 선택형 아이템들의 경우, 여기서 null 처리를 하면 
                // 나중에 실제 착수 시점에 데이터가 없어 에러가 날 수 있습니다.
                // ConsumeItemUI()가 실제 착수 시(PlaceStoneProcess 등)에 호출되므로 
                // 여기서는 중복 처리를 하지 않도록 주의해야 합니다.
            }         
        }

        return success;
    }

    /// <summary>
    /// 아이템 초기화  + 모드 
    /// </summary>
    public void ResetSelection()
    {
        CurrentSelectedItem = null;
        CurrentMode = InputMode.Normal;
        if (_test != null) _test.text = "";
    }
    /// <summary>
    /// 턴이 바낄때 호출할 UI 아이템 초기화
    /// </summary>
    public void ResetTurnLimit()
    {
        _hasUsedItemInTurn = false;
        GomokuManager.I.ItemPanel.SetInteractable(true); // UI 다시 활성화
    }
    /// <summary>
    /// 아이템 사용시 또 사용못하게 잠구기
    /// </summary>
    public void ConsumeItemUI()
    {
        if (CurrentSelectedItem == null) return;

        GomokuManager.I.ItemPanel.HideUsedItem(CurrentSelectedItem); // 사용한 아이템 안보이게함
        // 패널의 모든 버튼을 클릭 차단
        GomokuManager.I.ItemPanel.ClearAllToggles();
        GomokuManager.I.ItemPanel.SetInteractable(false);

        CurrentSelectedItem = null;
        CurrentMode = InputMode.Normal;
    }

    public void FullReset()
    {
        _hasUsedItemInTurn = false;    
        CurrentSelectedItem = null;   
        CurrentMode = InputMode.Normal; // 확실하게 노멀 모드로!
        
        if (GomokuManager.I != null && GomokuManager.I.ItemPanel != null)
        {
            GomokuManager.I.ItemPanel.ClearAllToggles();
            GomokuManager.I.ItemPanel.SetInteractable(true);
        }
        
    }
}