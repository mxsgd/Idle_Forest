using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Plants/Plant Database")]
public class PlantDatabase : ScriptableObject
{
    public List<PlantDefinition> plants;
}