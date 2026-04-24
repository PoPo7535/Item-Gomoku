using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오목 AI의 후보 생성과 minimax 의사결정을 담당함.
/// </summary>
public class GomokuAI
{
    private const int WinScore = 10000000;
    private const int OpenFourThreatScore = 900000;
    private const int BlockedFourThreatScore = 300000;
    private const int OpenThreeThreatScore = 50000;
    private const int CandidateRadius = 2;
    private const int MaxCandidateCount = 18;

    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    private readonly OmokuLogic _logic;
    private readonly GomokuBoardEvaluator _evaluator;
    private readonly int _boardSize;

    /// <summary>
    /// 오목 AI를 생성함.
    /// </summary>
    public GomokuAI(OmokuLogic logic, int boardSize)
    {
        _logic = logic;
        _boardSize = boardSize;
        _evaluator = new GomokuBoardEvaluator();
    }

    /// <summary>
    /// 지정한 탐색 깊이로 백돌 AI의 최선 수를 찾음.
    /// </summary>
    public GomokuMove FindBestMove(int searchDepth)
    {
        int clampedDepth = Mathf.Clamp(searchDepth, 1, 5);
        List<GomokuMove> fullCandidates = GenerateCandidates(StoneColor.White, false);

        if (fullCandidates.Count == 0)
        {
            return FindFallbackMove();
        }

        GomokuMove immediateWin = FindImmediateMove(fullCandidates, StoneColor.White, "Immediate win");
        if (immediateWin.IsValid)
        {
            return immediateWin;
        }

        if (clampedDepth >= 2)
        {
            GomokuMove immediateDefense = FindImmediateMove(fullCandidates, StoneColor.Black, "Immediate defense");
            if (immediateDefense.IsValid)
            {
                return immediateDefense;
            }

            GomokuMove threatDefense = FindThreatDefenseMove(fullCandidates);
            if (threatDefense.IsValid)
            {
                return threatDefense;
            }
        }

        List<GomokuMove> searchCandidates = GenerateCandidates(StoneColor.White, true);
        if (searchCandidates.Count == 0)
        {
            return FindFallbackMove();
        }

        return FindBestMinimaxMove(searchCandidates, clampedDepth);
    }

    /// <summary>
    /// 제한된 후보 목록에서 minimax 기준 최선 수를 찾음.
    /// </summary>
    private GomokuMove FindBestMinimaxMove(List<GomokuMove> candidates, int searchDepth)
    {
        GomokuMove bestMove = GomokuMove.Invalid("No evaluated move");
        int bestScore = int.MinValue;
        int alpha = int.MinValue + 1;
        int beta = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            GomokuMove candidate = candidates[i];
            int score;

            // AI 가상 착수 후 예외가 발생해도 반드시 복구함.
            PlaceTemporary(candidate.X, candidate.Y, StoneColor.White);
            try
            {
                score = Minimax(searchDepth - 1, false, alpha, beta, candidate, StoneColor.White);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            if (!bestMove.IsValid || score > bestScore)
            {
                bestScore = score;
                bestMove = new GomokuMove(candidate.X, candidate.Y, score, $"Minimax depth {searchDepth}");
            }

            alpha = Mathf.Max(alpha, bestScore);
        }

        return bestMove.IsValid ? bestMove : FindFallbackMove();
    }

    /// <summary>
    /// minimax와 alpha-beta pruning으로 현재 분기의 점수를 계산함.
    /// </summary>
    private int Minimax(int depth, bool isAiTurn, int alpha, int beta, GomokuMove lastMove, StoneColor lastColor)
    {
        if (lastMove.IsValid && _logic.CheckWin(lastMove.X, lastMove.Y, lastColor))
        {
            return lastColor == StoneColor.White ? WinScore + depth : -WinScore - depth;
        }

        if (depth <= 0)
        {
            return _evaluator.Evaluate(_logic, _boardSize);
        }

        StoneColor currentColor = isAiTurn ? StoneColor.White : StoneColor.Black;
        List<GomokuMove> candidates = GenerateCandidates(currentColor, true);

        if (candidates.Count == 0)
        {
            return _evaluator.Evaluate(_logic, _boardSize);
        }

        if (isAiTurn)
        {
            return EvaluateMaxBranch(depth, alpha, beta, candidates, currentColor);
        }

        return EvaluateMinBranch(depth, alpha, beta, candidates, currentColor);
    }

