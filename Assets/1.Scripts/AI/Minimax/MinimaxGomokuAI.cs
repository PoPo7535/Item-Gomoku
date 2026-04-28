using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// 오목 AI의 후보 생성과 minimax 의사결정을 담당함.
/// </summary>
public class MinimaxGomokuAI : IGomokuAI
{
    private const bool EnableAiDebugLog = false;
    private const bool EnableAiStatsLog = false;

    /// <summary>
    /// 특정 좌표의 위협 형태를 정리한 결과임.
    /// </summary>
    private struct ThreatAnalysis
    {
        public int Score;
        public int OpenThreeCount;
        public int BlockedFourCount;
        public int OpenFourCount;
    }

    /// <summary>
    /// 후보 생성 목적에 따라 평가 비용과 후보 상한을 구분함.
    /// </summary>
    private enum CandidateGenerationMode
    {
        RootEvaluation,
        SearchNode,
        ThreatScan
    }

    private sealed class SearchTimeoutException : System.Exception
    {
    }

    private const int WinScore = 10000000;
    private const int OpenFourThreatScore = 900000;
    private const int BlockedFourThreatScore = 300000;
    private const int OpenThreeThreatScore = 50000;
    private const int CandidateRadius = 2;
    private const int MaxCandidateCount = 18;
    private const int SearchNodeCandidateCount = 10;
    private const int ThreatScanCandidateCount = 24;
    private const int NormalPreciseRiskCandidateCount = 6;
    private const int HardPreciseRiskCandidateCount = 8;
    private const int BlockedFourOpenThreeRiskPenalty = 600000;
    private const int NormalStructuralRiskPenaltyBonus = 400000;
    private const int NormalFutureRouteRiskPenalty = 350000;
    private const int HardFutureRouteRiskPenalty = 550000;
    private const int NormalForcedComboResponsePenalty = 8000000;
    private const int HardForcedComboResponsePenalty = 9500000;
    private const int OpenFourOrderingBonus = 800000;
    private const int BlockedFourOrderingBonus = 250000;
    private const int OpenThreeOrderingBonus = 45000;
    private const int CompositeThreatOrderingBonus = 180000;
    private const int DefenseOrderingBonusDivisor = 2;

    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    private readonly OmokuLogic _logic;
    private readonly GomokuBoardEvaluator _evaluator;
    private readonly int _boardSize;
    private readonly StoneColor _aiColor;
    private readonly StoneColor _opponentColor;
    private CancellationToken _cancellationToken;
    private Stopwatch _searchStopwatch;
    private double _maxSearchTimeSeconds;
    private GomokuMove _bestMoveSoFar;
    private readonly Dictionary<int, int> _rootEvaluationCache = new Dictionary<int, int>();
    private int _generatedCandidateCallCount;
    private int _rootGeneratedCandidateCount;
    private int _searchNodeGeneratedCandidateCount;
    private int _threatScanGeneratedCandidateCount;
    private int _evaluateMoveCallCount;
    private int _rootEvaluationCacheHitCount;
    private int _lightweightEvaluationCallCount;
    private int _analyzeThreatCallCount;
    private int _minimaxNodeCount;
    private int _pruningCount;

    /// <summary>
    /// 오목 AI를 생성함.
    /// </summary>
    public MinimaxGomokuAI(OmokuLogic logic, int boardSize)
        : this(logic, boardSize, StoneColor.White)
    {
    }

    /// <summary>
    /// 지정된 AI 색상 기준으로 오목 AI를 생성함.
    /// </summary>
    /// <param name="logic">AI가 사용할 오목 규칙과 보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <param name="aiColor">AI가 사용할 돌 색상.</param>
    public MinimaxGomokuAI(OmokuLogic logic, int boardSize, StoneColor aiColor)
    {
        _logic = logic;
        _boardSize = boardSize;
        _evaluator = new GomokuBoardEvaluator();
        _aiColor = aiColor == StoneColor.Black ? StoneColor.Black : StoneColor.White;
        _opponentColor = GetOppositeColor(_aiColor);
    }

    /// <summary>
    /// 지정한 탐색 깊이로 백돌 AI의 최선 수를 찾음.
    /// </summary>
    public GomokuMove FindBestMove(int searchDepth)
    {
        return FindBestMove(searchDepth, CancellationToken.None);
    }

    /// <summary>
    /// 지정한 탐색 깊이와 취소 토큰으로 백돌 AI의 최선 수를 찾음.
    /// </summary>
    /// <param name="searchDepth">탐색 깊이.</param>
    /// <param name="cancellationToken">탐색 취소 토큰.</param>
    /// <returns>선택된 AI 착수 후보.</returns>
    public GomokuMove FindBestMove(int searchDepth, CancellationToken cancellationToken)
    {
        BeginSearch(cancellationToken, 0d);
        try
        {
            return FindBestMoveCore(searchDepth);
        }
        finally
        {
            EndSearch();
        }
    }

    /// <summary>
    /// 지정한 탐색 깊이, 취소 토큰, 시간 제한으로 백돌 AI의 최선 수를 찾음.
    /// </summary>
    /// <param name="searchDepth">탐색 깊이.</param>
    /// <param name="cancellationToken">탐색 취소 토큰.</param>
    /// <param name="maxSearchTimeSeconds">탐색 시간 제한 초 단위 값.</param>
    /// <returns>AI 탐색 상태와 선택된 착수 후보.</returns>
    public GomokuAISearchResult FindBestMove(int searchDepth, CancellationToken cancellationToken, double maxSearchTimeSeconds)
    {
        BeginSearch(cancellationToken, maxSearchTimeSeconds);
        try
        {
            GomokuMove move = FindBestMoveCore(searchDepth);
            double elapsedSeconds = GetElapsedSearchSeconds();
            GomokuAISearchResult result = move.IsValid
                ? GomokuAISearchResult.Completed(move, elapsedSeconds)
                : GomokuAISearchResult.NoMove(move.Reason, elapsedSeconds);
            LogSearchStats(result.Status, result.Move);
            return result;
        }
        catch (SearchTimeoutException)
        {
            GomokuAISearchResult result = GomokuAISearchResult.TimedOut(GetBestMoveSoFarOrFallback(), GetElapsedSearchSeconds());
            LogSearchStats(result.Status, result.Move);
            return result;
        }
        finally
        {
            EndSearch();
        }
    }

    /// <summary>
    /// 현재 설정된 탐색 조건으로 백돌 AI의 최선 수를 계산함.
    /// </summary>
    /// <param name="searchDepth">탐색 깊이.</param>
    /// <returns>선택된 AI 착수 후보.</returns>
    private GomokuMove FindBestMoveCore(int searchDepth)
    {
        ThrowIfCancellationRequested();
        int clampedDepth = System.Math.Min(System.Math.Max(searchDepth, 1), 5);
        bool isHardDifficulty = IsHardDifficulty(clampedDepth);
        bool shouldPrioritizeFutureRouteDefense = !IsEasyDifficulty(clampedDepth);
        List<GomokuMove> fullCandidates = GenerateCandidates(_aiColor, CandidateGenerationMode.RootEvaluation, false);
        LogAiDebug($"FindBestMove depth={clampedDepth}, fullCandidates={FormatCandidateList(fullCandidates)}");

        if (fullCandidates.Count == 0)
        {
            return FindFallbackMove();
        }

        _bestMoveSoFar = fullCandidates[0];

        GomokuMove openingMove = FindOpeningMove();
        if (openingMove.IsValid)
        {
            return openingMove;
        }

        GomokuMove immediateWin = FindImmediateMove(fullCandidates, _aiColor, "Immediate win");
        if (immediateWin.IsValid)
        {
            LogAiDebug($"Immediate win selected {FormatMove(immediateWin)}");
            return immediateWin;
        }

        GomokuMove immediateDefense = FindImmediateMove(fullCandidates, _opponentColor, "Immediate defense");
        if (immediateDefense.IsValid)
        {
            LogAiDebug($"Immediate defense selected {FormatMove(immediateDefense)}");
            return immediateDefense;
        } 

        GomokuMove threatDefense = FindThreatDefenseMove(fullCandidates, isHardDifficulty, shouldPrioritizeFutureRouteDefense);
        if (threatDefense.IsValid && IsForcedThreatDefense(threatDefense, isHardDifficulty))
        {
            // Hard에서는 복합 강제 위협도 minimax 전에 바로 차단함.
            return threatDefense;
        }

        if (IsEasyDifficulty(clampedDepth))
        {
            GomokuMove easyOpenThreeDefense = FindEasyOpenThreeDefenseMove();
            if (easyOpenThreeDefense.IsValid)
            {
                return easyOpenThreeDefense;
            }
        }

        List<GomokuMove> searchCandidates = GenerateCandidates(_aiColor, CandidateGenerationMode.RootEvaluation, true);
        if (searchCandidates.Count == 0)
        {
            return FindFallbackMove();
        }

        if (shouldPrioritizeFutureRouteDefense)
        {
            EnsureMinimaxDefenseCandidates(searchCandidates, fullCandidates, isHardDifficulty);
        }

        if (IsNormalDifficulty(clampedDepth))
        {
            ApplyNormalStructuralRiskPenalty(searchCandidates);
            ApplyFutureRouteRiskPenalty(searchCandidates, false);
        }
        else if (isHardDifficulty)
        {
            ApplyFutureRouteRiskPenalty(searchCandidates, true);
        }

        if (isHardDifficulty)
        {
            EnsureHardDefenseCandidate(searchCandidates, threatDefense);
        }

        return FindBestMinimaxMove(searchCandidates, clampedDepth);
    }

