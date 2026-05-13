using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 전용 아이템 인식 기억 상태를 관리함.
/// 상대 특수돌을 밟은 경험을 기억해 다음 스냅샷과 간파하기 판단에 반영함.
/// </summary>
public partial class GomokuManager
{
    [Header("AI 간파하기 사용 임계값")]
    [SerializeField, Min(0)] private int _aiDetectUseThreshold = 80;    // 간파하기 사용 판단용 정책 임계값이며 minimax 평가 점수가 아님.

    private const int AiInitialDetectItemCount = 3;
    private const int AiTransparentDetectBaseScore = 100;
    private const int AiFakeDetectBaseScore = 60;
    private const int AiDetectNearbyStoneScore = 8;
    private const int AiDetectCenterBonusMax = 6;
    private const int AiDetectNearbyRange = 2;

    private readonly HashSet<int> _knownAiOpponentSpecialStoneKeys = new HashSet<int>();
    private int _remainingAiDetectItemCount;

    /// <summary>
    /// 아이템 인식 설정에 맞춰 AI 탐색용 보드 스냅샷을 생성함.
    /// </summary>
    /// <returns>AI 탐색에 사용할 보드 스냅샷.</returns>
    private GomokuBoardSnapshot CreateAiSearchSnapshot()
    {
        if (!IsAiItemAwarenessEnabled())
        {
            return new GomokuBoardSnapshot(_logic.Board, _boardVersion);
        }

        // 알려진 상대 특수돌은 이미 학습한 정보이므로 일반 상대 돌처럼 공개함.
        IReadOnlyCollection<int> knownSpecialStoneKeys = GetKnownAiOpponentSpecialStoneKeys(AiStoneColor);
        return GomokuBoardSnapshot.CreateForViewer(_logic.Board, _boardVersion, AiStoneColor, knownSpecialStoneKeys);
    }

    /// <summary>
    /// AI가 알고 있는 상대 특수돌 좌표 기억을 초기화함.
    /// </summary>
    private void ResetAiItemAwarenessMemory()
    {
        _knownAiOpponentSpecialStoneKeys.Clear();
        _remainingAiDetectItemCount = IsAiItemFeatureEnabled() ? AiInitialDetectItemCount : 0;
    }

    /// <summary>
    /// AI가 기억 중인 상대 특수돌 중 가치가 충분한 좌표에 간파하기를 사용함.
    /// Detect count와 threshold를 통과한 경우에만 live board를 변경함.
    /// </summary>
    /// <returns>간파하기 사용 성공 여부.</returns>
    private bool TryUseAiDetectOnKnownSpecialStone()
    {
        if (!IsAiItemAwarenessEnabled() ||
            _remainingAiDetectItemCount <= 0 ||
            !TryFindBestKnownSpecialStoneToDetect(AiStoneColor, out int x, out int z, out int score))
        {
            return false;
        }

        if (score < _aiDetectUseThreshold)
        {
            return false;
        }

        return TryRemoveKnownSpecialStoneForAi(x, z, AiStoneColor);
    }

    /// <summary>
    /// AI가 밟은 상대 특수돌 좌표를 기억함.
    /// </summary>
    /// <param name="x">기억할 X 좌표.</param>
    /// <param name="z">기억할 Z 좌표.</param>
    /// <param name="stoneColor">AI 돌 색상.</param>
    private void RememberAiOpponentSpecialStone(int x, int z, StoneColor stoneColor)
    {
        if (!IsAiItemAwarenessEnabled() || !IsOpponentSpecialStone(x, z, stoneColor))
        {
            return;
        }

        // 좌표만 기억하고 실제 보드 데이터는 변경하지 않음.
        _knownAiOpponentSpecialStoneKeys.Add(CreateAiItemAwarenessKey(x, z));
    }

