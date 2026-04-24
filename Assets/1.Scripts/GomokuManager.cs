using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GomokuManager : MonoBehaviour
{
    [Header("프리팹 설정")]
    public GameObject BlackStonePrefab;
    public GameObject WhiteStonePrefab;

    [Header("렌더 텍스처 & 카메라 설정")]
    public RawImage GameViewImage; 
    public Camera BoardCamera;    

    [Header("자동 포인트 생성 설정")]
    public int LineCount = 15;
    public float Interval;   
    public Vector3 StartPos;         
    public GameObject GeneratedPoints;  // 생성된 포인트들을 담을 부모

    [Header("--- 기록 관리 ---")]
    private List<string> _blackHistory = new List<string>(); // 전체기록 흑
    private List<string> _whiteHistory = new List<string>(); // 전체기록 백 
    private int _lastX; // 최근착수 위치 x
    private int _lastZ; // 최근착수 위티 y

    private GameObject[,] _stoneObjects; //실제 돌 담은 공간
    private OmokuLogic _logic;           
    private bool _isBlackTurn = true;    //턴여부 true면 흑 false면 백
    private bool _isPlaying = false; // 게임시작여부

    void Awake()
    {

        // 포인트 생성 및 게임 초기화
        CreateClickPoints();
        Reset();
    }

    /// <summary>
    /// 게임 시작 시 225개의 클릭 감지용 포인트를 생성
    /// </summary>
    private void CreateClickPoints()
    {

        for (int i = GeneratedPoints.transform.childCount - 1; i >= 0; i--)
            Destroy(GeneratedPoints.transform.GetChild(i).gameObject);


        for (int x = 0; x < LineCount; x++)
        {
            for (int z = 0; z < LineCount; z++)
            {
                GameObject p = new GameObject($"Point_{x}_{z}");
                

                p.transform.SetParent(GeneratedPoints.transform);

                Vector3 worldPos = new Vector3(StartPos.x + (x * Interval), StartPos.y, StartPos.z + (z * Interval));
                p.transform.position = worldPos;


                SphereCollider sc = p.AddComponent<SphereCollider>();
                sc.radius = Interval * 0.45f;
                sc.isTrigger = true;
                p.layer = LayerMask.NameToLayer("Board");
            }
        }
    }

    void Update()
    {   
        if (!_isPlaying) return;

        if (Input.GetMouseButtonDown(0))
        {
            PlaceStone();
        }
    }
    
    /// <summary>
    /// 클릭한 위치에 돌 생성
    /// </summary>
    void PlaceStone()
    {
        if (GameViewImage == null || BoardCamera == null) return;

        // 1. UI 좌표 -> 렌더 텍스처 비율 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GameViewImage.rectTransform, 
            Input.mousePosition, 
            null, 
            out Vector2 localPoint
        );

        Rect r = GameViewImage.rectTransform.rect;
        float nX = (localPoint.x - r.x) / r.width;
        float nY = (localPoint.y - r.y) / r.height;

        // 2. 레이 발사
        Ray ray = BoardCamera.ViewportPointToRay(new Vector3(nX, nY, 0));
        int layerMask = 1 << LayerMask.NameToLayer("Board");

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
        {
            // 3. 포인트 오브젝트 확인
            string objectName = hit.transform.name;
            if (!objectName.StartsWith("Point_")) return;

            string[] nameParts = objectName.Split('_');
            int xIdx = int.Parse(nameParts[1]);
            int zIdx = int.Parse(nameParts[2]);

            // 잘못된거확인
            if (xIdx < 0 || xIdx >= LineCount || zIdx < 0 || zIdx >= LineCount)
            {
                Debug.LogError($"<color=red>[범위 초과]</color> 잘못된 인덱스입니다! x: {xIdx}, z: {zIdx}. (최대치: {LineCount-1})");
                return;
            }

            StoneColor currentColor = _isBlackTurn ? StoneColor.Black : StoneColor.White;

            // 4. 오목 로직 착수
            if (_logic.PlaceStone(xIdx, zIdx, currentColor))
            {
                Vector3 spawnPos = hit.transform.position;
                spawnPos.y += 0.05f;

                UpdateAndShowLastPlace(xIdx, zIdx);
                string posText = $"{xIdx},{zIdx}";
                if (_isBlackTurn) _blackHistory.Add(posText);
                else _whiteHistory.Add(posText);

                // 6. 돌 생성
                GameObject prefab = _isBlackTurn ? BlackStonePrefab : WhiteStonePrefab;
                GameObject stone = Instantiate(prefab, spawnPos, Quaternion.identity);
                stone.tag = "Stone"; 

                
                _stoneObjects[xIdx, zIdx] = stone;

                // 7. 승리 판정
                if (_logic.CheckWin(xIdx, zIdx, currentColor))
                {
                    Debug.Log($"<color=cyan>★ 승리! {currentColor} ★</color>");
                    Reset();
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
        _isPlaying = false;
        _logic = new OmokuLogic();
        _stoneObjects = new GameObject[LineCount, LineCount];
        _isBlackTurn = true;
        _lastX = 0; 
        _lastZ = 0;
        _blackHistory.Clear();
        _whiteHistory.Clear();

        GameObject[] stones = GameObject.FindGameObjectsWithTag("Stone");
        foreach (var s in stones) Destroy(s);

        Debug.Log("게임 데이터 및 돌 리셋 완료");
    }
    /// <summary>
    /// 좌표로 돌 착수 하기
    /// 사용 예시: ForcePlaceStone(14, 14); 14,14는 바둑판 우측하단
    /// 나오는 바둑알은 현재 자기턴 흑턴이면 흑 백턴이면 백
    /// </summary>
    public void ForcePlaceStone(int x, int z)
    {   
        if (!_isPlaying) return;
        // 1. 바둑판 범위를 벗어났는지 확인
        if (x < 0 || x >= LineCount || z < 0 || z >= LineCount)
        {
            Debug.LogError($"<color=red>[좌표 착수 실패]</color> 잘못된 인덱스입니다! x: {x}, z: {z}");
            return;
        }
        StoneColor color = _isBlackTurn ? StoneColor.Black : StoneColor.White;

        // 2. 로직 배열에 착수 시도 (이미 돌이 있거나 흑돌 금수 자리면 false를 반환하여 막아줌)
        if (_logic.PlaceStone(x, z, color))
        {
            // 3. 실제 유니티 월드(3D) 상의 생성 좌표 계산
            Vector3 spawnPos = new Vector3(
                StartPos.x + (x * Interval), 
                StartPos.y, 
                StartPos.z + (z * Interval)
            );
            spawnPos.y += 0.05f;
            UpdateAndShowLastPlace(x, z); // 최근기록 저장
            // 4. 프리팹 선택 및 생성
            GameObject prefab = (color == StoneColor.Black) ? BlackStonePrefab : WhiteStonePrefab;
            GameObject stone = Instantiate(prefab, spawnPos, Quaternion.identity);
            stone.tag = "Stone"; 

            // 5. 시각적 돌 오브젝트 배열에 저장
            _stoneObjects[x, z] = stone;

            // 6. 전체 기록 남기기
            string posText = $"{x},{z} (강제)";
            if (color == StoneColor.Black) _blackHistory.Add(posText);
            else _whiteHistory.Add(posText);

            // 7. 승리 판정도 동일하게 적용
            if (_logic.CheckWin(x, z, color))
            {
                Debug.Log($"<color=cyan>★ 승리! {color} ★</color>");
                Reset();
                return;
            }
            ChangeTurn();
        }
        else
        {
            Debug.LogWarning($"<color=orange>[강제 착수 실패]</color> ({x}, {z}) 위치에는 이미 돌이 있거나 금수 자리입니다.");
        }
    }

    public void ChangeTurn() => _isBlackTurn = !_isBlackTurn;
    /// <summary>
    /// 최근 착수 위치 알리기
    /// </summary>
    public void UpdateAndShowLastPlace(int x, int z)
    {
        _lastX = x; _lastZ = z;
        string lastPlayer = _isBlackTurn ? "흑돌" : "백돌";
        string nextPlayer = _isBlackTurn ? "백돌" : "흑돌";
        Debug.Log($"<color=orange>[턴 교체]</color> {nextPlayer} 차례 (상대 {lastPlayer}의 마지막 수: {x}, {z})");
    }


    /// <summary>
    /// 특정 좌표 돌 삭제
    /// </summary>
    public void RemoveStone(int x, int z)
    {   
        if (!_isPlaying) return;

        if (_stoneObjects[x, z] != null)
        {
            Destroy(_stoneObjects[x, z]);
            _stoneObjects[x, z] = null;
            _logic.Board[x, z] = new StoneData { Color = StoneColor.None };
        }
    }
    /// <summary>
    /// 턴알려주기
    /// </summary>
    public string GetCurrentTurnText() => _isBlackTurn ? "흑돌 턴" : "백돌 턴";

    /// <summary>
    /// 게임하는동안 좌표들 전체 기록
    /// </summary>
    public void ShowFullLog()
    {
        Debug.Log("흑돌 기보: " + string.Join(" -> ", _blackHistory));
        Debug.Log("백돌 기보: " + string.Join(" -> ", _whiteHistory));
    }
    
    /// <summary>
    /// (백,흑) 돌 착수 수 세기
    /// </summary>
    public int GetStoneCount(StoneColor color)
    {
        int count = 0;
        for (int x = 0; x < LineCount; x++)
            for (int y = 0; y < LineCount; y++)
                if (_logic.Board[x, y].Color == color) count++;
        return count;
    }

    

    /// <summary>
    /// [UI 연결용] 게임 시작 버튼 클릭 시 호출
    /// </summary>
    public void StartGame()
    {
        if (_isPlaying) return;
        _isPlaying = true; 
    }
    /// <summary>
    /// [UI 연결용] 클릭 시 0,0 좌표 돌 생성
    /// </summary>
    public void test()
    {
        ForcePlaceStone(0,0);
    }
    /// <summary>
    /// [UI 연결용] 클릭시 0,0좌표 돌 삭제
    /// </summary>
    public void test2()
    {
        RemoveStone(0,0);
    }
}