using UnityEngine;
using UnityEngine.UI;



// 얘는 보드 UI / 렌더링 / 클릭 좌표 변환 / 돌 표시 전부 담당
public class GomokuBoardView : MonoBehaviour
{
    [Header("프리팹 설정")]
    public GameObject BlackStonePrefab;
    public GameObject WhiteStonePrefab;

    [Header("고스트 돌 설정")]
    public GameObject BlackGhostObj; 
    public GameObject WhiteGhostObj;

    [Header("렌더 텍스처 & 카메라 설정")]
    public RawImage GameViewImage; 
    public Camera BoardCamera;    

    [Header("보드 생성 설정")]
    public int LineCount = 15;
    public float Interval = 1.0f;   
    public Vector3 StartPos;         
    public GameObject GeneratedPoints; // 포인트들이 생성될 부모 오브젝트

    private GameObject[,] _stoneObjects;

    /// <summary>
    /// 보드 초기화 및 포인트 생성
    /// </summary>
    public void Init()
    {
        _stoneObjects = new GameObject[LineCount, LineCount];
        CreateClickPoints();
    }

    /// <summary>
    /// 마우스 위치를 계산하여 보드 좌표와 인덱스를 반환
    /// </summary>
    public (Vector3 pos, int x, int z) GetBoardPosition()
    {
        if (GameViewImage == null || BoardCamera == null) return (Vector3.zero, 0, 0);

        // 1. UI 좌표 -> 렌더 텍스처 좌표 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GameViewImage.rectTransform, Input.mousePosition, null, out Vector2 localPoint);

        Rect r = GameViewImage.rectTransform.rect;
        float nX = (localPoint.x - r.x) / r.width;
        float nY = (localPoint.y - r.y) / r.height;

        // 2. 카메라 레이 발사
        Ray ray = BoardCamera.ViewportPointToRay(new Vector3(nX, nY, 0));
        int layerMask = 1 << LayerMask.NameToLayer("Board");

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
        {
            if (hit.transform.name.StartsWith("Point_"))
            {
                string[] parts = hit.transform.name.Split('_');
                int xIdx = int.Parse(parts[1]);
                int zIdx = int.Parse(parts[2]);
                return (hit.transform.position, xIdx, zIdx);
            }
        }
        return (Vector3.zero, 0, 0);
    }

    /// <summary>
    /// 실제 돌 프리팹을 화면에 생성하고 배열에 저장
    /// </summary>
    public void SpawnStone(int x, int z, bool isBlack, Vector3 pos)
    {   
        // 이 배열이 초기화되지 않았을 경우를 대비
        if (_stoneObjects == null) _stoneObjects = new GameObject[LineCount, LineCount];

        GameObject prefab = isBlack ? BlackStonePrefab : WhiteStonePrefab;
        
        Vector3 spawnPos = pos + new Vector3(0, 0.15f, 0); 
        
        GameObject stone = Instantiate(prefab, spawnPos, Quaternion.identity);
        stone.tag = "Stone";
        _stoneObjects[x, z] = stone;
    }

    /// <summary>
    /// 마우스 커서 위치에 따른 반투명 돌 표시
    /// </summary>
    public void UpdateGhostStone(Vector3 pos, bool isVisible, bool isBlack)
    {
        if (BlackGhostObj == null || WhiteGhostObj == null) return;

        // 일단 둘 다 끄기
        BlackGhostObj.SetActive(false);
        WhiteGhostObj.SetActive(false);

        if (!isVisible || pos == Vector3.zero) return;

        GameObject target = isBlack ? BlackGhostObj : WhiteGhostObj;
        target.transform.position = pos + new Vector3(0, 0.15f, 0);
        target.SetActive(true);
    }

    /// <summary>
    /// 클릭 감지용 포인트(SphereCollider) 생성
    /// </summary>
    public void CreateClickPoints()
    {
        if (GeneratedPoints == null) return;

        // 기존 자식 오브젝트 제거
        for (int i = GeneratedPoints.transform.childCount - 1; i >= 0; i--)
            Destroy(GeneratedPoints.transform.GetChild(i).gameObject);

        // 포인트 생성 루프
        for (int x = 0; x < LineCount; x++)
        {
            for (int z = 0; z < LineCount; z++)
            {
                GameObject p = new GameObject($"Point_{x}_{z}");
                p.transform.SetParent(GeneratedPoints.transform);

                Vector3 worldPos = new Vector3(
                    StartPos.x + (x * Interval), 
                    StartPos.y, 
                    StartPos.z + (z * Interval)
                );
                p.transform.position = worldPos;

                SphereCollider sc = p.AddComponent<SphereCollider>();
                sc.radius = Interval * 0.75f;
                sc.isTrigger = true;
                p.layer = LayerMask.NameToLayer("Board");
            }
        }
    }

    /// <summary>
    /// 보드 위의 모든 돌 오브젝트 삭제 및 데이터 초기화
    /// </summary>
    public void ClearBoard()
    {
        
        if (_stoneObjects != null)
        {
            for (int x = 0; x < LineCount; x++)
            {
                for (int z = 0; z < LineCount; z++)
                {
                    if (_stoneObjects[x, z] != null)
                    {
                        Destroy(_stoneObjects[x, z]);
                        _stoneObjects[x, z] = null;
                    }
                }
            }
        }

        GameObject[] stones = GameObject.FindGameObjectsWithTag("Stone");
        foreach (var s in stones) Destroy(s);
    }
    /// <summary>
    /// 좌표 기반으로 돌 강제 착수
    /// 예: 14,14 위치에 현재 턴 돌 놓기
    ///  BoardView.PlaceStoneByCoord(14, 14, IsBlackTurn);
    /// </summary>
    public void PlaceStoneByCoord(int x, int z, bool isBlackTurn)
    {
        // 범위 체크만 여기서 해도 OK (선택)
        if (x < 0 || x >= LineCount || z < 0 || z >= LineCount)
        {
            Debug.LogWarning($"잘못된 좌표: {x}, {z}");
            return;
        }

        // 좌표 → 월드 위치 변환
        Vector3 pos = new Vector3(
            StartPos.x + (x * Interval),
            StartPos.y,
            StartPos.z + (z * Interval)
        );

        // 핵심: 로직으로 넘김 (여기서 모든 처리됨)
        GomokuManager.I.PlaceStoneProcess(pos, x, z, isBlackTurn);
    }
    /// <summary>
    /// 특정 좌표(x,z)에 있는 오브젝트 돌만 삭제
    /// </summary>
    public void RemoveStone(int x, int z)
    {
        if (_stoneObjects[x, z] != null)
        {
            Destroy(_stoneObjects[x, z]);
            _stoneObjects[x, z] = null;
        }
    }

}