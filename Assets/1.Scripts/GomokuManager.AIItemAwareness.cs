using System.Collections.Generic;

/// <summary>
/// AI 전용 아이템 인식 기억 상태를 관리함.
/// </summary>
public partial class GomokuManager
{
    private readonly HashSet<int> _knownAiOpponentSpecialStoneKeys = new HashSet<int>();

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

        IReadOnlyCollection<int> knownSpecialStoneKeys = GetKnownAiOpponentSpecialStoneKeys(AiStoneColor);
        return GomokuBoardSnapshot.CreateForViewer(_logic.Board, _boardVersion, AiStoneColor, knownSpecialStoneKeys);
    }

    /// <summary>
    /// AI가 알고 있는 상대 특수돌 좌표 기억을 초기화함.
    /// </summary>
    private void ResetAiItemAwarenessMemory()
    {
        _knownAiOpponentSpecialStoneKeys.Clear();
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
