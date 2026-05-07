using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "GomokuItem", menuName = "Scriptable Objects/GomokuItem")]
public class GomokuItem : ScriptableObject
{
    [FormerlySerializedAs("name")] [SerializeField] public string itemName;
    [SerializeField] public string description;
    [SerializeField] public Sprite sprite;
}
