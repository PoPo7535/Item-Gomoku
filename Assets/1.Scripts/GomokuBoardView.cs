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
    public GameObject GeneratedPoints;

    private GameObject[,] _stoneObjects;

    public void Init()
    {
        _stoneObjects = new GameObject[LineCount, LineCount];
        CreateClickPoints();
    }

    // [이동됨] 마우스 위치를 계산하여 보드 좌표를 반환하는 함수
    public (Vector3 pos, int x, int z) GetBoardPosition()
    {
        if (GameViewImage == null || BoardCamera == null) return (Vector3.zero, 0, 0);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GameViewImage.rectTransform, Input.mousePosition, null, out Vector2 localPoint);

        Rect r = GameViewImage.rectTransform.rect;
        float nX = (localPoint.x - r.x) / r.width;
        float nY = (localPoint.y - r.y) / r.height;

        Ray ray = BoardCamera.ViewportPointToRay(new Vector3(nX, nY, 0));
        int layerMask = 1 << LayerMask.NameToLayer("Board");

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
        {
            if (hit.transform.name.StartsWith("Point_"))
            {
                string[] parts = hit.transform.name.Split('_');
                return (hit.transform.position, int.Parse(parts[1]), int.Parse(parts[2]));
            }
        }
        return (Vector3.zero, 0, 0);
    }

    // [이동됨] 실제 돌을 생성하는 시각적 처리
    public void SpawnStone(int x, int z, bool isBlack, Vector3 pos)
    {
        GameObject prefab = isBlack ? BlackStonePrefab : WhiteStonePrefab;
        Vector3 spawnPos = pos + new Vector3(0, 0.15f, 0);
        GameObject stone = Instantiate(prefab, spawnPos, Quaternion.identity);
        stone.tag = "Stone";
        _stoneObjects[x, z] = stone;
    }

    public void UpdateGhostStone(Vector3 pos, bool isVisible, bool isBlack)
    {
        BlackGhostObj.SetActive(false);
        WhiteGhostObj.SetActive(false);
        if (!isVisible) return;

        GameObject target = isBlack ? BlackGhostObj : WhiteGhostObj;
        if (target != null)
        {
            target.transform.position = pos + new Vector3(0, 0.15f, 0);
            target.SetActive(true);
        }
    }

    public void CreateClickPoints() { /* 기존 로직과 동일 */ }
    public void ClearBoard() { /* 기존 로직과 동일 */ }
}