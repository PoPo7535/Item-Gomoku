using System.Threading;

/// <summary>
/// 오목 AI 알고리즘이 제공해야 하는 공통 탐색 계약을 정의함.
/// </summary>
public interface IGomokuAI
{
    /// <summary>
    /// 지정된 탐색 깊이로 다음 착수 후보를 찾음.
    /// </summary>
    /// <param name="searchDepth">탐색 깊이 또는 알고리즘별 난이도 값.</param>
    /// <returns>AI가 선택한 착수 후보.</returns>
    GomokuMove FindBestMove(int searchDepth);

    /// <summary>
    /// 지정된 탐색 깊이와 취소 토큰으로 다음 착수 후보를 찾음.
    /// </summary>
    /// <param name="searchDepth">탐색 깊이 또는 알고리즘별 난이도 값.</param>
    /// <param name="cancellationToken">탐색 취소 토큰.</param>
    /// <returns>AI가 선택한 착수 후보.</returns>
    GomokuMove FindBestMove(int searchDepth, CancellationToken cancellationToken);

    /// <summary>
    /// 지정된 탐색 깊이, 취소 토큰, 시간 제한으로 다음 착수 후보를 찾음.
    /// </summary>
    /// <param name="searchDepth">탐색 깊이 또는 알고리즘별 난이도 값.</param>
    /// <param name="cancellationToken">외부 탐색 취소 토큰.</param>
    /// <param name="maxSearchTimeSeconds">탐색 시간 제한 초 단위 값.</param>
    /// <returns>AI 탐색 상태와 선택된 착수 후보.</returns>
    GomokuAISearchResult FindBestMove(int searchDepth, CancellationToken cancellationToken, double maxSearchTimeSeconds);
}
