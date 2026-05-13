using System;

/// <summary>
/// 현재 오목 보드 상태를 백돌 AI 관점에서 평가함.
/// </summary>
public class GomokuBoardEvaluator
{
    // leaf 보드 평가용 점수표임. MinimaxGomokuAI의 전술/pre-check 점수와 직접 비교하지 않음.
    // 이 값들은 minimax 끝단에서 현재 보드 모양을 비교하기 위한 평가 스케일임.
    // 아래 상수는 전체 보드 평가용이며 후보 ordering bonus나 아이템 정책 threshold와 별개임.
    private const int FiveScore = 1000000;
    private const int OpenFourScore = 180000;
    private const int BlockedFourScore = 18000;
    private const int GappedFourScore = 15000;
    private const int OpenThreeScore = 6000;
    private const int BlockedThreeScore = 1200;
    private const int BrokenThreeScore = 1000;
    private const int TwoScore = 80;
    private const int AttackMomentumBonus = 2500;
    private const int ForcedWinThreatBonus = 30000;
    private const int DoubleOpenThreeComboBonus = 18000;
    private const int DoubleOpenTwoComboBonus = 800;
    private const int BlockedFourOpenThreeComboBonus = 130000;
    private const int OpenFourOpenThreeComboBonus = 90000;

    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    /// <summary>
    /// 전체 보드를 백돌 AI 관점의 점수로 평가함.
    /// </summary>
    public int Evaluate(OmokuLogic logic, int boardSize)
    {
        return Evaluate(logic, boardSize, StoneColor.White);
    }

