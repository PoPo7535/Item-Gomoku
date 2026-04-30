/// <summary>
/// AI 탐색 요청의 완료 상태를 정의함.
/// </summary>
public enum GomokuAISearchResultStatus
{
    Completed,
    TimedOut,
    Canceled,
    Failed,
    NoMove,
}