    /// <summary>
    /// 지정 좌표가 AI가 기억하는 활성 상대 특수돌인지 확인함.
    /// </summary>
    /// <param name="x">검사할 X 좌표.</param>
    /// <param name="z">검사할 Z 좌표.</param>
    /// <param name="stoneColor">AI 돌 색상.</param>
    /// <returns>기억된 활성 상대 특수돌이면 true.</returns>
    private bool IsKnownAiOpponentSpecialStone(int x, int z, StoneColor stoneColor)
    {
        int key = CreateAiItemAwarenessKey(x, z);
        if (!_knownAiOpponentSpecialStoneKeys.Contains(key))
        {
            return false;
        }

        if (IsOpponentSpecialStone(x, z, stoneColor))
        {
            return true;
        }

        // 라이브 보드에 더 이상 활성 함정이 없으면 오래된 기억을 제거함.
        _knownAiOpponentSpecialStoneKeys.Remove(key);
        return false;
    }

    /// <summary>
    /// 간파하기를 사용할 가치가 가장 높은 기억된 상대 특수돌 좌표를 찾음.
    /// </summary>
    /// <param name="stoneColor">AI 돌 색상.</param>
    /// <param name="bestX">선택된 X 좌표.</param>
    /// <param name="bestZ">선택된 Z 좌표.</param>
    /// <param name="bestScore">선택된 좌표의 평가 점수.</param>
    /// <returns>후보 탐색 성공 여부.</returns>
    private bool TryFindBestKnownSpecialStoneToDetect(StoneColor stoneColor, out int bestX, out int bestZ, out int bestScore)
    {
        IReadOnlyCollection<int> knownKeys = GetKnownAiOpponentSpecialStoneKeys(stoneColor);
        int boardSize = GetBoardSize();
        bestX = -1;
        bestZ = -1;
        bestScore = int.MinValue;

        foreach (int key in knownKeys)
        {
            int x = key / boardSize;
            int z = key % boardSize;
            if (!IsOpponentSpecialStone(x, z, stoneColor))
            {
                continue;
            }

            int score = EvaluateKnownSpecialStoneDetectValue(x, z);
            if (score <= bestScore)
            {
                continue;
            }

            bestX = x;
            bestZ = z;
            bestScore = score;
        }

        return bestX >= 0 && bestZ >= 0;
    }

    /// <summary>
    /// AI 간파하기로 기억된 상대 특수돌을 live board에서 제거함.
    /// </summary>
    /// <param name="x">제거할 X 좌표.</param>
    /// <param name="z">제거할 Z 좌표.</param>
    /// <param name="stoneColor">AI 돌 색상.</param>
    /// <returns>제거 성공 여부.</returns>
    private bool TryRemoveKnownSpecialStoneForAi(int x, int z, StoneColor stoneColor)
    {
        if (_remainingAiDetectItemCount <= 0 || !IsOpponentSpecialStone(x, z, stoneColor))
        {
            return false;
        }

        StoneData removedStoneData = _logic.Board[x, z];
        _logic.Board[x, z] = new StoneData { Color = StoneColor.None, IsFake = false, IsTransparent = false };

        BoardView?.RemoveStone(x, z);
        BoardView?.SwapAllStonesVisual(IsStoneSwapped);

        ForgetAiOpponentSpecialStone(x, z);
        _remainingAiDetectItemCount--;
        NotifyBoardChanged();

        string stoneType = removedStoneData.IsTransparent ? "투명돌" : "가짜돌";
        Debug.Log($"<color=cyan>[AI 간파]</color> AI가 ({x}, {z})의 상대 {stoneType}을 제거했습니다.");
        return true;
    }

    /// <summary>
    /// 기억된 상대 특수돌 좌표의 간파하기 사용 가치를 계산함.
    /// </summary>
    /// <param name="x">평가할 X 좌표.</param>
    /// <param name="z">평가할 Z 좌표.</param>
    /// <returns>간파하기 사용 가치 점수.</returns>
    private int EvaluateKnownSpecialStoneDetectValue(int x, int z)
    {
        StoneData stoneData = _logic.Board[x, z];
        int score = stoneData.IsTransparent ? AiTransparentDetectBaseScore : AiFakeDetectBaseScore;
        score += CountNearbyRealStones(x, z, AiDetectNearbyRange) * AiDetectNearbyStoneScore;
        score += GetCenterProximityBonus(x, z);
        return score;
    }

