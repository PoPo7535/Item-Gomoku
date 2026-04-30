/// <summary>
/// AI 탐색 결과와 완료 상태를 함께 보관함.
/// </summary>
public readonly struct GomokuAISearchResult
{
    public readonly GomokuMove Move;
    public readonly GomokuAISearchResultStatus Status;
    public readonly string Reason;
    public readonly double ElapsedSeconds;

    /// <summary>
    /// AI 탐색 결과 값을 생성함.
    /// </summary>
    /// <param name="move">AI가 반환한 착수 후보.</param>
    /// <param name="status">탐색 완료 상태.</param>
    /// <param name="reason">결과 사유.</param>
    /// <param name="elapsedSeconds">탐색에 걸린 시간.</param>
    public GomokuAISearchResult(GomokuMove move, GomokuAISearchResultStatus status, string reason, double elapsedSeconds)
    {
        Move = move;
        Status = status;
        Reason = reason;
        ElapsedSeconds = elapsedSeconds;
    }

    /// <summary>
    /// 정상 완료된 탐색 결과를 생성함.
    /// </summary>
    /// <param name="move">AI가 선택한 착수 후보.</param>
    /// <param name="elapsedSeconds">탐색에 걸린 시간.</param>
    /// <returns>정상 완료 상태의 탐색 결과.</returns>
    public static GomokuAISearchResult Completed(GomokuMove move, double elapsedSeconds)
    {
        return new GomokuAISearchResult(move, GomokuAISearchResultStatus.Completed, "Completed", elapsedSeconds);
    }

    /// <summary>
    /// 시간 초과로 best-so-far 또는 fallback을 사용한 탐색 결과를 생성함.
    /// </summary>
    /// <param name="move">시간 초과 시 사용할 착수 후보.</param>
    /// <param name="elapsedSeconds">탐색에 걸린 시간.</param>
    /// <returns>시간 초과 상태의 탐색 결과.</returns>
    public static GomokuAISearchResult TimedOut(GomokuMove move, double elapsedSeconds)
    {
        return new GomokuAISearchResult(move, GomokuAISearchResultStatus.TimedOut, "Timed out", elapsedSeconds);
    }

    /// <summary>
    /// 외부 취소로 중단된 탐색 결과를 생성함.
    /// </summary>
    /// <param name="elapsedSeconds">탐색에 걸린 시간.</param>
    /// <returns>취소 상태의 탐색 결과.</returns>
    public static GomokuAISearchResult Canceled(double elapsedSeconds)
    {
        return new GomokuAISearchResult(GomokuMove.Invalid("AI search canceled"), GomokuAISearchResultStatus.Canceled, "Canceled", elapsedSeconds);
    }

    /// <summary>
    /// 예외로 실패한 탐색 결과를 생성함.
    /// </summary>
    /// <param name="reason">실패 사유.</param>
    /// <param name="elapsedSeconds">탐색에 걸린 시간.</param>
    /// <returns>실패 상태의 탐색 결과.</returns>
    public static GomokuAISearchResult Failed(string reason, double elapsedSeconds)
    {
        return new GomokuAISearchResult(GomokuMove.Invalid(reason), GomokuAISearchResultStatus.Failed, reason, elapsedSeconds);
    }

    /// <summary>
    /// 유효한 착수 후보를 찾지 못한 탐색 결과를 생성함.
    /// </summary>
    /// <param name="reason">후보 없음 사유.</param>
    /// <param name="elapsedSeconds">탐색에 걸린 시간.</param>
    /// <returns>후보 없음 상태의 탐색 결과.</returns>
    public static GomokuAISearchResult NoMove(string reason, double elapsedSeconds)
    {
        return new GomokuAISearchResult(GomokuMove.Invalid(reason), GomokuAISearchResultStatus.NoMove, reason, elapsedSeconds);
    }
}
