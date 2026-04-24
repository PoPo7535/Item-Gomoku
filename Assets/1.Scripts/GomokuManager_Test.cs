using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TestScene 전용 오목 흐름을 관리함.
/// </summary>
public sealed class GomokuManager_Test : MonoBehaviour
{
    private const int BoardSize = 15;

    private enum GhostPreviewState
    {
        Hidden,
        Valid,
        Forbidden,
    }

    private enum AiDifficulty
    {
        Easy = 1,
        Normal = 3,
        Hard = 5
    }

    private struct MoveRecord
    {
        public int XIndex;
        public int YIndex;
        public StoneColor StoneColor;
        public GameObject StoneObject;
        public bool WasBlackTurnBeforeMove;
        public bool WasGameOverBeforeMove;
        public int BlackStoneCountBeforeMove;
        public int WhiteStoneCountBeforeMove;
    }

    private struct TurnRecord
    {
        public MoveRecord PlayerMove;
        public bool HasAiMove;
        public MoveRecord AiMove;
    }

    [Header("프리팹 설정")]
    [SerializeField] private GameObject _blackStonePrefab;
    [SerializeField] private GameObject _whiteStonePrefab;
    [SerializeField] private float _stoneScale = 30f;

    [Header("고스트 프리팹 설정")]
    [SerializeField] private GameObject _blackGhostPrefab;
    [SerializeField] private GameObject _whiteGhostPrefab;

    [Header("보드 설정")]
    [SerializeField] private Transform _boardRoot;
    [SerializeField] private Camera _clickCamera;
    [SerializeField] private float _stoneHeight = 0.1f;
    [SerializeField] private float _ghostHeight = 0.15f;
    [SerializeField] private float _cellClickRadiusRatio = 0.5f;

    [Header("돌 정리 설정")]
    [SerializeField] private Transform _stoneRoot;
    [SerializeField] private Transform _blackStoneParent;
    [SerializeField] private Transform _whiteStoneParent;
    [SerializeField] private Transform _ghostParent;

    [Header("무르기 설정")]
    [SerializeField] private int _maxUndoCount;

    [Header("AI 설정")]
    [SerializeField] private AiDifficulty _aiDifficulty = AiDifficulty.Normal;

    private OmokuLogic _logic;
    private GomokuAI _ai;
    private GameObject[,] _stoneObjects;
    private Transform[,] _cellAnchors;
    private bool _isBlackTurn = true;
    private bool _isAiThinking;
    private bool _isGameOver;
    private bool _isReady;
    private int _blackStoneCount;
    private int _whiteStoneCount;
    private float _cellSpacing;
    private float _cellClickRadius;
    private GameObject _ghostStoneObject;
    private StoneColor _ghostStoneColor = StoneColor.None;
    private Stack<TurnRecord> _turnHistory;
    private int _remainingUndoCount;

    /// <summary>
    /// 테스트 게임 상태를 초기화함.
    /// </summary>
    private void Awake()
    {
        InitializeGame();
    }

    /// <summary>
    /// 좌클릭 입력을 감지해 착수를 시도함.
    /// </summary>
    private void Update()
    {
        if (!_isReady)
        {
            HideGhostPreview();
            return;
        }

        if (TryHandleUndoInput())
        {
            return;
        }

        if (_isGameOver || _isAiThinking || !_isBlackTurn)
        {
            HideGhostPreview();
            return;
        }

        UpdateGhostPreview();

        if (Input.GetMouseButtonDown(0))
        {
            TryHandlePlacementInput();
        }
    }

    /// <summary>
    /// 오목 로직과 시각 오브젝트 저장소를 준비함.
    /// </summary>
    private void InitializeGame()
    {
        _logic = new OmokuLogic();
        _ai = new GomokuAI(_logic, BoardSize);
        _stoneObjects = new GameObject[BoardSize, BoardSize];
        _cellAnchors = new Transform[BoardSize, BoardSize];
        _isBlackTurn = true;
        _isAiThinking = false;
        _isGameOver = false;
        _blackStoneCount = 0;
        _whiteStoneCount = 0;
        _cellSpacing = 0f;
        _cellClickRadius = 0f;
        _ghostStoneColor = StoneColor.None;
        _turnHistory = new Stack<TurnRecord>();
        _remainingUndoCount = _maxUndoCount > 0 ? _maxUndoCount : 0;
        _isReady = TryCacheCellAnchors() && TryValidateCellLayout() && ValidateStoneHierarchy();
    }

    /// <summary>
    /// 현재 클릭 위치를 보드 좌표로 해석해 착수를 처리함.
    /// </summary>
    private void TryHandlePlacementInput()
    {
        if (_isAiThinking || !_isBlackTurn)
        {
            return;
        }

        if (!TryGetBoardIndex(out int xIndex, out int yIndex))
        {
            return;
        }

        if (!TryPlaceStone(xIndex, yIndex, StoneColor.Black, out MoveRecord playerMoveRecord))
        {
            return;
        }

        TurnRecord turnRecord = new TurnRecord
        {
            PlayerMove = playerMoveRecord,
            HasAiMove = false,
        };

        if (_isGameOver)
        {
            _turnHistory.Push(turnRecord);
            HideGhostPreview();
            return;
        }

        _isBlackTurn = false;
        StartCoroutine(HandleAiTurnCoroutine(turnRecord));
    }

