using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// GomokuManager의 AI 모드 탐색, 검증, fallback 흐름을 담당함.
/// </summary>
public partial class GomokuManager
{
    private enum GomokuAIStoneColor
    {
        White,
        Black,
    }

    [Header("AI 설정")]
    [SerializeField] private GomokuAIStoneColor _aiStoneColor = GomokuAIStoneColor.White;
    [SerializeField] private GomokuAIAlgorithmType _aiAlgorithmType = GomokuAIAlgorithmType.Minimax;
    [SerializeField] private GomokuAIDifficulty _aiDifficulty = GomokuAIDifficulty.Normal;
    [SerializeField] private float _maxAiSearchTimeSeconds = 7f;

    private bool _isAiThinking;
    private int _boardVersion;
    private int _aiSearchRequestId;
    private CancellationTokenSource _aiSearchCancellationTokenSource;

    /// <summary>
    /// Inspector에서 선택한 AI 색상을 실제 돌 색상으로 반환함.
    /// </summary>
    private StoneColor AiStoneColor => _aiStoneColor == GomokuAIStoneColor.Black ? StoneColor.Black : StoneColor.White;

    /// <summary>
    /// AI 반대편 플레이어 색상을 반환함.
    /// </summary>
    private StoneColor PlayerStoneColor => GetOppositeStoneColor(AiStoneColor);

    /// <summary>
    /// 현재 네트워크 턴 값을 실제 돌 색상으로 반환함.
    /// </summary>
    private StoneColor CurrentTurnColor => IsBlackTurn ? StoneColor.Black : StoneColor.White;

    /// <summary>
    /// 현재 턴이 AI 턴인지 반환함.
    /// </summary>
    private bool IsAiTurn => CurrentTurnColor == AiStoneColor;

    /// <summary>
    /// 현재 턴이 플레이어 턴인지 반환함.
    /// </summary>
    private bool IsPlayerTurn => CurrentTurnColor == PlayerStoneColor;

    /// <summary>
    /// 매니저가 파괴될 때 진행 중인 AI 탐색을 정리함.
    /// </summary>
    private void OnDestroy()
    {
        CancelAiSearchRequest();
        base.OnDestroy();
    }

    /// <summary>
    /// 보드 변경 버전을 증가시켜 오래된 AI 결과를 폐기할 수 있게 함.
    /// </summary>
    private void NotifyBoardChanged()
    {
        _boardVersion++;
    }

    /// <summary>
    /// 새 보드 시작 시 AI 런타임 상태를 초기화함.
    /// </summary>
    private void ResetAiBoardState()
    {
        _isAiThinking = false;
        _boardVersion = 0;
        _aiSearchRequestId++;
    }

    /// <summary>
    /// 현재 상태가 AI 턴이면 비동기 AI 착수를 예약함.
    /// </summary>
    private void TryScheduleAiTurnIfNeeded()
    {
        ScheduleAiTurnIfNeededAsync().Forget();
    }

    /// <summary>
    /// 한 프레임 뒤 초기화 상태를 확인하고 AI 턴을 시작함.
    /// </summary>
    private async UniTaskVoid ScheduleAiTurnIfNeededAsync()
    {
        await UniTask.Yield();

        if (!CanStartAiTurn())
        {
            return;
        }

        HandleAiTurnAsync().Forget();
    }

    /// <summary>
    /// AI 턴을 시작할 수 있는 현재 상태인지 확인함.
    /// </summary>
    private bool CanStartAiTurn()
    {
        return App.I.PlayMode == GamePlayMode.AI &&
               IsPlaying &&
               !_isAiThinking &&
               IsAiTurn &&
               _logic != null;
    }