    /// <summary>
    /// 현재 탐색 깊이가 Easy 난이도 구간인지 확인함.
    /// </summary>
    /// <param name="searchDepth">현재 탐색 깊이.</param>
    /// <returns>Easy 난이도 여부.</returns>
    private bool IsEasyDifficulty(int searchDepth)
    {
        return searchDepth <= 2;
    }

    /// <summary>
    /// 현재 탐색 깊이가 Normal 난이도 구간인지 확인함.
    /// </summary>
    /// <param name="searchDepth">현재 탐색 깊이.</param>
    /// <returns>Normal 난이도 여부.</returns>
    private bool IsNormalDifficulty(int searchDepth)
    {
        return searchDepth >= 3 && searchDepth <= 4;
    }

    /// <summary>
    /// 현재 탐색 깊이가 Hard 난이도 구간인지 확인함.
    /// </summary>
    /// <param name="searchDepth">현재 탐색 깊이.</param>
    /// <returns>Hard 난이도 여부.</returns>
    private bool IsHardDifficulty(int searchDepth)
    {
        return searchDepth >= 5;
    }

    /// <summary>
    /// Normal 난이도 후보에서 플레이어의 다음 치명 발전 구조를 더 강하게 깎음.
    /// </summary>
    /// <param name="candidates">보정할 백돌 후보 목록.</param>
    private void ApplyNormalStructuralRiskPenalty(List<GomokuMove> candidates)
    {
        int preciseCandidateCount = System.Math.Min(candidates.Count, NormalPreciseRiskCandidateCount);
        for (int i = 0; i < preciseCandidateCount; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];
            int followUpRisk = EvaluatePlayerFollowUpRiskAfterAiMove(candidate.X, candidate.Y);
            if (followUpRisk <= 0)
            {
                continue;
            }

            // Normal에서는 다음 합법 발전 구조를 허용하는 수를 더 아래로 내림.
            candidate.Score -= NormalStructuralRiskPenaltyBonus;
            candidate.Reason = "Normal structural defense";
            candidates[i] = candidate;
        }