    /// <summary>
    /// 무르기 단축키 입력을 감지해 최근 턴 복구를 시도함.
    /// </summary>
    /// <returns>이번 프레임에 무르기 입력을 처리했는지 여부.</returns>
    private bool TryHandleUndoInput()
    {
        if (!Input.GetKeyDown(KeyCode.Space) || _isAiThinking)
        {
            return false;
        }

        return TryUndoLastTurn();
    }

    /// <summary>
    /// 보드 하위 셀 앵커를 읽어 15x15 캐시를 구성함.
    /// </summary>
    /// <returns>앵커 캐시 준비 성공 여부.</returns>
    private bool TryCacheCellAnchors()
    {
        if (_boardRoot == null)
        {
            Debug.LogError("BoardRoot가 연결되지 않아 셀 앵커를 캐싱할 수 없습니다.");
            return false;
        }

        if (_boardRoot.GetComponent<Collider>() == null)
        {
            Debug.LogError("BoardRoot에 Collider가 없어 클릭을 처리할 수 없습니다.");
            return false;
        }

        if (GetClickCamera() == null)
        {
            Debug.LogError("클릭에 사용할 카메라를 찾을 수 없습니다.");
            return false;
        }

        Transform[] childTransforms = _boardRoot.GetComponentsInChildren<Transform>(true);
        int cachedAnchorCount = 0;

        foreach (Transform childTransform in childTransforms)
        {
            if (childTransform == _boardRoot)
            {
                continue;
            }

            // 이름 규칙이 맞는 앵커만 유효한 셀로 취급함.
            if (!TryParseCellAnchorName(childTransform.name, out int xIndex, out int yIndex))
            {
                continue;
            }

            if (_cellAnchors[xIndex, yIndex] != null)
            {
                Debug.LogError($"중복된 셀 앵커가 있습니다: Cell_{xIndex}_{yIndex}");
                return false;
            }

            _cellAnchors[xIndex, yIndex] = childTransform;
            cachedAnchorCount++;
        }

        if (cachedAnchorCount != BoardSize * BoardSize)
        {
            LogMissingCellAnchors();
            Debug.LogError($"셀 앵커 개수가 부족합니다. 현재 {cachedAnchorCount}개 / 필요 {BoardSize * BoardSize}개");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 셀 앵커 이름에서 보드 인덱스를 파싱함.
    /// </summary>
    /// <param name="anchorName">파싱할 앵커 이름.</param>
    /// <param name="xIndex">파싱된 X 인덱스.</param>
    /// <param name="yIndex">파싱된 Y 인덱스.</param>
    /// <returns>이름 파싱 성공 여부.</returns>
    private static bool TryParseCellAnchorName(string anchorName, out int xIndex, out int yIndex)
    {
        xIndex = 0;
        yIndex = 0;

        if (string.IsNullOrWhiteSpace(anchorName))
        {
            return false;
        }

        string[] nameParts = anchorName.Split('_');
        if (nameParts.Length != 3)
        {
            return false;
        }

        if (!string.Equals(nameParts[0], "Cell", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(nameParts[1], out xIndex) || !int.TryParse(nameParts[2], out yIndex))
        {
            return false;
        }

        return xIndex >= 0 && xIndex < BoardSize && yIndex >= 0 && yIndex < BoardSize;
    }

    /// <summary>
    /// 누락된 셀 앵커 이름을 로그로 출력함.
    /// </summary>
    private void LogMissingCellAnchors()
    {
        List<string> missingAnchorNames = new List<string>();
        const int previewLimit = 20;

        for (int xIndex = 0; xIndex < BoardSize; xIndex++)
        {
            for (int yIndex = 0; yIndex < BoardSize; yIndex++)
            {
                if (_cellAnchors[xIndex, yIndex] == null)
                {
                    missingAnchorNames.Add($"Cell_{xIndex}_{yIndex}");
                }
            }
        }

        if (missingAnchorNames.Count > 0)
        {
            int previewCount = Mathf.Min(previewLimit, missingAnchorNames.Count);
            List<string> previewAnchorNames = missingAnchorNames.GetRange(0, previewCount);
            string suffix = missingAnchorNames.Count > previewCount ? " ..." : string.Empty;
            Debug.LogError($"누락된 셀 앵커 일부: {string.Join(", ", previewAnchorNames)}{suffix}");
        }
    }

    /// <summary>
    /// 마우스 클릭 위치를 가장 가까운 셀 앵커 인덱스로 변환함.
    /// </summary>
    /// <param name="xIndex">변환된 X 인덱스.</param>
    /// <param name="yIndex">변환된 Y 인덱스.</param>
    /// <returns>보드 클릭 해석 성공 여부.</returns>
    private bool TryGetBoardIndex(out int xIndex, out int yIndex)
    {
        xIndex = 0;
        yIndex = 0;

        if (!TryGetBoardHitPoint(out Vector3 boardHitPoint))
        {
            return false;
        }

        return TryFindClosestCellAnchor(boardHitPoint, out xIndex, out yIndex);
    }

    /// <summary>
    /// 보드 Collider 기준 월드 히트 지점을 구함.
    /// </summary>
    /// <param name="boardHitPoint">Raycast 성공 시 보드 월드 좌표.</param>
    /// <returns>보드 클릭 성공 여부.</returns>
    private bool TryGetBoardHitPoint(out Vector3 boardHitPoint)
    {
        boardHitPoint = Vector3.zero;

        if (_boardRoot == null)
        {
            Debug.LogWarning("BoardRoot가 연결되지 않아 클릭 좌표를 해석할 수 없습니다.");
            return false;
        }

        Collider boardCollider = _boardRoot.GetComponent<Collider>();
        if (boardCollider == null)
        {
            Debug.LogWarning("BoardRoot에 Collider가 없어 클릭을 처리할 수 없습니다.");
            return false;
        }

        Camera clickCamera = GetClickCamera();
        if (clickCamera == null)
        {
            Debug.LogWarning("클릭에 사용할 카메라를 찾을 수 없습니다.");
            return false;
        }

        Ray clickRay = clickCamera.ScreenPointToRay(Input.mousePosition);
        if (!boardCollider.Raycast(clickRay, out RaycastHit hit, float.MaxValue))
        {
            return false;
        }

        boardHitPoint = hit.point;
        return true;
    }

    /// <summary>
    /// 월드 좌표와 가장 가까운 셀 앵커를 찾아 보드 인덱스를 반환함.
    /// </summary>
    /// <param name="worldPoint">비교할 월드 좌표.</param>
    /// <param name="xIndex">가장 가까운 X 인덱스.</param>
    /// <param name="yIndex">가장 가까운 Y 인덱스.</param>
    /// <returns>가장 가까운 셀 탐색 성공 여부.</returns>
    private bool TryFindClosestCellAnchor(Vector3 worldPoint, out int xIndex, out int yIndex)
    {
        xIndex = -1;
        yIndex = -1;
        float closestDistanceSqr = float.MaxValue;

        for (int candidateX = 0; candidateX < BoardSize; candidateX++)
        {
            for (int candidateY = 0; candidateY < BoardSize; candidateY++)
            {
                Transform cellAnchor = _cellAnchors[candidateX, candidateY];
                if (cellAnchor == null)
                {
                    continue;
                }

                // 클릭 지점과 가장 가까운 교차점을 실제 착수 위치로 사용함.
                float distanceSqr = (cellAnchor.position - worldPoint).sqrMagnitude;
                if (distanceSqr >= closestDistanceSqr)
                {
                    continue;
                }

                closestDistanceSqr = distanceSqr;
                xIndex = candidateX;
                yIndex = candidateY;
            }
        }

        if (xIndex < 0 || yIndex < 0)
        {
            Debug.LogWarning("클릭 위치와 매칭되는 셀 앵커를 찾지 못했습니다.");
            return false;
        }

        // 셀 반경보다 먼 클릭은 가장자리 셀로 흡수하지 않음.
        float closestPlanarDistance = GetPlanarDistance(_cellAnchors[xIndex, yIndex].position, worldPoint);
        if (closestPlanarDistance > _cellClickRadius)
        {
            // Debug.Log($"클릭이 유효 셀 반경을 벗어나 착수를 무시함. Distance={closestPlanarDistance:0.###}, Radius={_cellClickRadius:0.###}");
            xIndex = -1;
            yIndex = -1;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 지정된 색상 기준으로 착수와 승패 판정을 진행함.
    /// </summary>
    /// <param name="xIndex">착수할 X 인덱스.</param>
    /// <param name="yIndex">착수할 Y 인덱스.</param>
    /// <param name="stoneColor">착수할 돌 색상.</param>
    /// <param name="moveRecord">성공 시 저장된 착수 기록.</param>
    /// <returns>착수 성공 여부.</returns>
    private bool TryPlaceStone(int xIndex, int yIndex, StoneColor stoneColor, out MoveRecord moveRecord)
    {
        GameObject stonePrefab = GetStonePrefab(stoneColor);
        moveRecord = CreateMoveRecord(xIndex, yIndex, stoneColor);

        if (stonePrefab == null)
        {
            Debug.LogWarning($"{stoneColor} 돌 프리팹이 연결되지 않았습니다.");
            return false;
        }

        if (!_logic.PlaceStone(xIndex, yIndex, stoneColor))
        {
            return false;
        }

        moveRecord.StoneObject = SpawnStone(stonePrefab, xIndex, yIndex, stoneColor);
        Debug.Log($"{stoneColor} 착수: ({xIndex}, {yIndex})");

        // 착수 기록 저장 이후에만 승리 여부를 확정함.
        if (_logic.CheckWin(xIndex, yIndex, stoneColor))
        {
            _isGameOver = true;
            HideGhostPreview();
            Debug.Log($"승리: {stoneColor}");
        }

        return true;
    }

    /// <summary>
    /// 보드 인덱스에 해당하는 위치에 돌 프리팹을 생성함.
    /// </summary>
    /// <param name="stonePrefab">생성할 돌 프리팹.</param>
    /// <param name="xIndex">보드 X 인덱스.</param>
    /// <param name="yIndex">보드 Y 인덱스.</param>
    /// <returns>생성된 돌 오브젝트.</returns>
    private GameObject SpawnStone(GameObject stonePrefab, int xIndex, int yIndex, StoneColor stoneColor)
    {
        Transform cellAnchor = _cellAnchors[xIndex, yIndex];
        if (cellAnchor == null)
        {
            Debug.LogError($"셀 앵커를 찾지 못해 돌을 생성할 수 없습니다: ({xIndex}, {yIndex})");
            return null;
        }

        Vector3 worldStonePosition = cellAnchor.position + (_boardRoot.up * _stoneHeight);
        Quaternion worldStoneRotation = _boardRoot.rotation * stonePrefab.transform.rotation;
        Transform stoneParent = GetStoneParent(stoneColor);
        GameObject stoneObject = Instantiate(stonePrefab, worldStonePosition, worldStoneRotation, stoneParent);
        stoneObject.transform.localScale = Vector3.one * _stoneScale;
        AlignStoneToCellCenter(stoneObject.transform, worldStonePosition);
        stoneObject.name = GetNextStoneName(stoneColor);
        _stoneObjects[xIndex, yIndex] = stoneObject;
        return stoneObject;
    }

    /// <summary>
    /// 현재 착수 직전 상태를 무르기 기록으로 생성함.
    /// </summary>
    /// <param name="xIndex">기록할 X 인덱스.</param>
    /// <param name="yIndex">기록할 Y 인덱스.</param>
    /// <param name="stoneColor">현재 착수 색상.</param>
    /// <returns>착수 전 상태가 담긴 무르기 기록.</returns>
    private MoveRecord CreateMoveRecord(int xIndex, int yIndex, StoneColor stoneColor)
    {
        MoveRecord moveRecord = new MoveRecord
        {
            XIndex = xIndex,
            YIndex = yIndex,
            StoneColor = stoneColor,
            WasBlackTurnBeforeMove = _isBlackTurn,
            WasGameOverBeforeMove = _isGameOver,
            BlackStoneCountBeforeMove = _blackStoneCount,
            WhiteStoneCountBeforeMove = _whiteStoneCount,
        };

        return moveRecord;
    }

    /// <summary>
    /// 현재 상태에서 무르기 실행이 가능한지 확인함.
    /// </summary>
    /// <returns>무르기 가능 여부.</returns>
    private bool CanUndo()
    {
        if (_turnHistory == null || _turnHistory.Count == 0)
        {
            return false;
        }

        return _maxUndoCount <= 0 || _remainingUndoCount > 0;
    }

    /// <summary>
    /// 무르기 사용 횟수를 차감함.
    /// </summary>
    private void ConsumeUndoUsage()
    {
        if (_maxUndoCount <= 0)
        {
            return;
        }

        _remainingUndoCount = Mathf.Max(0, _remainingUndoCount - 1);
    }

    /// <summary>
    /// 기록된 착수 전 상태를 실제 게임 상태에 복원함.
    /// </summary>
    /// <param name="moveRecord">복원할 무르기 기록.</param>
    private void RestoreMoveRecord(MoveRecord moveRecord)
    {
        // 착수 직전 상태를 그대로 복원해야 턴/종료 상태가 안정적으로 되돌아감.
        _logic.Board[moveRecord.XIndex, moveRecord.YIndex] = new StoneData
        {
            Color = StoneColor.None,
            IsFake = false,
        };

        if (moveRecord.StoneObject != null)
        {
            Destroy(moveRecord.StoneObject);
        }

        _stoneObjects[moveRecord.XIndex, moveRecord.YIndex] = null;
        _isBlackTurn = moveRecord.WasBlackTurnBeforeMove;
        _isGameOver = moveRecord.WasGameOverBeforeMove;
        _blackStoneCount = moveRecord.BlackStoneCountBeforeMove;
        _whiteStoneCount = moveRecord.WhiteStoneCountBeforeMove;
    }

    /// <summary>
    /// AI 응수를 포함한 최근 턴 전체를 되돌림.
    /// </summary>
    /// <returns>최근 턴 복구 성공 여부.</returns>
    private bool TryUndoLastTurn()
    {
        if (!CanUndo())
        {
            return false;
        }

        TurnRecord turnRecord = _turnHistory.Pop();
        RestoreTurnRecord(turnRecord);
        ConsumeUndoUsage();
        RefreshGhostPreview();
        Debug.Log($"턴 무르기 실행: {FormatTurnUndoLog(turnRecord)}");
        return true;
    }

    /// <summary>
    /// 기록된 턴 전체를 실제 게임 상태에 복원함.
    /// </summary>
    /// <param name="turnRecord">복원할 턴 기록.</param>
    private void RestoreTurnRecord(TurnRecord turnRecord)
    {
        if (turnRecord.HasAiMove)
        {
            RestoreMoveRecord(turnRecord.AiMove);
        }

        RestoreMoveRecord(turnRecord.PlayerMove);
        _isAiThinking = false;
    }

    /// <summary>
    /// 턴 무르기 로그에 표시할 수순 문자열을 구성함.
    /// </summary>
    /// <param name="turnRecord">로그로 출력할 턴 기록.</param>
    /// <returns>로그 출력용 수순 문자열.</returns>
    private static string FormatTurnUndoLog(TurnRecord turnRecord)
    {
        string playerMoveText = $"{turnRecord.PlayerMove.StoneColor} ({turnRecord.PlayerMove.XIndex}, {turnRecord.PlayerMove.YIndex})";
        if (!turnRecord.HasAiMove)
        {
            return playerMoveText;
        }

        string aiMoveText = $"{turnRecord.AiMove.StoneColor} ({turnRecord.AiMove.XIndex}, {turnRecord.AiMove.YIndex})";
        return $"{playerMoveText} -> {aiMoveText}";
    }

    /// <summary>
    /// 플레이어 착수 직후 한 프레임 대기한 뒤 AI 응수를 계산하고 턴 기록을 마감함.
    /// </summary>
    /// <param name="turnRecord">이번 턴에 누적할 턴 기록.</param>
    private IEnumerator HandleAiTurnCoroutine(TurnRecord turnRecord)
    {
        if (_ai == null || _isGameOver)
        {
            _turnHistory.Push(turnRecord);
            RefreshGhostPreview();
            yield break;
        }

        _isAiThinking = true;
        HideGhostPreview();
        yield return null;

        GomokuMove bestMove = _ai.FindBestMove(Mathf.Max(1, (int)_aiDifficulty));
        if (!bestMove.IsValid)
        {
            Debug.LogWarning("AI가 유효한 착수 위치를 찾지 못했습니다.");
            _isAiThinking = false;
            _isBlackTurn = true;
            _turnHistory.Push(turnRecord);
            RefreshGhostPreview();
            yield break;
        }

        if (!TryPlaceStone(bestMove.X, bestMove.Y, StoneColor.White, out MoveRecord aiMoveRecord))
        {
            Debug.LogWarning($"AI 착수에 실패했습니다: ({bestMove.X}, {bestMove.Y})");
            _isAiThinking = false;
            _isBlackTurn = true;
            _turnHistory.Push(turnRecord);
            RefreshGhostPreview();
            yield break;
        }

        turnRecord.HasAiMove = true;
        turnRecord.AiMove = aiMoveRecord;
        _isAiThinking = false;

        if (!_isGameOver)
        {
            _isBlackTurn = true;
        }

        _turnHistory.Push(turnRecord);
        RefreshGhostPreview();
    }

    /// <summary>
    /// 현재 상태에 맞춰 고스트 표시 여부를 다시 계산함.
    /// </summary>
    private void RefreshGhostPreview()
    {
        if (!_isReady || _isGameOver || _isAiThinking || !_isBlackTurn)
        {
            HideGhostPreview();
            return;
        }

        UpdateGhostPreview();
    }

    /// <summary>
    /// 돌 정리용 부모 구조가 유효한지 확인함.
    /// </summary>
    /// <returns>돌 부모 구조 준비 성공 여부.</returns>
    private bool ValidateStoneHierarchy()
    {
        if (_stoneRoot == null)
        {
            Debug.LogError("StoneRoot가 연결되지 않아 돌을 정리할 수 없습니다.");
            return false;
        }

        if (_blackStoneParent == null || _whiteStoneParent == null)
        {
            Debug.LogError("Black 또는 White 부모 오브젝트가 연결되지 않았습니다.");
            return false;
        }

        // 색상별 부모가 StoneRoot 하위인지 확인해야 하이어라키 규칙이 유지됨.
        if (_blackStoneParent.parent != _stoneRoot || _whiteStoneParent.parent != _stoneRoot)
        {
            Debug.LogError("Black/White 부모 오브젝트는 StoneRoot의 직계 자식이어야 합니다.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 마우스 위치 기준으로 고스트 돌 미리보기를 갱신함.
    /// </summary>
    private void UpdateGhostPreview()
    {
        if (!TryResolveGhostPreview(out GhostPreviewState previewState, out int xIndex, out int yIndex, out StoneColor previewColor))
        {
            HideGhostPreview();
            return;
        }

        if (previewState == GhostPreviewState.Hidden)
        {
            HideGhostPreview();
            return;
        }

        if (!TryEnsureGhostObject(previewColor))
        {
            HideGhostPreview();
            return;
        }

        Transform cellAnchor = _cellAnchors[xIndex, yIndex];
        if (cellAnchor == null)
        {
            HideGhostPreview();
            return;
        }

        Vector3 ghostPosition = cellAnchor.position + (_boardRoot.up * _ghostHeight);
        _ghostStoneObject.transform.position = ghostPosition;
        _ghostStoneObject.transform.rotation = _boardRoot.rotation * GetGhostRotation(previewColor);
        _ghostStoneObject.transform.localScale = Vector3.one * _stoneScale;
        AlignStoneToCellCenter(_ghostStoneObject.transform, ghostPosition);

        if (!_ghostStoneObject.activeSelf)
        {
            _ghostStoneObject.SetActive(true);
        }
    }

    /// <summary>
    /// 현재 hover 상태에서 보여줄 고스트 정책을 계산함.
    /// </summary>
    /// <param name="previewState">계산된 고스트 표시 상태.</param>
    /// <param name="xIndex">hover 셀 X 인덱스.</param>
    /// <param name="yIndex">hover 셀 Y 인덱스.</param>
    /// <param name="previewColor">현재 턴 기준 고스트 색상.</param>
    /// <returns>고스트 계산 성공 여부.</returns>
    private bool TryResolveGhostPreview(out GhostPreviewState previewState, out int xIndex, out int yIndex, out StoneColor previewColor)
    {
        previewState = GhostPreviewState.Hidden;
        xIndex = 0;
        yIndex = 0;
        previewColor = _isBlackTurn ? StoneColor.Black : StoneColor.White;

        if (_isAiThinking || _isGameOver || !_isBlackTurn)
        {
            return false;
        }

        if (!TryGetBoardIndex(out xIndex, out yIndex))
        {
            return false;
        }

        if (IsCellOccupied(xIndex, yIndex))
        {
            return true;
        }

        if (IsForbiddenPreview(xIndex, yIndex, previewColor))
        {
            previewState = GhostPreviewState.Forbidden;
            return true;
        }

        previewState = GhostPreviewState.Valid;
        return true;
    }

    /// <summary>
    /// 현재 색상에 맞는 고스트 오브젝트가 준비되어 있는지 확인함.
    /// </summary>
    /// <param name="ghostColor">준비할 고스트 색상.</param>
    /// <returns>고스트 오브젝트 준비 성공 여부.</returns>
    private bool TryEnsureGhostObject(StoneColor ghostColor)
    {
        GameObject ghostPrefab = GetGhostPrefab(ghostColor);
        if (ghostPrefab == null)
        {
            return false;
        }

        if (_ghostStoneObject != null && _ghostStoneColor == ghostColor)
        {
            return true;
        }

        if (_ghostStoneObject != null)
        {
            Destroy(_ghostStoneObject);
            _ghostStoneObject = null;
        }

        Transform ghostParent = GetGhostParent();
        _ghostStoneObject = Instantiate(ghostPrefab, Vector3.zero, Quaternion.identity, ghostParent);
        _ghostStoneObject.name = $"Ghost_{ghostColor}";
        _ghostStoneColor = ghostColor;
        return true;
    }

    /// <summary>
    /// 고스트 오브젝트를 숨김 상태로 전환함.
    /// </summary>
    private void HideGhostPreview()
    {
        if (_ghostStoneObject == null)
        {
            return;
        }

        if (_ghostStoneObject.activeSelf)
        {
            _ghostStoneObject.SetActive(false);
        }
    }

    /// <summary>
    /// 셀 앵커 배치와 클릭 기준 보드가 일치하는지 검증함.
    /// </summary>
    /// <returns>셀 배치 검증 성공 여부.</returns>
    private bool TryValidateCellLayout()
    {
        if (!TryCalculateCellSpacing(out float minCellSpacing))
        {
            Debug.LogError("셀 간격을 계산하지 못해 클릭 검증을 초기화할 수 없습니다.");
            return false;
        }

        _cellSpacing = minCellSpacing;
        _cellClickRadius = _cellSpacing * Mathf.Clamp(_cellClickRadiusRatio, 0.1f, 0.5f);

        ValidateAnchorBoundsAgainstBoard();
        return true;
    }

    /// <summary>
    /// 셀 앵커 배치에서 최소 인접 간격을 계산함.
    /// </summary>
    /// <param name="minCellSpacing">계산된 최소 셀 간격.</param>
    /// <returns>셀 간격 계산 성공 여부.</returns>
    private bool TryCalculateCellSpacing(out float minCellSpacing)
    {
        minCellSpacing = float.MaxValue;

        for (int xIndex = 0; xIndex < BoardSize; xIndex++)
        {
            for (int yIndex = 0; yIndex < BoardSize; yIndex++)
            {
                Transform currentAnchor = _cellAnchors[xIndex, yIndex];
                if (currentAnchor == null)
                {
                    continue;
                }

                if (xIndex + 1 < BoardSize && _cellAnchors[xIndex + 1, yIndex] != null)
                {
                    minCellSpacing = Mathf.Min(minCellSpacing, GetPlanarDistance(currentAnchor.position, _cellAnchors[xIndex + 1, yIndex].position));
                }

                if (yIndex + 1 < BoardSize && _cellAnchors[xIndex, yIndex + 1] != null)
                {
                    minCellSpacing = Mathf.Min(minCellSpacing, GetPlanarDistance(currentAnchor.position, _cellAnchors[xIndex, yIndex + 1].position));
                }
            }
        }

        return minCellSpacing > 0f && !float.IsInfinity(minCellSpacing);
    }

    /// <summary>
    /// 셀 앵커 범위와 보드 Collider 범위를 비교해 정합성을 확인함.
    /// </summary>
    private void ValidateAnchorBoundsAgainstBoard()
    {
        if (_boardRoot == null)
        {
            return;
        }

        BoxCollider boardCollider = _boardRoot.GetComponent<BoxCollider>();
        if (boardCollider == null)
        {
            Debug.LogWarning("BoardRoot가 BoxCollider가 아니어서 셀 배치 정합성 검증이 제한됩니다.");
            return;
        }

        if (!TryGetAnchorLocalBounds(out Vector2 minAnchorBounds, out Vector2 maxAnchorBounds))
        {
            Debug.LogWarning("셀 앵커 로컬 범위를 계산하지 못했습니다.");
            return;
        }

        float colliderMinX = boardCollider.center.x - (boardCollider.size.x * 0.5f);
        float colliderMaxX = boardCollider.center.x + (boardCollider.size.x * 0.5f);
        float colliderMinZ = boardCollider.center.z - (boardCollider.size.z * 0.5f);
        float colliderMaxZ = boardCollider.center.z + (boardCollider.size.z * 0.5f);
        float warningTolerance = _cellSpacing * 0.25f;

        bool isMisaligned =
            Mathf.Abs(minAnchorBounds.x - colliderMinX) > warningTolerance ||
            Mathf.Abs(maxAnchorBounds.x - colliderMaxX) > warningTolerance ||
            Mathf.Abs(minAnchorBounds.y - colliderMinZ) > warningTolerance ||
            Mathf.Abs(maxAnchorBounds.y - colliderMaxZ) > warningTolerance;

        if (isMisaligned)
        {
            Debug.LogWarning("BoardRoot Collider 범위와 Cell 앵커 범위가 어긋나 있습니다. 외곽 클릭 오차가 발생할 수 있습니다.");
        }
    }

    /// <summary>
    /// 셀 앵커들의 로컬 XZ 범위를 계산함.
    /// </summary>
    /// <param name="minBounds">최소 로컬 XZ 좌표.</param>
    /// <param name="maxBounds">최대 로컬 XZ 좌표.</param>
    /// <returns>범위 계산 성공 여부.</returns>
    private bool TryGetAnchorLocalBounds(out Vector2 minBounds, out Vector2 maxBounds)
    {
        minBounds = new Vector2(float.MaxValue, float.MaxValue);
        maxBounds = new Vector2(float.MinValue, float.MinValue);
        bool hasValidAnchor = false;

        for (int xIndex = 0; xIndex < BoardSize; xIndex++)
        {
            for (int yIndex = 0; yIndex < BoardSize; yIndex++)
            {
                Transform cellAnchor = _cellAnchors[xIndex, yIndex];
                if (cellAnchor == null)
                {
                    continue;
                }

                Vector3 localPosition = _boardRoot.InverseTransformPoint(cellAnchor.position);
                minBounds.x = Mathf.Min(minBounds.x, localPosition.x);
                minBounds.y = Mathf.Min(minBounds.y, localPosition.z);
                maxBounds.x = Mathf.Max(maxBounds.x, localPosition.x);
                maxBounds.y = Mathf.Max(maxBounds.y, localPosition.z);
                hasValidAnchor = true;
            }
        }

        return hasValidAnchor;
    }

    /// <summary>
    /// 해당 셀에 실제 돌이 이미 놓였는지 확인함.
    /// </summary>
    /// <param name="xIndex">검사할 X 인덱스.</param>
    /// <param name="yIndex">검사할 Y 인덱스.</param>
    /// <returns>셀 점유 여부.</returns>
    private bool IsCellOccupied(int xIndex, int yIndex)
    {
        return _logic != null && _logic.Board[xIndex, yIndex].Color != StoneColor.None;
    }

    /// <summary>
    /// 현재 hover 셀이 금수 경고 대상인지 확인함.
    /// </summary>
    /// <param name="xIndex">검사할 X 인덱스.</param>
    /// <param name="yIndex">검사할 Y 인덱스.</param>
    /// <param name="stoneColor">현재 턴 돌 색상.</param>
    /// <returns>금수 경고 대상 여부.</returns>
    private bool IsForbiddenPreview(int xIndex, int yIndex, StoneColor stoneColor)
    {
        if (_logic == null || stoneColor != StoneColor.Black)
        {
            return false;
        }

        return _logic.IsForbidden(xIndex, yIndex, stoneColor);
    }

    /// <summary>
    /// 돌의 시각 중심이 셀 중심과 맞도록 위치를 보정함.
    /// </summary>
    /// <param name="stoneTransform">정렬할 돌 Transform.</param>
    /// <param name="targetCenter">맞출 셀 중심 위치.</param>
    private void AlignStoneToCellCenter(Transform stoneTransform, Vector3 targetCenter)
    {
        if (!TryGetStoneRenderBounds(stoneTransform, out Bounds renderBounds))
        {
            stoneTransform.position = targetCenter;
            return;
        }

        Vector3 centerOffset = targetCenter - renderBounds.center;
        Vector3 lateralOffset = Vector3.ProjectOnPlane(centerOffset, _boardRoot != null ? _boardRoot.up : Vector3.up);
        stoneTransform.position += lateralOffset;
    }

    /// <summary>
    /// 돌 오브젝트의 전체 렌더러 경계 박스를 계산함.
    /// </summary>
    /// <param name="stoneTransform">대상 돌 Transform.</param>
    /// <param name="renderBounds">계산된 렌더러 경계 박스.</param>
    /// <returns>렌더러 경계 계산 성공 여부.</returns>
    private static bool TryGetStoneRenderBounds(Transform stoneTransform, out Bounds renderBounds)
    {
        Renderer[] renderers = stoneTransform.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            renderBounds = default;
            return false;
        }

        renderBounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            renderBounds.Encapsulate(renderers[index].bounds);
        }

        return true;
    }

    /// <summary>
    /// 보드 평면 기준 두 월드 좌표의 평면 거리를 계산함.
    /// </summary>
    /// <param name="from">기준 좌표.</param>
    /// <param name="to">비교 좌표.</param>
    /// <returns>보드 평면 기준 거리.</returns>
    private float GetPlanarDistance(Vector3 from, Vector3 to)
    {
        Vector3 planeNormal = _boardRoot != null ? _boardRoot.up : Vector3.up;
        Vector3 delta = Vector3.ProjectOnPlane(to - from, planeNormal);
        return delta.magnitude;
    }

    /// <summary>
    /// 돌 색상에 맞는 하이어라키 부모를 반환함.
    /// </summary>
    /// <param name="stoneColor">현재 생성할 돌 색상.</param>
    /// <returns>돌을 정리할 부모 Transform.</returns>
    private Transform GetStoneParent(StoneColor stoneColor)
    {
        return stoneColor == StoneColor.Black ? _blackStoneParent : _whiteStoneParent;
    }

    /// <summary>
    /// 현재 색상에 맞는 고스트 프리팹을 반환함.
    /// </summary>
    /// <param name="stoneColor">현재 턴 돌 색상.</param>
    /// <returns>해당 색상 고스트 프리팹.</returns>
    private GameObject GetGhostPrefab(StoneColor stoneColor)
    {
        if (stoneColor == StoneColor.Black)
        {
            return _blackGhostPrefab;
        }

        return stoneColor == StoneColor.White ? _whiteGhostPrefab : null;
    }

    /// <summary>
    /// 고스트 돌을 배치할 부모 Transform을 반환함.
    /// </summary>
    /// <returns>고스트 부모 Transform.</returns>
    private Transform GetGhostParent()
    {
        if (_ghostParent != null)
        {
            return _ghostParent;
        }

        if (_stoneRoot != null)
        {
            return _stoneRoot;
        }

        return transform;
    }

    /// <summary>
    /// 현재 색상에 맞는 고스트 회전을 반환함.
    /// </summary>
    /// <param name="stoneColor">현재 턴 돌 색상.</param>
    /// <returns>해당 색상 고스트 회전값.</returns>
    private Quaternion GetGhostRotation(StoneColor stoneColor)
    {
        GameObject ghostPrefab = GetGhostPrefab(stoneColor);
        return ghostPrefab != null ? ghostPrefab.transform.rotation : Quaternion.identity;
    }

    /// <summary>
    /// 돌 색상에 맞는 다음 생성 이름을 반환함.
    /// </summary>
    /// <param name="stoneColor">현재 생성할 돌 색상.</param>
    /// <returns>색상별 누적 번호가 반영된 돌 이름.</returns>
    private string GetNextStoneName(StoneColor stoneColor)
    {
        if (stoneColor == StoneColor.Black)
        {
            _blackStoneCount++;
            return $"Black_{_blackStoneCount}";
        }

        _whiteStoneCount++;
        return $"White_{_whiteStoneCount}";
    }

    /// <summary>
    /// 현재 설정에서 사용할 클릭 카메라를 반환함.
    /// </summary>
    /// <returns>입력 해석에 사용할 카메라.</returns>
    private Camera GetClickCamera()
    {
        if (_clickCamera != null)
        {
            return _clickCamera;
        }

        return Camera.main;
    }

    /// <summary>
    /// 턴 색상에 맞는 돌 프리팹을 반환함.
    /// </summary>
    /// <param name="stoneColor">현재 턴 돌 색상.</param>
    /// <returns>해당 색상 돌 프리팹.</returns>
    private GameObject GetStonePrefab(StoneColor stoneColor)
    {
        if (stoneColor == StoneColor.Black)
        {
            return _blackStonePrefab;
        }

        return stoneColor == StoneColor.White ? _whiteStonePrefab : null;
    }
}
