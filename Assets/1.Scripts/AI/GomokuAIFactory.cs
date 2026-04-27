/// <summary>
/// 선택된 AI 알고리즘 타입에 맞는 오목 AI 구현체를 생성함.
/// </summary>
public static class GomokuAIFactory
{
    /// <summary>
    /// 지정된 알고리즘 타입에 맞는 AI 구현체를 생성함.
    /// </summary>
    /// <param name="algorithmType">생성할 AI 알고리즘 타입.</param>
    /// <param name="logic">AI가 참조할 오목 규칙/보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <returns>생성된 AI 구현체.</returns>
    public static IGomokuAI Create(GomokuAIAlgorithmType algorithmType, OmokuLogic logic, int boardSize)
    {
        switch (algorithmType)
        {
            case GomokuAIAlgorithmType.Minimax:
                return new MinimaxGomokuAI(logic, boardSize);
            default:
                // 지원하지 않는 값은 현재 사용 가능한 Minimax로 대체함.
                return new MinimaxGomokuAI(logic, boardSize);
        }
    }

    /// <summary>
    /// 보드 스냅샷 복사본을 기반으로 AI 구현체를 생성함.
    /// </summary>
    /// <param name="algorithmType">생성할 AI 알고리즘 타입.</param>
    /// <param name="boardSnapshot">AI 전용 보드 스냅샷.</param>
    /// <returns>생성된 AI 구현체.</returns>
    public static IGomokuAI Create(GomokuAIAlgorithmType algorithmType, GomokuBoardSnapshot boardSnapshot)
    {
        OmokuLogic logicCopy = boardSnapshot.CreateLogicCopy();
        return Create(algorithmType, logicCopy, boardSnapshot.BoardSize);
    }
}
