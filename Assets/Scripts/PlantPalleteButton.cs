using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlantPaletteButton : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;

    private PlantDefinition _def;
    private PlantSelectionModel _selection;

    public void Init(PlantDefinition def, PlantSelectionModel selection)
    {
        _def = def;
        _selection = selection;
        if (icon) icon.sprite = def.icon;
        if (label) label.text = def.displayName;
        button.onClick.AddListener(OnClick);
    }

    private void OnClick() => _selection.Select(_def);
}