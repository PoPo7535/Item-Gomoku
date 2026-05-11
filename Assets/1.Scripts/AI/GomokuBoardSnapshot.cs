using System.Collections.Generic;

/// <summary>
/// AI 탐색에 사용할 라이브 보드의 독립 복사본을 보관함.
/// </summary>
public sealed class GomokuBoardSnapshot
{
    private readonly StoneData[,] _board;

    public int BoardSize { get; }
    public int BoardVersion { get; }

    /// <summary>
    /// 라이브 보드를 복사해 스냅샷을 생성함.
    /// </summary>
    /// <param name="sourceBoard">복사할 원본 보드.</param>
    /// <param name="boardVersion">스냅샷 생성 시점의 보드 버전.</param>
    public GomokuBoardSnapshot(StoneData[,] sourceBoard, int boardVersion)
    {
        BoardSize = sourceBoard != null ? sourceBoard.GetLength(0) : 15;
        BoardVersion = boardVersion;
        _board = new StoneData[BoardSize, BoardSize];

        if (sourceBoard == null)
        {
            return;
        }

        int sourceWidth = sourceBoard.GetLength(0);
        int sourceHeight = sourceBoard.GetLength(1);

        for (int x = 0; x < BoardSize && x < sourceWidth; x++)
        {
            for (int y = 0; y < BoardSize && y < sourceHeight; y++)
            {
                _board[x, y] = sourceBoard[x, y];
            }
        }
    }

    /// <summary>
    /// 뷰어(보드를 바라보는 주체)에 맞게 보드 상태를 변환한 스냅샷을 생성함.
    /// </summary>
    /// <param name="sourceBoard">복사할 원본 보드.</param>
    /// <param name="boardVersion">스냅샷 생성 시점의 보드 버전.</param>
    /// <param name="viewerColor">보드를 보는 플레이어의 돌 색상.</param>
    /// <returns>뷰어 기준으로 변환된 스냅샷.</returns>
    public static GomokuBoardSnapshot CreateForViewer(StoneData[,] sourceBoard, int boardVersion, StoneColor viewerColor)
    {
        GomokuBoardSnapshot snapshot = new GomokuBoardSnapshot(sourceBoard, boardVersion);
        snapshot.ApplyViewerVisibility(viewerColor, null);
        return snapshot;
    }

    /// <summary>
    /// 이미 알고 있는 특수돌 좌표를 공개한 뷰어 기준 스냅샷을 생성함.
    /// </summary>
    /// <param name="sourceBoard">복사할 원본 보드.</param>
    /// <param name="boardVersion">스냅샷 생성 시점의 보드 버전.</param>
    /// <param name="viewerColor">보드를 보는 플레이어의 돌 색상.</param>
    /// <param name="knownSpecialStoneKeys">이미 알고 있는 특수돌 좌표 키 목록.</param>
    /// <returns>기억 정보를 반영한 뷰어 기준 스냅샷.</returns>
    public static GomokuBoardSnapshot CreateForViewer(
        StoneData[,] sourceBoard,
        int boardVersion,
        StoneColor viewerColor,
        IReadOnlyCollection<int> knownSpecialStoneKeys)
    {
        GomokuBoardSnapshot snapshot = new GomokuBoardSnapshot(sourceBoard, boardVersion);
        snapshot.ApplyViewerVisibility(viewerColor, knownSpecialStoneKeys);
        return snapshot;
    }

    /// <summary>
    /// 스냅샷에서 AI 전용 OmokuLogic 복사본을 생성함.
    /// </summary>
    /// <returns>스냅샷 보드를 복사한 OmokuLogic.</returns>
    public OmokuLogic CreateLogicCopy()
    {
        OmokuLogic logic = new OmokuLogic();
        int targetWidth = logic.Board.GetLength(0);
        int targetHeight = logic.Board.GetLength(1);

        for (int x = 0; x < BoardSize && x < targetWidth; x++)
        {
            for (int y = 0; y < BoardSize && y < targetHeight; y++)
            {
                logic.Board[x, y] = _board[x, y];
            }
        }

        return logic;
    }

    /// <summary>
    /// 특수돌에 뷰어별 시야 규칙을 적용함.
    /// </summary>
    /// <param name="viewerColor">보드를 보는 플레이어의 돌 색상.</param>
    /// <param name="knownSpecialStoneKeys">이미 알고 있는 특수돌 좌표 키 목록.</param>
    private void ApplyViewerVisibility(StoneColor viewerColor, IReadOnlyCollection<int> knownSpecialStoneKeys)
    {
        for (int x = 0; x < BoardSize; x++)
        {
            for (int y = 0; y < BoardSize; y++)
            {
                StoneData stoneData = _board[x, y];
                if (stoneData.Color == StoneColor.None || stoneData.Color == viewerColor)
                {
                    continue;
                }

                if (IsKnownSpecialStoneCoordinate(x, y, knownSpecialStoneKeys))
                {
                    // 이미 알고 있는 상대 특수돌은 일반 상대 돌처럼 공개함.
                    _board[x, y] = new StoneData { Color = stoneData.Color, IsFake = false, IsTransparent = false };
                    continue;
                }

                if (stoneData.IsTransparent)
                {
                    // 모르는 상대 투명돌은 빈칸처럼 숨김.
                    _board[x, y] = new StoneData { Color = StoneColor.None, IsFake = false, IsTransparent = false };
                    continue;
                }

                if (stoneData.IsFake)
                {
                    // 모르는 상대 가짜돌은 일반 상대 돌처럼 표시함.
                    _board[x, y] = new StoneData { Color = stoneData.Color, IsFake = false, IsTransparent = false };
                }
            }
        }
    }

    /// <summary>
    /// 지정 좌표가 뷰어가 이미 알고 있는 좌표인지 확인함.
    /// </summary>
    /// <param name="x">검사할 X 좌표.</param>
    /// <param name="y">검사할 Y 좌표.</param>
    /// <param name="knownSpecialStoneKeys">이미 알고 있는 특수돌 좌표 키 목록.</param>
    /// <returns>알고 있는 좌표이면 true.</returns>
    private bool IsKnownSpecialStoneCoordinate(int x, int y, IReadOnlyCollection<int> knownSpecialStoneKeys)
    {
        if (knownSpecialStoneKeys == null)
        {
            return false;
        }

        int targetKey = x * BoardSize + y;
        foreach (int key in knownSpecialStoneKeys)
        {
            if (key == targetKey)
            {
                return true;
            }
        }

        return false;
    }
}