    /// <summary>
    /// 현재 보드 스냅샷으로 AI 추천 수를 비동기로 계산하고 적용함.
    /// </summary>
    private async UniTaskVoid HandleAiTurnAsync()
    {
        if (!CanStartAiTurn())
        {
            return;
        }

        CancelAiSearchRequest();
        _isAiThinking = true;
        BoardView?.UpdateGhostStone(Vector3.zero, false, false,false);

        int requestId = ++_aiSearchRequestId;
        CancellationTokenSource searchCancellationTokenSource = new CancellationTokenSource();
        _aiSearchCancellationTokenSource = searchCancellationTokenSource;

        GomokuBoardSnapshot snapshot = new GomokuBoardSnapshot(_logic.Board, _boardVersion);
        GomokuAISearchRequest request = new GomokuAISearchRequest(
            requestId,
            _aiAlgorithmType,
            _aiDifficulty,
            AiStoneColor,
            snapshot,
            _maxAiSearchTimeSeconds);

        try
        {
            GomokuAISearchResult searchResult = await GomokuAIAsyncRunner.FindBestMoveAsync(request, searchCancellationTokenSource.Token);
            await UniTask.SwitchToMainThread(searchCancellationTokenSource.Token);

            if (!IsAiSearchRequestActive(request))
            {
                return;
            }

            if (searchResult.Status == GomokuAISearchResultStatus.Canceled)
            {
                CompleteAiTurnWithoutMove("AI 탐색이 취소되었습니다.");
                return;
            }

            if (TryApplyAiSearchResult(request, searchResult.Move))
            {
                CompleteAiTurn();
                return;
            }

            if (TryApplyFallbackAiMove(request, searchResult.Reason))
            {
                CompleteAiTurn();
                return;
            }

            HoldAiTurnAfterInvalidResult($"AI 결과 적용 실패 후 안전 fallback도 찾지 못했습니다: {searchResult.Status}");
        }
        catch (OperationCanceledException)
        {
            if (IsAiSearchRequestActive(request))
            {
                CompleteAiTurnWithoutMove("AI 탐색이 취소되었습니다.");
            }
        }
        finally
        {
            DisposeAiSearchRequest(searchCancellationTokenSource);
        }
    }

    /// <summary>
    /// AI 탐색 결과가 현재 보드에 적용 가능한지 확인한 뒤 착수함.
    /// </summary>
    /// <param name="request">AI 탐색 요청 정보.</param>
    /// <param name="move">AI가 반환한 착수 후보.</param>
    /// <returns>AI 결과 착수 성공 여부.</returns>
    private bool TryApplyAiSearchResult(GomokuAISearchRequest request, GomokuMove move)
    {
        if (!CanApplyAiSearchResult(request, move))
        {
            return false;
        }

        return PlaceStoneProcess(move.X, move.Y, request.AiStoneColor);
    }

    /// <summary>
    /// AI 탐색 결과가 현재 라이브 보드에 적용 가능한지 검증함.
    /// </summary>
    /// <param name="request">AI 탐색 요청 정보.</param>
    /// <param name="move">AI가 반환한 착수 후보.</param>
    /// <returns>적용 가능 여부.</returns>
    private bool CanApplyAiSearchResult(GomokuAISearchRequest request, GomokuMove move)
    {
        return CanApplyAiSearchRequest(request) &&
               move.IsValid &&
               CanPlaceStoneSafely(move.X, move.Y, request.AiStoneColor);
    }

    /// <summary>
    /// AI 요청이 현재 라이브 게임 상태에 적용 가능한지 공통 검증함.
    /// </summary>
    /// <param name="request">AI 탐색 요청 정보.</param>
    /// <returns>현재 게임 상태에 적용 가능한 요청인지 여부.</returns>
    private bool CanApplyAiSearchRequest(GomokuAISearchRequest request)
    {
        return IsAiSearchRequestActive(request) &&
               App.I.PlayMode == GamePlayMode.AI &&
               IsPlaying &&
               IsAiTurn &&
               request.AiStoneColor == AiStoneColor &&
               _boardVersion == request.BoardVersion;
    }

