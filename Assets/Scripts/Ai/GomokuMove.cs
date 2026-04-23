/// <summary>
/// AI가 검토하거나 선택한 착수 정보를 표현함.
/// </summary>
public struct GomokuMove
{
    public int X;
    public int Y;
    public int Score;
    public bool IsValid;
    public string Reason;

    /// <summary>
    /// 유효한 착수 정보를 생성함.
    /// </summary>
    public GomokuMove(int x, int y, int score, string reason)
    {
        X = x;
        Y = y;
        Score = score;
        IsValid = true;
        Reason = reason;
    }

    /// <summary>
    /// 유효하지 않은 착수 정보를 생성함.
    /// </summary>
    public static GomokuMove Invalid(string reason)
    {
        return new GomokuMove
        {
            X = -1,
            Y = -1,
            Score = 0,
            IsValid = false,
            Reason = reason
        };
    }
}
