using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlantPaletteButton : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text price;
    [SerializeField] private Button button;
    [SerializeField] private PlantPlacer plantPlacer;


    private PlantDefinition _def;
    private PlantSelectionModel _selection;

    public void Init(PlantDefinition def, PlantSelectionModel selection, PlantPlacer placer)
    {
        _def = def;
        _selection = selection;
        plantPlacer = placer;
        if (icon) icon.sprite = def.icon;
        if (label) label.text = def.displayName;
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (_selection != null)
            _selection.Select(_def);

        if (plantPlacer != null)
            plantPlacer.TryPlacePlant(_def);
    }
}
