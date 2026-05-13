/// <summary>
/// Minimax 계층의 5칸 window raw 분석을 공통 처리함.
/// </summary>
internal static class ThreatPatternScanner
{
    private const int WindowSize = 5;

    /// <summary>
    /// 5칸 window 하나가 같은 색 돌과 빈칸만으로 구성되는지 분석함.
    /// </summary>
    /// <param name="logic">현재 보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <param name="originX">기준 X 좌표.</param>
    /// <param name="originY">기준 Y 좌표.</param>
    /// <param name="directionX">검사 방향 X.</param>
    /// <param name="directionY">검사 방향 Y.</param>
    /// <param name="offset">기준 좌표에서 window 시작점까지의 offset.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <param name="treatOriginAsColor">기준 좌표를 해당 색상의 가상 돌로 볼지 여부.</param>
    /// <param name="result">5칸 window raw 분석 결과.</param>
    /// <returns>상대 돌 없이 분석 가능한 window면 true.</returns>
    public static bool TryAnalyzeFiveCellWindow(
        OmokuLogic logic,
        int boardSize,
        int originX,
        int originY,
        int directionX,
        int directionY,
        int offset,
        StoneColor color,
        bool treatOriginAsColor,
        out ThreatPatternWindowResult result)
    {
        int colorCount = 0;
        int emptyCount = 0;
        int firstColorIndex = -1;
        int lastColorIndex = -1;
        int gapIndex = -1;
        int internalGapCount = 0;

        for (int index = 0; index < WindowSize; index++)
        {
            int targetX = originX + ((offset + index) * directionX);
            int targetY = originY + ((offset + index) * directionY);
            StoneColor targetColor = GetWindowCellColor(logic, boardSize, originX, originY, targetX, targetY, color, treatOriginAsColor);

            if (targetColor == color)
            {
                colorCount++;
                if (firstColorIndex < 0)
                {
                    firstColorIndex = index;
                }

                lastColorIndex = index;
                continue;
            }

            if (targetColor == StoneColor.None)
            {
                emptyCount++;
                continue;
            }

            result = default(ThreatPatternWindowResult);
            return false;
        }

        if (firstColorIndex >= 0 && lastColorIndex >= 0)
        {
            for (int index = firstColorIndex + 1; index < lastColorIndex; index++)
            {
                int targetX = originX + ((offset + index) * directionX);
                int targetY = originY + ((offset + index) * directionY);
                if (GetWindowCellColor(logic, boardSize, originX, originY, targetX, targetY, color, treatOriginAsColor) == StoneColor.None)
                {
                    if (gapIndex < 0)
                    {
                        gapIndex = index;
                    }

                    internalGapCount++;
                }
            }
        }

        bool hasEndExtension = HasWindowEndExtension(logic, boardSize, originX, originY, directionX, directionY, offset, color, treatOriginAsColor);
        bool hasOuterExtension = HasWindowOuterExtension(logic, boardSize, originX, originY, directionX, directionY, offset);
        result = new ThreatPatternWindowResult(colorCount, emptyCount, firstColorIndex, lastColorIndex, gapIndex, internalGapCount, hasEndExtension, hasOuterExtension);
        return true;
    }

    /// <summary>
    /// window 분석용 좌표의 돌 색상을 반환함.
    /// </summary>
    private static StoneColor GetWindowCellColor(
        OmokuLogic logic,
        int boardSize,
        int originX,
        int originY,
        int targetX,
        int targetY,
        StoneColor color,
        bool treatOriginAsColor)
    {
        if (treatOriginAsColor && targetX == originX && targetY == originY)
        {
            return color;
        }

        if (!IsInside(boardSize, targetX, targetY))
        {
            return color == StoneColor.Black ? StoneColor.White : StoneColor.Black;
        }

        StoneData stoneData = logic.Board[targetX, targetY];
        return stoneData.IsFake ? StoneColor.None : stoneData.Color;
    }

    /// <summary>
    /// 5칸 window 내부 양끝 중 하나가 확장 가능한 빈칸인지 확인함.
    /// </summary>
    private static bool HasWindowEndExtension(
        OmokuLogic logic,
        int boardSize,
        int originX,
        int originY,
        int directionX,
        int directionY,
        int offset,
        StoneColor color,
        bool treatOriginAsColor)
    {
        int firstX = originX + (offset * directionX);
        int firstY = originY + (offset * directionY);
        int lastX = originX + ((offset + WindowSize - 1) * directionX);
        int lastY = originY + ((offset + WindowSize - 1) * directionY);

        return GetWindowCellColor(logic, boardSize, originX, originY, firstX, firstY, color, treatOriginAsColor) == StoneColor.None ||
               GetWindowCellColor(logic, boardSize, originX, originY, lastX, lastY, color, treatOriginAsColor) == StoneColor.None;
    }

    /// <summary>
    /// 5칸 window 바깥쪽으로 확장 가능한 빈칸이 있는지 확인함.
    /// </summary>
    private static bool HasWindowOuterExtension(
        OmokuLogic logic,
        int boardSize,
        int originX,
        int originY,
        int directionX,
        int directionY,
        int offset)
    {
        int beforeX = originX + ((offset - 1) * directionX);
        int beforeY = originY + ((offset - 1) * directionY);
        int afterX = originX + ((offset + WindowSize) * directionX);
        int afterY = originY + ((offset + WindowSize) * directionY);

        return IsStrictEmpty(logic, boardSize, beforeX, beforeY) || IsStrictEmpty(logic, boardSize, afterX, afterY);
    }

    /// <summary>
    /// 지정 좌표가 보드 안의 실제 빈칸인지 확인함.
    /// </summary>
    private static bool IsStrictEmpty(OmokuLogic logic, int boardSize, int x, int y)
    {
        return IsInside(boardSize, x, y) && logic.Board[x, y].Color == StoneColor.None;
    }

    /// <summary>
    /// 지정 좌표가 보드 내부인지 확인함.
    /// </summary>
    private static bool IsInside(int boardSize, int x, int y)
    {
        return x >= 0 && x < boardSize && y >= 0 && y < boardSize;
    }
}
