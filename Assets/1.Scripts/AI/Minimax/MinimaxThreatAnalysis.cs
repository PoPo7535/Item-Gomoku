/// <summary>
/// 특정 좌표의 위협 형태를 정리한 결과임.
/// </summary>
internal struct MinimaxThreatAnalysis
{
    public int Score;
    public int OpenThreeCount;
    public int BlockedFourCount;
    public int OpenFourCount;
    public int GappedFourCount;
    public int BrokenThreeCount;
    public int OpenTwoDirectionCount;
}
