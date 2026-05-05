using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Utility;
using Cysharp.Threading.Tasks;

// 얘는 오목 규칙 + 턴 + 네트워크 + 게임 진행 전체 흐름 관리하자
public partial class GomokuManager : LocalFusionSingleton<GomokuManager>
{
    [Header("참조 설정")]
    public GomokuBoardView BoardView;

    [Header("게임 설정")]
    public float TurnTimeLimit = 30f;

    [Networked, OnChangedRender(nameof(OnTurnEvent))] public NetworkBool IsBlackTurn { get; set; } = true;
    public readonly List<Action<bool>> TurnEvents = new();

    private void OnTurnEvent()
    {
        foreach (var func in TurnEvents)
            func?.Invoke(IsBlackTurn);
    }
    [Networked, OnChangedRender(nameof(OnPlayEvent))] public NetworkBool IsPlaying { get; set; }

    public readonly List<Action<bool>> PlayEvents = new();
    private void OnPlayEvent()
    {
        foreach (var func in PlayEvents)
            func?.Invoke(IsPlaying);
        if (IsPlaying)
            OnTurnEvent();
    }


    [Networked] public TickTimer TickTimer { get; set; }
    
    private OmokuLogic _logic;

    // 이 클라이언트가 조작할 수 있는 돌 색상 (멀티/싱글 구분용 로컬 값)
    private StoneColor _myColor; 
    private bool _isSpawned = false;

    // ---  기록 관리 변수 ---
    private readonly List<string> _blackHistory = new(); 
    private readonly List<string> _whiteHistory = new(); 
    private int _lastX; 
    private int _lastZ;


    ///------ 아이템 관련 변수-------///
    public ItemPanel ItemPanel;
    // 다음 착수 시 마커를 숨길지 여부를 체크하는 플래그
    private bool _shouldHideNextMarker = false;

    public override void Spawned()
    {   
        //얘는 Spawned 실행되기전 Update실행 막기위함
        _isSpawned = true;

        if (BoardView != null) BoardView.Init();//보드판 셋팅
        
        ResetGame();
        //내가 클릭해서 둘 수 있는 돌 색 호스트는 흑 클라는 백 색지정
        if (App.I.PlayMode == GamePlayMode.Multi)
            _myColor = Object.HasStateAuthority ? StoneColor.Black : StoneColor.White;
        else
            _myColor = StoneColor.Black;
    }

    private void Update()
    {
        if (!_isSpawned || !IsPlaying) return;

        UpdateTurnTimer();

        var result = BoardView.GetBoardPosition(); // 보드판 좌표 받아오기

        HandleGhost(result); // 돌미리보기
        HandleInput(result); // 각 모드 입력처리
    }
    
