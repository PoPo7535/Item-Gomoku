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
    [Header("금수 돌 설정")]
    public GameObject ForbiddenPrefab; 
    [Header("투명 돌 프리팹")]
    public GameObject BlackTransparentPrefab;
    public GameObject WhiteTransparentPrefab;
    [Header("가짜 돌 프리팹")]
    public GameObject BlackFakePrefab;
    public GameObject WhiteFakePrefab;

    [Header("렌더 텍스처 & 카메라 설정")]
    public RawImage GameViewImage; 
    public Camera BoardCamera;
    [Header("최근 착수 표시")]
    public GameObject RealLastMoveMarker;   
    public GameObject FakeLastMoveMarker;      

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
        if (RealLastMoveMarker != null)
        RealLastMoveMarker.SetActive(false);

        if (FakeLastMoveMarker != null)
        FakeLastMoveMarker.SetActive(false);
    }
    /// <summary>
    /// 최근 착수 위치를 마커로 시각적으로 표시
    /// - realX, realZ : 실제 마지막 착수 위치 (항상 표시됨)
    /// - fakeX, fakeZ : 가짜 위치 (옵션, 전달 시 함께 표시)
    /// </summary
    public void ShowLastMoveMarkers(int realX, int realZ, int? fakeX = null, int? fakeZ = null)
    {
        // 진짜 위치 마커
        if (TryGetWorldPositionByCoord(realX, realZ, out Vector3 realPos))
        {
            RealLastMoveMarker.transform.position = realPos + new Vector3(0, 3.2f, 0);
            RealLastMoveMarker.SetActive(true);
        }

        // 가짜 위치 마커 (전달받은 좌표가 있을 때만 작동)
        if (fakeX.HasValue && fakeZ.HasValue &&
            TryGetWorldPositionByCoord(fakeX.Value, fakeZ.Value, out Vector3 fakePos))
        {
            FakeLastMoveMarker.transform.position = fakePos + new Vector3(0, 3.2f, 0);
            FakeLastMoveMarker.SetActive(true);
        }
        else
        {
            FakeLastMoveMarker.SetActive(false);
        }
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
        if (_stoneObjects == null) _stoneObjects = new GameObject[LineCount, LineCount];

        // 여기서도 GomokuManager의 IsStoneSwapped 변수 상태에 따라 반전 결정
        bool renderAsBlack = GomokuManager.I.IsStoneSwapped ? !isBlack : isBlack;

        GameObject prefab = renderAsBlack ? BlackStonePrefab : WhiteStonePrefab;
        Vector3 spawnPos = pos + new Vector3(0, 0.15f, 0); 
        
        GameObject stone = Instantiate(prefab, spawnPos, Quaternion.identity);
        stone.tag = "Stone";
        _stoneObjects[x, z] = stone;
    }

    /// <summary>
    /// 마우스 커서 위치에 따른 반투명 돌 표시
    /// </summary>
    public void UpdateGhostStone(Vector3 pos, bool isVisible, bool isBlack, bool isForbidden)
    {
        if (BlackGhostObj == null || WhiteGhostObj == null) return;

        // 일단 둘 다 끄기
        BlackGhostObj.SetActive(false);
        WhiteGhostObj.SetActive(false);
        ForbiddenPrefab.SetActive(false);

        if (!isVisible || pos == Vector3.zero) return;
        if (isForbidden) // 금수체크
        {
            ForbiddenPrefab.transform.position = pos + new Vector3(0, 0.15f, 0);
            ForbiddenPrefab.SetActive(true);
            return;
        }

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
    /// 좌표 기반 착수 위치를 월드 좌표로 변환함.
    /// </summary>
    /// <param name="x">보드 X 좌표.</param>
    /// <param name="z">보드 Z 좌표.</param>
    /// <param name="pos">변환된 월드 좌표.</param>
    /// <returns>좌표 변환 성공 여부.</returns>
    public bool TryGetWorldPositionByCoord(int x, int z, out Vector3 pos)
    {
        pos = Vector3.zero;
        if (x < 0 || x >= LineCount || z < 0 || z >= LineCount)
        {
            return false;
        }

        pos = new Vector3(
            StartPos.x + (x * Interval),
            StartPos.y,
            StartPos.z + (z * Interval));
        return true;
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

    /// <summary>
    /// 모든 돌을 다시 렌더링합니다. 투명돌과 가짜돌은 스왑(색상 반전)의 영향을 받지 않습니다.
    /// </summary>
    public void SwapAllStonesVisual(bool isSwapped)
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

                StoneData data = GomokuManager.I.GetStoneDataAt(x, z);
                if (data.Color == StoneColor.None) continue;

                bool isOriginalBlack = (data.Color == StoneColor.Black);

                // --- [A. 투명돌 처리] ---
                if (data.IsTransparent)
                {
                    if (data.Color == GomokuManager.I.MyColor)
                    {
                        GameObject tPrefab = isOriginalBlack ? BlackTransparentPrefab : WhiteTransparentPrefab;
                        SpawnVisualStone(x, z, tPrefab);
                    }
                    continue; 
                }

                // --- [B. 가짜돌 처리] ---
                if (data.IsFake)
                {
                    // 1. 내 가짜돌인 경우: 내가 알 수 있도록 가짜 프리팹 생성
                    if (data.Color == GomokuManager.I.MyColor)
                    {
                        GameObject fPrefab = isOriginalBlack ? BlackFakePrefab : WhiteFakePrefab;
                        SpawnVisualStone(x, z, fPrefab);
                    }
                    // 2. 상대방 가짜돌인 경우: 나를 속여야 하므로 '일반돌' 생성
                    else
                    {
                        // 일반돌과 동일하게 Swap 로직 적용
                        bool renderAsBlack = isOriginalBlack;
                        if (isSwapped) renderAsBlack = !renderAsBlack;
                        
                        GameObject prefab = renderAsBlack ? BlackStonePrefab : WhiteStonePrefab;
                        SpawnVisualStone(x, z, prefab);
                    }
                    continue;
                }

                // --- [C. 일반 돌 처리] ---
                bool normalRenderAsBlack = isOriginalBlack;
                if (isSwapped) normalRenderAsBlack = !normalRenderAsBlack;

                GameObject normalPrefab = normalRenderAsBlack ? BlackStonePrefab : WhiteStonePrefab;
                SpawnVisualStone(x, z, normalPrefab);
            }
        }
    }
    private void SpawnVisualStone(int x, int z, GameObject prefab)
    {
        if (TryGetWorldPositionByCoord(x, z, out Vector3 pos))
        {
            GameObject stone = Instantiate(prefab, pos + new Vector3(0, 0.15f, 0), Quaternion.identity);
            stone.tag = "Stone";
            _stoneObjects[x, z] = stone;
        }
    }


}
