using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Utility;

public class GomokuManager : LocalFusionSingleton<GomokuManager>
{
    [Header("참조 설정")]
    public GomokuBoardView BoardView; // 인스펙터에서 연결 필수

    [Header("게임 설정")]
    public float TurnTimeLimit = 30f;
    
    // 네트워크 동기화 변수
    [Networked] public NetworkBool IsBlackTurn { get; set; } = true;
    [Networked] public NetworkBool IsPlaying { get; set; }
    [Networked] public TickTimer TickTimer { get; set; }

    private OmokuLogic _logic;
    private StoneColor _myColor;
    private bool _isSpawned = false;

    // 기록 관리
    private readonly List<string> _blackHistory = new();
    private readonly List<string> _whiteHistory = new();

    public override void Spawned()
    {
        _isSpawned = true;
        
        // 시각적 요소(바둑판 포인트 등) 초기화는 View에게 맡김
        if (BoardView != null) BoardView.Init();
        
        ResetGame();

        // 플레이 모드에 따른 내 색상 설정
        switch (App.I.PlayMode)
        {
            case GamePlayMode.Single:
                _myColor = StoneColor.Black; 
                break;
            case GamePlayMode.Multi:
                _myColor = Object.HasStateAuthority ? StoneColor.Black : StoneColor.White;
                break;
        }
    }

    private void Update()
    {
        if (!_isSpawned || !IsPlaying)
        {
            // 게임 중이 아닐 땐 고스트 끄기 요청
            if (BoardView != null) BoardView.UpdateGhostStone(Vector3.zero, 0, 0, false, false);
            return;
        }

        UpdateTurnTimer();

        // 1. 레이캐스트 및 입력 처리
        var result = CalculateRay();
        
        // 2. 고스트 돌 표시 (View에게 시각적 처리 요청)
        HandleGhostDisplay(result);

        // 3. 입력 감지
        if (Input.GetMouseButtonDown(0))
        {
            HandleInput(result, false); // 로컬/싱글 플레이용
        }
        
        if (Input.GetMouseButtonDown(1))
        {
            HandleInput(result, true); // 멀티플레이용 RPC
        }
    }

    private void HandleInput((Vector3 pos, int x, int z) result, bool isMulti)
    {
        if (result.pos == Vector3.zero) return;

        if (isMulti)
        {
            // 내 턴일 때만 RPC 발사
            StoneColor currentTurnColor = IsBlackTurn ? StoneColor.Black : StoneColor.White;
            if (currentTurnColor == _myColor)
            {
                Rpc_PlaceStone(result.pos, IsBlackTurn, result.x, result.z);
            }
        }
        else
        {
            PlaceStone(result.pos, IsBlackTurn, result.x, result.z);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void Rpc_PlaceStone(Vector3 pos, bool isBlackTurn, int x, int z)
    {
        PlaceStone(pos, isBlackTurn, x, z);
    }

    private void PlaceStone(Vector3 pos, bool isBlackTurn, int x, int z)
    {
        StoneColor currentColor = isBlackTurn ? StoneColor.Black : StoneColor.White;

        if (_logic.PlaceStone(x, z, currentColor))
        {
            // 1. 시각적 돌 생성 (View 호출)
            BoardView.SpawnStoneVisual(x, z, isBlackTurn, pos);

            // 2. 기록 저장
            string posText = $"{x},{z}";
            if (isBlackTurn) _blackHistory.Add(posText);
            else _whiteHistory.Add(posText);
            
            Debug.Log($"<color=orange>[착수]</color> {currentColor}: ({x}, {z})");

            // 3. 승리 판정
            if (_logic.CheckWin(x, z, currentColor))
            {
                Debug.Log($"<color=cyan>★ 승리! {currentColor} ★</color>");
                ResetGame();
                return;
            }

            ChangeTurn();
        }
    }

    public void ChangeTurn()
    {
        IsBlackTurn = !IsBlackTurn;
        StartTurnTimer();
    }

    private void StartTurnTimer()
    {
        if (!Object.HasStateAuthority) return;
        TickTimer = TickTimer.CreateFromSeconds(App.I.Runner, TurnTimeLimit);
    }

    private void UpdateTurnTimer()
    {
        if (!Object.HasStateAuthority) return;

        if (TickTimer.ExpiredOrNotRunning(App.I.Runner))
        {
            ChangeTurn();
        }
    }

    private void HandleGhostDisplay((Vector3 pos, int x, int z) result)
    {
        if (BoardView == null) return;

        bool isVisible = result.pos != Vector3.zero && _logic.Board[result.x, result.z].Color == StoneColor.None;
        
        // 내 턴일 때만 고스트 보여주기 (멀티 기준)
        StoneColor currentTurn = IsBlackTurn ? StoneColor.Black : StoneColor.White;
        if (App.I.PlayMode == GamePlayMode.Multi && currentTurn != _myColor) isVisible = false;

        BoardView.UpdateGhostStone(result.pos, result.x, result.z, isVisible, IsBlackTurn);
    }

    private (Vector3 pos, int x, int z) CalculateRay()
    {
        // View에 있는 카메라와 이미지를 사용해 레이 계산
        if (BoardView.GameViewImage == null || BoardView.BoardCamera == null) return (Vector3.zero, 0, 0);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            BoardView.GameViewImage.rectTransform, Input.mousePosition, null, out Vector2 localPoint);

        Rect r = BoardView.GameViewImage.rectTransform.rect;
        float nX = (localPoint.x - r.x) / r.width;
        float nY = (localPoint.y - r.y) / r.height;

        Ray ray = BoardView.BoardCamera.ViewportPointToRay(new Vector3(nX, nY, 0));
        int layerMask = 1 << LayerMask.NameToLayer("Board");

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
        {
            if (hit.transform.name.StartsWith("Point_"))
            {
                string[] parts = hit.transform.name.Split('_');
                return (hit.transform.position, int.Parse(parts[1]), int.Parse(parts[2]));
            }
        }
        return (Vector3.zero, 0, 0);
    }

    public void ResetGame()
    {
        IsPlaying = false;
        IsBlackTurn = true;
        _logic = new OmokuLogic();
        _blackHistory.Clear();
        _whiteHistory.Clear();
        
        if (BoardView != null) BoardView.ClearBoard();
        Debug.Log("게임 리셋 완료");
    }

    public void StartGame()
    {
        if (IsPlaying) return;
        IsPlaying = true;
        StartTurnTimer();
    }
}