    /// <summary>
    /// 네트워크용 착수 요청 
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void Rpc_RequestPlaceStone(Vector3 pos, int x, int z, bool isBlack)
    {
        PlaceStoneProcess(pos, x, z, isBlack);
    }
    /// <summary>
    /// 최종 돌 착수
    /// (로직 적용 → 렌더링 → 기록 저장 → 승리 체크 → 턴 변경)
    /// </summary>
    public void PlaceStoneProcess(Vector3 pos, int x, int z, bool isBlackStone)
    {   
        if (pos == Vector3.zero) return;
        StoneColor color = isBlackStone ? StoneColor.Black : StoneColor.White;

        if (_logic.PlaceStone(x, z, color))
        {
            BoardView.SpawnStone(x, z, isBlackStone, pos); 
            
            // --- 아이템 효과 체크 부분 ---
            if (_shouldHideNextMarker)
            {
                // 이번 한 번만 숨기고 플래그 초기화
                _shouldHideNextMarker = false;
                Debug.Log("아이템 효과로 인해 최근 착수 마커를 표시하지 않습니다.");
            }
            else
            {
                // 평소에는 마커 표시
                BoardView.ShowLastMoveMarkers(x, z); 
            }
            // --------------------------

            NotifyBoardChanged();
            string posText = $"{x},{z}";
            if (isBlackStone) _blackHistory.Add(posText);
            else _whiteHistory.Add(posText);
            
            // 승리 체크 및 턴 변경 로직 (기존 코드 유지)
            if (_logic.CheckWin(x, z, color))
            {
                RPC_GameEnd();
                return;
            }
            ChangeTurn();
        }
    }
    public void SetAIDifficulty(GomokuAIDifficulty difficulty)
    {
        _aiDifficulty = difficulty;

    }
    /// <summary>
    /// 현재 마우스 위치에서 착수 가능 여부를 판단하고 돌 미리보기 표시
    /// </summary>
    private void HandleGhost((Vector3 pos, int x, int z) result)
    {
        bool canPlace = result.pos != Vector3.zero &&
                        _logic.Board[result.x, result.z].Color == StoneColor.None;

        StoneColor currentTurn = IsBlackTurn ? StoneColor.Black : StoneColor.White;

        if (App.I.PlayMode == GamePlayMode.Multi && currentTurn != _myColor)
            canPlace = false;

        if (App.I.PlayMode == GamePlayMode.AI && (!IsPlayerTurn || _isAiThinking))
            canPlace = false;

        bool isForbidden = false; // 금수 구분

        if (canPlace && IsBlackTurn) // 금수일시 
        {
            _logic.Board[result.x, result.z].Color = StoneColor.Black; // 임시로 보드데이터에 돌 두고
            isForbidden = _logic.IsForbidden(result.x, result.z, StoneColor.Black); // 여기서 금수결정
            _logic.Board[result.x, result.z].Color = StoneColor.None; // 원상복귀 
        }

        BoardView?.UpdateGhostStone(result.pos, canPlace, IsBlackTurn, isForbidden);
    }
    /// <summary>
    /// 현재 플레이 모드에 따라 입력 처리 분기 (싱글 / 멀티 / AI)
    /// </summary>
    private void HandleInput((Vector3 pos, int x, int z) result)
    {
        if (!Input.GetMouseButtonDown(0)) return;

        switch (App.I.PlayMode)
        {
            case GamePlayMode.Single:
                HandleSingleInput(result);
                break;

            case GamePlayMode.Multi:
                HandleMultiInput(result);
                break;

            case GamePlayMode.AI:
                HandleAIInput(result);
                break;
        }
    }
    /// <summary>
    /// 싱글입력 처리
    /// </summary>
    private void HandleSingleInput((Vector3 pos, int x, int z) result)
    {   

        PlaceStoneProcess(result.pos, result.x, result.z, IsBlackTurn);
    }
    /// <summary>
    /// 멀티 입력처리
    /// </summary>
    private void HandleMultiInput((Vector3 pos, int x, int z) result)
    {
        StoneColor currentTurn = IsBlackTurn ? StoneColor.Black : StoneColor.White;

        if (currentTurn != _myColor) return;

        if (GomokuItemManager.I.CurrentSelectedItem != null)
        {
            bool used = GomokuItemManager.I.TryUseItem(result.x, result.z); // 아이템 사용

            if (!used)return;
        }

        Rpc_RequestPlaceStone(result.pos, result.x, result.z, IsBlackTurn);
    }
    /// <summary>
    /// AI 입력처리 
    /// </summary>
    private void HandleAIInput((Vector3 pos, int x, int z) result)
    {
        // 플레이어만 입력 근데지금 흑고정이라 선택하게하면 바꿔야함여기
        if (_isAiThinking || !IsPlayerTurn)
        {
            return;
        }

        if (result.pos == Vector3.zero || !CanPlaceStoneSafely(result.x, result.z, PlayerStoneColor))
        {
            return;
        }

        PlaceStoneProcess(result.pos, result.x, result.z, PlayerStoneColor == StoneColor.Black);
    }

    /// <summary>
    /// 최근 기록 보기 // 현재안씀 X 
    /// </summary>
    public void UpdateAndShowLastPlace(int x, int z, bool isBlack)
    {
        _lastX = x; _lastZ = z;
        string lastPlayer = isBlack ? "흑돌" : "백돌";
        string nextPlayer = isBlack ? "백돌" : "흑돌";
        Debug.Log($"<color=orange>[턴 교체]</color> {nextPlayer} 차례 (상대 {lastPlayer}의 마지막 수: {x}, {z})");
    }

