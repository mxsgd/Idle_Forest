using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TileContextBuildButton : MonoBehaviour
{
    [SerializeField] private TileContextPanel panel;
    [SerializeField] private TileBuildAction action = TileBuildAction.Grass;

    private Button button;

    private void Start()
    {
        button = GetComponent<Button>();

        if (panel == null)
            panel = GetComponentInParent<TileContextPanel>(true);

        if (button != null)
            button.onClick.AddListener(OnClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);
    }

    private void OnClicked()
    {
        if (panel != null)
            panel.RequestBuild(action);
        Debug.Log("czemu");
    }
}