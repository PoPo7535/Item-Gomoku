/// <summary>
/// 5칸 window를 raw 분석한 결과임.
/// </summary>
internal readonly struct ThreatPatternWindowResult
{
    /// <summary>
    /// 5칸 window 분석 결과를 생성함.
    /// </summary>
    public ThreatPatternWindowResult(
        int colorCount,
        int emptyCount,
        int firstColorIndex,
        int lastColorIndex,
        int gapIndex,
        int internalGapCount,
        bool hasEndExtension,
        bool hasOuterExtension)
    {
        ColorCount = colorCount;
        EmptyCount = emptyCount;
        FirstColorIndex = firstColorIndex;
        LastColorIndex = lastColorIndex;
        GapIndex = gapIndex;
        InternalGapCount = internalGapCount;
        HasEndExtension = hasEndExtension;
        HasOuterExtension = hasOuterExtension;
    }

    public int ColorCount { get; }
    public int EmptyCount { get; }
    public int FirstColorIndex { get; }
    public int LastColorIndex { get; }
    public int GapIndex { get; }
    public int InternalGapCount { get; }
    public bool HasEndExtension { get; }
    public bool HasOuterExtension { get; }
}
