using UnityEngine;

/// <summary>
/// 보드 Collider 기준으로 TestScene용 셀 앵커를 생성함.
/// </summary>
public sealed class GridGenerator : MonoBehaviour
{
    private const int DefaultGridSize = 15;
    private const string CellNamePrefix = "Cell_";

    [Header("그리드 설정")]
    [SerializeField] private int _gridSize = DefaultGridSize;
    [SerializeField] private float _anchorHeightOffset = 0.02f;

    /// <summary>
    /// 보드 기준으로 셀 앵커를 다시 생성함.
    /// </summary>
    [ContextMenu("Generate Cell Anchors")]
    public void GenerateGrid()
    {
        if (!TryGetBoardCollider(out BoxCollider boardCollider))
        {
            return;
        }

        if (_gridSize < 2)
        {
            Debug.LogError("Grid Size는 2 이상이어야 합니다.");
            return;
        }

        ClearGeneratedGrid();

        float topY = boardCollider.center.y + (boardCollider.size.y * 0.5f) + _anchorHeightOffset;
        float minX = boardCollider.center.x - (boardCollider.size.x * 0.5f);
        float minZ = boardCollider.center.z - (boardCollider.size.z * 0.5f);
        float xInterval = boardCollider.size.x / (_gridSize - 1);
        float zInterval = boardCollider.size.z / (_gridSize - 1);

        for (int xIndex = 0; xIndex < _gridSize; xIndex++)
        {
            for (int yIndex = 0; yIndex < _gridSize; yIndex++)
            {
                CreateCellAnchor(xIndex, yIndex, minX, minZ, topY, xInterval, zInterval);
            }
        }

        Debug.Log($"{name}에 셀 앵커 {_gridSize * _gridSize}개를 생성했습니다.");
    }

    /// <summary>
    /// 생성한 셀 앵커를 모두 정리함.
    /// </summary>
    [ContextMenu("Clear Cell Anchors")]
    public void ClearGeneratedGrid()
    {
        for (int childIndex = transform.childCount - 1; childIndex >= 0; childIndex--)
        {
            Transform childTransform = transform.GetChild(childIndex);
            if (!IsGeneratedCellAnchor(childTransform.name))
            {
                continue;
            }

            DestroyAnchorObject(childTransform.gameObject);
        }
    }

    /// <summary>
    /// 보드 배치에 필요한 BoxCollider를 찾음.
    /// </summary>
    /// <param name="boardCollider">찾은 보드 Collider.</param>
    /// <returns>Collider 탐색 성공 여부.</returns>
    private bool TryGetBoardCollider(out BoxCollider boardCollider)
    {
        boardCollider = GetComponent<BoxCollider>();
        if (boardCollider != null)
        {
            return true;
        }

        Debug.LogError("GridGenerator는 BoxCollider가 있는 보드 오브젝트에 부착해야 합니다.");
        return false;
    }

    /// <summary>
    /// 지정한 인덱스 위치에 셀 앵커를 생성함.
    /// </summary>
    /// <param name="xIndex">생성할 X 인덱스.</param>
    /// <param name="yIndex">생성할 Y 인덱스.</param>
    /// <param name="minX">보드 최소 X 로컬 좌표.</param>
    /// <param name="minZ">보드 최소 Z 로컬 좌표.</param>
    /// <param name="topY">보드 상면 기준 Y 로컬 좌표.</param>
    /// <param name="xInterval">X축 셀 간격.</param>
    /// <param name="zInterval">Z축 셀 간격.</param>
    private void CreateCellAnchor(int xIndex, int yIndex, float minX, float minZ, float topY, float xInterval, float zInterval)
    {
        string cellName = GetCellName(xIndex, yIndex);
        GameObject cellObject = new GameObject(cellName);
        Transform cellTransform = cellObject.transform;

        cellTransform.SetParent(transform, false);
        cellTransform.localPosition = new Vector3(minX + (xIndex * xInterval), topY, minZ + (yIndex * zInterval));
        cellTransform.localRotation = Quaternion.identity;
        cellTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// 생성 대상 셀 앵커 이름을 반환함.
    /// </summary>
    /// <param name="xIndex">셀 X 인덱스.</param>
    /// <param name="yIndex">셀 Y 인덱스.</param>
    /// <returns>셀 앵커 이름.</returns>
    private static string GetCellName(int xIndex, int yIndex)
    {
        return $"{CellNamePrefix}{xIndex}_{yIndex}";
    }

    /// <summary>
    /// 이름이 자동 생성 셀 앵커 규칙과 맞는지 확인함.
    /// </summary>
    /// <param name="objectName">검사할 오브젝트 이름.</param>
    /// <returns>자동 생성 셀 앵커 여부.</returns>
    private static bool IsGeneratedCellAnchor(string objectName)
    {
        return !string.IsNullOrEmpty(objectName) && objectName.StartsWith(CellNamePrefix);
    }

    /// <summary>
    /// 에디터/런타임 환경에 맞게 앵커 오브젝트를 제거함.
    /// </summary>
    /// <param name="targetObject">제거할 오브젝트.</param>
    private static void DestroyAnchorObject(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return;
        }

        // 에디터 생성물 정리 시 즉시 제거해야 중복 생성이 남지 않음.
        if (Application.isPlaying)
        {
            Object.Destroy(targetObject);
            return;
        }

        Object.DestroyImmediate(targetObject);
    }
}
