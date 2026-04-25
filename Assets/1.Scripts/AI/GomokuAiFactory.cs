using UnityEngine;

/// <summary>
/// 선택된 AI 알고리즘 타입에 맞는 오목 AI 구현체를 생성함.
/// </summary>
public static class GomokuAiFactory
{
    /// <summary>
    /// 지정된 알고리즘 타입에 맞는 AI 구현체를 생성함.
    /// </summary>
    /// <param name="algorithmType">생성할 AI 알고리즘 타입.</param>
    /// <param name="logic">AI가 참조할 오목 규칙/보드 상태.</param>
    /// <param name="boardSize">보드 크기.</param>
    /// <returns>생성된 AI 구현체.</returns>
    public static IGomokuAI Create(GomokuAiAlgorithmType algorithmType, OmokuLogic logic, int boardSize)
    {
        switch (algorithmType)
        {
            case GomokuAiAlgorithmType.Minimax:
                return new MinimaxGomokuAI(logic, boardSize);
            default:
                Debug.LogWarning($"지원하지 않는 AI 알고리즘입니다: {algorithmType}. Minimax로 대체합니다.");
                return new MinimaxGomokuAI(logic, boardSize);
        }
    }
}
