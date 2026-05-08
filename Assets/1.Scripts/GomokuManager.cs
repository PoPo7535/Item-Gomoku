using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Utility;
using Cysharp.Threading.Tasks;
using UnityEngine.EventSystems;

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
    public StoneColor MyColor => _myColor;
    public StoneColor hostColor;
    private bool _isSpawned = false;

    // ---  기록 관리 변수 ---
    private readonly List<string> _blackHistory = new(); 
    private readonly List<string> _whiteHistory = new(); 
    private int _lastX; 
    private int _lastZ;


    ///------------------ 아이템 관련 변수---------------///
    public ItemPanel ItemPanel;

    // 착수 숨김 효과 활성 여부
    private bool _shouldHideNextMarker = false;
    // 타이머 절반 효과 활성 여부
    [Networked] public NetworkBool IsTimerHalfEffect { get; set; }
    // 더블 표시 효과 활성 여부
    [Networked] public NetworkBool IsDoubleMarkerEffect { get; set; }
    [Networked] public int NetFakeX { get; set; } = -1;
    [Networked] public int NetFakeZ { get; set; } = -1;
    [Networked] public int CurrentFakeX { get; set; } = -1; // 가짜 마커 위치 저장용
    [Networked] public int CurrentFakeZ { get; set; } = -1; // 가짜 마커 위치 저장용
    // 돌바꾸기 효과 활성 여부 
    [Networked] public NetworkBool IsStoneSwapped { get; set; } 
    [Networked] public NetworkBool SwapUsedByBlack { get; set; } // 아이템 쓴사람이 누구인지 확인 // 흑인지 백인지 

    public override void Spawned()
    {   
        //얘는 Spawned 실행되기전 Update실행 막기위함
        _isSpawned = true;

        if (BoardView != null) BoardView.Init();//보드판 셋팅
        
        ResetGame();
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
    /// 플레이 모드에 따라 로컬 플레이어가 조작할 돌 색상을 설정
    /// </summary>
    private void SetupPlayerColor()
    {
        hostColor = App.I.PlayMode == GamePlayMode.AI ? StoneColor.White : StoneColor.Black;
        if (App.I.PlayMode == GamePlayMode.Multi)
        {
            _myColor = Object.HasStateAuthority ? StoneColor.Black : StoneColor.White;
        }
        else if (App.I.PlayMode == GamePlayMode.AI)
        {
            _myColor = StoneColor.White;
        }
        else
        {
            _myColor = StoneColor.Black;
        }
    }
    
    /// <summary>
    /// 네트워크용 착수 요청 
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void Rpc_RequestPlaceStone(Vector3 pos, int x, int z, bool isBlack, int fX = -1, int fZ = -1)
    {
       
        PlaceStoneProcess(pos, x, z, isBlack, fX, fZ);
    }
    /// <summary>
    /// 최종 돌 착수
    /// (로직 적용 → 렌더링 → 기록 저장 → 승리 체크 → 턴 변경)
    /// </summary>
    public void PlaceStoneProcess(Vector3 pos, int x, int z, bool isBlackStone, int fX = -1, int fZ = -1)
    {   
        if (pos == Vector3.zero) return;
        StoneColor color = isBlackStone ? StoneColor.Black : StoneColor.White;

        if (_logic.PlaceStone(x, z, color))
        {   
            GomokuItemManager.I.ConsumeItemUI(); //아이템 ui 삭제 아직 껍데기임
            BoardView.SpawnStone(x, z, isBlackStone, pos); 
            
            if (_shouldHideNextMarker) 
            {
                _shouldHideNextMarker = false;
            }
            else
            {
                // fX, fZ가 -1이 아니라면 가짜 마커도 같이 뜸
                int? fakeX = (fX != -1) ? fX : (int?)null;
                int? fakeZ = (fZ != -1) ? fZ : (int?)null;
                if (Object.HasStateAuthority) // 권한 확인 추가
                {
                    CurrentFakeX = fX; 
                    CurrentFakeZ = fZ;
                }
                BoardView.ShowLastMoveMarkers(x, z, fakeX, fakeZ);
            }

            NotifyBoardChanged();
            if (isBlackStone) _blackHistory.Add($"{x},{z}");
            else _whiteHistory.Add($"{x},{z}");
            
            if (_logic.CheckWin(x, z, color)) { RPC_GameEnd(); return; }
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
        if (EventSystem.current.IsPointerOverGameObject())
        return;

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

        // 아이템 선택 중일 때 처리
        if (GomokuItemManager.I.CurrentSelectedItem != null)
        {
            bool used = GomokuItemManager.I.TryUseItem(result.x, result.z);
            if (!used) return;
        }

        // --- 더블 표시 아이템 로직 추가 ---
        int fakeX = -1;
        int fakeZ = -1;

        if (IsDoubleMarkerEffect)
        {
            // 내 돌 중 하나를 랜덤하게 골라 가짜 마커 좌표로 설정
            var randomStone = GetRandomExistStone(_myColor);
            fakeX = randomStone.x;
            fakeZ = randomStone.z;
        }

        // 서버에 착수 요청 (가짜 좌표 포함)
        Rpc_RequestPlaceStone(result.pos, result.x, result.z, IsBlackTurn, fakeX, fakeZ);
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
        SetupPlayerColor();
        Debug.Log("게임 리셋 및 기록 초기화 완료");
    }
    /// <summary>
    /// 게임 시작 UI 버튼용
    /// </summary>
    public void StartGame()
    {   
        if(IsPlaying) return;
        if (App.I.PlayMode == GamePlayMode.Multi && !Object.HasStateAuthority) return;
        SetupPlayerColor();
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
        SetupPlayerColor();
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
        //공통 처리 
        IsBlackTurn = !IsBlackTurn; // 저기턴변경 false 가 백
        GomokuItemManager.I.ResetSelection(); //아이템 선택되어있는거 싹다 꺼두기
        ItemPanel.ClearAllToggles(); // 아이템패널 토글싹다 꺼두기 

        // 호스트가 처리함
        if (Object.HasStateAuthority)
        {
            // 더블 표시 및 가짜 마커 초기화
            IsDoubleMarkerEffect = false;
            NetFakeX = -1;
            NetFakeZ = -1;

            // 돌 바꾸기 아이템 효과가 활성 상태인지 확인
            if (IsStoneSwapped)
            {
                // 한 턴이 지나서, 다시 내 턴이 오면 다시원상복구
                if (IsBlackTurn == SwapUsedByBlack) 
                {
                    IsStoneSwapped = false; //효과초기화
                    // 호스트가 RPC를 쏴서 모든 사람의 화면을 원상복구시킴
                    RPC_ApplyStoneSwap(false); 
                    Debug.Log("<color=green>돌 바꾸기 효과 종료 (호스트 명령)</color>");
                }
            }
            // ----------------------------------------------

            StartTurnTimer(); // 타이머도 호스트만 관리
        }

        // 3. AI 차례 확인 (모든 플레이어 공통 체크)
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

        OfflineUIManager.I.ToggleAiMsg(); // 토글 메세지용 on
        await UniTask.Delay(TimeSpan.FromSeconds(1.5f)); // 1.5초후 시작
        OfflineUIManager.I.ToggleAiMsg(); // off

        TryScheduleAiTurnIfNeeded();
    }
    /// <summary>
    /// 타이머 시작 로직 수정
    /// </summary>
    private void StartTurnTimer() 
    {
        if (!Object.HasStateAuthority) return;

        float currentLimit = TurnTimeLimit;

        // 만약 타이머 감소 효과가 켜져 있다면
        if (IsTimerHalfEffect)
        {
            currentLimit = TurnTimeLimit / 2f; // 시간 절반
            IsTimerHalfEffect = false; // 일회성이므로 사용 후 즉시 해제
            Debug.Log($"상대방 턴 제한 시간 단축 적용: {currentLimit}초");
        }

        TickTimer = TickTimer.CreateFromSeconds(App.I.Runner, currentLimit); 
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
    /// 착수 숨김 사용 RPC
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All,HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_UseHideMoveItem()
    {
        _shouldHideNextMarker = true;
        Debug.Log("<color=yellow>[아이템 발동] 다음 돌은 위치 표시가 숨겨집니다!</color>");
    }
    /// <summary>
    /// 타이머 감소 아이템 사용 RPC
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_UseTimerReductionItem()
    {
        IsTimerHalfEffect = true;
        Debug.Log("<color=red>[아이템 발동] 다음 상대의 턴 시간이 절반으로 줄어듭니다!</color>");
    }
    /// <summary>
    /// 더블 표시 아이템 사용 RPC
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    public void RPC_UseDoubleMarkerItem()
    {
        IsDoubleMarkerEffect = true;
    }
    /// <summary>
    /// 판 위에 놓인 특정 색상의 돌 중 하나를 랜덤하게 좌표반환
    /// </summary>
    private (int x, int z) GetRandomExistStone(StoneColor color)
    {
        List<(int x, int z)> stones = new List<(int x, int z)>();

        for (int i = 0; i < 15; i++)
        {
            for (int j = 0; j < 15; j++)
            {
                if (_logic.Board[i, j].Color == color)
                {
                    stones.Add((i, j));
                }
            }
        }

        if (stones.Count == 0) return (-1, -1); // 돌이 없으면 -1 반환

        int randomIndex = UnityEngine.Random.Range(0, stones.Count);
        return stones[randomIndex];
    }
    /// <summary>
    /// 간파하기에 사용할 클릭한 좌표에 가짜마커 일시 마커 비활성화
    /// </summary>
    public bool CheckAndDestroyFakeMarker(int x, int z)
    {
        // 클릭한 좌표가 가짜 마커 좌표와 일치하는지 확인
        if (x == CurrentFakeX && z == CurrentFakeZ)
        {
            // 모든 클라이언트에서 가짜 마커를 끄도록 RPC 호출
            RPC_DestroyFakeMarker();
            return true;
        }
        return false;
    }
    /// <summary>
    /// 모든 클라이언트에서 가짜 마커를 시각적으로 비활성화하고 서버의 가짜 좌표 데이터를 초기화
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_DestroyFakeMarker()
    {
        BoardView.FakeLastMoveMarker.SetActive(false);
        CurrentFakeX = -1;
        CurrentFakeZ = -1;
        Debug.Log("<color=cyan>[아이템 발동] 가짜 마커가 간파되어 사라졌습니다!</color>");
    }




    /// <summary>
    /// 돌 바꾸기 RPC 요청
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_UseStoneSwapItem()
    {
        IsStoneSwapped = true; 
        SwapUsedByBlack = IsBlackTurn; //아이템을 쓴 순간의 턴을 기억함
        
        RPC_ApplyStoneSwap(true); 
        Debug.Log("돌 바꾸기 아이템 사용됨 (서버)");
    }
    /// <summary>
    /// 실제 보드판에 있는 돌 색상 반전 
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyStoneSwap(bool targetState) 
    {
        BoardView.SwapAllStonesVisual(targetState);
    }
    /// <summary>
    /// 특정 보드 좌표(x, z)에 놓인 돌의 실제 데이터 색상 가져오기 보드뷰에서 쓸거임
    /// </summary>
    public StoneColor GetStoneColorAt(int x, int z) => _logic.Board[x, z].Color;
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestMakeTransparent(int x, int z, bool isBlack)
    {
        // 서버가 모든 클라이언트에게 비주얼 교체 명령
        RPC_ApplyTransparent(x, z, isBlack);
        
    }
    // 2. 실제 비주얼 교체 실행 RPC
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyTransparent(int x, int z, bool isBlack)
    {
        BoardView.ConvertToTransparent(x, z, isBlack);
        Debug.Log($"<color=cyan>[아이템] ({x},{z}) 좌표의 돌이 투명해졌습니다.</color>");
    }
}
