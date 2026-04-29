using UnityEngine;
using UnityEngine.UI;

public class BoardViewController : MonoBehaviour
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

    [Header("보드 설정")]
    public int LineCount = 15;
    public float Interval;
    public Vector3 StartPos;
    public GameObject GeneratedPoints;

    // =========================
    // Point 생성
    // =========================
    public void CreatePoints()
    {
        for (int i = GeneratedPoints.transform.childCount - 1; i >= 0; i--)
            Destroy(GeneratedPoints.transform.GetChild(i).gameObject);

        for (int x = 0; x < LineCount; x++)
        {
            for (int z = 0; z < LineCount; z++)
            {
                GameObject p = new GameObject($"Point_{x}_{z}");
                p.transform.SetParent(GeneratedPoints.transform);

                Vector3 pos = new Vector3(
                    StartPos.x + (x * Interval),
                    StartPos.y,
                    StartPos.z + (z * Interval)
                );

                p.transform.position = pos;

                SphereCollider sc = p.AddComponent<SphereCollider>();
                sc.radius = Interval * 0.75f;
                sc.isTrigger = true;
                p.layer = LayerMask.NameToLayer("Board");
            }
        }
    }

    // =========================
    // Ray 계산 (중요)
    // =========================
    public (Vector3 pos, int x, int z) CalculateRay()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            GameViewImage.rectTransform,
            Input.mousePosition,
            null,
            out Vector2 localPoint
        );

        Rect r = GameViewImage.rectTransform.rect;
        float nX = (localPoint.x - r.x) / r.width;
        float nY = (localPoint.y - r.y) / r.height;

        Ray ray = BoardCamera.ViewportPointToRay(new Vector3(nX, nY, 0));
        int mask = 1 << LayerMask.NameToLayer("Board");

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, mask))
        {
            string name = hit.transform.name;

            if (!name.StartsWith("Point_"))
                return (hit.point, 0, 0);

            string[] parts = name.Split('_');

            int x = int.Parse(parts[1]);
            int z = int.Parse(parts[2]);

            return (hit.transform.position, x, z);
        }

        return (Vector3.zero, 0, 0);
    }

    // =========================
    // Ghost 표시
    // =========================
    public void ShowGhost(bool isBlack, Vector3 pos)
    {
        BlackGhostObj.SetActive(false);
        WhiteGhostObj.SetActive(false);

        GameObject target = isBlack ? BlackGhostObj : WhiteGhostObj;

        target.transform.position = pos + Vector3.up * 0.15f;
        target.SetActive(true);
    }

    public void HideGhost()
    {
        BlackGhostObj.SetActive(false);
        WhiteGhostObj.SetActive(false);
    }
}