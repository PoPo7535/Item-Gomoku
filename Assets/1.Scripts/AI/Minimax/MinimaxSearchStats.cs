/// <summary>
/// Minimax 탐색 1회 동안 수집하는 성능 계측 값을 관리함.
/// </summary>
internal sealed class MinimaxSearchStats
{
    public int GeneratedCandidateCallCount;
    public int RootGeneratedCandidateCount;
    public int SearchNodeGeneratedCandidateCount;
    public int ThreatScanGeneratedCandidateCount;
    public int EvaluateMoveCallCount;
    public int RootEvaluationCacheHitCount;
    public int LightweightEvaluationCallCount;
    public int AnalyzeThreatCallCount;
    public int MinimaxNodeCount;
    public int PruningCount;

    /// <summary>
    /// 탐색 1회에만 유효한 계측 값을 초기화함.
    /// </summary>
    public void Reset()
    {
        GeneratedCandidateCallCount = 0;
        RootGeneratedCandidateCount = 0;
        SearchNodeGeneratedCandidateCount = 0;
        ThreatScanGeneratedCandidateCount = 0;
        EvaluateMoveCallCount = 0;
        RootEvaluationCacheHitCount = 0;
        LightweightEvaluationCallCount = 0;
        AnalyzeThreatCallCount = 0;
        MinimaxNodeCount = 0;
        PruningCount = 0;
    }

    /// <summary>
    /// 후보 생성 결과를 모드별로 계측함.
    /// </summary>
    /// <param name="mode">후보 생성 모드.</param>
    /// <param name="candidateCount">최종 후보 개수.</param>
    public void RecordGeneratedCandidates(CandidateGenerationMode mode, int candidateCount)
    {
        GeneratedCandidateCallCount++;
        switch (mode)
        {
            case CandidateGenerationMode.RootEvaluation:
                RootGeneratedCandidateCount += candidateCount;
                break;
            case CandidateGenerationMode.SearchNode:
                SearchNodeGeneratedCandidateCount += candidateCount;
                break;
            case CandidateGenerationMode.ThreatScan:
                ThreatScanGeneratedCandidateCount += candidateCount;
                break;
        }
    }
}