    /// <summary>
    /// 현재 보드를 지정한 AI 색상 관점의 점수로 평가함.
    /// </summary>
    /// <param name="logic">현재 보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <param name="perspectiveColor">양수 점수로 평가할 기준 색상.</param>
    /// <returns>기준 색상 관점의 보드 점수.</returns>
    public int Evaluate(OmokuLogic logic, int boardSize, StoneColor perspectiveColor)
    {
        int score = 0;
        StoneColor normalizedPerspectiveColor = NormalizePerspectiveColor(perspectiveColor);

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                StoneColor color = logic.Board[x, y].Color;
                if (color == StoneColor.None || logic.Board[x, y].IsFake)
                {
                    continue;
                }

                int colorWeight = color == normalizedPerspectiveColor ? 1 : -1;
                score += colorWeight * EvaluateStone(logic, boardSize, x, y, color, normalizedPerspectiveColor);
            }
        }

        return score;
    }

    /// <summary>
    /// 특정 좌표에 한 수를 뒀다고 가정했을 때의 보드 점수를 평가함.
    /// </summary>
    public int EvaluateMove(OmokuLogic logic, int boardSize, int x, int y, StoneColor color)
    {
        return EvaluateMove(logic, boardSize, x, y, color, StoneColor.White);
    }

    /// <summary>
    /// 특정 좌표에 수를 두고 지정한 AI 색상 관점의 보드 점수를 평가함.
    /// </summary>
    /// <param name="logic">현재 보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <param name="x">평가할 착수 X 좌표.</param>
    /// <param name="y">평가할 착수 Y 좌표.</param>
    /// <param name="color">평가할 착수 색상.</param>
    /// <param name="perspectiveColor">양수 점수로 평가할 기준 색상.</param>
    /// <returns>착수 후 기준 색상 관점의 보드 점수.</returns>
    public int EvaluateMove(OmokuLogic logic, int boardSize, int x, int y, StoneColor color, StoneColor perspectiveColor)
    {
        if (!logic.IsInside(x, y) || logic.Board[x, y].Color != StoneColor.None)
        {
            return int.MinValue;
        }

        // leaf 평가 전용 임시 착수라 실제 게임 보드 상태로 남기면 안 됨.
        logic.Board[x, y] = new StoneData { Color = color, IsFake = false };
        try
        {
            return Evaluate(logic, boardSize, perspectiveColor);
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
            CollectThreatPatternCounts(logic, boardSize, x, y, color, out int openThreeCount, out int blockedFourCount, out _, out _);
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
    private int EvaluateStone(OmokuLogic logic, int boardSize, int x, int y, StoneColor color, StoneColor perspectiveColor)
    {
        int score = 0;
        CollectThreatPatternCounts(logic, boardSize, x, y, color, out int openThreeCount, out int blockedFourCount, out int openFourCount, out int openTwoDirectionCount);

        for (int i = 0; i < DirectionX.Length; i++)
        {
            int previousX = x - DirectionX[i];
            int previousY = y - DirectionY[i];

            if (IsSameColor(logic, previousX, previousY, color))
            {
                continue;
            }

            score += EvaluateLine(logic, boardSize, x, y, DirectionX[i], DirectionY[i], color, perspectiveColor);
        }

        score += GetComboThreatBonus(color, openThreeCount, blockedFourCount, openFourCount, openTwoDirectionCount, perspectiveColor);
        return score;
    }

    /// <summary>
    /// 한 방향의 연속 돌과 열린 끝 개수를 바탕으로 패턴 점수를 계산함.
    /// </summary>
    private int EvaluateLine(OmokuLogic logic, int boardSize, int x, int y, int directionX, int directionY, StoneColor color, StoneColor perspectiveColor)
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
        int continuousPatternScore = ScorePattern(count, openEnds);
        int windowPatternScore = ScoreGappedWindowPattern(logic, boardSize, x, y, directionX, directionY, color);
        int patternScore = Math.Max(continuousPatternScore, windowPatternScore);
        return patternScore + GetAttackBonus(count, openEnds, color, perspectiveColor);
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
    /// 기준 돌을 포함하는 5칸 창에서 끊어진 위협 패턴 점수를 계산함.
    /// </summary>
    /// <param name="logic">현재 보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <param name="x">기준 X 좌표.</param>
    /// <param name="y">기준 Y 좌표.</param>
    /// <param name="directionX">검사 방향 X.</param>
    /// <param name="directionY">검사 방향 Y.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <returns>끊어진 위협 패턴 점수.</returns>
    private int ScoreGappedWindowPattern(OmokuLogic logic, int boardSize, int x, int y, int directionX, int directionY, StoneColor color)
    {
        MinimaxThreatPatternType windowPattern = GetGappedWindowPatternType(logic, boardSize, x, y, directionX, directionY, color);
        if (windowPattern == MinimaxThreatPatternType.GappedFour)
        {
            return GappedFourScore;
        }

        if (windowPattern == MinimaxThreatPatternType.BrokenThree)
        {
            return BrokenThreeScore;
        }

        return 0;
    }

    /// <summary>
    /// 백돌 AI가 다음 턴 강제승으로 이어가기 쉬운 공격 패턴에 추가 가중치를 부여함.
    /// </summary>
    /// <param name="count">연속된 돌 개수.</param>
    /// <param name="openEnds">열린 끝 개수.</param>
    /// <param name="color">현재 패턴의 돌 색상.</param>
    /// <returns>공격 성향을 강화할 추가 점수.</returns>
    private int GetAttackBonus(int count, int openEnds, StoneColor color, StoneColor perspectiveColor)
    {
        if (color != perspectiveColor)
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
    /// <param name="openTwoDirectionCount">열린 2가 만들어진 방향 수.</param>
    private void CollectThreatPatternCounts(OmokuLogic logic, int boardSize, int x, int y, StoneColor color, out int openThreeCount, out int blockedFourCount, out int openFourCount, out int openTwoDirectionCount)
    {
        openThreeCount = 0;
        blockedFourCount = 0;
        openFourCount = 0;
        openTwoDirectionCount = 0;

        for (int i = 0; i < DirectionX.Length; i++)
        {
            int previousX = x - DirectionX[i];
            int previousY = y - DirectionY[i];

            if (IsSameColor(logic, previousX, previousY, color))
            {
                continue;
            }

            MinimaxThreatPatternType patternType = GetThreatPatternType(logic, boardSize, x, y, DirectionX[i], DirectionY[i], color);
            if (patternType == MinimaxThreatPatternType.OpenThree)
            {
                openThreeCount++;
            }
            else if (patternType == MinimaxThreatPatternType.OpenTwo)
            {
                // 열린 2는 방향 수만 세서 낮은 복합 잠재력으로 사용함.
                openTwoDirectionCount++;
            }
            else if (patternType == MinimaxThreatPatternType.BlockedFour)
            {
                blockedFourCount++;
            }
            else if (patternType == MinimaxThreatPatternType.OpenFour)
            {
                openFourCount++;
            }
            else if (patternType == MinimaxThreatPatternType.GappedFour)
            {
                // 점수는 별도 유지하되 복합 위협 판정에서는 forcing four 계열로 묶음.
                blockedFourCount++;
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
    private MinimaxThreatPatternType GetThreatPatternType(OmokuLogic logic, int boardSize, int x, int y, int directionX, int directionY, StoneColor color)
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
            return MinimaxThreatPatternType.Five;
        }

        if (count == 4 && openEnds == 2)
        {
            return MinimaxThreatPatternType.OpenFour;
        }

        if (count == 4 && openEnds == 1)
        {
            return MinimaxThreatPatternType.BlockedFour;
        }

        MinimaxThreatPatternType gappedPatternType = GetGappedWindowPatternType(logic, boardSize, x, y, directionX, directionY, color);
        if (gappedPatternType == MinimaxThreatPatternType.GappedFour)
        {
            // XXX_X 계열이 연속 3으로 먼저 소비되지 않도록 gap four를 우선 분류함.
            return MinimaxThreatPatternType.GappedFour;
        }

        if (count == 3 && openEnds == 2)
        {
            return MinimaxThreatPatternType.OpenThree;
        }

        if (count == 3 && openEnds == 1)
        {
            return MinimaxThreatPatternType.BlockedThree;
        }

        if (gappedPatternType == MinimaxThreatPatternType.BrokenThree)
        {
            // 끊어진 3은 단순 열린 2보다 강한 개발 위협으로 우선 유지함.
            return MinimaxThreatPatternType.BrokenThree;
        }

        if (count == 2 && openEnds == 2)
        {
            return MinimaxThreatPatternType.OpenTwo;
        }

        return gappedPatternType;
    }

    /// <summary>
    /// 기준 돌을 포함하는 5칸 창에서 끊어진 위협 패턴 종류를 반환함.
    /// </summary>
    /// <param name="logic">현재 보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <param name="x">기준 X 좌표.</param>
    /// <param name="y">기준 Y 좌표.</param>
    /// <param name="directionX">검사 방향 X.</param>
    /// <param name="directionY">검사 방향 Y.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <returns>끊어진 위협 패턴 종류.</returns>
    private MinimaxThreatPatternType GetGappedWindowPatternType(OmokuLogic logic, int boardSize, int x, int y, int directionX, int directionY, StoneColor color)
    {
        // 이미 놓인 돌 기준 평가라 같은 window가 여러 run에서 중복 집계되지 않게 대표 시작점을 사용함.
        MinimaxThreatPatternType strongestPattern = MinimaxThreatPatternType.None;

        for (int offset = -4; offset <= 0; offset++)
        {
            if (!ThreatPatternScanner.TryAnalyzeFiveCellWindow(logic, boardSize, x, y, directionX, directionY, offset, color, false, out ThreatPatternWindowResult window))
            {
                continue;
            }

            if (!IsRepresentativeWindowStart(offset, window.FirstColorIndex))
            {
                continue;
            }

            if (window.ColorCount == 4 && window.EmptyCount == 1 && IsInternalGap(window.GapIndex, window.FirstColorIndex, window.LastColorIndex))
            {
                strongestPattern = MinimaxThreatPatternType.GappedFour;
                continue;
            }

            if (strongestPattern == MinimaxThreatPatternType.None &&
                window.ColorCount == 3 &&
                window.EmptyCount == 2 &&
                window.InternalGapCount == 1 &&
                HasGapBetweenStones(window.FirstColorIndex, window.LastColorIndex, window.GapIndex) &&
                (window.HasEndExtension || window.HasOuterExtension))
            {
                strongestPattern = MinimaxThreatPatternType.BrokenThree;
            }
        }

        return strongestPattern;
    }

    /// <summary>
    /// 현재 기준 좌표가 해당 5칸 window의 대표 시작 돌인지 확인함.
    /// </summary>
    /// <param name="offset">기준 좌표에서 창 시작점까지의 offset.</param>
    /// <param name="firstColorIndex">창 안 첫 같은 색 돌 위치.</param>
    /// <returns>대표 시작 돌 여부.</returns>
    private bool IsRepresentativeWindowStart(int offset, int firstColorIndex)
    {
        // 같은 gap window가 좌우 run 시작점에서 중복 평가되지 않도록 첫 돌에서만 점수화함.
        return firstColorIndex >= 0 && offset + firstColorIndex == 0;
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
    /// 복합 위협 조합일 때 추가 보너스를 계산함.
    /// </summary>
    /// <param name="color">현재 평가 중인 돌 색상.</param>
    /// <param name="openThreeCount">열린 3 개수.</param>
    /// <param name="blockedFourCount">막힌 4 개수.</param>
    /// <param name="openFourCount">열린 4 개수.</param>
    /// <param name="openTwoDirectionCount">열린 2가 만들어진 방향 수.</param>
    /// <returns>복합 위협 보너스 점수.</returns>
    private int GetComboThreatBonus(StoneColor color, int openThreeCount, int blockedFourCount, int openFourCount, int openTwoDirectionCount, StoneColor perspectiveColor)
    {
        if (color != perspectiveColor)
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

        if (openTwoDirectionCount >= 2)
        {
            // 열린 2 두 방향은 낮은 장기 잠재력으로만 작게 보정함.
            return DoubleOpenTwoComboBonus;
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

    /// <summary>
    /// 평가 기준 색상이 None이면 기존 백돌 관점으로 보정함.
    /// </summary>
    /// <param name="perspectiveColor">외부에서 전달된 평가 기준 색상.</param>
    /// <returns>유효한 평가 기준 색상.</returns>
    private static StoneColor NormalizePerspectiveColor(StoneColor perspectiveColor)
    {
        return perspectiveColor == StoneColor.Black ? StoneColor.Black : StoneColor.White;
    }
}
