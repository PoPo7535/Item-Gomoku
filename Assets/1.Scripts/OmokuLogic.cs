using UnityEngine;

public enum StoneColor { None, Black, White }

public struct StoneData
{
    public StoneColor Color; 
    public bool IsFake; // 아이템 기능 만들때 쓸수있음
}

public class OmokuLogic
{
    private const int BoardSize = 15;
    public StoneData[,] Board = new StoneData[BoardSize, BoardSize]; //바둑판 데이터 배열 

    // 8방향 탐색을 위한 방향 벡터 (가로, 세로, 대각선 / , 대각선 \ )
    private readonly int[] dx = { 1, 0, 1, 1 };
    private readonly int[] dy = { 0, 1, 1, -1 };

    
    /// <summary>
    /// 특정 좌표에 착수 + 보드 데이터 갱신
    /// </summary>
    public bool PlaceStone(int x, int y, StoneColor color, bool isFake = false)
    {
        if (!IsInside(x, y) || Board[x, y].Color != StoneColor.None)
            return false;

        // 흑돌(Black)일 경우에만 렌주룰 금수 체크 (가짜 돌이 아닐 때만)
        if (color == StoneColor.Black && !isFake)
        {
            if (IsForbidden(x, y, color))
            {
                Debug.Log($"{x} , {y} 는 금수입니다");
                return false;
            }
        }

        Board[x, y] = new StoneData { Color = color, IsFake = isFake };
        return true;
    }


    /// <summary>
    /// 금수 여부 체크 현재는 전부다 체크
    /// </summary>
    public bool IsForbidden(int x, int y, StoneColor color)
    {
        // 1. 장목 체크 (6목 이상)
        for (int i = 0; i < 4; i++)
        {
            if (GetSequenceCount(x, y, dx[i], dy[i], color) > 5) return true;
        }

        // 2. 44 체크
        int fourCount = 0;
        for (int i = 0; i < 4; i++)
        {
            if (IsFour(x, y, dx[i], dy[i], color)) fourCount++;
        }
        if (fourCount >= 2) return true;

        // 3. 33 체크 (열린 3이 2개 이상)
        int threeCount = 0;
        for (int i = 0; i < 4; i++)
        {
            if (IsOpenThree(x, y, dx[i], dy[i], color)) threeCount++;
        }
        if (threeCount >= 2) return true;

        return false;
    }

    
    /// <summary>
    /// 방금 놓은 돌을 기준으로 오목이 완성되었는지 확인
    /// </summary>
    public bool CheckWin(int x, int y, StoneColor color)
    {
        if (Board[x, y].IsFake) return false;

        for (int i = 0; i < 4; i++)
        {
            int count = GetSequenceCount(x, y, dx[i], dy[i], color);
            
            if (color == StoneColor.Black && count == 5) return true;
            if (color == StoneColor.White && count >= 5) return true;
        }
        return false;
    }


    //---------- 유틸리티 및 보조 판정 함수 -----------

    
    /// <summary>
    /// 특정 방향(양방향 포함)으로 연속된 돌의 개수를 세어 반환
    /// </summary>
    public int GetSequenceCount(int x, int y, int dX, int dY, StoneColor color)
    {
        return 1 + CountStones(x, y, dX, dY, color) + CountStones(x, y, -dX, -dY, color);
    }

    
    /// <summary>
    /// 특정 방향으로 같은 색 돌이 몇 개 연속되는지 카운트
    /// </summary>
    public int CountStones(int x, int y, int dX, int dY, StoneColor color)
    {
        int cnt = 0;
        int cx = x + dX, cy = y + dY;
        while (IsInside(cx, cy) && Board[cx, cy].Color == color && !Board[cx, cy].IsFake)
        {
            cnt++; cx += dX; cy += dY;
        }
        return cnt;
    }

        
    /// <summary>
    /// 해당 방향으로 돌을 놓았을 때 4가 되는지 확인
    /// </summary>
    public bool IsFour(int x, int y, int dX, int dY, StoneColor color)
    {
        return GetSequenceCount(x, y, dX, dY, color) == 4;
    }

    
    /// <summary>
    /// 양 끝이 비어있는 열린 3인지 확인. (33 금수 판정용)
    /// </summary>
    public bool IsOpenThree(int x, int y, int dX, int dY, StoneColor color)
    {
        if (GetSequenceCount(x, y, dX, dY, color) != 3) return false;

        int headX = x + (CountStones(x, y, dX, dY, color) + 1) * dX;
        int headY = y + (CountStones(x, y, dX, dY, color) + 1) * dY;
        int tailX = x - (CountStones(x, y, -dX, -dY, color) + 1) * dX;
        int tailY = y - (CountStones(x, y, -dX, -dY, color) + 1) * dY;

        return IsInside(headX, headY) && Board[headX, headY].Color == StoneColor.None &&
               IsInside(tailX, tailY) && Board[tailX, tailY].Color == StoneColor.None;
    }


    /// <summary>
    /// 좌표가 바둑판 배열 범위 안에 있는지 확인
    /// </summary>
    public bool IsInside(int x, int y) => x >= 0 && x < BoardSize && y >= 0 && y < BoardSize;
}