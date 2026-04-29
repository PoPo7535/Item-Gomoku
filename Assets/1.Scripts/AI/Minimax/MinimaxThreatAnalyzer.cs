/// <summary>
/// 보드의 특정 좌표에서 생기는 지역 위협 형태를 분석함.
/// </summary>
internal sealed class MinimaxThreatAnalyzer
{
    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    private readonly OmokuLogic _logic;
    private readonly int _openFourThreatScore;
    private readonly int _blockedFourThreatScore;
    private readonly int _openThreeThreatScore;

    /// <summary>
    /// 지역 위협 분석기를 생성함.
    /// </summary>
    /// <param name="logic">분석할 보드 상태.</param>
    /// <param name="openFourThreatScore">열린 4 위협 점수.</param>
    /// <param name="blockedFourThreatScore">막힌 4 위협 점수.</param>
    /// <param name="openThreeThreatScore">열린 3 위협 점수.</param>
    public MinimaxThreatAnalyzer(
        OmokuLogic logic,
        int openFourThreatScore,
        int blockedFourThreatScore,
        int openThreeThreatScore)
    {
        _logic = logic;
        _openFourThreatScore = openFourThreatScore;
        _blockedFourThreatScore = blockedFourThreatScore;
        _openThreeThreatScore = openThreeThreatScore;
    }

    /// <summary>
    /// 특정 좌표가 만드는 열린 4, 막힌 4, 열린 3 위협 점수를 계산함.
    /// </summary>
    /// <param name="x">검사할 X 좌표.</param>
    /// <param name="y">검사할 Y 좌표.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <returns>지역 위협 분석 결과.</returns>
    public MinimaxThreatAnalysis AnalyzeThreatAt(int x, int y, StoneColor color)
    {
        MinimaxThreatAnalysis analysis = new MinimaxThreatAnalysis();

        for (int i = 0; i < DirectionX.Length; i++)
        {
            int count = CountLine(x, y, DirectionX[i], DirectionY[i], color);
            int openEnds = CountOpenEnds(x, y, DirectionX[i], DirectionY[i], color);

            if (count >= 4 && openEnds == 2)
            {
                analysis.OpenFourCount++;
                analysis.Score = System.Math.Max(analysis.Score, _openFourThreatScore);
            }
            else if (count >= 4 && openEnds == 1)
            {
                analysis.BlockedFourCount++;
                analysis.Score = System.Math.Max(analysis.Score, _blockedFourThreatScore);
            }
            else if (count == 3 && openEnds == 2)
            {
                analysis.OpenThreeCount++;
                analysis.Score = System.Math.Max(analysis.Score, _openThreeThreatScore);
            }
        }

        return analysis;
    }

    /// <summary>
    /// 지정 좌표가 빈 칸인지 확인함.
    /// </summary>
    /// <param name="x">검사할 X 좌표.</param>
    /// <param name="y">검사할 Y 좌표.</param>
    /// <returns>빈 칸 여부.</returns>
    public bool IsEmpty(int x, int y)
    {
        return _logic.IsInside(x, y) && _logic.Board[x, y].Color == StoneColor.None;
    }

    /// <summary>
    /// 특정 방향 양쪽의 연속 돌 개수를 합산함.
    /// </summary>
    /// <param name="x">기준 X 좌표.</param>
    /// <param name="y">기준 Y 좌표.</param>
    /// <param name="directionX">검사 방향 X.</param>
    /// <param name="directionY">검사 방향 Y.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <returns>양방향 연속 돌 개수.</returns>
    private int CountLine(int x, int y, int directionX, int directionY, StoneColor color)
    {
        return 1 +
               CountSameColor(x, y, directionX, directionY, color) +
               CountSameColor(x, y, -directionX, -directionY, color);
    }

    /// <summary>
    /// 특정 방향 양쪽 끝의 열린 상태 개수를 계산함.
    /// </summary>
    /// <param name="x">기준 X 좌표.</param>
    /// <param name="y">기준 Y 좌표.</param>
    /// <param name="directionX">검사 방향 X.</param>
    /// <param name="directionY">검사 방향 Y.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <returns>열린 끝 개수.</returns>
    private int CountOpenEnds(int x, int y, int directionX, int directionY, StoneColor color)
    {
        int forwardCount = CountSameColor(x, y, directionX, directionY, color);
        int backwardCount = CountSameColor(x, y, -directionX, -directionY, color);
        int openEnds = 0;

        int forwardX = x + (forwardCount + 1) * directionX;
        int forwardY = y + (forwardCount + 1) * directionY;
        if (IsEmpty(forwardX, forwardY))
        {
            openEnds++;
        }

        int backwardX = x - (backwardCount + 1) * directionX;
        int backwardY = y - (backwardCount + 1) * directionY;
        if (IsEmpty(backwardX, backwardY))
        {
            openEnds++;
        }

        return openEnds;
    }

    /// <summary>
    /// 특정 방향으로 같은 색 돌이 몇 개 이어지는지 계산함.
    /// </summary>
    /// <param name="x">기준 X 좌표.</param>
    /// <param name="y">기준 Y 좌표.</param>
    /// <param name="directionX">검사 방향 X.</param>
    /// <param name="directionY">검사 방향 Y.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <returns>연속 돌 개수.</returns>
    private int CountSameColor(int x, int y, int directionX, int directionY, StoneColor color)
    {
        int count = 0;
        int currentX = x + directionX;
        int currentY = y + directionY;

        while (_logic.IsInside(currentX, currentY) &&
               _logic.Board[currentX, currentY].Color == color &&
               !_logic.Board[currentX, currentY].IsFake)
        {
            count++;
            currentX += directionX;
            currentY += directionY;
        }

        return count;
    }
}
