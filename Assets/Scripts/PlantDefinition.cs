using UnityEngine;

[CreateAssetMenu(menuName = "Plants/Plant Definition")]
public class PlantDefinition : ScriptableObject
{
    public string displayName;
    public GameObject prefab;
    public Sprite icon;
    public int baseCost;
    public float costMultiplier = 1.5f;
}
