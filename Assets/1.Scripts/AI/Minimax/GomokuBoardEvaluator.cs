using System;

/// <summary>
/// 현재 오목 보드 상태를 백돌 AI 관점에서 평가함.
/// </summary>
public class GomokuBoardEvaluator
{
    private enum ThreatPatternType
    {
        None,
        OpenThree,
        BlockedThree,
        BlockedFour,
        OpenFour,
        Five
    }

    private const int FiveScore = 1000000;
    private const int OpenFourScore = 180000;
    private const int BlockedFourScore = 18000;
    private const int OpenThreeScore = 6000;
    private const int BlockedThreeScore = 1200;
    private const int TwoScore = 80;
    private const int AttackMomentumBonus = 2500;
    private const int ForcedWinThreatBonus = 30000;
    private const int DoubleOpenThreeComboBonus = 18000;
    private const int BlockedFourOpenThreeComboBonus = 130000;
    private const int OpenFourOpenThreeComboBonus = 90000;

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
    /// 특정 착수가 막힌 4와 열린 3 복합 위협을 만드는지 확인함.
    /// </summary>
    /// <param name="logic">현재 보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <param name="x">가정할 착수 X 좌표.</param>
    /// <param name="y">가정할 착수 Y 좌표.</param>
    /// <param name="color">가정할 돌 색상.</param>
    /// <returns>해당 착수가 막힌 4와 열린 3을 동시에 만드는지 여부.</returns>
    public bool CreatesBlockedFourOpenThreeThreat(OmokuLogic logic, int boardSize, int x, int y, StoneColor color)
    {
        if (!logic.IsInside(x, y) || logic.Board[x, y].Color != StoneColor.None)
        {
            return false;
        }

        logic.Board[x, y] = new StoneData { Color = color, IsFake = false };
        try
        {
            CollectThreatPatternCounts(logic, boardSize, x, y, color, out int openThreeCount, out int blockedFourCount, out _);
            return blockedFourCount > 0 && openThreeCount > 0;
        }
        finally
        {
            logic.Board[x, y] = new StoneData { Color = StoneColor.None, IsFake = false };
        }
    }

    /// <summary>
    /// 특정 돌이 만드는 모든 방향 패턴 점수를 계산함.
    /// </summary>
    private int EvaluateStone(OmokuLogic logic, int boardSize, int x, int y, StoneColor color)
    {
        int score = 0;
        CollectThreatPatternCounts(logic, boardSize, x, y, color, out int openThreeCount, out int blockedFourCount, out int openFourCount);

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

        score += GetComboThreatBonus(color, openThreeCount, blockedFourCount, openFourCount);
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
        int patternScore = ScorePattern(count, openEnds);
        return patternScore + GetAttackBonus(count, openEnds, color);
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

        return Math.Max(1, count);
    }

    /// <summary>
    /// 백돌 AI가 다음 턴 강제승으로 이어가기 쉬운 공격 패턴에 추가 가중치를 부여함.
    /// </summary>
    /// <param name="count">연속된 돌 개수.</param>
    /// <param name="openEnds">열린 끝 개수.</param>
    /// <param name="color">현재 패턴의 돌 색상.</param>
    /// <returns>공격 성향을 강화할 추가 점수.</returns>
    private int GetAttackBonus(int count, int openEnds, StoneColor color)
    {
        if (color != StoneColor.White)
        {
            return 0;
        }

        if (count >= 4 && openEnds == 2)
        {
            // 열린 4는 다음 턴 강제승 압박이므로 추가 우대함.
            return ForcedWinThreatBonus;
        }

        if (count >= 4 && openEnds == 1)
        {
            // 막힌 4도 즉시 위닝 플랜으로 이어질 수 있어 보너스를 부여함.
            return AttackMomentumBonus * 2;
        }

        if (count == 3 && openEnds == 2)
        {
            // 열린 3 확장은 주도권을 이어가는 핵심 패턴이라 조금 더 높게 평가함.
            return AttackMomentumBonus;
        }

        return 0;
    }

