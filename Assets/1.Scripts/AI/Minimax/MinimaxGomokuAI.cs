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
    // 현재 true 값은 개발/시연 중 탐색 흐름을 관찰하기 위한 상세 로그 설정임.
    // 로그 on/off 정책 변경은 성능 관찰 결과가 달라질 수 있으므로 별도 작업에서 다룸.
    private const bool EnableAiDebugLog = true;
    private const bool EnableAiStatsLog = true;

    // 탐색 제한 시간을 넘겼을 때 내부 흐름만 빠져나오기 위한 예외임.
    private sealed class SearchTimeoutException : System.Exception
    {
    }

    // 전술/pre-check/후보 정렬용 점수표임. leaf evaluator 점수와 직접 비교하는 스케일 아님.
    // 여기 값은 즉시 전술 판단과 동률 보정용 대표 강도이고, 최종 보드 평가값이 아님.
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
    // 정렬 보너스는 alpha-beta 탐색 순서 개선용이며 leaf 평가 점수에 직접 더하는 값이 아님.
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

        // 사전 검사 흐름은 minimax 전에 확정성이 높은 수를 먼저 처리하는 안전장치임.
        // 오프닝 -> 즉시 승리 -> 즉시 방어 -> AI 열린 4 공격 -> 상대 직접 위협 방어 -> AI 복합 위협 -> 제한 후보 minimax 순서임.
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

        GomokuMove compoundThreat = FindAiCompoundThreatMove(fullCandidates);
        if (compoundThreat.IsValid)
        {
            LogAiDebug($"Compound threat selected {FormatMove(compoundThreat)}");
            return compoundThreat;
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
        if (candidates.Count > 0)
        {
            // 첫 후보를 기본 fallback으로 보관해 시간 초과 시에도 착수 후보를 잃지 않음.
            _bestMoveSoFar = candidates[0];
        }

        GomokuMove bestCompletedMove = GomokuMove.Invalid("No completed depth");
        for (int depth = 1; depth <= searchDepth; depth++)
        {
            ResetKillerMoves();
            GomokuMove depthBestMove = SearchRootAtDepth(candidates, depth);
            if (depthBestMove.IsValid)
            {
                // 완료된 depth 결과만 timeout fallback으로 commit함.
                bestCompletedMove = depthBestMove;
                _bestMoveSoFar = depthBestMove;
            }
        }

        return bestCompletedMove.IsValid ? bestCompletedMove : FindFallbackMove();
    }

    /// <summary>
    /// 지정한 root depth에서 모든 root 후보를 평가함.
    /// </summary>
    /// <param name="candidates">평가할 root 후보 목록.</param>
    /// <param name="searchDepth">현재 iterative deepening depth.</param>
    /// <returns>현재 depth를 끝까지 평가한 최선 후보.</returns>
    private GomokuMove SearchRootAtDepth(List<GomokuMove> candidates, int searchDepth)
    {
        GomokuMove bestMove = GomokuMove.Invalid("No evaluated move");
        int bestScore = int.MinValue;
        int alpha = int.MinValue + 1;
        int beta = int.MaxValue;

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
            }

            alpha = System.Math.Max(alpha, bestScore);
        }

        return bestMove;
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
        ApplyKillerMoveOrdering(candidates, depth, currentColor);

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
    /// 현재 후보 목록 안에 존재하는 killer move를 앞쪽으로 재배치함.
    /// </summary>
    /// <param name="candidates">현재 노드의 제한 후보 목록.</param>
    /// <param name="depth">현재 remaining depth.</param>
    /// <param name="currentColor">현재 착수 색상.</param>
    private void ApplyKillerMoveOrdering(List<GomokuMove> candidates, int depth, StoneColor currentColor)
    {
        int insertIndex = 0;
        for (int slot = 0; slot < KillerMoveSlotsPerDepth; slot++)
        {
            if (!TryGetKillerMove(depth, slot, out GomokuMove killerMove))
            {
                continue;
            }

            int candidateIndex = FindCandidateIndex(candidates, killerMove, insertIndex);
            if (candidateIndex < 0 || !IsLegalMove(killerMove.X, killerMove.Y, currentColor))
            {
                continue;
            }

            // Killer move는 제한 후보 목록 안에 있을 때만 순서만 앞당김.
            GomokuMove candidate = candidates[candidateIndex];
            candidates.RemoveAt(candidateIndex);
            candidates.Insert(insertIndex, candidate);
            insertIndex++;
        }
    }

    /// <summary>
    /// 후보 목록에서 같은 좌표의 후보 인덱스를 찾음.
    /// </summary>
    /// <param name="candidates">검사할 후보 목록.</param>
    /// <param name="move">찾을 후보 좌표.</param>
    /// <param name="startIndex">검색 시작 인덱스.</param>
    /// <returns>같은 좌표의 후보 인덱스. 없으면 -1.</returns>
    private int FindCandidateIndex(List<GomokuMove> candidates, GomokuMove move, int startIndex)
    {
        for (int i = startIndex; i < candidates.Count; i++)
        {
            if (IsSameMove(candidates[i], move))
            {
                return i;
            }
        }

        return -1;
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
                RecordKillerMove(depth, candidate);
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
                RecordKillerMove(depth, candidate);
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
    /// AI가 한 수로 복합 공격 위협을 만들 수 있는 후보를 찾음.
    /// </summary>
    /// <param name="candidates">검사할 루트 후보 목록.</param>
    /// <returns>복합 공격 위협을 만드는 최선 후보.</returns>
    private GomokuMove FindAiCompoundThreatMove(List<GomokuMove> candidates)
    {
        GomokuMove bestThreat = GomokuMove.Invalid("Compound threat not found");
        int bestPriority = 0;
        int bestFourThreatCount = 0;
        int bestOpenThreeCount = 0;
        int bestThreatScore = int.MinValue;
        int bestCandidateScore = int.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            ThrowIfCancellationRequested();
            GomokuMove candidate = candidates[i];

            // 흑돌 금수 후보는 복합 위협처럼 보여도 선택하면 안 됨.
            if (!IsLegalMove(candidate.X, candidate.Y, _aiColor))
            {
                continue;
            }

            ThreatAnalysis threatAnalysis;

            // 합법 후보만 가상 착수해 복합 위협 여부를 확인함.
            PlaceTemporary(candidate.X, candidate.Y, _aiColor);
            try
            {
                threatAnalysis = AnalyzeThreatAt(candidate.X, candidate.Y, _aiColor);
            }
            finally
            {
                RestoreTemporary(candidate.X, candidate.Y);
            }

            int priority = GetCompoundThreatPriority(threatAnalysis);
            if (priority <= 0)
            {
                continue;
            }

            int fourThreatCount = GetCompoundFourThreatDirectionCount(threatAnalysis);
            int openThreeDirectionCount = CountDirectionBits(threatAnalysis.OpenThreeDirectionMask);
            if (!bestThreat.IsValid ||
                priority > bestPriority ||
                (priority == bestPriority && fourThreatCount > bestFourThreatCount) ||
                (priority == bestPriority && fourThreatCount == bestFourThreatCount && openThreeDirectionCount > bestOpenThreeCount) ||
                (priority == bestPriority && fourThreatCount == bestFourThreatCount && openThreeDirectionCount == bestOpenThreeCount && threatAnalysis.Score > bestThreatScore) ||
                (priority == bestPriority && fourThreatCount == bestFourThreatCount && openThreeDirectionCount == bestOpenThreeCount && threatAnalysis.Score == bestThreatScore && candidate.Score > bestCandidateScore))
            {
                bestPriority = priority;
                bestFourThreatCount = fourThreatCount;
                bestOpenThreeCount = openThreeDirectionCount;
                bestThreatScore = threatAnalysis.Score;
                bestCandidateScore = candidate.Score;
                bestThreat = new GomokuMove(candidate.X, candidate.Y, threatAnalysis.Score, GetCompoundThreatReason(threatAnalysis));
            }
        }

        return bestThreat;
    }

    /// <summary>
    /// AI 복합 위협 분석 결과를 공격 우선순위 등급으로 변환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <returns>클수록 먼저 선택해야 하는 복합 공격 등급.</returns>
    private int GetCompoundThreatPriority(ThreatAnalysis analysis)
    {
        int fourThreatDirectionCount = GetCompoundFourThreatDirectionCount(analysis);

        if (fourThreatDirectionCount >= 2)
        {
            return 4;
        }

        if (HasDistinctThreatDirections(analysis.BlockedFourDirectionMask, analysis.OpenThreeDirectionMask))
        {
            return 3;
        }

        if (HasDistinctThreatDirections(analysis.GappedFourDirectionMask, analysis.OpenThreeDirectionMask))
        {
            return 2;
        }

        if (CountDirectionBits(analysis.OpenThreeDirectionMask) >= 2)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// AI 복합 위협 분석 결과에 맞는 선택 사유 문자열을 반환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <returns>복합 공격 사유 문자열.</returns>
    private string GetCompoundThreatReason(ThreatAnalysis analysis)
    {
        int fourThreatDirectionCount = GetCompoundFourThreatDirectionCount(analysis);

        if (fourThreatDirectionCount >= 2)
        {
            return "Two fours compound attack";
        }

        if (HasDistinctThreatDirections(analysis.BlockedFourDirectionMask, analysis.OpenThreeDirectionMask))
        {
            return "Blocked four open three compound attack";
        }

        if (HasDistinctThreatDirections(analysis.GappedFourDirectionMask, analysis.OpenThreeDirectionMask))
        {
            return "Gapped four open three compound attack";
        }

        if (CountDirectionBits(analysis.OpenThreeDirectionMask) >= 2)
        {
            return "Double open three compound attack";
        }

        return "Compound threat attack";
    }

    /// <summary>
    /// 복합 공격 판정에 사용할 four 계열 위협 방향 수를 반환함.
    /// </summary>
    /// <param name="analysis">현재 위협 분석 결과.</param>
    /// <returns>막힌 4와 끊어진 4가 존재하는 서로 다른 방향 수.</returns>
    private int GetCompoundFourThreatDirectionCount(ThreatAnalysis analysis)
    {
        return CountDirectionBits(analysis.BlockedFourDirectionMask | analysis.GappedFourDirectionMask);
    }

    /// <summary>
    /// 두 위협 마스크가 서로 다른 방향의 위협을 포함하는지 확인함.
    /// </summary>
    /// <param name="firstMask">첫 번째 위협 방향 마스크.</param>
    /// <param name="secondMask">두 번째 위협 방향 마스크.</param>
    /// <returns>서로 다른 방향에 위협이 존재하면 true.</returns>
    private bool HasDistinctThreatDirections(int firstMask, int secondMask)
    {
        return firstMask != 0 &&
               secondMask != 0 &&
               CountDirectionBits(firstMask | secondMask) >= 2;
    }

    /// <summary>
    /// 방향 마스크에 켜진 bit 수를 계산함.
    /// </summary>
    /// <param name="directionMask">방향 bit 마스크.</param>
    /// <returns>켜진 방향 bit 수.</returns>
    private int CountDirectionBits(int directionMask)
    {
        int count = 0;

        while (directionMask != 0)
        {
            count += directionMask & 1;
            directionMask >>= 1;
        }

        return count;
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
        if (analysis.OpenFourCount > 0)
        {
            return 4;
        }

        // 상대 열린 4가 없을 때만 four + 열린 3 복합 위협을 우선 차단함.
        if ((analysis.BlockedFourCount > 0 || analysis.GappedFourCount > 0) && analysis.OpenThreeCount > 0)
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
