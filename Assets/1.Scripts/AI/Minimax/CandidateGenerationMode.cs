/// <summary>
/// 후보 생성 목적에 따라 평가 비용과 후보 상한을 구분함.
/// </summary>
internal enum CandidateGenerationMode
{
    RootEvaluation,
    SearchNode,
    ThreatScan
}
