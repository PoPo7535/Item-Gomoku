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
    /// 뷰어에 맞게 보드 상태를 변환한 스냅샷을 생성함.
    /// </summary>
    /// <param name="sourceBoard">복사할 원본 보드.</param>
    /// <param name="boardVersion">스냅샷 시점의 보드 버전.</param>
    /// <param name="viewerColor">보드를 보는 플레이어의 돌 색상.</param>
    /// <returns>뷰어에 맞게 변환된 스냅샷.</returns>
    public static GomokuBoardSnapshot CreateForViewer(StoneData[,] sourceBoard, int boardVersion, StoneColor viewerColor)
    {
        GomokuBoardSnapshot snapshot = new GomokuBoardSnapshot(sourceBoard, boardVersion);
        snapshot.ApplyViewerVisibility(viewerColor);
        return snapshot;
    }

    /// <summary>
    /// 스냅샷 내용을 가진 AI 전용 OmokuLogic 복사본을 생성함.
    /// </summary>
    /// <returns>스냅샷 보드가 복사된 OmokuLogic.</returns>
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
    /// 뷰어 색상에 따라 특수 돌을 변환함.
    /// </summary>
    /// <param name="viewerColor">보드를 보는 플레이어의 돌 색상.</param>
    private void ApplyViewerVisibility(StoneColor viewerColor)
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

                if (stoneData.IsTransparent)
                {
                    // Opponent transparent stones look empty to the AI.
                    _board[x, y] = new StoneData { Color = StoneColor.None, IsFake = false, IsTransparent = false };
                    continue;
                }

                if (stoneData.IsFake)
                {
                    // Opponent fake stones look like normal opponent stones to the AI.
                    _board[x, y] = new StoneData { Color = stoneData.Color, IsFake = false, IsTransparent = false };
                }
            }
        }
    }
}