    /// <summary>
    /// 전체 기록 보기
    /// </summary>
    public void ShowFullLog()
    {
        Debug.Log("흑돌 기보: " + string.Join(" -> ", _blackHistory));
        Debug.Log("백돌 기보: " + string.Join(" -> ", _whiteHistory));
    }
    /// <summary>
    /// 특정 좌표 돌 삭제
    /// </summary>
    public void RemoveStoneProcess(int x, int z)
    {
        // 1. 로직 제거
        _logic.Board[x, z].Color = StoneColor.None;
        NotifyBoardChanged();

        // 2. 뷰 제거
        BoardView.RemoveStone(x, z);
    }
    /// <summary>
    /// [네트워크용] 게임 초기화
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_GameEnd()
    {
        ResetGame();
    }
    /// <summary>
    /// 게임 초기화
    /// </summary>
    public void ResetGame()
    {   
        //호스트만 초기화 
        CancelAiSearchRequest();
        if (Object.HasStateAuthority)
        {
            IsPlaying = false;
            TickTimer = TickTimer.None;
        }
       
        IsBlackTurn = true;
        _logic = new OmokuLogic();
        ResetAiBoardState();
        _blackHistory.Clear();
        _whiteHistory.Clear();
        _lastX = 0; _lastZ = 0;
        if (BoardView != null) BoardView.ClearBoard();
        
        BoardView?.UpdateGhostStone(Vector3.zero, false, false,false);
        if (BoardView.RealLastMoveMarker != null)
        BoardView.RealLastMoveMarker.SetActive(false);

        if (BoardView.FakeLastMoveMarker != null)
        BoardView.FakeLastMoveMarker.SetActive(false);
        GomokuItemManager.I.ResetSelection();
        Debug.Log("게임 리셋 및 기록 초기화 완료");
    }
    /// <summary>
    /// 게임 시작 UI 버튼용
    /// </summary>
    public void StartGame()
    {   
        if(IsPlaying) return;
        if (App.I.PlayMode == GamePlayMode.Multi && !Object.HasStateAuthority) return;
        IsPlaying = true;
        StartTurnTimer();
        TryScheduleAiTurnIfNeeded();
        GomokuItemManager.I.ResetSelection();
    }
    /// <summary>
    /// 게임 재시작 UI 버튼용
    /// </summary>
    public void RestartGame()
    {
        if (App.I.PlayMode == GamePlayMode.Multi && !Object.HasStateAuthority) return;
        ResetGame(); 
        IsPlaying = true;
        StartTurnTimer();
        TryScheduleAiTurnIfNeeded();
        GomokuItemManager.I.ResetSelection();
    }
    /// <summary>
    /// 턴변경
    /// </summary>
    public void ChangeTurn() 
    { 
        IsBlackTurn = !IsBlackTurn;
        GomokuItemManager.I.ResetSelection();
        ItemPanel.ClearAllToggles();
        StartTurnTimer();
        ProcessAiTurn();
        
    }
    /// <summary>
    /// 턴이 변경된 이후 AI 착수 
    /// </summary>
    private async void ProcessAiTurn()
    {
        if (App.I.PlayMode != GamePlayMode.AI)
            return;

        if (IsBlackTurn == false)
            return;

        OfflineUIManager.I.ToggleAiMsg();
        await UniTask.Delay(TimeSpan.FromSeconds(1.5f));
        OfflineUIManager.I.ToggleAiMsg();

        TryScheduleAiTurnIfNeeded();
    }
    /// <summary>
    /// 타이머 시작
    /// </summary>
    private void StartTurnTimer() 
    {   //CreateFromSeconds 시간생성 TurnTimeLimit 이거만큼
        if (Object.HasStateAuthority)TickTimer = TickTimer.CreateFromSeconds(App.I.Runner, TurnTimeLimit); 
    }
    /// <summary>
    /// 타이머 종료
    /// </summary>
    private void UpdateTurnTimer() 
    {   //ExpiredOrNotRunning 이거 시간이 다댔는지 확인함 다되면 true
        if (Object.HasStateAuthority && TickTimer.ExpiredOrNotRunning(App.I.Runner))ChangeTurn(); 
    }

    ///--------------------------------------아이템 관련함수------------------------------------------
    /// <summary>
    /// 아이템 매니저에서 쓸 자기턴확인용
    /// </summary>
    public bool IsMyTurn()
    {
        // 게임 안하는 중이면 당연히 false
        if (!IsPlaying) return false;

        // 현재 턴 색
        StoneColor currentTurn = IsBlackTurn ? StoneColor.Black : StoneColor.White;

        // 멀티
        if (App.I.PlayMode == GamePlayMode.Multi)
        {
            return currentTurn == _myColor;
        }

        return false;
    }
    /// <summary>
    /// 착수 숨김 아이템 사용을 모든 클라이언트에 알림
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All,HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_UseHideMoveItem()
    {
        _shouldHideNextMarker = true;
        Debug.Log("<color=yellow>[아이템 발동] 다음 돌은 위치 표시가 숨겨집니다!</color>");
    }
}
