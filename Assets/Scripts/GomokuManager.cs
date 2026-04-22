using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class GomokuManager : MonoBehaviour
{
    [Header("프리팹 설정")]

    public GameObject BlackStonePrefab; 
    public GameObject WhiteStonePrefab;

    [Header("판 설정")]
    public float BoardPhysicalSize = 5.0f; 
    public int LineCount = 15;

    [Header("--- 기록 관리 ---")]
    // 전체기록 정보 
    private List<string> _blackHistory = new List<string>();
    private List<string> _whiteHistory = new List<string>();
    //마지막 착수 정보
    private int _lastX; 
    private int _lastZ; 

    private GameObject[,] _stoneObjects; //돌 오브젝트 담을곳
    private OmokuLogic _logic; //돌 데이터 
    private bool _isBlackTurn = true; // 턴 확인
    public Camera boardCamera; 
    public RawImage GameViewImage;
    void Awake()
    {
        Reset();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PlaceStone();
        }
    }

    /// <summary>
    /// 마우스 클릭 위치를 바둑판 좌표로 변환하여 돌을 착수하고 승리를 판정
    /// </summary>
void PlaceStone()
{
    // 1. RawImage 상의 마우스 클릭 위치를 0~1 비율(Normalized)로 변환
    RectTransformUtility.ScreenPointToLocalPointInRectangle(
        GameViewImage.rectTransform, 
        Input.mousePosition, 
        null, 
        out Vector2 localPoint
    );

    Rect r = GameViewImage.rectTransform.rect;
    float normalizedX = (localPoint.x - r.x) / r.width;
    float normalizedY = (localPoint.y - r.y) / r.height;

    // 2. 렌더 텍스처 전용 카메라에서 레이 발사
    Ray ray = boardCamera.ViewportPointToRay(new Vector3(normalizedX, normalizedY, 0));
    Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red, 5f);

    if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
    {
        // 3. 인덱스 계산을 위해 클릭 지점을 'Cube'의 로컬 좌표로 변환
        // hit.collider는 현재 레이가 맞은 Cube입니다.
        Vector3 localHitPos = hit.collider.transform.InverseTransformPoint(hit.point);

        float interval = BoardPhysicalSize / (LineCount - 1);

        // 로컬 좌표를 interval로 나누어 오프셋 계산
        int xIdxOffset = Mathf.RoundToInt(localHitPos.x / interval);
        int zIdxOffset = Mathf.RoundToInt(localHitPos.z / interval);

        int halfCount = (LineCount - 1) / 2;
        int displayX = Mathf.Clamp(xIdxOffset + halfCount, 0, LineCount - 1);
        int displayZ = Mathf.Clamp(zIdxOffset + halfCount, 0, LineCount - 1);

        StoneColor currentColor = _isBlackTurn ? StoneColor.Black : StoneColor.White;

        // 4. 오목 로직 체크 (중복 착수 등)
        if (_logic.PlaceStone(displayX, displayZ, currentColor))
        {
            // 5. 실제 돌이 배치될 위치 계산 (그리드 스냅)
            // 로컬 좌표로 먼저 잡고, 다시 월드 좌표로 변환하여 정확한 위치에 소환
            Vector3 spawnLocalPos = new Vector3(xIdxOffset * interval, 0.1f, zIdxOffset * interval);
            Vector3 finalPos = hit.collider.transform.TransformPoint(spawnLocalPos);

            // 6. 돌 생성 및 데이터 저장
            GameObject prefab = _isBlackTurn ? BlackStonePrefab : WhiteStonePrefab;
            GameObject stone = Instantiate(prefab, finalPos, Quaternion.identity);
            
         
            stone.transform.SetParent(hit.collider.transform);
            
            _stoneObjects[displayX, displayZ] = stone;

            // 7. 정보 업데이트 및 승리 판정
            UpdateAndShowLastPlace(displayX, displayZ); 
            
            string posText = $"{displayX},{displayZ}";
            if (_isBlackTurn) _blackHistory.Add(posText);
            else _whiteHistory.Add(posText);

            Debug.Log($"<color=cyan>[{currentColor}] ({displayX}, {displayZ}) 착수 성공!</color>");

            if (_logic.CheckWin(displayX, displayZ, currentColor))
            {   
                Debug.Log($"<color=yellow>★ 승리! {currentColor} ★</color>");
                return;
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
        if (_stoneObjects == null)
        {
            _stoneObjects = new GameObject[LineCount, LineCount];
        }
        _isBlackTurn = true;
        _lastX = 0;
        _lastZ = 0;
        _blackHistory.Clear();
        _whiteHistory.Clear();

        for (int x = 0; x < LineCount; x++)
        {
            for (int y = 0; y < LineCount; y++)
            {
                if (_stoneObjects[x, y] != null)
                {
                    Destroy(_stoneObjects[x, y]);
                    _stoneObjects[x, y] = null;
                }
            }
        }
        Debug.Log("게임 리셋 완료");
    }
    
    //--------------밑에 함수는 아이템 만들때 쓸수도있는거?----------------//

    /// <summary>
    /// 턴 변경
    /// </summary>
    public void ChangeTurn()
    {
        _isBlackTurn = !_isBlackTurn;
    }

    /// <summary>
    /// 특정 좌표 돌삭제
    /// </summary>
    public void RemoveStone(int x, int y)
    {
        if (_stoneObjects[x, y] != null)
        {
            Destroy(_stoneObjects[x, y]);
            _stoneObjects[x, y] = null;
            _logic.Board[x, y] = new StoneData { Color = StoneColor.None, IsFake = false };
            Debug.Log($"({x}, {y}) 돌 삭제됨");
        }
    }


    /// <summary>
    /// 특정 좌표 돌생성 _ 미완
    /// </summary>
    public void AddStone(int x, int y, StoneColor color)
    {
        if (!_logic.IsInside(x, y)) return;
        if (_stoneObjects[x, y] != null) return;
        if (!_logic.PlaceStone(x, y, color)) return;

        float interval = BoardPhysicalSize / (LineCount - 1);
        int halfCount = (LineCount - 1) / 2;

        int xOffset = x - halfCount;
        int zOffset = y - halfCount;

        float finalX = xOffset * interval;
        float finalZ = zOffset * interval;

        Vector3 pos = new Vector3(finalX, 2.6f, finalZ); // 여기실제 바둑판 사이즈보고 바꿀것!

        GameObject prefab = (color == StoneColor.Black) ? BlackStonePrefab : WhiteStonePrefab;

        GameObject stone = Instantiate(prefab, pos, Quaternion.identity);
        _stoneObjects[x, y] = stone;
    }
    
    /// <summary>
    /// 누구턴인지 알려주기
    /// </summary>
    public string IsMyTurn()
    {
        string result = _isBlackTurn ? "흑돌 턴" : "백돌 턴"; 
        return result;
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
    /// 전체 기록 확인
    /// </summary>
    public void ShowFullLog()
    {
        Debug.Log("흑돌 기보: " + string.Join(" -> ", _blackHistory));
        Debug.Log("백돌 기보: " + string.Join(" -> ", _whiteHistory));
    }
    
}