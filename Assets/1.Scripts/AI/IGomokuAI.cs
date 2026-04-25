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
}
