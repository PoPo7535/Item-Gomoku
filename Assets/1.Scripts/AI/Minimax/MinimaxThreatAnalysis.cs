/// <summary>
/// 특정 좌표의 위협 형태를 정리한 결과임.
/// </summary>
internal struct MinimaxThreatAnalysis
{
    // 합산 점수가 아니라 해당 좌표에서 발견된 가장 강한 단일 전술 위협 점수임.
    // 복합 위협 판단은 Score가 아니라 count와 direction mask 필드를 함께 봐야 함.
    public int Score;
    public int OpenThreeCount;
    public int BlockedFourCount;
    public int OpenFourCount;
    public int GappedFourCount;
    public int BrokenThreeCount;
    public int OpenTwoDirectionCount;
    public int OpenThreeDirectionMask;
    public int BlockedFourDirectionMask;
    public int GappedFourDirectionMask;
}
