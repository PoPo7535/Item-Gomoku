using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "GomokuItem", menuName = "Scriptable Objects/GomokuItem")]
public class GomokuItem : ScriptableObject
{
    [FormerlySerializedAs("name")] [SerializeField] public string itemName;
    [SerializeField] public string description;
    [SerializeField] public Sprite sprite;
    [SerializeField] public ItemType type;
}

public enum ItemType
{
    Detect,                 // 간파하기
    DoubleShow,             // 더블표시
    FakeStone,              // 가짜돌
    HideStone,              // 돌숨기기
    SwapStone,              // 돌바꾸기
    TimerDecreasing,        // 타이머 감소
    TransparentStone,       // 투명 돌
}
