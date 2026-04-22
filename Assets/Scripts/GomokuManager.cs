using System.Collections.Generic;
using UnityEngine;


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
        Ray ray = boardCamera.ScreenPointToRay(Input.mousePosition);

        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red, 5f);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // 1. 간격 및 좌표 계산
            float interval = BoardPhysicalSize / (LineCount - 1);


            Vector3 relativeHitPoint = hit.point - boardCamera.transform.parent.position; 


            int xIdxOffset = Mathf.RoundToInt(hit.point.x / interval);
            int zIdxOffset = Mathf.RoundToInt(hit.point.z / interval);

            int halfCount = (LineCount - 1) / 2;
            int displayX = Mathf.Clamp(xIdxOffset + halfCount, 0, LineCount - 1);
            int displayZ = Mathf.Clamp(zIdxOffset + halfCount, 0, LineCount - 1);

            StoneColor currentColor = _isBlackTurn ? StoneColor.Black : StoneColor.White;

            // 2. 로직 체크 (금수, 중복 착수 등)
            if (_logic.PlaceStone(displayX, displayZ, currentColor))
            {
                // 3. 실제 돌이 배치될 월드 좌표 (스냅)
                float finalX = xIdxOffset * interval;
                float finalZ = zIdxOffset * interval;
                
        
                Vector3 finalPos = new Vector3(finalX, hit.point.y + 0.1f, finalZ);

                // 4. 정보 업데이트 및 히스토리 기록
                UpdateAndShowLastPlace(displayX, displayZ); 
                string posText = $"{displayX},{displayZ}";
                if (_isBlackTurn) _blackHistory.Add(posText);
                else _whiteHistory.Add(posText);

                // 5. 돌 생성 및 저장
                GameObject prefab = _isBlackTurn ? BlackStonePrefab : WhiteStonePrefab;
                GameObject stone = Instantiate(prefab, finalPos, Quaternion.identity);
                _stoneObjects[displayX, displayZ] = stone;

                Debug.Log($"<color=cyan>[{currentColor}] ({displayX}, {displayZ}) 착수 성공!</color>");

                // 6. 승리 판정
                if (_logic.CheckWin(displayX, displayZ, currentColor))
                {   
                    Debug.Log($"<color=yellow>★ 승리! {currentColor} ★</color>");
                    return;
                }

                // 7. 턴 변경
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