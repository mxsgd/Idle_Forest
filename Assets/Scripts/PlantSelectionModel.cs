using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Plants/Plant Selection Model")]
public class PlantSelectionModel : ScriptableObject
{
    public PlantDefinition Current { get; private set; }
    public event Action<PlantDefinition> OnPlantSelected;

    public void Select(PlantDefinition def)
    {
        if (Current == def) return;
        Current = def;
        OnPlantSelected?.Invoke(def);
    }

    public void Clear()
    {
        if (Current == null) return;
        Current = null;
        OnPlantSelected?.Invoke(null);
    }
}