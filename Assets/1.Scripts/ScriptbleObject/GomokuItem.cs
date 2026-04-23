using UnityEngine;

[CreateAssetMenu(fileName = "GomokuItem", menuName = "Scriptable Objects/GomokuItem")]
public class GomokuItem : ScriptableObject
{
    [SerializeField] public string name;
    [SerializeField] public string description;
    [SerializeField] public Sprite image;
}