    /// <summary>
    /// AI 차례의 maximizing 분기를 평가함.
    /// </summary>
    private int EvaluateMaxBranch(int depth, int alpha, int beta, List<GomokuMove> candidates, StoneColor currentColor)
    {
        int bestScore = int.MinValue + 1;

        for (int i = 0; i < candidates.Count; i++)
        {
            GomokuMove candidate = candidates[i];
            int score;

            // 탐색용 착수는 원본 보드를 오염시키지 않도록 반드시 되돌림.
            PlaceTemporary(candidate.X, candidate.Y, currentColor);
            try
            {
                score = Minimax(depth - 1, false, alpha, beta, candidate, currentColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            bestScore = Mathf.Max(bestScore, score);
            alpha = Mathf.Max(alpha, bestScore);

            if (beta <= alpha)
            {
                // 더 나은 결과가 나올 수 없는 분기는 가지치기함.
                break;
            }
        }

        return bestScore;
    }

    /// <summary>
    /// 플레이어 차례의 minimizing 분기를 평가함.
    /// </summary>
    private int EvaluateMinBranch(int depth, int alpha, int beta, List<GomokuMove> candidates, StoneColor currentColor)
    {
        int worstScore = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            GomokuMove candidate = candidates[i];
            int score;

            // 플레이어 응수도 가상 착수 후 반드시 복구함.
            PlaceTemporary(candidate.X, candidate.Y, currentColor);
            try
            {
                score = Minimax(depth - 1, true, alpha, beta, candidate, currentColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            worstScore = Mathf.Min(worstScore, score);
            beta = Mathf.Min(beta, worstScore);

            if (beta <= alpha)
            {
                // 플레이어가 더 나쁜 결과를 강제할 수 있는 분기는 중단함.
                break;
            }
        }

        return worstScore;
    }

    /// <summary>
    /// 즉시 승리 또는 즉시 방어가 가능한 수를 찾음.
    /// </summary>
    private GomokuMove FindImmediateMove(List<GomokuMove> candidates, StoneColor color, string reason)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            GomokuMove candidate = candidates[i];

            if (!IsLegalMove(candidate.X, candidate.Y, color))
            {
                continue;
            }

            bool isWin;

            // 한 수로 승리 가능한 좌표는 minimax보다 우선함.
            PlaceTemporary(candidate.X, candidate.Y, color);
            try
            {
                isWin = _logic.CheckWin(candidate.X, candidate.Y, color);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            if (isWin)
            {
                return new GomokuMove(candidate.X, candidate.Y, color == StoneColor.White ? WinScore : -WinScore, reason);
            }
        }

        return GomokuMove.Invalid(reason + " not found");
    }

    /// <summary>
    /// 흑돌의 열린 4와 열린 3 위협을 막을 방어 수를 찾음.
    /// </summary>
    private GomokuMove FindThreatDefenseMove(List<GomokuMove> candidates)
    {
        GomokuMove bestDefense = GomokuMove.Invalid("Threat defense not found");
        int bestThreatScore = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            GomokuMove candidate = candidates[i];

            if (!IsLegalMove(candidate.X, candidate.Y, StoneColor.Black))
            {
                continue;
            }

            int threatScore;

            // 흑돌이 해당 좌표에 두면 생기는 위협을 측정하고 바로 복구함.
            PlaceTemporary(candidate.X, candidate.Y, StoneColor.Black);
            try
            {
                threatScore = EvaluateThreatAt(candidate.X, candidate.Y, StoneColor.Black);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            if (threatScore > bestThreatScore)
            {
                bestThreatScore = threatScore;
                bestDefense = new GomokuMove(candidate.X, candidate.Y, threatScore, "Threat defense");
            }
        }

        return bestDefense;
    }

    /// <summary>
    /// 특정 좌표가 만드는 열린 4, 막힌 4, 열린 3 위협 점수를 계산함.
    /// </summary>
    private int EvaluateThreatAt(int x, int y, StoneColor color)
    {
        int bestScore = 0;

        for (int i = 0; i < DirectionX.Length; i++)
        {
            int count = CountLine(x, y, DirectionX[i], DirectionY[i], color);
            int openEnds = CountOpenEnds(x, y, DirectionX[i], DirectionY[i], color);

            if (count >= 4 && openEnds == 2)
            {
                bestScore = Mathf.Max(bestScore, OpenFourThreatScore);
            }
            else if (count >= 4 && openEnds == 1)
            {
                bestScore = Mathf.Max(bestScore, BlockedFourThreatScore);
            }
            else if (count == 3 && openEnds == 2)
            {
                bestScore = Mathf.Max(bestScore, OpenThreeThreatScore);
            }
        }

        return bestScore;
    }

    /// <summary>
    /// 기존 돌 주변 반경 안에서 후보 수를 생성함.
    /// </summary>
    private List<GomokuMove> GenerateCandidates(StoneColor color, bool limitCandidates)
    {
        List<GomokuMove> candidates = new List<GomokuMove>();
        bool hasStone = false;

        for (int x = 0; x < _boardSize; x++)
        {
            for (int y = 0; y < _boardSize; y++)
            {
                if (_logic.Board[x, y].Color != StoneColor.None)
                {
                    hasStone = true;
                    AddNearbyCandidates(candidates, x, y, color);
                }
            }
        }

        if (!hasStone)
        {
            int center = _boardSize / 2;
            candidates.Add(new GomokuMove(center, center, 0, "Center fallback"));
        }

        SortCandidates(candidates, color);

        if (limitCandidates && candidates.Count > MaxCandidateCount)
        {
            candidates.RemoveRange(MaxCandidateCount, candidates.Count - MaxCandidateCount);
        }

        return candidates;
    }

    /// <summary>
    /// 특정 돌 주변의 빈 좌표를 후보 목록에 추가함.
    /// </summary>
    private void AddNearbyCandidates(List<GomokuMove> candidates, int originX, int originY, StoneColor color)
    {
        for (int x = originX - CandidateRadius; x <= originX + CandidateRadius; x++)
        {
            for (int y = originY - CandidateRadius; y <= originY + CandidateRadius; y++)
            {
                if (!IsLegalMove(x, y, color) || ContainsMove(candidates, x, y))
                {
                    continue;
                }

                int score = _evaluator.EvaluateMove(_logic, _boardSize, x, y, color);
                candidates.Add(new GomokuMove(x, y, score, "Nearby candidate"));
            }
        }
    }

    /// <summary>
    /// 특정 방향 양쪽의 연속 돌 개수를 합산함.
    /// </summary>
    private int CountLine(int x, int y, int directionX, int directionY, StoneColor color)
    {
        return 1 +
               CountSameColor(x, y, directionX, directionY, color) +
               CountSameColor(x, y, -directionX, -directionY, color);
    }

    /// <summary>
    /// 특정 방향 양쪽 끝의 열린 상태 개수를 계산함.
    /// </summary>
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

    /// <summary>
    /// 지정 좌표가 빈 칸인지 확인함.
    /// </summary>
    private bool IsEmpty(int x, int y)
    {
        return _logic.IsInside(x, y) && _logic.Board[x, y].Color == StoneColor.None;
    }

    /// <summary>
    /// 후보 수를 평가 점수와 색상 역할 기준으로 정렬함.
    /// </summary>
    private void SortCandidates(List<GomokuMove> candidates, StoneColor color)
    {
        if (color == StoneColor.Black)
        {
            // 백돌 AI 평가 기준에서 흑돌 응수는 낮은 점수가 더 위협적인 수임.
            candidates.Sort((left, right) => left.Score.CompareTo(right.Score));
            return;
        }

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
    }

    /// <summary>
    /// 후보 목록에 같은 좌표가 이미 있는지 확인함.
    /// </summary>
    private bool ContainsMove(List<GomokuMove> candidates, int x, int y)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].X == x && candidates[i].Y == y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 해당 색상이 지정 좌표에 둘 수 있는지 확인함.
    /// </summary>
    private bool IsLegalMove(int x, int y, StoneColor color)
    {
        if (!_logic.IsInside(x, y) || _logic.Board[x, y].Color != StoneColor.None)
        {
            return false;
        }

        if (color == StoneColor.Black && _logic.IsForbidden(x, y, color))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 탐색용 임시 돌을 보드에 배치함.
    /// </summary>
    private void PlaceTemporary(int x, int y, StoneColor color)
    {
        _logic.Board[x, y] = new StoneData { Color = color, IsFake = false };
    }

    /// <summary>
    /// 탐색용 임시 돌을 보드에서 제거함.
    /// </summary>
    private void RestoreTemporary(int x, int y)
    {
        _logic.Board[x, y] = new StoneData { Color = StoneColor.None, IsFake = false };
    }

    /// <summary>
    /// 후보가 없을 때 사용할 중앙 또는 첫 빈 좌표를 반환함.
    /// </summary>
    private GomokuMove FindFallbackMove()
    {
        int center = _boardSize / 2;
        if (_logic.IsInside(center, center) && _logic.Board[center, center].Color == StoneColor.None)
        {
            return new GomokuMove(center, center, 0, "Center fallback");
        }

        for (int x = 0; x < _boardSize; x++)
        {
            for (int y = 0; y < _boardSize; y++)
            {
                if (_logic.Board[x, y].Color == StoneColor.None)
                {
                    return new GomokuMove(x, y, 0, "First empty fallback");
                }
            }
        }

        return GomokuMove.Invalid("No empty position");
    }
}