        SortCandidates(candidates, _aiColor);
    }

    /// <summary>
    /// Normal 또는 Hard에서 플레이어 우회 수 이후 다음 턴 치명 발전점이 남는 후보를 추가로 감점함.
    /// </summary>
    /// <param name="candidates">보정할 백돌 후보 목록.</param>
    /// <param name="isHardDifficulty">Hard 난이도 여부.</param>
    private void ApplyFutureRouteRiskPenalty(List<GomokuMove> candidates, bool isHardDifficulty)
    {
        int preciseCandidateCount = System.Math.Min(candidates.Count, isHardDifficulty ? HardPreciseRiskCandidateCount : NormalPreciseRiskCandidateCount);
        for (int i = 0; i < preciseCandidateCount; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];
            int futureRouteRisk = EvaluatePlayerFutureRouteRiskAfterAiMove(candidate.X, candidate.Y, isHardDifficulty) +
                                  EvaluatePlayerStrongestResponseRiskAfterAiMove(candidate.X, candidate.Y, isHardDifficulty);
            if (futureRouteRisk <= 0)
            {
                continue;
            }

            // 우회 수 뒤 핵심 완성점이 살아 있으면 구조적으로 위험한 후보로 봄.
            candidate.Score -= futureRouteRisk;
            candidate.Reason = isHardDifficulty ? "Hard future-route defense" : "Normal future-route defense";
            candidates[i] = candidate;
        }

        SortCandidates(candidates, _aiColor);
        LogAiDebug($"ApplyFutureRouteRiskPenalty result={FormatCandidateList(candidates)}");
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
        bool shouldApplyResponsePenalty = !IsEasyDifficulty(searchDepth);

        if (candidates.Count > 0)
        {
            // 첫 후보를 기본 fallback으로 보관해 시간 초과 시에도 착수 후보를 잃지 않음.
            _bestMoveSoFar = candidates[0];
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];
            int score;

            // AI 가상 착수 후 예외가 발생해도 반드시 복구함.
            PlaceTemporary(candidate.X, candidate.Y, _aiColor);
            try
            {
                score = Minimax(searchDepth - 1, false, alpha, beta, candidate, _aiColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            if (shouldApplyResponsePenalty)
            {
                // 플레이어 최강 응수를 허용하는 수는 minimax 점수에서도 직접 크게 깎음.
                int responsePenalty = EvaluatePlayerStrongestResponseRiskAfterAiMove(candidate.X, candidate.Y, IsHardDifficulty(searchDepth));
                score -= responsePenalty;
                LogAiDebug($"Minimax candidate {FormatMove(candidate)} raw={score + responsePenalty}, responsePenalty={responsePenalty}, final={score}");
            }
            else
            {
                LogAiDebug($"Minimax candidate {FormatMove(candidate)} raw={score}, responsePenalty=0, final={score}");
            }

            if (!bestMove.IsValid || score > bestScore)
            {
                bestScore = score;
                bestMove = new GomokuMove(candidate.X, candidate.Y, score, $"Minimax depth {searchDepth}");
                _bestMoveSoFar = bestMove;
            }

            alpha = System.Math.Max(alpha, bestScore);
        }

        return bestMove.IsValid ? bestMove : FindFallbackMove();
    }

    /// <summary>
    /// minimax와 alpha-beta pruning으로 현재 분기의 점수를 계산함.
    /// </summary>
    private int Minimax(int depth, bool isAiTurn, int alpha, int beta, GomokuMove lastMove, StoneColor lastColor)
    {
        ThrowIfCancellationRequested();
        _minimaxNodeCount++;

        if (lastMove.IsValid && _logic.CheckWin(lastMove.X, lastMove.Y, lastColor))
        {
            return lastColor == _aiColor ? WinScore + depth : -WinScore - depth;
        }

        if (depth <= 0)
        {
            return _evaluator.Evaluate(_logic, _boardSize, _aiColor);
        }

        StoneColor currentColor = isAiTurn ? _aiColor : _opponentColor;
        List<GomokuMove> candidates = GenerateCandidates(currentColor, CandidateGenerationMode.SearchNode, true);

        if (candidates.Count == 0)
        {
            return _evaluator.Evaluate(_logic, _boardSize, _aiColor);
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
            ThrowIfCancellationRequested();
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

            bestScore = System.Math.Max(bestScore, score);
            alpha = System.Math.Max(alpha, bestScore);

            if (beta <= alpha)
            {
                _pruningCount++;
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
            ThrowIfCancellationRequested();
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

            worstScore = System.Math.Min(worstScore, score);
            beta = System.Math.Min(beta, worstScore);

            if (beta <= alpha)
            {
                _pruningCount++;
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
            ThrowIfCancellationRequested();
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
                return new GomokuMove(candidate.X, candidate.Y, color == _aiColor ? WinScore : -WinScore, reason);
            }
        }

        return GomokuMove.Invalid(reason + " not found");
    }

    /// <summary>
    /// 흑돌의 열린 4와 열린 3 위협을 막을 방어 수를 찾음.
    /// </summary>
    private GomokuMove FindThreatDefenseMove(List<GomokuMove> candidates, bool isHardDifficulty, bool shouldPrioritizeFutureRouteDefense)
    {
        GomokuMove bestDefense = GomokuMove.Invalid("Threat defense not found");
        int bestDefenseScore = int.MinValue;
        bool foundForcedDefense = false;
        int bestBaseThreatPriority = int.MinValue;
        int bestPromotedThreatPriority = int.MinValue;
        int bestFutureRouteRisk = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];

            if (!IsLegalMove(candidate.X, candidate.Y, _opponentColor))
            {
                continue;
            }

            ThreatAnalysis threatAnalysis;

            // 흑돌이 해당 좌표에 두면 생기는 위협을 측정하고 바로 복구함.
            PlaceTemporary(candidate.X, candidate.Y, _opponentColor);
            try
            {
                threatAnalysis = AnalyzeThreatAt(candidate.X, candidate.Y, _opponentColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            int defenseScore = threatAnalysis.Score;

            // 플레이어 열린 3 대응에서는 다음 턴 복합 위협 함정을 추가로 피해야 함.
            if (threatAnalysis.Score == OpenThreeThreatScore)
            {
                defenseScore -= EvaluatePlayerFollowUpRiskAfterAiMove(candidate.X, candidate.Y);
            }

            bool isForcedDefense = IsForcedThreatAnalysis(threatAnalysis, isHardDifficulty);
            bool shouldRunPreciseFutureCheck = shouldPrioritizeFutureRouteDefense && i < GetPreciseThreatDefenseCandidateCount(isHardDifficulty);
            int futureRouteRisk = shouldRunPreciseFutureCheck
                ? EvaluatePlayerFutureRouteRiskAfterAiMove(candidate.X, candidate.Y, isHardDifficulty)
                : 0;
            bool blocksFutureComboFinisher = shouldRunPreciseFutureCheck &&
                                             BlocksPlayerFutureComboFinisher(candidate.X, candidate.Y);
            int baseThreatPriority = GetThreatPriority(threatAnalysis);
            int promotedThreatPriority = GetPromotedThreatPriority(threatAnalysis, blocksFutureComboFinisher);
            LogAiDebug(
                $"ThreatDefense candidate=({candidate.X},{candidate.Y}) score={threatAnalysis.Score} " +
                $"open3={threatAnalysis.OpenThreeCount} blocked4={threatAnalysis.BlockedFourCount} open4={threatAnalysis.OpenFourCount} " +
                $"forced={isForcedDefense} baseTier={baseThreatPriority} promotedTier={promotedThreatPriority} " +
                $"futureRisk={futureRouteRisk} defenseScore={defenseScore}");

            // Normal/Hard에서는 다음 턴 치명 완성 루트를 허용하는 방어 수를 먼저 탈락시킴.
            if (!bestDefense.IsValid ||
                (isForcedDefense && !foundForcedDefense) ||
                (isForcedDefense == foundForcedDefense &&
                 (baseThreatPriority > bestBaseThreatPriority ||
                  (baseThreatPriority == bestBaseThreatPriority &&
                   (promotedThreatPriority > bestPromotedThreatPriority ||
                    (promotedThreatPriority == bestPromotedThreatPriority &&
                     (futureRouteRisk < bestFutureRouteRisk ||
                      (futureRouteRisk == bestFutureRouteRisk && defenseScore > bestDefenseScore))))))))
            {
                bestDefenseScore = defenseScore;
                foundForcedDefense = isForcedDefense;
                bestBaseThreatPriority = baseThreatPriority;
                bestPromotedThreatPriority = promotedThreatPriority;
                bestFutureRouteRisk = futureRouteRisk;
                bestDefense = new GomokuMove(candidate.X, candidate.Y, defenseScore, GetThreatDefenseReason(threatAnalysis));
            }
        }

        return bestDefense;
    }

    /// <summary>
    /// 특정 AI 수 이후 플레이어가 막힌 4와 열린 3 함정을 만들 수 있는지 평가함.
    /// </summary>
    /// <param name="aiMoveX">AI 착수 X 좌표.</param>
    /// <param name="aiMoveY">AI 착수 Y 좌표.</param>
    /// <returns>후속 강제패 위험에 대한 페널티 점수.</returns>
    private int EvaluatePlayerFollowUpRiskAfterAiMove(int aiMoveX, int aiMoveY)
    {
        int penalty = 0;

        // AI가 해당 위치를 둔 뒤 플레이어 다음 수의 복합 위협을 확인함.
        PlaceTemporary(aiMoveX, aiMoveY, _aiColor);
        try
        {
            List<GomokuMove> playerResponses = GenerateCandidates(_opponentColor, CandidateGenerationMode.ThreatScan, true);
            for (int i = 0; i < playerResponses.Count; i++)
            {
                ThrowIfCancellationRequested();
                GomokuMove response = playerResponses[i];
                if (!IsLegalMove(response.X, response.Y, _opponentColor))
                {
                    continue;
                }

                if (_evaluator.CreatesBlockedFourOpenThreeThreat(_logic, _boardSize, response.X, response.Y, _opponentColor))
                {
                    // 방어 후 바로 복합 위협을 허용하면 거의 강제패 함정으로 취급함.
                    penalty = System.Math.Max(penalty, BlockedFourOpenThreeRiskPenalty);
                }
            }
        }
        finally
        {
            RestoreTemporary(aiMoveX, aiMoveY);
        }

        return penalty;
    }

    /// <summary>
    /// AI 수 이후 플레이어 우회 수와 다음 턴 치명 완성점까지 이어지는 루트를 평가함.
    /// </summary>
    /// <param name="aiMoveX">AI 착수 X 좌표.</param>
    /// <param name="aiMoveY">AI 착수 Y 좌표.</param>
    /// <param name="isHardDifficulty">Hard 난이도 여부.</param>
    /// <returns>우회 루트를 허용하면 감점할 위험 점수.</returns>
    private int EvaluatePlayerFutureRouteRiskAfterAiMove(int aiMoveX, int aiMoveY, bool isHardDifficulty)
    {
        int penalty = 0;

        PlaceTemporary(aiMoveX, aiMoveY, _aiColor);
        try
        {
            List<GomokuMove> playerResponses = GenerateCandidates(_opponentColor, CandidateGenerationMode.ThreatScan, true);
            for (int i = 0; i < playerResponses.Count; i++)
            {
                ThrowIfCancellationRequested();
                GomokuMove response = playerResponses[i];
                if (!IsLegalMove(response.X, response.Y, _opponentColor))
                {
                    continue;
                }

                // 플레이어가 우회 수를 둔 뒤에도 핵심 완성점이 남는지 확인함.
                PlaceTemporary(response.X, response.Y, _opponentColor);
                try
                {
                    if (HasPlayerFutureComboFinisher())
                    {
                        penalty = System.Math.Max(penalty, isHardDifficulty ? HardFutureRouteRiskPenalty : NormalFutureRouteRiskPenalty);
                    }
                }
                finally
                {
                    RestoreTemporary(response.X, response.Y);
                }
            }
        }
        finally
        {
            RestoreTemporary(aiMoveX, aiMoveY);
        }

        return penalty;
    }

    /// <summary>
    /// AI 수 이후 플레이어의 즉시 최강 응수가 치명 조합이면 매우 큰 패널티를 반환함.
    /// </summary>
    /// <param name="aiMoveX">AI 착수 X 좌표.</param>
    /// <param name="aiMoveY">AI 착수 Y 좌표.</param>
    /// <param name="isHardDifficulty">Hard 난이도 여부.</param>
    /// <returns>플레이어 최강 응수 허용에 대한 큰 패널티.</returns>
    private int EvaluatePlayerStrongestResponseRiskAfterAiMove(int aiMoveX, int aiMoveY, bool isHardDifficulty)
    {
        int penalty = 0;

        PlaceTemporary(aiMoveX, aiMoveY, _aiColor);
        try
        {
            List<GomokuMove> playerResponses = GenerateCandidates(_opponentColor, CandidateGenerationMode.ThreatScan, true);
            for (int i = 0; i < playerResponses.Count; i++)
            {
                ThrowIfCancellationRequested();
                GomokuMove response = playerResponses[i];
                if (!IsLegalMove(response.X, response.Y, _opponentColor))
                {
                    continue;
                }

                if (_evaluator.CreatesBlockedFourOpenThreeThreat(_logic, _boardSize, response.X, response.Y, _opponentColor))
                {
                    // 빨간 칸처럼 최강 응수를 바로 허용하는 수는 패배 수에 가깝게 벌점 줌.
                    LogAiDebug(
                        $"Strongest response detected ai=({aiMoveX},{aiMoveY}) response=({response.X},{response.Y}) " +
                        $"penalty={(isHardDifficulty ? HardForcedComboResponsePenalty : NormalForcedComboResponsePenalty)}");
                    penalty = System.Math.Max(
                        penalty,
                        isHardDifficulty ? HardForcedComboResponsePenalty : NormalForcedComboResponsePenalty);
                }
            }
        }
        finally
        {
            RestoreTemporary(aiMoveX, aiMoveY);
        }

        return penalty;
    }

    /// <summary>
    /// 현재 보드에서 플레이어가 다음 턴 치명 조합을 완성할 합법 좌표가 남아 있는지 확인함.
    /// </summary>
    /// <returns>막힌 4와 열린 3 조합 완성점 존재 여부.</returns>
    private bool HasPlayerFutureComboFinisher()
    {
        List<GomokuMove> playerFinishers = GenerateCandidates(_opponentColor, CandidateGenerationMode.ThreatScan, true);
        for (int i = 0; i < playerFinishers.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove finisher = playerFinishers[i];
            if (!IsLegalMove(finisher.X, finisher.Y, _opponentColor))
            {
                continue;
            }

            if (_evaluator.CreatesBlockedFourOpenThreeThreat(_logic, _boardSize, finisher.X, finisher.Y, _opponentColor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 현재 AI 후보가 플레이어의 다음 합법 치명 완성점을 직접 막는 좌표인지 확인함.
    /// </summary>
    /// <param name="aiMoveX">확인할 AI 후보 X 좌표.</param>
    /// <param name="aiMoveY">확인할 AI 후보 Y 좌표.</param>
    /// <returns>플레이어 미래 완성점 좌표를 직접 차단하는지 여부.</returns>
    private bool BlocksPlayerFutureComboFinisher(int aiMoveX, int aiMoveY)
    {
        if (!IsLegalMove(aiMoveX, aiMoveY, _aiColor))
        {
            return false;
        }

        PlaceTemporary(aiMoveX, aiMoveY, _aiColor);
        try
        {
            List<GomokuMove> playerResponses = GenerateCandidates(_opponentColor, CandidateGenerationMode.ThreatScan, true);
            for (int i = 0; i < playerResponses.Count; i++)
            {
                ThrowIfCancellationRequested();
                GomokuMove response = playerResponses[i];
                if (!IsLegalMove(response.X, response.Y, _opponentColor))
                {
                    continue;
                }

                // 플레이어 우회 응수 뒤, 지금 칸이 미래 완성점이었는지 역으로 확인함.
                PlaceTemporary(response.X, response.Y, _opponentColor);
                try
                {
                    RestoreTemporary(aiMoveX, aiMoveY);

                    if (IsLegalMove(aiMoveX, aiMoveY, _opponentColor) &&
                        _evaluator.CreatesBlockedFourOpenThreeThreat(_logic, _boardSize, aiMoveX, aiMoveY, _opponentColor))
                    {
                        LogAiDebug($"Future combo finisher blocked by ai=({aiMoveX},{aiMoveY}) after response=({response.X},{response.Y})");
                        return true;
                    }
                }
                finally
                {
                    PlaceTemporary(aiMoveX, aiMoveY, _aiColor);
                    RestoreTemporary(response.X, response.Y);
                }
            }
        }
        finally
        {
            RestoreTemporary(aiMoveX, aiMoveY);
        }

        return false;
    }

    /// <summary>
    /// 현재 위협 방어 수가 즉시 반영해야 할 강제 방어인지 확인함.
    /// </summary>
    /// <param name="threatDefense">평가된 위협 방어 수.</param>
    /// <param name="isHardDifficulty">Hard 난이도 여부.</param>
    /// <returns>즉시 방어가 필요한 강한 위협 여부.</returns>
    private bool IsForcedThreatDefense(GomokuMove threatDefense, bool isHardDifficulty)
    {
        if (!threatDefense.IsValid)
        {
            return false;
        }

        if (threatDefense.Reason == "Open four defense")
        {
            return true;
        }

        return threatDefense.Reason == "Blocked four defense";
    }

    /// <summary>
    /// Easy 난이도에서 플레이어 열린 3의 양 끝을 직접 찾아 차단 수를 반환함.
    /// </summary>
    /// <returns>차단할 끝점 수, 없으면 Invalid.</returns>
    private GomokuMove FindEasyOpenThreeDefenseMove()
    {
        for (int x = 0; x < _boardSize; x++)
        {
            ThrowIfCancellationRequested();
            for (int y = 0; y < _boardSize; y++)
            {
                if (_logic.Board[x, y].Color != _opponentColor || _logic.Board[x, y].IsFake)
                {
                    continue;
                }

                for (int directionIndex = 0; directionIndex < DirectionX.Length; directionIndex++)
                {
                    if (TryFindDirectOpenThreeBlockMove(x, y, DirectionX[directionIndex], DirectionY[directionIndex], out GomokuMove defenseMove))
                    {
                        return defenseMove;
                    }
                }
            }
        }

        return GomokuMove.Invalid("Easy open three defense not found");
    }

    /// <summary>
    /// 특정 방향의 열린 3 또는 변형 패턴을 확인하고 즉시 차단 수를 찾음.
    /// </summary>
    /// <param name="startX">검사 시작 X 좌표.</param>
    /// <param name="startY">검사 시작 Y 좌표.</param>
    /// <param name="directionX">검사 방향 X.</param>
    /// <param name="directionY">검사 방향 Y.</param>
    /// <param name="defenseMove">찾은 차단 수.</param>
    /// <returns>유효한 열린 3 차단 수를 찾았는지 여부.</returns>
    private bool TryFindDirectOpenThreeBlockMove(int startX, int startY, int directionX, int directionY, out GomokuMove defenseMove)
    {
        defenseMove = GomokuMove.Invalid("Easy open three defense not found");
        int previousX = startX - directionX;
        int previousY = startY - directionY;
        if (IsOpponentStone(previousX, previousY))
        {
            return false;
        }

        int x1 = startX + directionX;
        int y1 = startY + directionY;
        int x2 = startX + (directionX * 2);
        int y2 = startY + (directionY * 2);
        int x3 = startX + (directionX * 3);
        int y3 = startY + (directionY * 3);
        int x4 = startX + (directionX * 4);
        int y4 = startY + (directionY * 4);

        if (IsEmpty(previousX, previousY) &&
            IsOpponentStone(startX, startY) &&
            IsOpponentStone(x1, y1) &&
            IsOpponentStone(x2, y2) &&
            IsEmpty(x3, y3))
        {
            // 연속 열린 3은 양 끝 중 아무 곳이나 바로 막음.
            return TryCreateImmediateDefenseMove(previousX, previousY, out defenseMove) ||
                   TryCreateImmediateDefenseMove(x3, y3, out defenseMove);
        }

        if (IsEmpty(previousX, previousY) &&
            IsOpponentStone(startX, startY) &&
            IsOpponentStone(x1, y1) &&
            IsEmpty(x2, y2) &&
            IsOpponentStone(x3, y3) &&
            IsEmpty(x4, y4))
        {
            // 벌어진 열린 3은 틈을 우선 막고, 필요하면 양 끝도 차단 후보로 봄.
            return TryCreateImmediateDefenseMove(x2, y2, out defenseMove) ||
                   TryCreateImmediateDefenseMove(previousX, previousY, out defenseMove) ||
                   TryCreateImmediateDefenseMove(x4, y4, out defenseMove);
        }

        if (IsEmpty(previousX, previousY) &&
            IsOpponentStone(startX, startY) &&
            IsEmpty(x1, y1) &&
            IsOpponentStone(x2, y2) &&
            IsOpponentStone(x3, y3) &&
            IsEmpty(x4, y4))
        {
            // 반대 형태의 벌어진 열린 3도 같은 방식으로 즉시 차단함.
            return TryCreateImmediateDefenseMove(x1, y1, out defenseMove) ||
                   TryCreateImmediateDefenseMove(previousX, previousY, out defenseMove) ||
                   TryCreateImmediateDefenseMove(x4, y4, out defenseMove);
        }

        return false;
    }

    /// <summary>
    /// Easy 전용 열린 3 차단 좌표를 즉시 방어 수로 변환함.
    /// </summary>
    /// <param name="blockX">차단 좌표 X.</param>
    /// <param name="blockY">차단 좌표 Y.</param>
    /// <param name="defenseMove">생성된 차단 수.</param>
    /// <returns>유효한 차단 수 생성 여부.</returns>
    private bool TryCreateImmediateDefenseMove(int blockX, int blockY, out GomokuMove defenseMove)
    {
        defenseMove = GomokuMove.Invalid("Easy open three defense not found");

        if (!IsLegalMove(blockX, blockY, _aiColor))
        {
            return false;
        }

        defenseMove = new GomokuMove(blockX, blockY, OpenThreeThreatScore, "Easy open three defense");
        return true;
    }

    /// <summary>
    /// 특정 좌표가 만드는 열린 4, 막힌 4, 열린 3 위협 점수를 계산함.
    /// </summary>
    private ThreatAnalysis AnalyzeThreatAt(int x, int y, StoneColor color)
    {
        _analyzeThreatCallCount++;
        ThreatAnalysis analysis = new ThreatAnalysis();

        for (int i = 0; i < DirectionX.Length; i++)
        {
            int count = CountLine(x, y, DirectionX[i], DirectionY[i], color);
            int openEnds = CountOpenEnds(x, y, DirectionX[i], DirectionY[i], color);

            if (count >= 4 && openEnds == 2)
            {
                analysis.OpenFourCount++;
                analysis.Score = System.Math.Max(analysis.Score, OpenFourThreatScore);
            }
            else if (count >= 4 && openEnds == 1)
            {
                analysis.BlockedFourCount++;
                analysis.Score = System.Math.Max(analysis.Score, BlockedFourThreatScore);
            }
            else if (count == 3 && openEnds == 2)
            {
                analysis.OpenThreeCount++;
                analysis.Score = System.Math.Max(analysis.Score, OpenThreeThreatScore);
            }
        }

        return analysis;
    }

    /// <summary>
    /// 분석된 위협이 즉시 차단해야 할 강제급인지 판정함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <param name="isHardDifficulty">Hard 난이도 여부.</param>
    /// <returns>강제 방어급 위협 여부.</returns>
    private bool IsForcedThreatAnalysis(ThreatAnalysis analysis, bool isHardDifficulty)
    {
        if (analysis.OpenFourCount > 0 || analysis.BlockedFourCount > 0)
        {
            return true;
        }

        // Hard에서만 막힌 4와 열린 3 복합 위협을 강제급으로 승격함.
        return isHardDifficulty && analysis.BlockedFourCount > 0 && analysis.OpenThreeCount > 0;
    }

    /// <summary>
    /// 위협 분석 결과에 맞는 방어 사유 문자열을 반환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <param name="isHardDifficulty">Hard 난이도 여부.</param>
    /// <returns>방어 사유 문자열.</returns>
    private string GetThreatDefenseReason(ThreatAnalysis analysis)
    {
        if (analysis.OpenFourCount > 0)
        {
            return "Open four defense";
        }

        if (analysis.BlockedFourCount > 0)
        {
            return "Blocked four defense";
        }

        return "Threat defense";
    }

    /// <summary>
    /// 현재 위협 분석 결과를 방어 우선순위 등급으로 변환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <returns>클수록 먼저 차단해야 하는 현재 위협 등급.</returns>
    private int GetThreatPriority(ThreatAnalysis analysis)
    {
        // 복합 위협은 일반 위협보다 항상 먼저 차단해야 함.
        if (analysis.BlockedFourCount > 0 && analysis.OpenThreeCount > 0)
        {
            return 4;
        }

        if (analysis.OpenFourCount > 0)
        {
            return 3;
        }

        if (analysis.BlockedFourCount > 0)
        {
            return 2;
        }

        if (analysis.OpenThreeCount > 0)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// 구조적 핵심 발전점을 막는 후보면 현재 위협 등급을 한 단계 승격함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <param name="blocksFutureRoute">미래 치명 완성 루트를 끊는 구조적 핵심 칸 여부.</param>
    /// <returns>승격이 반영된 현재 위협 등급.</returns>
    private int GetPromotedThreatPriority(ThreatAnalysis analysis, bool blocksFutureRoute)
    {
        int basePriority = GetThreatPriority(analysis);
        if (!blocksFutureRoute)
        {
            return basePriority;
        }

        // blocked four + open three 완성 루트를 끊는 핵심 칸은 한 단계 위협으로 승격함.
        return System.Math.Min(basePriority + 1, 4);
    }

    /// <summary>
    /// Hard 위협 방어 후보 간 우선순위를 반환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <param name="isHardDifficulty">Hard 난이도 여부.</param>
    /// <returns>클수록 먼저 차단해야 하는 위협 우선순위.</returns>
    private int GetHardThreatPriority(ThreatAnalysis analysis, bool isHardDifficulty)
    {
        if (!isHardDifficulty)
        {
            return 0;
        }

        // Hard에서는 복합 위협을 일반 막힌 4보다 먼저 차단해야 함.
        if (analysis.BlockedFourCount > 0 && analysis.OpenThreeCount > 0)
        {
            return 4;
        }

        if (analysis.OpenFourCount > 0)
        {
            return 3;
        }

        if (analysis.BlockedFourCount > 0)
        {
            return 2;
        }

        return 1;
    }

    /// <summary>
    /// AI 첫 착수 상황이면 플레이어 첫 돌 주변에서 opening 수를 선택함.
    /// </summary>
    /// <returns>opening 규칙으로 고른 첫 수, 없으면 Invalid.</returns>
    private GomokuMove FindOpeningMove()
    {
        if (!IsAiFirstMoveState(out int playerX, out int playerY))
        {
            return GomokuMove.Invalid("Opening not applicable");
        }

        List<GomokuMove> nearbyMoves = CollectAdjacentOpeningMoves(playerX, playerY);
        if (nearbyMoves.Count == 0)
        {
            return GomokuMove.Invalid("Opening candidates not found");
        }

        // 첫 수는 플레이어 첫 돌 근처에서만 고르되 evaluator로 가장 좋은 수를 선택함.
        SortCandidates(nearbyMoves, _aiColor);
        GomokuMove bestMove = nearbyMoves[0];
        return new GomokuMove(bestMove.X, bestMove.Y, bestMove.Score, "Opening response");
    }

    /// <summary>
    /// 현재 보드가 흑돌 1개, 백돌 0개인 AI 첫 착수 상태인지 확인함.
    /// </summary>
    /// <param name="playerX">플레이어 첫 돌 X 좌표.</param>
    /// <param name="playerY">플레이어 첫 돌 Y 좌표.</param>
    /// <returns>AI 첫 착수 상태 여부.</returns>
    private bool IsAiFirstMoveState(out int playerX, out int playerY)
    {
        playerX = -1;
        playerY = -1;
        int opponentStoneCount = 0;
        int aiStoneCount = 0;

        for (int x = 0; x < _boardSize; x++)
        {
            ThrowIfCancellationRequested();
            for (int y = 0; y < _boardSize; y++)
            {
                StoneData stoneData = _logic.Board[x, y];
                if (stoneData.IsFake || stoneData.Color == StoneColor.None)
                {
                    continue;
                }

                if (stoneData.Color == _opponentColor)
                {
                    opponentStoneCount++;
                    playerX = x;
                    playerY = y;
                    continue;
                }

                if (stoneData.Color == _aiColor)
                {
                    aiStoneCount++;
                }
            }
        }

        return opponentStoneCount == 1 && aiStoneCount == 0;
    }

    /// <summary>
    /// 플레이어 첫 돌 주변 8방향의 유효 opening 후보를 수집함.
    /// </summary>
    /// <param name="originX">플레이어 첫 돌 X 좌표.</param>
    /// <param name="originY">플레이어 첫 돌 Y 좌표.</param>
    /// <returns>유효한 인접 opening 후보 목록.</returns>
    private List<GomokuMove> CollectAdjacentOpeningMoves(int originX, int originY)
    {
        List<GomokuMove> nearbyMoves = new List<GomokuMove>();

        for (int deltaX = -1; deltaX <= 1; deltaX++)
        {
            for (int deltaY = -1; deltaY <= 1; deltaY++)
            {
                if (deltaX == 0 && deltaY == 0)
                {
                    continue;
                }

                int targetX = originX + deltaX;
                int targetY = originY + deltaY;
                if (!IsLegalMove(targetX, targetY, _aiColor))
                {
                    continue;
                }

                _evaluateMoveCallCount++;
                int score = _evaluator.EvaluateMove(_logic, _boardSize, targetX, targetY, _aiColor, _aiColor);
                nearbyMoves.Add(new GomokuMove(targetX, targetY, score, "Opening neighbor"));
            }
        }

        return nearbyMoves;
    }

    /// <summary>
    /// 기존 돌 주변 반경 안에서 후보 수를 생성함.
    /// </summary>
    private List<GomokuMove> GenerateCandidates(StoneColor color, CandidateGenerationMode mode, bool limitCandidates)
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
                    AddNearbyCandidates(candidates, x, y, color, mode);
                }
            }
        }

        if (!hasStone)
        {
            int center = _boardSize / 2;
            candidates.Add(new GomokuMove(center, center, 0, "Center fallback"));
        }

        SortCandidates(candidates, color);

        int candidateLimit = GetCandidateLimit(mode);
        if (limitCandidates && candidates.Count > candidateLimit)
        {
            candidates.RemoveRange(candidateLimit, candidates.Count - candidateLimit);
        }

        RecordGeneratedCandidates(mode, candidates.Count);
        return candidates;
    }

    /// <summary>
    /// 후보 생성 모드에 맞는 후보 상한을 반환함.
    /// </summary>
    /// <param name="mode">후보 생성 모드.</param>
    /// <returns>후보 목록에 유지할 최대 개수.</returns>
    private int GetCandidateLimit(CandidateGenerationMode mode)
    {
        switch (mode)
        {
            case CandidateGenerationMode.SearchNode:
                return SearchNodeCandidateCount;
            case CandidateGenerationMode.ThreatScan:
                return ThreatScanCandidateCount;
            default:
                return MaxCandidateCount;
        }
    }

    /// <summary>
    /// 위협 방어 후보 중 정밀 미래 경로 평가를 수행할 상위 후보 개수를 반환함.
    /// </summary>
    /// <param name="isHardDifficulty">Hard 난이도 여부.</param>
    /// <returns>정밀 위협 평가를 적용할 후보 수.</returns>
    private int GetPreciseThreatDefenseCandidateCount(bool isHardDifficulty)
    {
        return isHardDifficulty ? HardPreciseRiskCandidateCount : NormalPreciseRiskCandidateCount;
    }

    /// <summary>
    /// minimax가 핵심 방어 후보를 직접 비교할 수 있도록 후보 제한 전에 보장함.
    /// </summary>
    /// <param name="searchCandidates">현재 제한된 백돌 후보 목록.</param>
    /// <param name="fullCandidates">제한 전 백돌 후보 목록.</param>
    /// <param name="isHardDifficulty">Hard 난이도 여부.</param>
    private void EnsureMinimaxDefenseCandidates(List<GomokuMove> searchCandidates, List<GomokuMove> fullCandidates, bool isHardDifficulty)
    {
        for (int i = 0; i < fullCandidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = fullCandidates[i];
            if (ContainsMove(searchCandidates, candidate.X, candidate.Y))
            {
                continue;
            }

            ThreatAnalysis threatAnalysis;
            PlaceTemporary(candidate.X, candidate.Y, _opponentColor);
            try
            {
                threatAnalysis = AnalyzeThreatAt(candidate.X, candidate.Y, _opponentColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            bool isOpenThreeDefense = threatAnalysis.OpenThreeCount > 0;
            bool blocksFutureComboFinisher = i < GetPreciseThreatDefenseCandidateCount(isHardDifficulty) &&
                                             BlocksPlayerFutureComboFinisher(candidate.X, candidate.Y);
            if (!isOpenThreeDefense && !blocksFutureComboFinisher)
            {
                continue;
            }

            // minimax가 비교할 수 있도록 핵심 방어 후보를 후보 목록에 보장함.
            int bonusScore = blocksFutureComboFinisher ? BlockedFourThreatScore : OpenThreeThreatScore;
            string reason = blocksFutureComboFinisher ? "Future combo finisher block" : "Open three defense candidate";
            searchCandidates.Add(new GomokuMove(candidate.X, candidate.Y, candidate.Score + bonusScore, reason));
            LogAiDebug($"EnsureMinimaxDefenseCandidates added ({candidate.X},{candidate.Y}) reason={reason} baseScore={candidate.Score} boostedScore={candidate.Score + bonusScore}");
        }

        SortCandidates(searchCandidates, _aiColor);
        LogAiDebug($"Search candidates after guarantees={FormatCandidateList(searchCandidates)}");

        if (searchCandidates.Count > MaxCandidateCount)
        {
            searchCandidates.RemoveRange(MaxCandidateCount, searchCandidates.Count - MaxCandidateCount);
        }
    }

    /// <summary>
    /// Hard에서 강제 방어 좌표가 후보 제한에서 밀리지 않도록 보장함.
    /// </summary>
    /// <param name="candidates">현재 백돌 후보 목록.</param>
    /// <param name="threatDefense">보장할 위협 방어 수.</param>
    private void EnsureHardDefenseCandidate(List<GomokuMove> candidates, GomokuMove threatDefense)
    {
        if (!IsForcedThreatDefense(threatDefense, true))
        {
            return;
        }

        GomokuMove promotedDefense = new GomokuMove(threatDefense.X, threatDefense.Y, WinScore - 1, threatDefense.Reason);

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].X != threatDefense.X || candidates[i].Y != threatDefense.Y)
            {
                continue;
            }

            candidates[i] = promotedDefense;
            SortCandidates(candidates, _aiColor);
            return;
        }

        candidates.Add(promotedDefense);
        SortCandidates(candidates, _aiColor);

        if (candidates.Count > MaxCandidateCount)
        {
            candidates.RemoveRange(MaxCandidateCount, candidates.Count - MaxCandidateCount);
        }
    }

    /// <summary>
    /// 특정 돌 주변의 빈 좌표를 후보 목록에 추가함.
    /// </summary>
    private void AddNearbyCandidates(List<GomokuMove> candidates, int originX, int originY, StoneColor color, CandidateGenerationMode mode)
    {
        for (int x = originX - CandidateRadius; x <= originX + CandidateRadius; x++)
        {
            ThrowIfCancellationRequested();
            for (int y = originY - CandidateRadius; y <= originY + CandidateRadius; y++)
            {
                if (!IsLegalMove(x, y, color) || ContainsMove(candidates, x, y))
                {
                    continue;
                }

                int score = EvaluateCandidateScore(x, y, color, mode);
                candidates.Add(new GomokuMove(x, y, score, "Nearby candidate"));
            }
        }
    }

    /// <summary>
    /// 후보 생성 모드에 맞는 후보 점수를 계산함.
    /// </summary>
    /// <param name="x">후보 X 좌표.</param>
    /// <param name="y">후보 Y 좌표.</param>
    /// <param name="color">후보 돌 색상.</param>
    /// <param name="mode">후보 생성 모드.</param>
    /// <returns>정렬에 사용할 후보 점수.</returns>
    private int EvaluateCandidateScore(int x, int y, StoneColor color, CandidateGenerationMode mode)
    {
        if (mode == CandidateGenerationMode.RootEvaluation)
        {
            return EvaluateRootCandidateScore(x, y, color);
        }

        return EvaluateLightweightCandidateScore(x, y, color);
    }

    /// <summary>
    /// 루트 보드 기준 후보 평가를 탐색 1회 동안만 캐싱해 중복 전체 평가를 줄임.
    /// </summary>
    /// <param name="x">후보 X 좌표.</param>
    /// <param name="y">후보 Y 좌표.</param>
    /// <param name="color">후보 돌 색상.</param>
    /// <returns>루트 후보 평가 점수.</returns>
    private int EvaluateRootCandidateScore(int x, int y, StoneColor color)
    {
        int cacheKey = GetRootEvaluationCacheKey(x, y, color);
        if (_rootEvaluationCache.TryGetValue(cacheKey, out int cachedScore))
        {
            _rootEvaluationCacheHitCount++;
            return cachedScore;
        }

        // 루트 보드는 동일 탐색 안에서만 고정되므로 EvaluateMove 결과를 안전하게 재사용함.
        _evaluateMoveCallCount++;
        int score = _evaluator.EvaluateMove(_logic, _boardSize, x, y, color, _aiColor);
        _rootEvaluationCache[cacheKey] = score;
        return score;
    }

    /// <summary>
    /// 루트 후보 평가 캐시에 사용할 좌표와 색상 기반 키를 생성함.
    /// </summary>
    /// <param name="x">후보 X 좌표.</param>
    /// <param name="y">후보 Y 좌표.</param>
    /// <param name="color">후보 돌 색상.</param>
    /// <returns>캐시 키.</returns>
    private int GetRootEvaluationCacheKey(int x, int y, StoneColor color)
    {
        return ((x * _boardSize) + y) * 8 + (int)color;
    }

    /// <summary>
    /// 전체 보드 평가 없이 지역 위협과 중앙 근접도만으로 후보 점수를 계산함.
    /// </summary>
    /// <param name="x">후보 X 좌표.</param>
    /// <param name="y">후보 Y 좌표.</param>
    /// <param name="color">후보 돌 색상.</param>
    /// <returns>정렬에 사용할 경량 후보 점수.</returns>
    private int EvaluateLightweightCandidateScore(int x, int y, StoneColor color)
    {
        _lightweightEvaluationCallCount++;
        ThreatAnalysis ownThreat = AnalyzeThreatAt(x, y, color);
        StoneColor opponentColor = GetOppositeColor(color);
        ThreatAnalysis opponentThreat = AnalyzeThreatAt(x, y, opponentColor);
        int center = _boardSize / 2;
        int centerDistance = System.Math.Abs(x - center) + System.Math.Abs(y - center);
        int centerBonus = _boardSize - centerDistance;
        int score = ownThreat.Score +
                    GetThreatOrderingBonus(ownThreat) +
                    opponentThreat.Score / 2 +
                    GetThreatOrderingBonus(opponentThreat) / DefenseOrderingBonusDivisor +
                    centerBonus;

        return color == _aiColor ? score : -score;
    }

    /// <summary>
    /// 후보 정렬용 위협 형태 보너스를 계산함.
    /// </summary>
    /// <param name="analysis">후보 좌표의 위협 분석 결과.</param>
    /// <returns>정렬 우선순위를 높일 보너스 점수.</returns>
    private int GetThreatOrderingBonus(ThreatAnalysis analysis)
    {
        int bonus = 0;

        if (analysis.OpenFourCount > 0)
        {
            bonus += OpenFourOrderingBonus;
        }

        if (analysis.BlockedFourCount > 0)
        {
            bonus += BlockedFourOrderingBonus;
        }

        if (analysis.OpenThreeCount > 0)
        {
            bonus += OpenThreeOrderingBonus * analysis.OpenThreeCount;
        }

        if (analysis.BlockedFourCount > 0 && analysis.OpenThreeCount > 0)
        {
            // 복합 위협은 단일 열린 3보다 먼저 탐색되도록 추가 보정함.
            bonus += CompositeThreatOrderingBonus;
        }

        return bonus;
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
    /// 지정 좌표가 실제 상대 돌인지 확인함.
    /// </summary>
    /// <param name="x">검사할 X 좌표.</param>
    /// <param name="y">검사할 Y 좌표.</param>
    /// <returns>실제 상대 돌 여부.</returns>
    private bool IsOpponentStone(int x, int y)
    {
        return _logic.IsInside(x, y) &&
               _logic.Board[x, y].Color == _opponentColor &&
               !_logic.Board[x, y].IsFake;
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
        if (color == _opponentColor)
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

        if (color == StoneColor.Black && IsForbiddenAfterTemporaryPlacement(x, y, color))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 흑돌을 실제 착수와 같은 상태로 임시 배치한 뒤 금수 여부를 확인함.
    /// </summary>
    /// <param name="x">검사할 X 좌표.</param>
    /// <param name="y">검사할 Y 좌표.</param>
    /// <param name="color">검사할 돌 색상.</param>
    /// <returns>임시 착수 후 금수 여부.</returns>
    private bool IsForbiddenAfterTemporaryPlacement(int x, int y, StoneColor color)
    {
        if (color != StoneColor.Black)
        {
            return false;
        }

        // OmokuLogic.PlaceStone과 같은 순서로 둔 뒤 금수 여부를 검사함.
        PlaceTemporary(x, y, color);
        try
        {
            if (_logic.CheckWin(x, y, color))
            {
                return false;
            }

            return _logic.IsForbidden(x, y, color);
        }
        finally
        {
            RestoreTemporary(x, y);
        }
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
    /// AI 디버그 로그를 조건부로 출력함.
    /// </summary>
    /// <param name="message">출력할 로그 메시지.</param>
    private void LogAiDebug(string message)
    {
        if (!EnableAiDebugLog)
        {
            return;
        }

        Debug.Log($"[MinimaxGomokuAI] {message}");
    }

    /// <summary>
    /// AI 탐색 상태와 제한 시간을 초기화함.
    /// </summary>
    /// <param name="cancellationToken">외부 탐색 취소 토큰.</param>
    /// <param name="maxSearchTimeSeconds">탐색 시간 제한 초 단위 값.</param>
    private void BeginSearch(CancellationToken cancellationToken, double maxSearchTimeSeconds)
    {
        _cancellationToken = cancellationToken;
        _maxSearchTimeSeconds = System.Math.Max(0d, maxSearchTimeSeconds);
        _searchStopwatch = _maxSearchTimeSeconds > 0d ? Stopwatch.StartNew() : null;
        _bestMoveSoFar = GomokuMove.Invalid("Best move not evaluated yet");
        ResetSearchCachesAndStats();
    }

    /// <summary>
    /// AI 탐색 상태를 정리함.
    /// </summary>
    private void EndSearch()
    {
        _rootEvaluationCache.Clear();
        _searchStopwatch = null;
        _maxSearchTimeSeconds = 0d;
    }

    /// <summary>
    /// 탐색 1회에만 유효한 캐시와 계측 값을 초기화함.
    /// </summary>
    private void ResetSearchCachesAndStats()
    {
        _rootEvaluationCache.Clear();
        _generatedCandidateCallCount = 0;
        _rootGeneratedCandidateCount = 0;
        _searchNodeGeneratedCandidateCount = 0;
        _threatScanGeneratedCandidateCount = 0;
        _evaluateMoveCallCount = 0;
        _rootEvaluationCacheHitCount = 0;
        _lightweightEvaluationCallCount = 0;
        _analyzeThreatCallCount = 0;
        _minimaxNodeCount = 0;
        _pruningCount = 0;
    }

    /// <summary>
    /// 후보 생성 결과를 모드별로 계측함.
    /// </summary>
    /// <param name="mode">후보 생성 모드.</param>
    /// <param name="candidateCount">최종 후보 개수.</param>
    private void RecordGeneratedCandidates(CandidateGenerationMode mode, int candidateCount)
    {
        _generatedCandidateCallCount++;
        switch (mode)
        {
            case CandidateGenerationMode.RootEvaluation:
                _rootGeneratedCandidateCount += candidateCount;
                break;
            case CandidateGenerationMode.SearchNode:
                _searchNodeGeneratedCandidateCount += candidateCount;
                break;
            case CandidateGenerationMode.ThreatScan:
                _threatScanGeneratedCandidateCount += candidateCount;
                break;
        }
    }

    /// <summary>
    /// 탐색 성능 계측 값을 조건부로 출력함.
    /// </summary>
    /// <param name="status">탐색 종료 상태.</param>
    /// <param name="move">최종 선택된 후보 수.</param>
    private void LogSearchStats(GomokuAISearchResultStatus status, GomokuMove move)
    {
        if (!EnableAiStatsLog)
        {
            return;
        }

        // ThreadPool 탐색 중 Unity 콘솔 API 의존을 피하기 위해 .NET 디버그 출력만 사용함.
        System.Diagnostics.Debug.WriteLine(
            $"[MinimaxGomokuAI] status={status}, move={FormatMove(move)}, elapsed={GetElapsedSearchSeconds():0.000}s, " +
            $"candidateCalls={_generatedCandidateCallCount}, rootCandidates={_rootGeneratedCandidateCount}, " +
            $"searchNodeCandidates={_searchNodeGeneratedCandidateCount}, threatScanCandidates={_threatScanGeneratedCandidateCount}, " +
            $"evaluateMoveCalls={_evaluateMoveCallCount}, rootCacheHits={_rootEvaluationCacheHitCount}, " +
            $"lightweightCalls={_lightweightEvaluationCallCount}, threatAnalyses={_analyzeThreatCallCount}, " +
            $"nodes={_minimaxNodeCount}, prunes={_pruningCount}");
    }

    /// <summary>
    /// 현재 탐색에 걸린 시간을 초 단위로 반환함.
    /// </summary>
    /// <returns>탐색 경과 시간.</returns>
    private double GetElapsedSearchSeconds()
    {
        return _searchStopwatch != null ? _searchStopwatch.Elapsed.TotalSeconds : 0d;
    }

    /// <summary>
    /// 시간 초과 시 사용할 best-so-far 또는 안전한 fallback 후보를 반환함.
    /// </summary>
    /// <returns>시간 초과 시 적용할 착수 후보.</returns>
    private GomokuMove GetBestMoveSoFarOrFallback()
    {
        if (_bestMoveSoFar.IsValid && IsLegalMove(_bestMoveSoFar.X, _bestMoveSoFar.Y, _aiColor))
        {
            return _bestMoveSoFar;
        }

        return FindFallbackMove();
    }

    /// <summary>
    /// 현재 AI 탐색 취소 요청이 들어왔는지 확인함.
    /// </summary>
    private void ThrowIfCancellationRequested()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_searchStopwatch != null && _searchStopwatch.Elapsed.TotalSeconds >= _maxSearchTimeSeconds)
        {
            throw new SearchTimeoutException();
        }
    }

    /// <summary>
    /// 단일 후보 수를 로그용 문자열로 변환함.
    /// </summary>
    /// <param name="move">변환할 후보 수.</param>
    /// <returns>좌표와 점수, 사유를 포함한 문자열.</returns>
    private string FormatMove(GomokuMove move)
    {
        return $"({move.X},{move.Y}) score={move.Score} reason={move.Reason}";
    }

    /// <summary>
    /// 후보 목록을 로그용 문자열로 변환함.
    /// </summary>
    /// <param name="moves">변환할 후보 목록.</param>
    /// <returns>후보 목록 요약 문자열.</returns>
    private string FormatCandidateList(List<GomokuMove> moves)
    {
        if (moves == null || moves.Count == 0)
        {
            return "[]";
        }

        List<string> formattedMoves = new List<string>(moves.Count);
        for (int i = 0; i < moves.Count; i++)
        {
            formattedMoves.Add(FormatMove(moves[i]));
        }

        return string.Join(" | ", formattedMoves);
    }

    /// <summary>
    /// 후보가 없을 때 사용할 중앙 또는 첫 빈 좌표를 반환함.
    /// </summary>
    private GomokuMove FindFallbackMove()
    {
        int center = _boardSize / 2;
        if (IsLegalMove(center, center, _aiColor))
        {
            return new GomokuMove(center, center, 0, "Center fallback");
        }

        for (int x = 0; x < _boardSize; x++)
        {
            for (int y = 0; y < _boardSize; y++)
            {
                if (IsLegalMove(x, y, _aiColor))
                {
                    return new GomokuMove(x, y, 0, "First empty fallback");
                }
            }
        }

        return GomokuMove.Invalid("No empty position");
    }

    /// <summary>
    /// 상대의 미래 복합 위협 완성점을 직접 막을 AI 수를 찾음.
    /// </summary>
    /// <returns>복합 위협 차단 수, 없으면 Invalid.</returns>
    private GomokuMove FindPlayerFutureComboBlockMove()
{
    // 위협 스캔용 상위 후보만 검사해 미래 복합 위협 완성점을 찾음.
    List<GomokuMove> playerFinishers = GenerateCandidates(_opponentColor, CandidateGenerationMode.ThreatScan, true);

    GomokuMove bestBlock = GomokuMove.Invalid("Player future combo block not found");
    int bestThreatScore = 0;

    for (int i = 0; i < playerFinishers.Count; i++)
    {
        GomokuMove finisher = playerFinishers[i];

        // 플레이어가 실제로 둘 수 있는 자리인지 확인
        if (!IsLegalMove(finisher.X, finisher.Y, _opponentColor))
        {
            continue;
        }

        int threatScore = 0;

        // 1️⃣ 막힌4 + 열린3 복합 위협 검사
        if (_evaluator.CreatesBlockedFourOpenThreeThreat(
            _logic,
            _boardSize,
            finisher.X,
            finisher.Y,
            _opponentColor))
        {
            threatScore = System.Math.Max(threatScore, BlockedFourOpenThreeRiskPenalty);
        }

        // 2️⃣ 실제 패턴 분석 (열린4, 열린3 등)
        PlaceTemporary(finisher.X, finisher.Y, _opponentColor);
        try
        {
            ThreatAnalysis analysis = AnalyzeThreatAt(finisher.X, finisher.Y, _opponentColor);

            if (analysis.OpenFourCount > 0)
            {
                threatScore = System.Math.Max(threatScore, OpenFourThreatScore);
            }
            else if (analysis.BlockedFourCount > 0 && analysis.OpenThreeCount > 0)
            {
                threatScore = System.Math.Max(threatScore, BlockedFourOpenThreeRiskPenalty);
            }
            else if (analysis.BlockedFourCount > 0)
            {
                threatScore = System.Math.Max(threatScore, BlockedFourThreatScore);
            }
            else if (analysis.OpenThreeCount >= 2)
            {
                threatScore = System.Math.Max(threatScore, BlockedFourOpenThreeRiskPenalty);
            }
        }
        finally
        {
            // 반드시 원상복구
            RestoreTemporary(finisher.X, finisher.Y);
        }

        // 위협이 없으면 패스
        if (threatScore <= 0)
        {
            continue;
        }

        // AI가 그 칸에 둘 수 있어야 실제 방어 가능
        if (!IsLegalMove(finisher.X, finisher.Y, _aiColor))
        {
            continue;
        }

        // 가장 위험한 칸 선택
        if (!bestBlock.IsValid || threatScore > bestThreatScore)
        {
            bestThreatScore = threatScore;

            bestBlock = new GomokuMove(
                finisher.X,
                finisher.Y,
                threatScore,
                "Block player future combo finisher");
        }
    }

    return bestBlock;
}

}