    /// <summary>
    /// 기준 돌이 만드는 방향별 위협 패턴 개수를 집계함.
    /// </summary>
    /// <param name="logic">현재 보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <param name="x">기준 X 좌표.</param>
    /// <param name="y">기준 Y 좌표.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <param name="openThreeCount">열린 3 개수.</param>
    /// <param name="blockedFourCount">막힌 4 개수.</param>
    /// <param name="openFourCount">열린 4 개수.</param>
    private void CollectThreatPatternCounts(OmokuLogic logic, int boardSize, int x, int y, StoneColor color, out int openThreeCount, out int blockedFourCount, out int openFourCount)
    {
        openThreeCount = 0;
        blockedFourCount = 0;
        openFourCount = 0;

        for (int i = 0; i < DirectionX.Length; i++)
        {
            int previousX = x - DirectionX[i];
            int previousY = y - DirectionY[i];

            if (IsSameColor(logic, previousX, previousY, color))
            {
                continue;
            }

            ThreatPatternType patternType = GetThreatPatternType(logic, boardSize, x, y, DirectionX[i], DirectionY[i], color);
            if (patternType == ThreatPatternType.OpenThree)
            {
                openThreeCount++;
            }
            else if (patternType == ThreatPatternType.BlockedFour)
            {
                blockedFourCount++;
            }
            else if (patternType == ThreatPatternType.OpenFour)
            {
                openFourCount++;
            }
        }
    }

    /// <summary>
    /// 한 방향 패턴이 어떤 위협 단계인지 분류함.
    /// </summary>
    /// <param name="logic">현재 보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <param name="x">기준 X 좌표.</param>
    /// <param name="y">기준 Y 좌표.</param>
    /// <param name="directionX">검사할 방향 X.</param>
    /// <param name="directionY">검사할 방향 Y.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <returns>해당 방향의 위협 패턴 종류.</returns>
    private ThreatPatternType GetThreatPatternType(OmokuLogic logic, int boardSize, int x, int y, int directionX, int directionY, StoneColor color)
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

        if (count >= 5)
        {
            return ThreatPatternType.Five;
        }

        if (count == 4 && openEnds == 2)
        {
            return ThreatPatternType.OpenFour;
        }

        if (count == 4 && openEnds == 1)
        {
            return ThreatPatternType.BlockedFour;
        }

        if (count == 3 && openEnds == 2)
        {
            return ThreatPatternType.OpenThree;
        }

        if (count == 3 && openEnds == 1)
        {
            return ThreatPatternType.BlockedThree;
        }

        return ThreatPatternType.None;
    }

    /// <summary>
    /// 복합 위협 조합일 때 추가 보너스를 계산함.
    /// </summary>
    /// <param name="color">현재 평가 중인 돌 색상.</param>
    /// <param name="openThreeCount">열린 3 개수.</param>
    /// <param name="blockedFourCount">막힌 4 개수.</param>
    /// <param name="openFourCount">열린 4 개수.</param>
    /// <returns>복합 위협 보너스 점수.</returns>
    private int GetComboThreatBonus(StoneColor color, int openThreeCount, int blockedFourCount, int openFourCount)
    {
        if (color != StoneColor.White)
        {
            return 0;
        }

        if (blockedFourCount > 0 && openThreeCount > 0)
        {
            // 막힌 4와 열린 3 동시 형성은 다음 턴 강제승에 매우 가까운 전술 패턴으로 최우선 우대함.
            return BlockedFourOpenThreeComboBonus;
        }

        if (openFourCount > 0 && openThreeCount > 0)
        {
            // 열린 4와 열린 3 조합은 즉시 위닝 플랜에 가까워 크게 우대함.
            return OpenFourOpenThreeComboBonus;
        }

        if (openThreeCount >= 2)
        {
            // 열린 3 두 개는 이중 위협으로 이어질 가능성이 높아 별도 우대함.
            return DoubleOpenThreeComboBonus;
        }

        return 0;
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
