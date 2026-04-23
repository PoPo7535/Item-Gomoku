using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "GomokuItem", menuName = "Scriptable Objects/GomokuItem")]
public class GomokuItem : ScriptableObject
{
    [SerializeField] public string name;
    [SerializeField] public string description;
    [SerializeField] public Sprite sprite;
}
