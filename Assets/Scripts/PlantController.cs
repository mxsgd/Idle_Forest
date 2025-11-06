using UnityEngine;

public class PlantPaletteUI : MonoBehaviour
{
    [SerializeField] private PlantDatabase database;
    [SerializeField] private PlantSelectionModel selection;
    [SerializeField] private PlantPaletteButton buttonPrefab;
    [SerializeField] private Transform contentParent;
    [SerializeField] private PlantPlacer plantPlacer;

    private void Start()
    {
        foreach (var def in database.plants)
        {
            var btn = Instantiate(buttonPrefab, contentParent);
            btn.Init(def, selection, plantPlacer);
        }
    }
}