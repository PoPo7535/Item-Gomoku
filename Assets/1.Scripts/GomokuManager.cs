using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Utility;


// 얘는 오목 규칙 + 턴 + 네트워크 + 게임 진행 전체 흐름 관리하자
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

    // 이 클라이언트가 조작할 수 있는 돌 색상 (멀티/싱글 구분용 로컬 값)
    private StoneColor _myColor; 
    private bool _isSpawned = false;

    // ---  기록 관리 변수 ---
    private readonly List<string> _blackHistory = new(); 
    private readonly List<string> _whiteHistory = new(); 
    private int _lastX; 
    private int _lastZ; 

    public override void Spawned()
    {
        _isSpawned = true;
        if (BoardView != null) BoardView.Init();
        
        if (Object.HasStateAuthority)
        {
            IsPlaying = false;
            IsBlackTurn = true;
            TickTimer = TickTimer.None;
        }
  

        ResetGame();

        //내가 클릭해서 둘 수 있는 돌 색 호스트는 흑 클라는 백 색지정
        if (App.I.PlayMode == GamePlayMode.Multi)
            _myColor = Object.HasStateAuthority ? StoneColor.Black : StoneColor.White;
        else
            _myColor = StoneColor.Black;
    }

    private void Update()
    {   

        // 게임시작과 Spawned이거실행안댔으면 리턴
        if (!_isSpawned || !IsPlaying) return;


        UpdateTurnTimer();

        // 보드뷰에서 마우스좌표 + 칸좌표 가져옴
        var result = BoardView.GetBoardPosition();

        // 둘수있는자리면 true 아니면 fales
        bool canPlace = result.pos != Vector3.zero && _logic.Board[result.x, result.z].Color == StoneColor.None;

        // 자기턴 돌 색상지정
        StoneColor currentTurn = IsBlackTurn ? StoneColor.Black : StoneColor.White;

        // 멀티플레이에서는 자신의 턴이 아닐 경우 입력 및 착수 시도 차단
        if (App.I.PlayMode == GamePlayMode.Multi && currentTurn != _myColor) canPlace = false;

        //돌 미리보기 canPlace = fales면안보임
        BoardView.UpdateGhostStone(result.pos, canPlace, IsBlackTurn);

        if (Input.GetMouseButtonDown(0) && App.I.PlayMode == GamePlayMode.Single)
            PlaceStoneProcess(result.pos, result.x, result.z, IsBlackTurn);
        
        if (Input.GetMouseButtonDown(0) && App.I.PlayMode == GamePlayMode.Multi)
        {   
            //실제로 여기서차단함
            if (currentTurn == _myColor)
                Rpc_RequestPlaceStone(result.pos, result.x, result.z, IsBlackTurn);
        }

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
            UpdateAndShowLastPlace(x, z, isBlackStone);
            string posText = $"{x},{z}";
            if (isBlackStone) _blackHistory.Add(posText);
            else _whiteHistory.Add(posText);

            if (_logic.CheckWin(x, z, color))
            {
                Debug.Log($"<color=cyan>★ 승리! {color} ★</color>");
                
                ResetGame();
                return;
            }
            ChangeTurn();
        }
    }

    /// <summary>
    /// 최근 기록 보기
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
    /// 게임 초기화
    /// </summary>
    public void ResetGame()
    {
        if (Object.HasStateAuthority)
        {
            IsPlaying = false;
            TickTimer = TickTimer.None;
        }

        IsBlackTurn = true;
        _logic = new OmokuLogic();
        _blackHistory.Clear();
        _whiteHistory.Clear();
        _lastX = 0; _lastZ = 0;
        if (BoardView != null) BoardView.ClearBoard();
        BoardView.UpdateGhostStone(Vector3.zero, false, false);
        Debug.Log("게임 리셋 및 기록 초기화 완료");
    }
    /// <summary>
    /// 게임 시작 UI 버튼용
    /// </summary>
    public void StartGame()
    {   
        if (App.I.PlayMode == GamePlayMode.Multi && !Object.HasStateAuthority) return;
        IsPlaying = true;
        StartTurnTimer();
    }
    /// <summary>
    /// 턴변경
    /// </summary>
    public void ChangeTurn() 
    { 
        IsBlackTurn = !IsBlackTurn; 
        StartTurnTimer(); 
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
}