    /// <summary>
    /// AI 추천 수가 무효일 때 현재 라이브 보드 기준 안전 fallback 착수를 시도함.
    /// </summary>
    /// <param name="request">AI 탐색 요청 정보.</param>
    /// <param name="reason">fallback을 시도하는 이유.</param>
    /// <returns>fallback 착수 성공 여부.</returns>
    private bool TryApplyFallbackAiMove(GomokuAISearchRequest request, string reason)
    {
        if (!CanApplyAiSearchRequest(request) ||
            !TryFindSafeAiFallbackMove(request.AiStoneColor, out int x, out int z))
        {
            return false;
        }

        Debug.LogWarning($"{reason} 안전 fallback으로 AI 착수를 대체합니다: ({x}, {z})");
        return PlaceStoneProcess(x, z, request.AiStoneColor);
    }

    /// <summary>
    /// 현재 라이브 보드에서 AI가 둘 수 있는 안전한 fallback 좌표를 찾음.
    /// </summary>
    /// <param name="stoneColor">fallback을 찾을 돌 색상.</param>
    /// <param name="x">찾은 X 좌표.</param>
    /// <param name="z">찾은 Z 좌표.</param>
    /// <returns>fallback 좌표 탐색 성공 여부.</returns>
    private bool TryFindSafeAiFallbackMove(StoneColor stoneColor, out int x, out int z)
    {
        int boardSize = GetBoardSize();
        int center = boardSize / 2;
        if (CanPlaceStoneSafely(center, center, stoneColor))
        {
            x = center;
            z = center;
            return true;
        }

        for (int ix = 0; ix < boardSize; ix++)
        {
            for (int iz = 0; iz < boardSize; iz++)
            {
                if (!CanPlaceStoneSafely(ix, iz, stoneColor))
                {
                    continue;
                }

                x = ix;
                z = iz;
                return true;
            }
        }

        x = -1;
        z = -1;
        return false;
    }

    /// <summary>
    /// AI 전용 좌표 기반 착수를 기존 착수 경로로 적용함.
    /// </summary>
    /// <param name="x">착수 X 좌표.</param>
    /// <param name="z">착수 Z 좌표.</param>
    /// <param name="stoneColor">착수 돌 색상.</param>
    /// <returns>착수 요청 성공 여부.</returns>
    private bool PlaceStoneProcess(int x, int z, StoneColor stoneColor)
    {
        if (!CanPlaceStoneSafely(x, z, stoneColor) ||
            BoardView == null ||
            !BoardView.TryGetWorldPositionByCoord(x, z, out Vector3 pos))
        {
            return false;
        }

        PlaceStoneProcess(pos, x, z, stoneColor == StoneColor.Black);
        return true;
    }

    /// <summary>
    /// 현재 보드에서 지정 좌표에 안전하게 착수 가능한지 확인함.
    /// </summary>
    /// <param name="x">검사할 X 좌표.</param>
    /// <param name="z">검사할 Z 좌표.</param>
    /// <param name="stoneColor">검사할 돌 색상.</param>
    /// <returns>착수 가능 여부.</returns>
    private bool CanPlaceStoneSafely(int x, int z, StoneColor stoneColor)
    {
        return _logic != null &&
               _logic.IsInside(x, z) &&
               _logic.Board[x, z].Color == StoneColor.None &&
               !IsForbiddenAfterTemporaryPlacement(x, z, stoneColor);
    }

