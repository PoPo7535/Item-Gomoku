using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 전용 일반 아이템 보유 상태와 사용 정책을 관리함.
/// </summary>
public partial class GomokuManager
{
    [Header("AI 아이템")]
    [SerializeField, Min(0)] private int _aiRandomItemGrantCount = 3;
    [SerializeField, Min(0)] private int _aiItemMinStoneCount = 4;
    [SerializeField, Range(0f, 1f)] private float _aiBeforeSearchItemUseChance = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _aiBeforePlaceItemUseChance = 0.25f;

    private static readonly ItemType[] AiRandomItemPool =
    {
        ItemType.TimerDecreasing,
        ItemType.HideStone,
        ItemType.DoubleShow,
        ItemType.SwapStone,
    };

    private readonly List<ItemType> _aiItemInventory = new List<ItemType>();
    private bool _hasAiUsedItemThisTurn;

    /// <summary>
    /// AI 아이템 보유 상태를 새 게임 기준으로 초기화함.
    /// </summary>
    private void ResetAiItemState()
    {
        _aiItemInventory.Clear();
        _hasAiUsedItemThisTurn = false;

        if (!IsAiItemFeatureEnabled())
        {
            return;
        }

        GiveRandomAiItems();
    }

    /// <summary>
    /// AI 턴마다 아이템 사용 제한 상태를 초기화함.
    /// </summary>
    private void BeginAiItemTurn()
    {
        _hasAiUsedItemThisTurn = false;
    }

