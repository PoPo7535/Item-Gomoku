using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TestScene 전용 오목 흐름을 관리함.
/// </summary>
public sealed class GomokuManager_Test : MonoBehaviour
{
    private const int BoardSize = 15;

    [Header("프리팹 설정")]
    [SerializeField] private GameObject _blackStonePrefab;
    [SerializeField] private GameObject _whiteStonePrefab;
    [SerializeField] private float _stoneScale = 30f;

    [Header("보드 설정")]
    [SerializeField] private Transform _boardRoot;
    [SerializeField] private Camera _clickCamera;
    [SerializeField] private float _stoneHeight = 0.1f;

    [Header("돌 정리 설정")]
    [SerializeField] private Transform _stoneRoot;
    [SerializeField] private Transform _blackStoneParent;
    [SerializeField] private Transform _whiteStoneParent;

    private OmokuLogic _logic;
    private GameObject[,] _stoneObjects;
    private Transform[,] _cellAnchors;
    private bool _isBlackTurn = true;
    private bool _isGameOver;
    private bool _isReady;
    private int _blackStoneCount;
    private int _whiteStoneCount;

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
        if (!_isReady || _isGameOver)
        {
            return;
        }

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
        _stoneObjects = new GameObject[BoardSize, BoardSize];
        _cellAnchors = new Transform[BoardSize, BoardSize];
        _isBlackTurn = true;
        _isGameOver = false;
        _blackStoneCount = 0;
        _whiteStoneCount = 0;
        _isReady = TryCacheCellAnchors() && ValidateStoneHierarchy();
    }

    /// <summary>
    /// 현재 클릭 위치를 보드 좌표로 해석해 착수를 처리함.
    /// </summary>
    private void TryHandlePlacementInput()
    {
        if (!TryGetBoardIndex(out int xIndex, out int yIndex))
        {
            return;
        }

        TryPlaceStone(xIndex, yIndex);
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

        return xIndex >= 0 && yIndex >= 0;
    }

    /// <summary>
    /// 현재 턴 기준으로 착수와 승패 판정을 진행함.
    /// </summary>
    /// <param name="xIndex">착수할 X 인덱스.</param>
    /// <param name="yIndex">착수할 Y 인덱스.</param>
    private void TryPlaceStone(int xIndex, int yIndex)
    {
        StoneColor currentColor = _isBlackTurn ? StoneColor.Black : StoneColor.White;
        GameObject stonePrefab = GetStonePrefab(currentColor);

        if (stonePrefab == null)
        {
            Debug.LogWarning($"{currentColor} 돌 프리팹이 연결되지 않았습니다.");
            return;
        }

        if (!_logic.PlaceStone(xIndex, yIndex, currentColor))
        {
            return;
        }

        SpawnStone(stonePrefab, xIndex, yIndex);
        Debug.Log($"{currentColor} 착수: ({xIndex}, {yIndex})");

        if (_logic.CheckWin(xIndex, yIndex, currentColor))
        {
            _isGameOver = true;
            Debug.Log($"승리: {currentColor}");
            return;
        }

        _isBlackTurn = !_isBlackTurn;
    }

    /// <summary>
    /// 보드 인덱스에 해당하는 위치에 돌 프리팹을 생성함.
    /// </summary>
    /// <param name="stonePrefab">생성할 돌 프리팹.</param>
    /// <param name="xIndex">보드 X 인덱스.</param>
    /// <param name="yIndex">보드 Y 인덱스.</param>
    private void SpawnStone(GameObject stonePrefab, int xIndex, int yIndex)
    {
        Transform cellAnchor = _cellAnchors[xIndex, yIndex];
        if (cellAnchor == null)
        {
            Debug.LogError($"셀 앵커를 찾지 못해 돌을 생성할 수 없습니다: ({xIndex}, {yIndex})");
            return;
        }

        Vector3 worldStonePosition = cellAnchor.position + (_boardRoot.up * _stoneHeight);
        Quaternion worldStoneRotation = _boardRoot.rotation * stonePrefab.transform.rotation;
        StoneColor currentColor = _isBlackTurn ? StoneColor.Black : StoneColor.White;
        Transform stoneParent = GetStoneParent(currentColor);
        GameObject stoneObject = Instantiate(stonePrefab, worldStonePosition, worldStoneRotation, stoneParent);
        stoneObject.transform.localScale = Vector3.one * _stoneScale;
        AlignStoneToCellCenter(stoneObject.transform, worldStonePosition);
        stoneObject.name = GetNextStoneName(currentColor);
        _stoneObjects[xIndex, yIndex] = stoneObject;
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
    /// 돌 색상에 맞는 하이어라키 부모를 반환함.
    /// </summary>
    /// <param name="stoneColor">현재 생성할 돌 색상.</param>
    /// <returns>돌을 정리할 부모 Transform.</returns>
    private Transform GetStoneParent(StoneColor stoneColor)
    {
        return stoneColor == StoneColor.Black ? _blackStoneParent : _whiteStoneParent;
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
