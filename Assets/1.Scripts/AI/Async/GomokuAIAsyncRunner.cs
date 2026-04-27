using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// AI 탐색을 ThreadPool에서 실행하고 결과를 반환함.
/// </summary>
public static class GomokuAIAsyncRunner
{
    /// <summary>
    /// 스냅샷 기반 AI 탐색을 백그라운드 스레드에서 실행함.
    /// </summary>
    /// <param name="request">AI 탐색 요청.</param>
    /// <param name="cancellationToken">탐색 취소 토큰.</param>
    /// <returns>AI가 선택한 착수 후보.</returns>
    public static UniTask<GomokuMove> FindBestMoveAsync(GomokuAISearchRequest request, CancellationToken cancellationToken)
    {
        return UniTask.RunOnThreadPool(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                OmokuLogic logicCopy = request.BoardSnapshot.CreateLogicCopy();
                IGomokuAI ai = GomokuAIFactory.Create(request.AlgorithmType, logicCopy, request.BoardSize);
                return ai.FindBestMove(request.SearchDepth, cancellationToken);
            },
            cancellationToken: cancellationToken);
    }
}
