using UnityEngine;
using UnityEngine.UI;

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
    public GameObject GeneratedPoints; // 생성된 포인트들을 담을 부모 오브젝트

    // 실제 돌 오브젝트들을 관리하는 배열 (최적화용)
    private GameObject[,] _stoneObjects;

    /// <summary>
    /// 게임 시작 시 초기화 (Manager에서 호출)
    /// </summary>
    public void Init()
    {
        _stoneObjects = new GameObject[LineCount, LineCount];
        CreateClickPoints();
    }

    /// <summary>
    /// 바둑판의 클릭 감지용 포인트(SphereCollider)들을 생성
    /// </summary>
    public void CreateClickPoints()
    {
        // 기존에 생성된 포인트가 있다면 제거
        if (GeneratedPoints != null)
        {
            for (int i = GeneratedPoints.transform.childCount - 1; i >= 0; i--)
                Destroy(GeneratedPoints.transform.GetChild(i).gameObject);
        }

        for (int x = 0; x < LineCount; x++)
        {
            for (int z = 0; z < LineCount; z++)
            {
                GameObject p = new GameObject($"Point_{x}_{z}");
                p.transform.SetParent(GeneratedPoints.transform);

                // 간격(Interval)에 따른 월드 좌표 계산
                Vector3 worldPos = new Vector3(
                    StartPos.x + (x * Interval), 
                    StartPos.y, 
                    StartPos.z + (z * Interval)
                );
                p.transform.position = worldPos;

                // 클릭 감지를 위한 트리거 콜라이더 추가
                SphereCollider sc = p.AddComponent<SphereCollider>();
                sc.radius = Interval * 0.75f;
                sc.isTrigger = true;
                
                // 레이캐스트 충돌을 위해 레이어 설정 (Board 레이어 미리 생성 필요)
                p.layer = LayerMask.NameToLayer("Board");
            }
        }
    }

    /// <summary>
    /// 실제 돌 프리팹을 화면에 생성
    /// </summary>
    public void SpawnStoneVisual(int x, int z, bool isBlack, Vector3 pos)
    {
        if (x < 0 || x >= LineCount || z < 0 || z >= LineCount) return;

        GameObject prefab = isBlack ? BlackStonePrefab : WhiteStonePrefab;
        
        // 돌이 판 위에 살짝 떠 있게 생성 (Y값 보정)
        Vector3 spawnPos = pos + new Vector3(0, 0.15f, 0);
        GameObject stone = Instantiate(prefab, spawnPos, Quaternion.identity);
        
        // 나중에 리셋할 때 찾기 쉽도록 태그와 배열에 저장
        stone.tag = "Stone"; 
        _stoneObjects[x, z] = stone;
    }

    /// <summary>
    /// 마우스 커서 위치에 따른 고스트 돌(미리보기) 업데이트
    /// </summary>
    public void UpdateGhostStone(Vector3 pos, int x, int z, bool isVisible, bool isBlack)
    {
        // 기본적으로 둘 다 비활성화
        BlackGhostObj.SetActive(false);
        WhiteGhostObj.SetActive(false);

        if (!isVisible) return;

        GameObject targetGhost = isBlack ? BlackGhostObj : WhiteGhostObj;
        
        if (targetGhost != null)
        {
            targetGhost.transform.position = pos + new Vector3(0, 0.15f, 0);
            targetGhost.SetActive(true);
        }
    }

    /// <summary>
    /// 보드 위의 모든 돌을 제거하고 배열 초기화 (최적화 방식)
    /// </summary>
    public void ClearBoard()
    {
        if (_stoneObjects == null) return;

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
        
        // 혹시 모를 잔여 오브젝트 방어 코드
        GameObject[] extraStones = GameObject.FindGameObjectsWithTag("Stone");
        foreach (var s in extraStones) Destroy(s);
    }
}