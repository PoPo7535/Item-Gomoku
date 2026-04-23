using UnityEngine;

/// <summary>
/// 현재 오목 보드 상태를 백돌 AI 관점에서 평가함.
/// </summary>
public class GomokuBoardEvaluator
{
    private const int FiveScore = 1000000;
    private const int OpenFourScore = 100000;
    private const int BlockedFourScore = 10000;
    private const int OpenThreeScore = 2500;
    private const int BlockedThreeScore = 500;
    private const int TwoScore = 80;

    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    /// <summary>
    /// 전체 보드를 백돌 AI 관점의 점수로 평가함.
    /// </summary>
    public int Evaluate(OmokuLogic logic, int boardSize)
    {
        int score = 0;

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                StoneColor color = logic.Board[x, y].Color;
                if (color == StoneColor.None || logic.Board[x, y].IsFake)
                {
                    continue;
                }

                int colorWeight = color == StoneColor.White ? 1 : -1;
                score += colorWeight * EvaluateStone(logic, boardSize, x, y, color);
            }
        }

        return score;
    }

    /// <summary>
    /// 특정 좌표에 한 수를 뒀다고 가정했을 때의 보드 점수를 평가함.
    /// </summary>
    public int EvaluateMove(OmokuLogic logic, int boardSize, int x, int y, StoneColor color)
    {
        if (!logic.IsInside(x, y) || logic.Board[x, y].Color != StoneColor.None)
        {
            return int.MinValue;
        }

        logic.Board[x, y] = new StoneData { Color = color, IsFake = false };
        try
        {
            return Evaluate(logic, boardSize);
        }
        finally
        {
            // 평가 중 예외가 발생해도 임시 착수는 반드시 복구함.
            logic.Board[x, y] = new StoneData { Color = StoneColor.None, IsFake = false };
        }
    }

    /// <summary>
    /// 특정 돌이 만드는 모든 방향 패턴 점수를 계산함.
    /// </summary>
    private int EvaluateStone(OmokuLogic logic, int boardSize, int x, int y, StoneColor color)
    {
        int score = 0;

        for (int i = 0; i < DirectionX.Length; i++)
        {
            int previousX = x - DirectionX[i];
            int previousY = y - DirectionY[i];

            if (IsSameColor(logic, previousX, previousY, color))
            {
                continue;
            }

            score += EvaluateLine(logic, boardSize, x, y, DirectionX[i], DirectionY[i], color);
        }

        return score;
    }

    /// <summary>
    /// 한 방향의 연속 돌과 열린 끝 개수를 바탕으로 패턴 점수를 계산함.
    /// </summary>
    private int EvaluateLine(OmokuLogic logic, int boardSize, int x, int y, int directionX, int directionY, StoneColor color)
    {
        int count = 0;
        int currentX = x;
        int currentY = y;

        while (IsInside(boardSize, currentX, currentY) && IsSameColor(logic, currentX, currentY, color))
        {
            count++;
            currentX += directionX;
            currentY += directionY;
        }

        int openEnds = 0;
        if (IsEmpty(logic, currentX, currentY))
        {
            openEnds++;
        }

        int beforeX = x - directionX;
        int beforeY = y - directionY;
        if (IsEmpty(logic, beforeX, beforeY))
        {
            openEnds++;
        }

        // 열린 끝 개수에 따라 공격/방어 가치가 크게 달라짐.
        return ScorePattern(count, openEnds);
    }

    /// <summary>
    /// 연속 돌 개수와 열린 끝 개수에 해당하는 패턴 점수를 반환함.
    /// </summary>
    private int ScorePattern(int count, int openEnds)
    {
        if (count >= 5)
        {
            return FiveScore;
        }

        if (count == 4 && openEnds == 2)
        {
            return OpenFourScore;
        }

        if (count == 4 && openEnds == 1)
        {
            return BlockedFourScore;
        }

        if (count == 3 && openEnds == 2)
        {
            return OpenThreeScore;
        }

        if (count == 3 && openEnds == 1)
        {
            return BlockedThreeScore;
        }

        if (count == 2 && openEnds > 0)
        {
            return TwoScore * openEnds;
        }

        return Mathf.Max(1, count);
    }

    /// <summary>
    /// 지정 좌표가 보드 내부인지 확인함.
    /// </summary>
    private bool IsInside(int boardSize, int x, int y)
    {
        return x >= 0 && x < boardSize && y >= 0 && y < boardSize;
    }

    /// <summary>
    /// 지정 좌표가 비어 있는지 확인함.
    /// </summary>
    private bool IsEmpty(OmokuLogic logic, int x, int y)
    {
        return logic.IsInside(x, y) && logic.Board[x, y].Color == StoneColor.None;
    }

    /// <summary>
    /// 지정 좌표가 같은 색 돌인지 확인함.
    /// </summary>
    private bool IsSameColor(OmokuLogic logic, int x, int y, StoneColor color)
    {
        return logic.IsInside(x, y) &&
               logic.Board[x, y].Color == color &&
               !logic.Board[x, y].IsFake;
    }
}