    /// <summary>
    /// AI 탐색 전에 사용할 수 있는 아이템을 우선순위에 따라 시도함.
    /// </summary>
    /// <returns>아이템 사용 성공 여부.</returns>
    private bool TryUseAiItemBeforeSearch()
    {
        if (!CanUseAiItemThisTurn())
        {
            return false;
        }

        if (TryUseAiDetectOnKnownSpecialStone())
        {
            _hasAiUsedItemThisTurn = true;
            return true;
        }

        if (TryUseAiTimerReductionItem())
        {
            _hasAiUsedItemThisTurn = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// AI 착수 전에 사용할 수 있는 착수형 아이템을 우선순위에 따라 시도함.
    /// </summary>
    /// <param name="fakeX">더블 표시 가짜 마커 X 좌표.</param>
    /// <param name="fakeZ">더블 표시 가짜 마커 Z 좌표.</param>
    /// <returns>아이템 사용 성공 여부.</returns>
    private bool TryUseAiItemBeforePlace(out int fakeX, out int fakeZ)
    {
        fakeX = -1;
        fakeZ = -1;

        if (!CanUseAiItemThisTurn() || !HasReachedAiItemMinStoneCount())
        {
            return false;
        }

        if (TryUseAiHideStoneItem())
        {
            _hasAiUsedItemThisTurn = true;
            return true;
        }

        if (TryUseAiDoubleShowItem(out fakeX, out fakeZ))
        {
            _hasAiUsedItemThisTurn = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// AI 아이템 기능을 사용할 수 있는 설정인지 확인함.
    /// </summary>
    /// <returns>AI 아이템 기능 사용 가능 여부.</returns>
    private bool IsAiItemFeatureEnabled()
    {
        return App.I != null &&
               App.I.PlayMode == GamePlayMode.AI &&
               _enableAiItemAwareness;
    }

    /// <summary>
    /// 현재 AI 턴에서 아이템을 사용할 수 있는지 확인함.
    /// </summary>
    /// <returns>아이템 사용 가능 여부.</returns>
    private bool CanUseAiItemThisTurn()
    {
        return IsAiItemFeatureEnabled() && !_hasAiUsedItemThisTurn;
    }

    /// <summary>
    /// AI 일반 아이템 pool에서 중복 없이 랜덤 지급함.
    /// </summary>
    private void GiveRandomAiItems()
    {
        List<ItemType> availableItems = new List<ItemType>(AiRandomItemPool);
        int grantCount = Mathf.Min(_aiRandomItemGrantCount, availableItems.Count);

        for (int i = 0; i < grantCount; i++)
        {
            int randomIndex = Random.Range(0, availableItems.Count);
            _aiItemInventory.Add(availableItems[randomIndex]);
            availableItems.RemoveAt(randomIndex);
        }
    }

    /// <summary>
    /// AI가 타이머 감소 아이템 사용을 시도함.
    /// </summary>
    /// <returns>타이머 감소 아이템 사용 성공 여부.</returns>
    private bool TryUseAiTimerReductionItem()
    {
        if (!HasReachedAiItemMinStoneCount() ||
            !ShouldUseAiRandomItem(_aiBeforeSearchItemUseChance) ||
            !ConsumeAiItem(ItemType.TimerDecreasing))
        {
            return false;
        }

        IsTimerHalfEffect = true;
        Debug.Log("<color=red>[AI 아이템]</color> AI가 타이머 감소 아이템을 사용했습니다.");
        return true;
    }

    /// <summary>
    /// AI가 착수 숨김 아이템 사용을 시도함.
    /// </summary>
    /// <returns>착수 숨김 아이템 사용 성공 여부.</returns>
    private bool TryUseAiHideStoneItem()
    {
        if (!ShouldUseAiRandomItem(_aiBeforePlaceItemUseChance) ||
            !ConsumeAiItem(ItemType.HideStone))
        {
            return false;
        }

        _shouldHideNextMarker = true;
        Debug.Log("<color=yellow>[AI 아이템]</color> AI가 착수 숨김 아이템을 사용했습니다.");
        return true;
    }

    /// <summary>
    /// AI가 더블 표시 아이템 사용을 시도함.
    /// </summary>
    /// <param name="fakeX">가짜 마커 X 좌표.</param>
    /// <param name="fakeZ">가짜 마커 Z 좌표.</param>
    /// <returns>더블 표시 아이템 사용 성공 여부.</returns>
    private bool TryUseAiDoubleShowItem(out int fakeX, out int fakeZ)
    {
        fakeX = -1;
        fakeZ = -1;

        if (!ShouldUseAiRandomItem(_aiBeforePlaceItemUseChance) ||
            !_aiItemInventory.Contains(ItemType.DoubleShow))
        {
            return false;
        }

        (int x, int z) randomStone = GetRandomExistStone(AiStoneColor);
        if (randomStone.x < 0 || randomStone.z < 0)
        {
            return false;
        }

        if (!ConsumeAiItem(ItemType.DoubleShow))
        {
            return false;
        }

        IsDoubleMarkerEffect = true;
        fakeX = randomStone.x;
        fakeZ = randomStone.z;
        Debug.Log("<color=yellow>[AI 아이템]</color> AI가 더블 표시 아이템을 사용했습니다.");
        return true;
    }

    /// <summary>
    /// AI 인벤토리에서 지정 아이템을 사용 처리함.
    /// </summary>
    /// <param name="itemType">사용할 아이템 타입.</param>
    /// <returns>아이템 소비 성공 여부.</returns>
    private bool ConsumeAiItem(ItemType itemType)
    {
        return _aiItemInventory.Remove(itemType);
    }

    /// <summary>
    /// AI 일반 아이템 사용 확률을 통과했는지 확인함.
    /// </summary>
    /// <param name="chance">사용 확률.</param>
    /// <returns>확률 통과 여부.</returns>
    private bool ShouldUseAiRandomItem(float chance)
    {
        return Random.value <= chance;
    }

    /// <summary>
    /// AI 일반 아이템 사용이 허용되는 초반 수 제한을 넘었는지 확인함.
    /// </summary>
    /// <returns>일반 아이템 사용 가능 수 도달 여부.</returns>
    private bool HasReachedAiItemMinStoneCount()
    {
        if (_logic == null || _logic.Board == null)
        {
            return false;
        }

        int stoneCount = 0;
        int boardSize = GetBoardSize();
        for (int x = 0; x < boardSize; x++)
        {
            for (int z = 0; z < boardSize; z++)
            {
                if (_logic.Board[x, z].Color == StoneColor.None)
                {
                    continue;
                }

                stoneCount++;
                if (stoneCount >= _aiItemMinStoneCount)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