    /// <summary>
    /// 실제 착수와 같은 순서로 흑돌 금수 여부를 확인함.
    /// </summary>
    /// <param name="x">검사할 X 좌표.</param>
    /// <param name="z">검사할 Z 좌표.</param>
    /// <param name="stoneColor">검사할 돌 색상.</param>
    /// <returns>임시 착수 후 금수 여부.</returns>
    private bool IsForbiddenAfterTemporaryPlacement(int x, int z, StoneColor stoneColor)
    {
        if (_logic == null || stoneColor != StoneColor.Black)
        {
            return false;
        }

        // 실제 PlaceStone과 동일하게 흑돌을 둔 뒤 승리 여부를 먼저 확인함.
        _logic.Board[x, z] = new StoneData { Color = stoneColor, IsFake = false };
        try
        {
            if (_logic.CheckWin(x, z, stoneColor))
            {
                return false;
            }

            return _logic.IsForbidden(x, z, stoneColor);
        }
        finally
        {
            _logic.Board[x, z] = new StoneData { Color = StoneColor.None, IsFake = false };
        }
    }

    /// <summary>
    /// 현재 적용 가능한 최신 AI 요청인지 확인함.
    /// </summary>
    /// <param name="request">확인할 AI 요청.</param>
    /// <returns>최신 요청 여부.</returns>
    private bool IsAiSearchRequestActive(GomokuAISearchRequest request)
    {
        return request.RequestId == _aiSearchRequestId && _isAiThinking;
    }

    /// <summary>
    /// 진행 중인 AI 탐색 요청을 취소하고 토큰을 정리함.
    /// </summary>
    private void CancelAiSearchRequest()
    {
        if (_aiSearchCancellationTokenSource == null)
        {
            return;
        }

        _aiSearchCancellationTokenSource.Cancel();
        _aiSearchCancellationTokenSource.Dispose();
        _aiSearchCancellationTokenSource = null;
        _aiSearchRequestId++;
        _isAiThinking = false;
    }

    /// <summary>
    /// 완료된 AI 탐색 요청 토큰을 정리함.
    /// </summary>
    /// <param name="searchCancellationTokenSource">정리할 토큰 소스.</param>
    private void DisposeAiSearchRequest(CancellationTokenSource searchCancellationTokenSource)
    {
        if (_aiSearchCancellationTokenSource != searchCancellationTokenSource)
        {
            return;
        }

        _aiSearchCancellationTokenSource.Dispose();
        _aiSearchCancellationTokenSource = null;
    }

    /// <summary>
    /// AI 착수 없이 AI thinking 상태를 안전하게 해제함.
    /// </summary>
    /// <param name="warningMessage">출력할 경고 메시지.</param>
    private void CompleteAiTurnWithoutMove(string warningMessage)
    {
        if (!string.IsNullOrEmpty(warningMessage))
        {
            Debug.LogWarning(warningMessage);
        }

        _isAiThinking = false;
    }

    /// <summary>
    /// AI 착수 후 AI thinking 상태를 해제함.
    /// </summary>
    private void CompleteAiTurn()
    {
        _isAiThinking = false;
    }

    /// <summary>
    /// 유효한 AI 착수를 찾지 못한 뒤 AI 턴을 유지하는 안전 상태로 정리함.
    /// </summary>
    /// <param name="warningMessage">출력할 경고 메시지.</param>
    private void HoldAiTurnAfterInvalidResult(string warningMessage)
    {
        Debug.LogWarning(warningMessage);
        _isAiThinking = false;
        BoardView?.UpdateGhostStone(Vector3.zero, false, false,false);
    }

    /// <summary>
    /// 현재 보드 크기를 반환함.
    /// </summary>
    /// <returns>보드 크기.</returns>
    private int GetBoardSize()
    {
        if (_logic != null && _logic.Board != null)
        {
            return _logic.Board.GetLength(0);
        }

        return BoardView != null ? BoardView.LineCount : 15;
    }

    /// <summary>
    /// 지정한 돌 색상의 반대 색상을 반환함.
    /// </summary>
    /// <param name="stoneColor">기준 돌 색상.</param>
    /// <returns>반대 돌 색상.</returns>
    private static StoneColor GetOppositeStoneColor(StoneColor stoneColor)
    {
        return stoneColor == StoneColor.Black ? StoneColor.White : StoneColor.Black;
    }
}
