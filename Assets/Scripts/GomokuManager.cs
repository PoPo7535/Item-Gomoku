using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GomokuManager : MonoBehaviour
{
    [Header("프리팹 설정")]
    public GameObject BlackStonePrefab;
    public GameObject WhiteStonePrefab;

    [Header("렌더 텍스처 & 카메라 설정")]
    public RawImage GameViewImage; // UI의 RawImage 연결
    public Camera BoardCamera;    // 바둑판 전용 카메라 연결

    [Header("판 설정")]
    public int LineCount = 15;

    [Header("--- 기록 관리 ---")]
    private List<string> _blackHistory = new List<string>();
    private List<string> _whiteHistory = new List<string>();
    private int _lastX;
    private int _lastZ;

    private GameObject[,] _stoneObjects; // 돌 오브젝트 담을 곳
    private OmokuLogic _logic;           // 돌 데이터 로직
    private bool _isBlackTurn = true;    // 턴 확인

    void Awake()
    {
        Reset();
    }

    void Update()
    {
        // 마우스 클릭 시 착수 로직 실행
        if (Input.GetMouseButtonDown(0))
        {
            PlaceStone();
        }
    }

    /// <summary>
    /// RenderTexture UI 클릭을 월드 좌표로 변환하여 돌을 착수
    /// </summary>
    void PlaceStone()
    {
        if (GameViewImage == null || BoardCamera == null) return;

        // 1. UI 좌표를 RenderTexture 비율(0~1)로 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GameViewImage.rectTransform, 
            Input.mousePosition, 
            null, 
            out Vector2 localPoint
        );

        Rect r = GameViewImage.rectTransform.rect;
        float nX = (localPoint.x - r.x) / r.width;
        float nY = (localPoint.y - r.y) / r.height;

        // 2. 비율을 사용하여 카메라에서 레이 발사
        Ray ray = BoardCamera.ViewportPointToRay(new Vector3(nX, nY, 0));

        // 3. 'Board' 레이어만 맞추도록 마스크 설정 (돌에 레이가 맞는 현상 방지)
        int layerMask = 1 << LayerMask.NameToLayer("Board");

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
        {
            // 4. 바둑판 콜라이더의 실제 물리적 경계선(Bounds) 가져오기
            Bounds b = hit.collider.bounds;

            // 5. 클릭 지점이 콜라이더 내에서 어느 정도 비율인지 계산 (0~1)
            float pctX = (hit.point.x - b.min.x) / b.size.x;
            float pctZ = (hit.point.z - b.min.z) / b.size.z;

            // 6. 비율을 바둑판 인덱스(0~14)로 변환
            int xIdx = Mathf.Clamp(Mathf.RoundToInt(pctX * (LineCount - 1)), 0, LineCount - 1);
            int zIdx = Mathf.Clamp(Mathf.RoundToInt(pctZ * (LineCount - 1)), 0, LineCount - 1);

            StoneColor currentColor = _isBlackTurn ? StoneColor.Black : StoneColor.White;

            // 7. 오목 로직 체크 (금수 및 중복 착수 확인)
            if (_logic.PlaceStone(xIdx, zIdx, currentColor))
            {
                // 8. 인덱스를 기준으로 실제 월드 생성 좌표 역산 (정밀 스냅)
                float finalX = b.min.x + ((float)xIdx / (LineCount - 1)) * b.size.x;
                float finalZ = b.min.z + ((float)zIdx / (LineCount - 1)) * b.size.z;
                
                // 바둑판 표면(max.y) 바로 위(0.05f)에 생성
                Vector3 finalPos = new Vector3(finalX, b.max.y + 0.05f, finalZ);

                // 9. 최근 착수 정보 업데이트 및 기록
                UpdateAndShowLastPlace(xIdx, zIdx);
                string posText = $"{xIdx},{zIdx}";
                if (_isBlackTurn) _blackHistory.Add(posText);
                else _whiteHistory.Add(posText);

                // 10. 돌 생성 및 배열 저장
                GameObject prefab = _isBlackTurn ? BlackStonePrefab : WhiteStonePrefab;
                GameObject stone = Instantiate(prefab, finalPos, Quaternion.identity);
                stone.tag = "Stone"; // 리셋을 위한 태그 설정
                _stoneObjects[xIdx, zIdx] = stone;

                Debug.Log($"[{currentColor}] ({xIdx}, {zIdx}) 착수 성공.");

                // 11. 승리 판정
                if (_logic.CheckWin(xIdx, zIdx, currentColor))
                {
                    Debug.Log($"<color=cyan>★ 승리! {currentColor} ★</color>");
                    // 여기에 승리 팝업 등 추가 로직 가능
                }

                ChangeTurn();
            }
        }
    }

    /// <summary>
    /// 게임 초기화
    /// </summary>
    public void Reset()
    {
        _logic = new OmokuLogic();
        _stoneObjects = new GameObject[LineCount, LineCount];
        _isBlackTurn = true;
        _lastX = 0;
        _lastZ = 0;
        _blackHistory.Clear();
        _whiteHistory.Clear();

        // 씬에 생성된 모든 돌 파괴
        GameObject[] stones = GameObject.FindGameObjectsWithTag("Stone");
        foreach (var s in stones) Destroy(s);

        Debug.Log("게임 리셋 완료");
    }

    public void ChangeTurn() => _isBlackTurn = !_isBlackTurn;

    /// <summary>
    /// 최근 착수 위치 알려주기
    /// </summary>
    public void UpdateAndShowLastPlace(int x, int z)
    {
        _lastX = x;
        _lastZ = z;
        string lastPlayer = _isBlackTurn ? "흑돌" : "백돌";
        string nextPlayer = _isBlackTurn ? "백돌" : "흑돌";

        Debug.Log($"<color=orange>[턴 교체]</color> {nextPlayer} 차례입니다. " +
                $"(상대 {lastPlayer}의 마지막 착수: {_lastX}, {_lastZ})");
    }

    /// <summary>
    /// 특정 좌표 돌삭제
    /// </summary>
    public void RemoveStone(int x, int z)
    {
        if (_stoneObjects[x, z] != null)
        {
            Destroy(_stoneObjects[x, z]);
            _stoneObjects[x, z] = null;
            _logic.Board[x, z] = new StoneData { Color = StoneColor.None };
            Debug.Log($"({x}, {z}) 돌 삭제됨");
        }
    }

    /// <summary>
    /// 누구턴인지 알려주기
    /// </summary>
    public string GetCurrentTurnText() => _isBlackTurn ? "흑돌 턴" : "백돌 턴";

    /// <summary>
    /// 전체 기록 확인
    /// </summary>
    public void ShowFullLog()
    {
        Debug.Log("흑돌 기보: " + string.Join(" -> ", _blackHistory));
        Debug.Log("백돌 기보: " + string.Join(" -> ", _whiteHistory));
    }

    /// <summary>
    /// 특정(백,흑) 돌 갯수 확인
    /// </summary>
    public int GetStoneCount(StoneColor color)
    {
        int count = 0;

        for (int x = 0; x < LineCount; x++)
        {
            for (int y = 0; y < LineCount; y++)
            {
                if (_logic.Board[x, y].Color == color)
                    count++;
            }
        }

        return count;
    }
}