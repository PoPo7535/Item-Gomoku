/// <summary>
/// 비동기 AI 탐색에 필요한 입력 값을 보관함.
/// </summary>
public readonly struct GomokuAiSearchRequest
{
    public readonly int RequestId;
    public readonly GomokuAiAlgorithmType AlgorithmType;
    public readonly GomokuAiDifficulty Difficulty;
    public readonly GomokuBoardSnapshot BoardSnapshot;

    public int BoardVersion => BoardSnapshot != null ? BoardSnapshot.BoardVersion : -1;
    public int BoardSize => BoardSnapshot != null ? BoardSnapshot.BoardSize : 15;
    public int SearchDepth => System.Math.Max(1, (int)Difficulty);

    /// <summary>
    /// 비동기 AI 탐색 요청 값을 생성함.
    /// </summary>
    /// <param name="requestId">요청 식별자.</param>
    /// <param name="algorithmType">사용할 AI 알고리즘.</param>
    /// <param name="difficulty">AI 난이도.</param>
    /// <param name="boardSnapshot">탐색 기준 보드 스냅샷.</param>
    public GomokuAiSearchRequest(
        int requestId,
        GomokuAiAlgorithmType algorithmType,
        GomokuAiDifficulty difficulty,
        GomokuBoardSnapshot boardSnapshot)
    {
        RequestId = requestId;
        AlgorithmType = algorithmType;
        Difficulty = difficulty;
        BoardSnapshot = boardSnapshot;
    }
}
