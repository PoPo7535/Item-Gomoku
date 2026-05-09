using UnityEngine;

public enum StoneColor { None, Black, White }

public struct StoneData
{
    public StoneColor Color;
    public bool IsFake; // 가짜돌 여부
    public bool IsTransparent; // 투명돌 여부 
}

public class OmokuLogic
{
    private const int BoardSize = 15;
    public StoneData[,] Board = new StoneData[BoardSize, BoardSize]; // 실제 돌데이터는 여기에 담긴다 0,0좌표 흑돌 이렇게

    private readonly int[] dx = { 1, 0, 1, 1 };
    private readonly int[] dy = { 0, 1, 1, -1 };


    /// <summary>
    /// 바둑판 데이터 초기화
    /// </summary>
    public OmokuLogic()
    {
        for (int i = 0; i < BoardSize; i++)
            for (int j = 0; j < BoardSize; j++)
                Board[i, j].Color = StoneColor.None;
    }

    /// <summary>
    /// 실제 바둑판에 착수 후 데이터 저장
    /// </summary>
    public bool PlaceStone(int x, int y, StoneColor color, bool isFake = false, bool isTransparent = false)
    {
        // 1. 범위를 벗어나면 바로 탈락
        if (!IsInside(x, y)) return false;

        // --- [수정된 체크 로직] ---
        bool isCurrentEmpty = Board[x, y].Color == StoneColor.None;
        bool isMyFakeStone = (Board[x, y].Color == color && Board[x, y].IsFake);
        bool isUpgradingToReal = !isFake; // 새로 두는 돌이 진짜돌인가?

        // 착수가 불가능한 경우: 
        // 빈칸이 아니면서 + (내 가짜돌을 진짜로 업그레이드하는 상황도 아닐 때)
        if (!isCurrentEmpty && !(isMyFakeStone && isUpgradingToReal))
        {
            return false;
        }
        // ------------------------

        // 데이터 업데이트 (덮어쓰기)
        Board[x, y] = new StoneData 
        { 
            Color = color, 
            IsFake = isFake, 
            IsTransparent = isTransparent 
        };

        // 2. 흑돌일 때만 렌주룰 금수 체크 (투명 돌은 진짜 돌이므로 금수 체크 대상)
        if (color == StoneColor.Black && !isFake)
        {
            if (CheckWin(x, y, color)) return true;

            if (IsForbidden(x, y, color))
            {
                // 금수일 경우 아예 지우는 게 아니라, 가짜돌 상태로 되돌림 (데이터 복구)
                // 실제 돌은 보이지만 판정은 가짜인상태
                Board[x, y] = new StoneData { Color = color, IsFake = true, IsTransparent = false };
                return false;
            }
        }

        return true;
    }

    // --- [금수 체크 메인] ---
    public bool IsForbidden(int x, int y, StoneColor color)
    {
        // 1. 장목 (6목 이상)
        for (int i = 0; i < 4; i++)
        {
            if (GetSequenceCount(x, y, dx[i], dy[i], color) > 5) return true;
        }

        // 2. 44 체크
        int fourCount = 0;
        for (int i = 0; i < 4; i++)
        {
            if (CheckFour(x, y, dx[i], dy[i], color)) fourCount++;
        }
        if (fourCount >= 2) return true;

        // 3. 33 체크
        int threeCount = 0;
        for (int i = 0; i < 4; i++)
        {
            if (IsOpenThree(x, y, dx[i], dy[i], color)) threeCount++;
        }
        if (threeCount >= 2) return true;

        return false;
    }

    /// <summary>
    /// [4 판정 시뮬레이션] 해당 자리에 두었을 때 '4'가 되는지 주변 빈칸을 수읽기함
    /// </summary>-
    private bool CheckFour(int x, int y, int dX, int dY, StoneColor color)
    {
        bool isFour = false;

        for (int i = -4; i <= 4; i++)
        {
            int tx = x + (dX * i);
            int ty = y + (dY * i);

            // 빈 곳(tx, ty)에 가상으로 하나 더 놔봅니다.
            if (IsInside(tx, ty) && Board[tx, ty].Color == StoneColor.None)
            {
                Board[tx, ty].Color = color; // 가상 착수
                
                // 그 자리에 두었을 때 5목이 완성된다면, 현재 상태는 '4'입니다.
                if (GetSequenceCount(tx, ty, dX, dY, color) == 5)
                {
                    isFour = true;
                }

                Board[tx, ty].Color = StoneColor.None; // 가상 착수만 다시 복구
                
                if (isFour) break;
            }
        }
        return isFour;
    }

    /// <summary>
    /// [열린 3 판정 시뮬레이션] 띄움 삼삼(X.XX)을 잡아내기 위해 가상 수읽기 진행
    /// </summary>
    public bool IsOpenThree(int x, int y, int dX, int dY, StoneColor color)
    {
        bool foundOpenFour = false;

        for (int i = -4; i <= 4; i++)
        {
            int tx = x + (dX * i);
            int ty = y + (dY * i);

            if (IsInside(tx, ty) && Board[tx, ty].Color == StoneColor.None)
            {
                Board[tx, ty].Color = color; // 빈 곳에 가상 착수
                
                // 하나 더 두었을 때 '열린 4'가 된다면, 현재 상태는 '열린 3'입니다.
                if (CheckOpenFour(tx, ty, dX, dY, color)) 
                {
                    foundOpenFour = true;
                }
                
                Board[tx, ty].Color = StoneColor.None; // 가상 착수 복구
            }
            if (foundOpenFour) break;
        }
        return foundOpenFour;
    }
    /// <summary>
    /// [열린 4 판정] 4개의 돌 양 끝이 모두 비어있어 수비가 불가능한 '열린 4'인지 확인
    /// </summary>
    private bool CheckOpenFour(int x, int y, int dX, int dY, StoneColor color)
    {
        if (GetSequenceCount(x, y, dX, dY, color) != 4) return false;

        int winPoints = 0;
        for (int i = -4; i <= 4; i++)
        {
            int tx = x + (dX * i);
            int ty = y + (dY * i);
            if (IsInside(tx, ty) && Board[tx, ty].Color == StoneColor.None)
            {
                if (GetSequenceCount(tx, ty, dX, dY, color) == 5) winPoints++;
            }
        }
        return winPoints == 2; 
    }

    /// <summary>
    /// [승리 판정] 흑/백의 서로 다른 승리 조건을 체크
    /// </summary>
    public bool CheckWin(int x, int y, StoneColor color)
    {
        if (Board[x, y].IsFake) return false;

        for (int i = 0; i < 4; i++)
        {
            int count = GetSequenceCount(x, y, dx[i], dy[i], color);
            // 렌주룰: 흑은 정확히 5개여야 승리, 백은 5개 이상이면 승리
            if (color == StoneColor.Black && count == 5) return true;
            if (color == StoneColor.White && count >= 5) return true;
        }
        return false;
    }
    /// <summary>
    /// [직선 탐색] 특정 좌표와 방향을 기준으로 연결된 같은 색 돌의 총 개수 반환
    /// </summary>
    public int GetSequenceCount(int x, int y, int dX, int dY, StoneColor color)
    {
        return 1 + CountStones(x, y, dX, dY, color) + CountStones(x, y, -dX, -dY, color);
    }
    /// <summary>
    /// [단방향 카운트] 한쪽 방향으로 벽이나 다른 색 돌을 만날 때까지 개수를 셈
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
    /// 바둑판 인덱스 범위 체크
    /// </summary>
    public bool IsInside(int x, int y) => x >= 0 && x < BoardSize && y >= 0 && y < BoardSize;
}