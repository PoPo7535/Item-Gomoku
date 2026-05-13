using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// Minimax 탐색 상태, 임시 보드 조작, 로그와 fallback 처리를 담당하는 partial 영역임.
/// </summary>
public partial class MinimaxGomokuAI
{
    private const int KillerMoveTableDepth = 8;
    private const int KillerMoveSlotsPerDepth = 2;

    private readonly GomokuMove[,] _killerMoves = new GomokuMove[KillerMoveTableDepth + 1, KillerMoveSlotsPerDepth];

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
    /// 개발/시연 중 탐색 선택 이유를 보는 상세 로그임.
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
        ResetKillerMoves();
        _stats.Reset();
    }

    /// <summary>
    /// 탐색 1회 동안 사용할 killer move table을 초기화함.
    /// </summary>
    private void ResetKillerMoves()
    {
        for (int depth = 0; depth <= KillerMoveTableDepth; depth++)
        {
            for (int slot = 0; slot < KillerMoveSlotsPerDepth; slot++)
            {
                _killerMoves[depth, slot] = GomokuMove.Invalid("No killer move");
            }
        }
    }

    /// <summary>
    /// 특정 remaining depth에서 beta cut-off를 만든 killer move를 기록함.
    /// </summary>
    /// <param name="depth">현재 remaining depth.</param>
    /// <param name="move">기록할 killer move.</param>
    private void RecordKillerMove(int depth, GomokuMove move)
    {
        if (!move.IsValid || depth < 0 || depth > KillerMoveTableDepth)
        {
            return;
        }

        if (IsSameMove(_killerMoves[depth, 0], move) || IsSameMove(_killerMoves[depth, 1], move))
        {
            return;
        }

        _killerMoves[depth, 1] = _killerMoves[depth, 0];
        _killerMoves[depth, 0] = move;
    }

    /// <summary>
    /// 특정 remaining depth와 slot에 저장된 killer move를 가져옴.
    /// </summary>
    /// <param name="depth">현재 remaining depth.</param>
    /// <param name="slot">killer move slot.</param>
    /// <param name="move">저장된 killer move.</param>
    /// <returns>유효한 killer move가 있으면 true.</returns>
    private bool TryGetKillerMove(int depth, int slot, out GomokuMove move)
    {
        move = GomokuMove.Invalid("No killer move");

        if (depth < 0 || depth > KillerMoveTableDepth || slot < 0 || slot >= KillerMoveSlotsPerDepth)
        {
            return false;
        }

        move = _killerMoves[depth, slot];
        return move.IsValid;
    }

    /// <summary>
    /// 두 후보가 같은 좌표를 가리키는지 확인함.
    /// </summary>
    /// <param name="first">첫 번째 후보.</param>
    /// <param name="second">두 번째 후보.</param>
    /// <returns>두 후보가 모두 유효하고 좌표가 같으면 true.</returns>
    private static bool IsSameMove(GomokuMove first, GomokuMove second)
    {
        return first.IsValid &&
               second.IsValid &&
               first.X == second.X &&
               first.Y == second.Y;
    }

    /// <summary>
    /// 후보 생성 결과를 모드별로 계측함.
    /// </summary>
    /// <param name="mode">후보 생성 모드.</param>
    /// <param name="candidateCount">최종 후보 개수.</param>
    private void RecordGeneratedCandidates(CandidateGenerationMode mode, int candidateCount)
    {
        _stats.RecordGeneratedCandidates(mode, candidateCount);
    }

    /// <summary>
    /// 탐색 성능 계측 값을 조건부로 출력함.
    /// 성능 튜닝용 로그이며 실제 착수 평가 로직에는 관여하지 않음.
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
            $"candidateCalls={_stats.GeneratedCandidateCallCount}, rootCandidates={_stats.RootGeneratedCandidateCount}, " +
            $"searchNodeCandidates={_stats.SearchNodeGeneratedCandidateCount}, threatScanCandidates={_stats.ThreatScanGeneratedCandidateCount}, " +
            $"evaluateMoveCalls={_stats.EvaluateMoveCallCount}, rootCacheHits={_stats.RootEvaluationCacheHitCount}, " +
            $"lightweightCalls={_stats.LightweightEvaluationCallCount}, threatAnalyses={_stats.AnalyzeThreatCallCount}, " +
            $"nodes={_stats.MinimaxNodeCount}, prunes={_stats.PruningCount}");
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
}
