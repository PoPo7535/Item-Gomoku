using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 전용 일반 아이템 보유 상태와 사용 정책을 관리함.
/// 플레이어 UI 선택 상태와 분리된 AI 전용 인벤토리/정책 계층임.
/// </summary>
public partial class GomokuManager
{
    [Header("AI 아이템")]
    [SerializeField, Min(0)] private int _aiRandomItemGrantCount = 3;
    [SerializeField, Min(0)] private int _aiItemMinStoneCount = 4;
    [SerializeField, Range(0f, 1f)] private float _aiBeforeSearchItemUseChance = 0.125f;   // 탐색 전 일반 아이템 사용 확률이며 보드 평가 점수와 무관함.
    [SerializeField, Range(0f, 1f)] private float _aiBeforePlaceItemUseChance = 0.125f;    // 착수 전 일반 아이템 사용 확률이며 임계값 점수가 아님.

    private const int AiSpecialStoneSearchRange = 2;
    private const int AiSpecialStoneMoveProximityScore = 4;

    private static readonly ItemType[] AiRandomItemPool =
    {
        ItemType.TimerDecreasing,
        ItemType.HideStone,
        ItemType.TransparentStone,
        ItemType.FakeStone,
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

        // BeforeSearch는 Detect를 먼저 보고, 조건이 안 맞으면 타이머 감소만 검토함.
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
    /// <param name="move">AI가 적용하려는 정상 착수 후보.</param>
    /// <param name="fakeX">더블 표시 가짜 마커 X 좌표.</param>
    /// <param name="fakeZ">더블 표시 가짜 마커 Z 좌표.</param>
    /// <returns>아이템 사용 성공 여부.</returns>
    private bool TryUseAiItemBeforePlace(GomokuMove move, out int fakeX, out int fakeZ)
    {
        fakeX = -1;
        fakeZ = -1;

        if (!CanUseAiItemThisTurn() || !HasReachedAiItemMinStoneCount())
        {
            return false;
        }

        // BeforePlace는 정상 탐색 결과에만 적용되고 fallback 착수에는 얹지 않음.
        if (TryUseAiHideStoneItem())
        {
            _hasAiUsedItemThisTurn = true;
            return true;
        }

        if (TryUseAiTransparentStoneBeforePlace(move))
        {
            _hasAiUsedItemThisTurn = true;
            return true;
        }

        if (TryUseAiFakeStoneBeforePlace(move))
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
    /// AI 인벤토리 지급과 아이템 사용 정책을 실행할지 결정하는 스위치임.
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
    /// AI가 투명돌 아이템 사용을 시도함.
    /// </summary>
    /// <param name="move">AI가 적용하려는 정상 착수 후보.</param>
    /// <returns>투명돌 아이템 사용 성공 여부.</returns>
    private bool TryUseAiTransparentStoneBeforePlace(GomokuMove move)
    {
        if (!ShouldUseAiRandomItem(_aiBeforePlaceItemUseChance) ||
            !_aiItemInventory.Contains(ItemType.TransparentStone) ||
            !TryFindAiTransparentStoneTarget(move, out int x, out int z))
        {
            return false;
        }

        _logic.Board[x, z].IsTransparent = true;
        BoardView?.SwapAllStonesVisual(IsStoneSwapped);
        NotifyBoardChanged();

        ConsumeAiItem(ItemType.TransparentStone);
        Debug.Log($"<color=yellow>[AI 아이템]</color> AI가 ({x}, {z}) 돌에 투명돌 아이템을 사용했습니다.");
        return true;
    }

    /// <summary>
    /// AI가 가짜돌 아이템 사용을 시도함.
    /// </summary>
    /// <param name="move">AI가 적용하려는 정상 착수 후보.</param>
    /// <returns>가짜돌 아이템 사용 성공 여부.</returns>
    private bool TryUseAiFakeStoneBeforePlace(GomokuMove move)
    {
        if (!ShouldUseAiRandomItem(_aiBeforePlaceItemUseChance) ||
            !_aiItemInventory.Contains(ItemType.FakeStone) ||
            !TryFindAiFakeStoneTarget(move, out int x, out int z) ||
            BoardView == null ||
            !BoardView.TryGetWorldPositionByCoord(x, z, out Vector3 pos))
        {
            return false;
        }

        PlaceStoneProcess(pos, x, z, AiStoneColor == StoneColor.Black, -1, -1, true);
        ConsumeAiItem(ItemType.FakeStone);
        Debug.Log($"<color=yellow>[AI 아이템]</color> AI가 ({x}, {z})에 가짜돌 아이템을 사용했습니다.");
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
    /// AI 투명돌 아이템을 적용할 자기 일반 돌 좌표를 찾음.
    /// </summary>
    /// <param name="move">AI가 적용하려는 정상 착수 후보.</param>
    /// <param name="bestX">선택된 X 좌표.</param>
    /// <param name="bestZ">선택된 Z 좌표.</param>
    /// <returns>대상 좌표 탐색 성공 여부.</returns>
    private bool TryFindAiTransparentStoneTarget(GomokuMove move, out int bestX, out int bestZ)
    {
        bestX = -1;
        bestZ = -1;
        int bestScore = int.MinValue;
        int boardSize = GetBoardSize();

        for (int x = 0; x < boardSize; x++)
        {
            for (int z = 0; z < boardSize; z++)
            {
                if (!IsAiNormalStone(x, z))
                {
                    continue;
                }

                int score = EvaluateAiSpecialStoneTargetValue(x, z, move);
                if (score <= bestScore)
                {
                    continue;
                }

                bestX = x;
                bestZ = z;
                bestScore = score;
            }
        }

        return bestX >= 0 && bestZ >= 0;
    }

    /// <summary>
    /// AI 가짜돌 아이템을 설치할 안전한 빈 좌표를 찾음.
    /// </summary>
    /// <param name="move">AI가 적용하려는 정상 착수 후보.</param>
    /// <param name="bestX">선택된 X 좌표.</param>
    /// <param name="bestZ">선택된 Z 좌표.</param>
    /// <returns>대상 좌표 탐색 성공 여부.</returns>
    private bool TryFindAiFakeStoneTarget(GomokuMove move, out int bestX, out int bestZ)
    {
        bestX = -1;
        bestZ = -1;
        int bestScore = int.MinValue;
        int boardSize = GetBoardSize();

        for (int x = 0; x < boardSize; x++)
        {
            for (int z = 0; z < boardSize; z++)
            {
                if ((x == move.X && z == move.Y) || !CanPlaceStoneSafely(x, z, AiStoneColor))
                {
                    continue;
                }

                int score = EvaluateAiSpecialStoneTargetValue(x, z, move);
                if (score <= bestScore)
                {
                    continue;
                }

                bestX = x;
                bestZ = z;
                bestScore = score;
            }
        }

        return bestX >= 0 && bestZ >= 0;
    }

    /// <summary>
    /// AI 특수돌 아이템 대상 좌표의 가치를 계산함.
    /// </summary>
    /// <param name="x">평가할 X 좌표.</param>
    /// <param name="z">평가할 Z 좌표.</param>
    /// <param name="move">AI가 적용하려는 정상 착수 후보.</param>
    /// <returns>대상 가치 점수.</returns>
    private int EvaluateAiSpecialStoneTargetValue(int x, int z, GomokuMove move)
    {
        int score = CountNearbyRealStones(x, z, AiSpecialStoneSearchRange) * AiDetectNearbyStoneScore;
        score += GetCenterProximityBonus(x, z);

        int moveDistance = Mathf.Abs(x - move.X) + Mathf.Abs(z - move.Y);
        score += Mathf.Max(0, (AiSpecialStoneSearchRange * 2 - moveDistance) * AiSpecialStoneMoveProximityScore);
        return score;
    }

    /// <summary>
    /// 지정 좌표가 AI 자신의 일반 돌인지 확인함.
    /// </summary>
    /// <param name="x">검사할 X 좌표.</param>
    /// <param name="z">검사할 Z 좌표.</param>
    /// <returns>AI 자신의 일반 돌이면 true.</returns>
    private bool IsAiNormalStone(int x, int z)
    {
        if (_logic == null || !_logic.IsInside(x, z))
        {
            return false;
        }

        StoneData stoneData = _logic.Board[x, z];
        return stoneData.Color == AiStoneColor &&
               !stoneData.IsFake &&
               !stoneData.IsTransparent;
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
