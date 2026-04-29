using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using ThreatAnalysis = MinimaxThreatAnalysis;

/// <summary>
/// 오목 AI의 후보 생성과 minimax 의사결정을 담당함.
/// </summary>
public partial class MinimaxGomokuAI : IGomokuAI
{
    // AI 디버그/성능 로그 출력 여부임.
    private const bool EnableAiDebugLog = false;
    private const bool EnableAiStatsLog = false;

    // 탐색 제한 시간을 넘겼을 때 내부 흐름만 빠져나오기 위한 예외임.
    private sealed class SearchTimeoutException : System.Exception
    {
    }

    // 승패와 주요 위협 패턴의 기본 점수표임.
    private const int WinScore = 10000000;
    private const int OpenFourThreatScore = 900000;
    private const int BlockedFourThreatScore = 300000;
    private const int OpenThreeThreatScore = 50000;

    // 후보 생성 범위와 모드별 후보 제한 개수임.
    private const int CandidateRadius = 2;
    private const int MaxCandidateCount = 18;
    private const int SearchNodeCandidateCount = 10;
    private const int ThreatScanCandidateCount = 24;
    private const int NormalPreciseRiskCandidateCount = 6;
    private const int HardPreciseRiskCandidateCount = 8;

    // 상대의 복합 위협과 미래 루트를 피하기 위한 위험 보정값임.
    private const int BlockedFourOpenThreeRiskPenalty = 600000;
    private const int NormalStructuralRiskPenaltyBonus = 400000;
    private const int NormalFutureRouteRiskPenalty = 350000;
    private const int HardFutureRouteRiskPenalty = 550000;
    private const int NormalForcedComboResponsePenalty = 8000000;
    private const int HardForcedComboResponsePenalty = 9500000;

    // 후보 정렬 시 위협 형태를 먼저 보게 만드는 ordering 보너스임.
    private const int OpenFourOrderingBonus = 800000;
    private const int BlockedFourOrderingBonus = 250000;
    private const int OpenThreeOrderingBonus = 45000;
    private const int CompositeThreatOrderingBonus = 180000;
    private const int DefenseOrderingBonusDivisor = 2;

    // 오목 라인 판정에 사용하는 4방향 벡터임.
    private static readonly int[] DirectionX = { 1, 0, 1, 1 };
    private static readonly int[] DirectionY = { 0, 1, 1, -1 };

    // 탐색이 참조하는 보드/평가기/위협 분석기와 색상 정보임.
    private readonly OmokuLogic _logic;
    private readonly GomokuBoardEvaluator _evaluator;
    private readonly MinimaxThreatAnalyzer _threatAnalyzer;
    private readonly int _boardSize;
    private readonly StoneColor _aiColor;
    private readonly StoneColor _opponentColor;

    // 탐색 1회마다 바뀌는 취소/시간 제한/best-so-far 상태임.
    private CancellationToken _cancellationToken;
    private Stopwatch _searchStopwatch;
    private double _maxSearchTimeSeconds;
    private GomokuMove _bestMoveSoFar;

    // 루트 후보 평가 캐시와 탐색 성능 계측값임.
    private readonly Dictionary<int, int> _rootEvaluationCache = new Dictionary<int, int>();
    private readonly MinimaxSearchStats _stats = new MinimaxSearchStats();

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
        _threatAnalyzer = new MinimaxThreatAnalyzer(_logic, OpenFourThreatScore, BlockedFourThreatScore, OpenThreeThreatScore);
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
        _stats.MinimaxNodeCount++;

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
                _stats.PruningCount++;
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
                _stats.PruningCount++;
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
        _stats.AnalyzeThreatCallCount++;
        return _threatAnalyzer.AnalyzeThreatAt(x, y, color);
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
        return _threatAnalyzer.IsEmpty(x, y);
    }

}
