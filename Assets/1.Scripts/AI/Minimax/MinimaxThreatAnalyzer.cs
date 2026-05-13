/// <summary>
/// 보드의 특정 좌표에서 생기는 지역 위협 형태를 분석함.
/// </summary>
internal sealed class MinimaxThreatAnalyzer
{
    // 후보 좌표를 가상 착수한 것처럼 취급하는 전술 분석기임.
    // Evaluator의 실제 보드 대표 window 보정과 바로 공통화하면 판단 결과가 달라질 수 있음.
    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    private readonly OmokuLogic _logic;
    private readonly int _openFourThreatScore;
    private readonly int _blockedFourThreatScore;
    private readonly int _openThreeThreatScore;
    private readonly int _gappedFourThreatScore;
    private readonly int _brokenThreeThreatScore;
    private readonly int _openTwoThreatScore;

    /// <summary>
    /// 지역 위협 분석기를 생성함.
    /// </summary>
    /// <param name="logic">분석할 보드 상태.</param>
    /// <param name="openFourThreatScore">열린 4 위협 점수.</param>
    /// <param name="blockedFourThreatScore">막힌 4 위협 점수.</param>
    /// <param name="openThreeThreatScore">열린 3 위협 점수.</param>
    /// <param name="gappedFourThreatScore">끊어진 4 위협 점수.</param>
    /// <param name="brokenThreeThreatScore">끊어진 3 위협 점수.</param>
    /// <param name="openTwoThreatScore">열린 2 잠재력 점수.</param>
    public MinimaxThreatAnalyzer(
        OmokuLogic logic,
        int openFourThreatScore,
        int blockedFourThreatScore,
        int openThreeThreatScore,
        int gappedFourThreatScore,
        int brokenThreeThreatScore,
        int openTwoThreatScore)
    {
        _logic = logic;
        _openFourThreatScore = openFourThreatScore;
        _blockedFourThreatScore = blockedFourThreatScore;
        _openThreeThreatScore = openThreeThreatScore;
        _gappedFourThreatScore = gappedFourThreatScore;
        _brokenThreeThreatScore = brokenThreeThreatScore;
        _openTwoThreatScore = openTwoThreatScore;
    }

    /// <summary>
    /// Returns the bit mask for a threat direction.
    /// </summary>
    /// <param name="directionIndex">Direction index.</param>
    /// <returns>Bit mask for the direction.</returns>
    private static int GetDirectionMask(int directionIndex)
    {
        return 1 << directionIndex;
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
        // (x, y)는 아직 비어 있어도 해당 색 돌이 놓인 후보 좌표처럼 분석함.
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
                analysis.BlockedFourDirectionMask |= GetDirectionMask(i);
                analysis.Score = System.Math.Max(analysis.Score, _blockedFourThreatScore);
            }
            else if (count == 3 && openEnds == 2)
            {
                analysis.OpenThreeCount++;
                analysis.OpenThreeDirectionMask |= GetDirectionMask(i);
                analysis.Score = System.Math.Max(analysis.Score, _openThreeThreatScore);
            }
            else if (count == 2 && openEnds == 2)
            {
                // 열린 2는 강제 수가 아니라 낮은 잠재력으로만 반영함.
                analysis.OpenTwoDirectionCount++;
                analysis.Score = System.Math.Max(analysis.Score, _openTwoThreatScore);
            }

            AnalyzeWindowThreats(x, y, DirectionX[i], DirectionY[i], i, color, ref analysis);
        }

        return analysis;
    }

    /// <summary>
    /// 기준 좌표를 포함하는 5칸 창에서 단일 gap 위협을 분석함.
    /// </summary>
    /// <param name="x">기준 X 좌표.</param>
    /// <param name="y">기준 Y 좌표.</param>
    /// <param name="directionX">검사 방향 X.</param>
    /// <param name="directionY">검사 방향 Y.</param>
    /// <param name="directionIndex">복합 위협 방향 mask에 사용할 방향 인덱스.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <param name="analysis">갱신할 위협 분석 결과.</param>
    private void AnalyzeWindowThreats(int x, int y, int directionX, int directionY, int directionIndex, StoneColor color, ref MinimaxThreatAnalysis analysis)
    {
        bool foundGappedFour = false;
        bool foundBrokenThree = false;

        for (int offset = -4; offset <= 0; offset++)
        {
            if (!ThreatPatternScanner.TryAnalyzeFiveCellWindow(_logic, _logic.Board.GetLength(0), x, y, directionX, directionY, offset, color, true, out ThreatPatternWindowResult window))
            {
                continue;
            }

            if (window.ColorCount == 4 && window.EmptyCount == 1 && IsInternalGap(window.GapIndex, window.FirstColorIndex, window.LastColorIndex))
            {
                foundGappedFour = true;
                continue;
            }

            if (window.ColorCount == 3 &&
                window.EmptyCount == 2 &&
                HasGapBetweenStones(window.FirstColorIndex, window.LastColorIndex, window.GapIndex) &&
                (window.HasEndExtension || window.HasOuterExtension))
            {
                foundBrokenThree = true;
            }
        }

        if (foundGappedFour)
        {
            analysis.GappedFourCount++;
            analysis.GappedFourDirectionMask |= GetDirectionMask(directionIndex);
            analysis.Score = System.Math.Max(analysis.Score, _gappedFourThreatScore);
        }

        if (foundBrokenThree)
        {
            analysis.BrokenThreeCount++;
            analysis.Score = System.Math.Max(analysis.Score, _brokenThreeThreatScore);
        }
    }

    /// <summary>
    /// 4돌 1빈칸 창에서 gap이 돌 사이에 있는지 확인함.
    /// </summary>
    /// <param name="gapIndex">빈칸 위치.</param>
    /// <param name="firstColorIndex">첫 돌 위치.</param>
    /// <param name="lastColorIndex">마지막 돌 위치.</param>
    /// <returns>내부 gap 여부.</returns>
    private bool IsInternalGap(int gapIndex, int firstColorIndex, int lastColorIndex)
    {
        return gapIndex > firstColorIndex && gapIndex < lastColorIndex;
    }

    /// <summary>
    /// 3돌 창에서 돌 사이에 끊어진 빈칸이 있는지 확인함.
    /// </summary>
    /// <param name="firstColorIndex">첫 돌 위치.</param>
    /// <param name="lastColorIndex">마지막 돌 위치.</param>
    /// <param name="gapIndex">돌 사이 빈칸 위치.</param>
    /// <returns>끊어진 3 형태 여부.</returns>
    private bool HasGapBetweenStones(int firstColorIndex, int lastColorIndex, int gapIndex)
    {
        return gapIndex > firstColorIndex && gapIndex < lastColorIndex;
    }

    /// <summary>
    /// 지정한 돌 색상의 반대 색상을 반환함.
    /// </summary>
    /// <param name="color">기준 돌 색상.</param>
    /// <returns>반대 돌 색상.</returns>
    private static StoneColor GetOppositeColor(StoneColor color)
    {
        return color == StoneColor.Black ? StoneColor.White : StoneColor.Black;
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
