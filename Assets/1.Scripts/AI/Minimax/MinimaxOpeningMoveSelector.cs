using System.Collections.Generic;

/// <summary>
/// Minimax AI의 초반 응수 선택을 담당하는 partial 영역임.
/// </summary>
public partial class MinimaxGomokuAI
{
    /// <summary>
    /// AI 첫 착수 상황이면 플레이어 첫 돌 주변에서 opening 수를 선택함.
    /// </summary>
    /// <returns>opening 규칙으로 고른 첫 수, 없으면 Invalid.</returns>
    private GomokuMove FindOpeningMove()
    {
        if (!IsAiFirstMoveState(out int playerX, out int playerY))
        {
            return GomokuMove.Invalid("Opening not applicable");
        }

        List<GomokuMove> nearbyMoves = CollectAdjacentOpeningMoves(playerX, playerY);
        if (nearbyMoves.Count == 0)
        {
            return GomokuMove.Invalid("Opening candidates not found");
        }

        // 첫 수는 플레이어 첫 돌 근처에서만 고르되 evaluator로 가장 좋은 수를 선택함.
        SortCandidates(nearbyMoves, _aiColor);
        GomokuMove bestMove = nearbyMoves[0];
        return new GomokuMove(bestMove.X, bestMove.Y, bestMove.Score, "Opening response");
    }

    /// <summary>
    /// 현재 보드가 흑돌 1개, 백돌 0개인 AI 첫 착수 상태인지 확인함.
    /// </summary>
    /// <param name="playerX">플레이어 첫 돌 X 좌표.</param>
    /// <param name="playerY">플레이어 첫 돌 Y 좌표.</param>
    /// <returns>AI 첫 착수 상태 여부.</returns>
    private bool IsAiFirstMoveState(out int playerX, out int playerY)
    {
        playerX = -1;
        playerY = -1;
        int opponentStoneCount = 0;
        int aiStoneCount = 0;

        for (int x = 0; x < _boardSize; x++)
        {
            ThrowIfCancellationRequested();
            for (int y = 0; y < _boardSize; y++)
            {
                StoneData stoneData = _logic.Board[x, y];
                if (stoneData.IsFake || stoneData.Color == StoneColor.None)
                {
                    continue;
                }

                if (stoneData.Color == _opponentColor)
                {
                    opponentStoneCount++;
                    playerX = x;
                    playerY = y;
                    continue;
                }

                if (stoneData.Color == _aiColor)
                {
                    aiStoneCount++;
                }
            }
        }

        return opponentStoneCount == 1 && aiStoneCount == 0;
    }

    /// <summary>
    /// 플레이어 첫 돌 주변 8방향의 유효 opening 후보를 수집함.
    /// </summary>
    /// <param name="originX">플레이어 첫 돌 X 좌표.</param>
    /// <param name="originY">플레이어 첫 돌 Y 좌표.</param>
    /// <returns>유효한 인접 opening 후보 목록.</returns>
    private List<GomokuMove> CollectAdjacentOpeningMoves(int originX, int originY)
    {
        List<GomokuMove> nearbyMoves = new List<GomokuMove>();

        for (int deltaX = -1; deltaX <= 1; deltaX++)
        {
            for (int deltaY = -1; deltaY <= 1; deltaY++)
            {
                if (deltaX == 0 && deltaY == 0)
                {
                    continue;
                }

                int targetX = originX + deltaX;
                int targetY = originY + deltaY;
                if (!IsLegalMove(targetX, targetY, _aiColor))
                {
                    continue;
                }

                _stats.EvaluateMoveCallCount++;
                int score = _evaluator.EvaluateMove(_logic, _boardSize, targetX, targetY, _aiColor, _aiColor);
                nearbyMoves.Add(new GomokuMove(targetX, targetY, score, "Opening neighbor"));
            }
        }

        return nearbyMoves;
    }
}
