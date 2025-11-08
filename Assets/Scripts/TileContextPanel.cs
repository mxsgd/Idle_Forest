using TMPro;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[RequireComponent(typeof(CanvasGroup))]
public class TileContextPanel : MonoBehaviour
{
    [SerializeField] private TileClickSelector selector;
    [SerializeField] private TileBuildController buildController;

    [SerializeField, Tooltip("Canvas group that is toggled when the panel is shown or hidden.")]
    private CanvasGroup panelCanvasGroup;

    [Header("Buttons")]
    [SerializeField] private Button grassButton;
    [SerializeField] private Button bushButton;
    [SerializeField] private Button treeButton;

    [Header("Ceny")]
    [SerializeField] private TMP_Text grassPriceLabel;
    [SerializeField] private TMP_Text bushPriceLabel;
    [SerializeField] private TMP_Text treePriceLabel;

    private IdleEconomyManager economy;
    private TileGrid.Tile currentTile;

    private void Awake()
    {
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

            EnsureDependencies();

        HidePanel();
    }

    private void OnEnable()
    {
        EnsureDependencies();
        economy = IdleEconomyManager.Instance;

        if (selector != null)
            selector.TileSelected += OnTileSelected;

        if (economy != null)
            economy.CurrencyChanged += OnCurrencyChanged;

        RegisterButton(grassButton, OnGrassClicked);
        RegisterButton(bushButton, OnBushClicked);
        RegisterButton(treeButton, OnTreeClicked);
    }

    private void OnDisable()
    {
        if (selector != null)
            selector.TileSelected -= OnTileSelected;

        if (economy != null)
            economy.CurrencyChanged -= OnCurrencyChanged;

        currentTile = null;
        HidePanel();
    }


    private void OnCurrencyChanged(float _)
    {
        RefreshOptions();
    }

    private void OnTileSelected(TileGrid.Tile tile)
    {
        currentTile = tile;

        if (tile == null)
        {
            HidePanel();
            return;
        }

        ShowPanel();
        RefreshOptions();
    }

    private void OnGrassClicked()
    {
        HandleBuild(TileBuildAction.Grass);
        Debug.Log("ongrassclicked");
    }

    private void OnBushClicked()
    {
        HandleBuild(TileBuildAction.Bush);
    }

    private void OnTreeClicked()
    {
        HandleBuild(TileBuildAction.Tree);
    }

    private void HandleBuild(TileBuildAction action)
    {
        if (buildController == null || currentTile == null)
            return;

        if (buildController.TryBuild(action, currentTile, out var failureReason))
        {
            RefreshOptions();
        }
        else if (!string.IsNullOrEmpty(failureReason))
        {
            Debug.LogWarning(failureReason);
            RefreshOptions();
        }
    }
    public void RequestBuild(TileBuildAction action)
    {
        HandleBuild(action);
        Debug.Log("request clicked");
    }
    private void EnsureDependencies()
    {
        if (selector == null)
            selector = FindObjectOfType<TileClickSelector>(true);

        if (buildController == null)
            buildController = FindObjectOfType<TileBuildController>(true);
    }

    private void RegisterButton(Button button, UnityAction callback)
    {
        if (button == null)
            return;

        if (button.TryGetComponent<TileContextBuildButton>(out _))
            return;

        button.onClick.AddListener(callback);
    }

    private void UnregisterButton(Button button, UnityAction callback)
    {
        if (button == null)
            return;

        if (button.TryGetComponent<TileContextBuildButton>(out _))
            return;

        button.onClick.RemoveListener(callback);
    }

    private void RefreshOptions()
    {
        if (buildController == null || currentTile == null)
            return;

        ApplyOption(grassButton, grassPriceLabel, buildController.GetBuildOption(currentTile, TileBuildAction.Grass));
        ApplyOption(bushButton, bushPriceLabel, buildController.GetBuildOption(currentTile, TileBuildAction.Bush));
        ApplyOption(treeButton, treePriceLabel, buildController.GetBuildOption(currentTile, TileBuildAction.Tree));
    }

    private void ApplyOption(Button button, TMP_Text label, TileBuildController.TileBuildOption option)
    {
        if (button != null)
            button.interactable = option.canBuild;

        if (label != null)
        {
            string priceText = option.cost > 0f ? option.cost.ToString("N0") : "0";
            if (!option.canBuild && !string.IsNullOrEmpty(option.reason))
                priceText += $"\n{option.reason}";

            label.text = priceText;
        }
    }

    private void ShowPanel()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void HidePanel()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }
    }
}