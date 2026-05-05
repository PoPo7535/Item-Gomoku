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
    private const bool EnableAiDebugLog = true;
    private const bool EnableAiStatsLog = true;

    // 탐색 제한 시간을 넘겼을 때 내부 흐름만 빠져나오기 위한 예외임.
    private sealed class SearchTimeoutException : System.Exception
    {
    }

    // 전술/pre-check/후보 정렬용 점수표임. leaf evaluator 점수와 직접 비교하는 스케일 아님.
    private const int WinScore = 10000000;
    private const int OpenFourThreatScore = 900000;
    private const int BlockedFourThreatScore = 300000;
    private const int OpenThreeThreatScore = 50000;
    private const int GappedFourThreatScore = 250000;
    private const int BrokenThreeThreatScore = 30000;
    private const int OpenTwoThreatScore = 6000;

    // 후보 생성 범위와 모드별 후보 제한 개수임.
    private const int CandidateRadius = 2;
    private const int MaxCandidateCount = 18;
    private const int SearchNodeCandidateCount = 10;
    private const int ThreatScanCandidateCount = 24;

    // 후보 정렬 시 위협 형태를 먼저 보게 만드는 ordering 보너스임.
    private const int OpenFourOrderingBonus = 800000;
    private const int BlockedFourOrderingBonus = 250000;
    private const int OpenThreeOrderingBonus = 45000;
    private const int CompositeThreatOrderingBonus = 180000;
    private const int GappedFourOrderingBonus = 200000;
    private const int BrokenThreeOrderingBonus = 25000;
    private const int OpenTwoOrderingBonus = 5000;
    private const int DoubleOpenTwoOrderingBonus = 18000;
    private const int DefenseOrderingBonusDivisor = 2;

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
        _threatAnalyzer = new MinimaxThreatAnalyzer(
            _logic,
            OpenFourThreatScore,
            BlockedFourThreatScore,
            OpenThreeThreatScore,
            GappedFourThreatScore,
            BrokenThreeThreatScore,
            OpenTwoThreatScore);
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
        List<GomokuMove> fullCandidates = GenerateCandidates(_aiColor, CandidateGenerationMode.RootEvaluation, false);
        LogAiDebug($"FindBestMove depth={clampedDepth}, fullCandidates={FormatCandidateList(fullCandidates)}");

        if (fullCandidates.Count == 0)
        {
            return FindFallbackMove();
        }

        _bestMoveSoFar = fullCandidates[0];

        // 오프닝 -> 즉시 승리 -> 즉시 방어 -> AI 열린 4 공격 -> 상대 직접 위협 방어 -> 제한 후보 minimax 순서임.
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

        GomokuMove openFourAttack = FindOpenFourAttackMove(fullCandidates);
        if (openFourAttack.IsValid)
        {
            LogAiDebug($"Open four attack selected {FormatMove(openFourAttack)}");
            return openFourAttack;
        }

        GomokuMove threatDefense = FindThreatDefenseMove(fullCandidates);
        if (threatDefense.IsValid)
        {
            // 난이도와 무관한 직접 위협은 minimax 전에 공통으로 차단함.
            return threatDefense;
        }

        List<GomokuMove> searchCandidates = GenerateCandidates(_aiColor, CandidateGenerationMode.RootEvaluation, true);
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

            LogAiDebug($"Minimax candidate {FormatMove(candidate)} score={score}");

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
    /// AI가 열린 4를 만들 수 있는 공격 후보를 찾음.
    /// </summary>
    /// <param name="candidates">검사할 루트 후보 목록.</param>
    /// <returns>열린 4를 만드는 최선 공격 후보.</returns>
    private GomokuMove FindOpenFourAttackMove(List<GomokuMove> candidates)
    {
        GomokuMove bestAttack = GomokuMove.Invalid("Open four attack not found");
        int bestOpenFourCount = 0;
        int bestThreatScore = int.MinValue;
        int bestCandidateScore = int.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];

            if (!IsLegalMove(candidate.X, candidate.Y, _aiColor))
            {
                continue;
            }

            ThreatAnalysis threatAnalysis;

            // AI 열린 4 공격권은 상대 직접 위협 방어보다 먼저 확보함.
            PlaceTemporary(candidate.X, candidate.Y, _aiColor);
            try
            {
                threatAnalysis = AnalyzeThreatAt(candidate.X, candidate.Y, _aiColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            if (threatAnalysis.OpenFourCount <= 0)
            {
                continue;
            }

            if (!bestAttack.IsValid ||
                threatAnalysis.OpenFourCount > bestOpenFourCount ||
                (threatAnalysis.OpenFourCount == bestOpenFourCount && threatAnalysis.Score > bestThreatScore) ||
                (threatAnalysis.OpenFourCount == bestOpenFourCount && threatAnalysis.Score == bestThreatScore && candidate.Score > bestCandidateScore))
            {
                bestOpenFourCount = threatAnalysis.OpenFourCount;
                bestThreatScore = threatAnalysis.Score;
                bestCandidateScore = candidate.Score;
                bestAttack = new GomokuMove(candidate.X, candidate.Y, threatAnalysis.Score, "Open four attack");
            }
        }

        return bestAttack;
    }

    /// <summary>
    /// 상대의 직접 위협을 난이도와 무관하게 막을 방어 수를 찾음.
    /// </summary>
    private GomokuMove FindThreatDefenseMove(List<GomokuMove> candidates)
    {
        GomokuMove bestDefense = GomokuMove.Invalid("Threat defense not found");
        int bestThreatPriority = 0;
        int bestDefenseScore = int.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];

            if (!IsLegalMove(candidate.X, candidate.Y, _opponentColor))
            {
                continue;
            }

            ThreatAnalysis threatAnalysis;

            // 상대가 해당 좌표에 뒀을 때 바로 생기는 위협만 공통 방어 대상으로 봄.
            PlaceTemporary(candidate.X, candidate.Y, _opponentColor);
            try
            {
                threatAnalysis = AnalyzeThreatAt(candidate.X, candidate.Y, _opponentColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            int threatPriority = GetThreatPriority(threatAnalysis);
            if (threatPriority <= 0)
            {
                continue;
            }

            int defenseScore = threatAnalysis.Score;
            LogAiDebug(
                $"ThreatDefense candidate=({candidate.X},{candidate.Y}) score={threatAnalysis.Score} " +
                $"open3={threatAnalysis.OpenThreeCount} broken3={threatAnalysis.BrokenThreeCount} " +
                $"blocked4={threatAnalysis.BlockedFourCount} gapped4={threatAnalysis.GappedFourCount} open4={threatAnalysis.OpenFourCount} " +
                $"tier={threatPriority}");

            if (!bestDefense.IsValid ||
                threatPriority > bestThreatPriority ||
                (threatPriority == bestThreatPriority && defenseScore > bestDefenseScore))
            {
                bestThreatPriority = threatPriority;
                bestDefenseScore = defenseScore;
                bestDefense = new GomokuMove(candidate.X, candidate.Y, defenseScore, GetThreatDefenseReason(threatAnalysis));
            }
        }

        return bestDefense;
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
    /// 위협 분석 결과에 맞는 방어 사유 문자열을 반환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
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

        if (analysis.GappedFourCount > 0)
        {
            return "Gapped four defense";
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

        if (analysis.BlockedFourCount > 0 || analysis.GappedFourCount > 0)
        {
            return 2;
        }

        // 순수 열린 3은 강제 방어가 아니라 Minimax/평가 경쟁에 맡김.
        return 0;
    }

}
