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
        if (_hasUsedItemInTurn) return false; // 아이템을 사용했다면
        if (CurrentSelectedItem == null) return false; // 현재선택된 아이템이 없다면
        if (!GomokuManager.I.IsMyTurn()) return false; // 현재 내턴이라면

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
                GomokuManager.I.RPC_UseStoneSwapItem(); // 완성 
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
            if (_test != null) _test.text = $"아이템 사용 완료 : {CurrentSelectedItem.itemName}";
            if (CurrentSelectedItem.type == ItemType.FakeStone || CurrentSelectedItem.type == ItemType.TransparentStone)
            {
                
            }
            else
            {
                CurrentSelectedItem = null;
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