    /// <summary>
    /// 지정 좌표 주변의 실제 돌 개수를 계산함.
    /// </summary>
    /// <param name="centerX">중심 X 좌표.</param>
    /// <param name="centerZ">중심 Z 좌표.</param>
    /// <param name="range">확인할 주변 범위.</param>
    /// <returns>주변 실제 돌 개수.</returns>
    private int CountNearbyRealStones(int centerX, int centerZ, int range)
    {
        int count = 0;
        int boardSize = GetBoardSize();
        for (int x = centerX - range; x <= centerX + range; x++)
        {
            for (int z = centerZ - range; z <= centerZ + range; z++)
            {
                if (x == centerX && z == centerZ)
                {
                    continue;
                }

                if (x < 0 || x >= boardSize || z < 0 || z >= boardSize)
                {
                    continue;
                }

                StoneData stoneData = _logic.Board[x, z];
                if (stoneData.Color == StoneColor.None || stoneData.IsFake || stoneData.IsTransparent)
                {
                    continue;
                }

                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 중앙에 가까운 좌표에 소량 보너스를 부여함.
    /// </summary>
    /// <param name="x">평가할 X 좌표.</param>
    /// <param name="z">평가할 Z 좌표.</param>
    /// <returns>중앙 근접 보너스.</returns>
    private int GetCenterProximityBonus(int x, int z)
    {
        int boardSize = GetBoardSize();
        int center = boardSize / 2;
        int manhattanDistance = Mathf.Abs(x - center) + Mathf.Abs(z - center);
        return Mathf.Max(0, AiDetectCenterBonusMax - manhattanDistance);
    }

    /// <summary>
    /// 라이브 보드에 아직 존재하는 기억된 상대 특수돌 키를 반환함.
    /// </summary>
    /// <param name="stoneColor">AI 돌 색상.</param>
    /// <returns>활성 상태인 known 특수돌 좌표 키 목록.</returns>
    private IReadOnlyCollection<int> GetKnownAiOpponentSpecialStoneKeys(StoneColor stoneColor)
    {
        RemoveStaleAiItemAwarenessKeys(stoneColor);
        return new List<int>(_knownAiOpponentSpecialStoneKeys);
    }

    /// <summary>
    /// AI가 기억하는 상대 특수돌 좌표를 제거함.
    /// </summary>
    /// <param name="x">제거할 X 좌표.</param>
    /// <param name="z">제거할 Z 좌표.</param>
    private void ForgetAiOpponentSpecialStone(int x, int z)
    {
        _knownAiOpponentSpecialStoneKeys.Remove(CreateAiItemAwarenessKey(x, z));
    }

    /// <summary>
    /// 라이브 보드와 더 이상 일치하지 않는 아이템 인식 좌표를 제거함.
    /// </summary>
    /// <param name="stoneColor">AI 돌 색상.</param>
    private void RemoveStaleAiItemAwarenessKeys(StoneColor stoneColor)
    {
        int boardSize = GetBoardSize();
        List<int> staleKeys = null;

        foreach (int key in _knownAiOpponentSpecialStoneKeys)
        {
            int x = key / boardSize;
            int z = key % boardSize;
            if (IsOpponentSpecialStone(x, z, stoneColor))
            {
                continue;
            }

            if (staleKeys == null)
            {
                staleKeys = new List<int>();
            }

            staleKeys.Add(key);
        }

        if (staleKeys == null)
        {
            return;
        }

        foreach (int key in staleKeys)
        {
            _knownAiOpponentSpecialStoneKeys.Remove(key);
        }
    }

    /// <summary>
    /// 보드 좌표를 AI 아이템 인식용 키로 변환함.
    /// </summary>
    /// <param name="x">변환할 X 좌표.</param>
    /// <param name="z">변환할 Z 좌표.</param>
    /// <returns>좌표를 나타내는 정수 키.</returns>
    private int CreateAiItemAwarenessKey(int x, int z)
    {
        return x * GetBoardSize() + z;
    }
}
