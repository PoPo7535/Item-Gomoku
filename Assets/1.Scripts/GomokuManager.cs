using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Utility;

public class GomokuManager : LocalFusionSingleton<GomokuManager>
{
    [Header("참조 설정")]
    public GomokuBoardView BoardView;

    [Header("게임 설정")]
    public float TurnTimeLimit = 30f;


    [Networked] public NetworkBool IsBlackTurn { get; set; } = true;
    [Networked] public NetworkBool IsPlaying { get; set; }
    [Networked] public TickTimer TickTimer { get; set; }

    private OmokuLogic _logic;
    private StoneColor _myColor;
    private bool _isSpawned = false;

    public override void Spawned()
    {
        _isSpawned = true;
        if (BoardView != null) BoardView.Init();
        
        // 호스트가 명확하게 초기화하여 시작 버그 방지
        if (Object.HasStateAuthority)
        {
            IsPlaying = false;
            IsBlackTurn = true;
            TickTimer = TickTimer.None;
        }

        ResetGame();

        // 내 색상 설정
        if (App.I.PlayMode == GamePlayMode.Multi)
            _myColor = Object.HasStateAuthority ? StoneColor.Black : StoneColor.White;
        else
            _myColor = StoneColor.Black;
    }

    private void Update()
    {
        if (!_isSpawned || !IsPlaying)
        {
            if (BoardView != null) BoardView.UpdateGhostStone(Vector3.zero, false, false);
            return;
        }

        UpdateTurnTimer();

        var result = BoardView.GetBoardPosition();
        
        // 고스트 돌 조건: 레이 성공 AND 돌이 없는 자리
        bool canPlace = result.pos != Vector3.zero && _logic.Board[result.x, result.z].Color == StoneColor.None;
        
        // 멀티라면 내 턴일 때만 고스트 표시
        StoneColor currentTurn = IsBlackTurn ? StoneColor.Black : StoneColor.White;
        if (App.I.PlayMode == GamePlayMode.Multi && currentTurn != _myColor) canPlace = false;

        BoardView.UpdateGhostStone(result.pos, canPlace, IsBlackTurn);

        // 입력 처리 (0: 왼쪽클릭 - 싱글/테스트, 1: 오른쪽클릭 - 멀티RPC)
        if (Input.GetMouseButtonDown(0))
        {
            PlaceStoneProcess(result.pos, result.x, result.z);
        }
        
        if (Input.GetMouseButtonDown(1))
        {
            if (currentTurn == _myColor)
                Rpc_RequestPlaceStone(result.pos, result.x, result.z);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void Rpc_RequestPlaceStone(Vector3 pos, int x, int z)
    {
        PlaceStoneProcess(pos, x, z);
    }

    private void PlaceStoneProcess(Vector3 pos, int x, int z)
    {
        if (pos == Vector3.zero) return;
        StoneColor color = IsBlackTurn ? StoneColor.Black : StoneColor.White;

        if (_logic.PlaceStone(x, z, color))
        {
            BoardView.SpawnStone(x, z, IsBlackTurn, pos);

            if (_logic.CheckWin(x, z, color))
            {
                Debug.Log($"★ {color} 승리!");
                ResetGame();
                return;
            }
            ChangeTurn();
        }
    }

    public void StartGame()
    {
        if (App.I.PlayMode == GamePlayMode.Multi && !Object.HasStateAuthority) return;
        IsPlaying = true;
        StartTurnTimer();
    }

    public void ResetGame()
    {
        if (Object.HasStateAuthority) IsPlaying = false;
        IsBlackTurn = true;
        _logic = new OmokuLogic();
        if (BoardView != null) BoardView.ClearBoard();
    }

    public void ChangeTurn()
    {
        IsBlackTurn = !IsBlackTurn;
        StartTurnTimer();
    }

    private void StartTurnTimer()
    {
        if (Object.HasStateAuthority)
            TickTimer = TickTimer.CreateFromSeconds(App.I.Runner, TurnTimeLimit);
    }

    private void UpdateTurnTimer()
    {
        if (Object.HasStateAuthority && TickTimer.ExpiredOrNotRunning(App.I.Runner))
            ChangeTurn();
    }
}